// Browser side of the file exchange.
//
// Everything else in this app is C#. This exists because a page cannot hand the user a file
// without the browser's help: there is no way to write to disk from managed code in a tab.

/**
 * Offers a file to the user for saving.
 *
 * @param {string} name suggested file name
 * @param {Uint8Array} bytes file contents
 */
export function saveFile(name, bytes) {
    const blob = new Blob([bytes], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Released on the next turn of the event loop, once the browser has taken what it needs.
    setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * Reads what was kept from a previous visit.
 *
 * @param {string} key where it was filed
 * @returns {string|null} the contents, or null when there is nothing there
 */
export function readStored(key) {
    return localStorage.getItem(key);
}

/**
 * Keeps something for next time.
 *
 * A full or disabled store throws, which the caller treats as nothing having been kept: the work is
 * still in the tab, and interrupting someone mid-empire to say so would help no one.
 *
 * @param {string} key where to file it
 * @param {string} value the contents
 */
export function writeStored(key, value) {
    localStorage.setItem(key, value);
}

/**
 * Brings the chosen item of a scrolling list into view without scrolling the page.
 *
 * A list that scrolls inside its own box opens showing its first rows, which for a design already
 * holding the twenty-eighth room means opening on rooms it does not have. Only the box is scrolled —
 * scrollIntoView would take the page with it and undo the point of the box, which is to keep the
 * scene above on screen.
 *
 * @param {HTMLElement} list the scrolling container
 * @param {string} selector what marks the chosen item within it
 */
export function revealSelected(list, selector) {
    const chosen = list?.querySelector(selector);

    if (!chosen) {
        return;
    }

    // Measured between the two boxes rather than from offsetTop, which is counted from whichever
    // ancestor happens to be positioned and put the first room 492 pixels down a list it starts.
    const listBox = list.getBoundingClientRect();
    const box = chosen.getBoundingClientRect();

    // Centred rather than merely brought inside the edge, so what surrounds it is visible too: the
    // choice next to the one you have is most of why you are looking.
    const delta = (box.top - listBox.top) - (list.clientHeight - box.height) / 2;
    list.scrollTop = Math.max(0, list.scrollTop + delta);
}

/**
 * Ties a panel to the thing it describes, and opens it on hover, on focus or on a tap.
 *
 * The panel is a popover, which is to say the browser draws it in the top layer where no ancestor
 * can clip it. That is the whole reason for using one: these hang off chips inside a narrow column
 * that is itself sticky and scrolls, and anything positioned in the ordinary way would be cut off by
 * one of those boxes long before it was read.
 *
 * Nothing about where it goes is left to the browser, since the top layer has no idea what it was
 * opened from. It is placed under its anchor, flipped above when there is no room below, and clamped
 * so neither edge leaves the viewport.
 *
 * All of the listening happens here rather than in the component, so a hover costs no round trip
 * into managed code.
 *
 * @param {HTMLElement} anchor what the panel describes
 * @param {HTMLElement} panel the popover itself
 */
export function bindPopover(anchor, panel) {
    if (!anchor || !panel || anchor.dataset.semBound === 'yes') {
        return;
    }

    anchor.dataset.semBound = 'yes';

    // A tap has no hover to leave, so a click pins the panel until something dismisses it.
    let pinned = false;

    const place = () => {
        const at = anchor.getBoundingClientRect();
        const box = panel.getBoundingClientRect();
        const margin = 8;

        // Below by preference, above when the room below will not take it, and whichever is roomier
        // when neither will.
        const below = window.innerHeight - at.bottom - margin;
        const above = at.top - margin;
        const goesBelow = box.height <= below || below >= above;

        const top = goesBelow
            ? Math.min(at.bottom + 4, window.innerHeight - box.height - margin)
            : Math.max(at.top - box.height - 4, margin);

        const left = Math.min(
            Math.max(at.left, margin),
            Math.max(margin, window.innerWidth - box.width - margin));

        panel.style.top = `${Math.max(margin, top)}px`;
        panel.style.left = `${left}px`;
    };

    const show = () => {
        if (!panel.matches(':popover-open')) {
            panel.showPopover();
        }

        // After showing, so the panel has been laid out and has a size to place.
        place();
    };

    const hide = () => {
        if (!pinned && panel.matches(':popover-open')) {
            panel.hidePopover();
        }
    };

    anchor.addEventListener('mouseenter', show);
    anchor.addEventListener('mouseleave', hide);
    anchor.addEventListener('focus', show);
    anchor.addEventListener('blur', () => { pinned = false; hide(); });

    anchor.addEventListener('click', event => {
        event.preventDefault();
        pinned = !pinned;

        if (pinned) {
            show();
        } else {
            panel.hidePopover();
        }
    });

    anchor.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            pinned = false;
            panel.hidePopover();
        }
    });

    // The browser closes it for its own reasons too — another popover opening, a click outside —
    // and the pin has to let go when it does, or the next hover would find it still held.
    panel.addEventListener('toggle', event => {
        if (event.newState === 'closed') {
            pinned = false;
        }
    });
}

/**
 * Turns the browser's own "leave site?" warning on and off.
 *
 * Closing the tab or reloading it is the one way out of the app that the app cannot ask about
 * itself, and it is the way that loses the work: everything else is a move between its own pages,
 * where a proper question can be put. The browser writes the wording and ignores the returned
 * string; all this decides is whether it appears at all.
 *
 * @param {boolean} unsaved whether there is work that closing the tab would lose
 */
export function warnBeforeLeaving(unsaved) {
    window.removeEventListener('beforeunload', warn);

    if (unsaved) {
        window.addEventListener('beforeunload', warn);
    }
}

function warn(event) {
    event.preventDefault();

    // Required by browsers old enough to want it, ignored by the rest.
    event.returnValue = '';
}

/**
 * Puts text on the clipboard.
 *
 * The clipboard needs a secure context and a recent gesture, and a browser may refuse for either
 * reason, so the caller is told whether it worked rather than being left to claim it did.
 *
 * @param {string} text what to copy
 * @returns {Promise<boolean>} whether it was copied
 */
export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}
