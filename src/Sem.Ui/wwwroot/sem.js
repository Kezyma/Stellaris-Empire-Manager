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
 * How the file pickers identify themselves to the browser.
 *
 * The browser keeps the directory last used under an id and reopens there next time, across
 * sessions - which is the only way this app can ever land in the player's Stellaris folder. A page
 * may not choose a directory: startIn takes a handful of well-known names and nothing else, and
 * given a path it refuses outright, saying the value "is not a valid enum value of type
 * WellKnownDirectory". So the first pick opens at Documents and every one after opens where the
 * player themselves went. Open and save share the id deliberately: exporting should offer to write
 * back where the file was imported from.
 */
const DESIGNS_PICKER = 'sem-designs';

/** What the pickers offer, which the browser will only accept as extensions. */
const DESIGNS_TYPES = [{
    description: 'Stellaris empire designs',
    accept: { 'text/plain': ['.txt'] },
}];

/**
 * Generous enough for any real designs file; the player's own is about forty kilobytes. The same
 * number the fallback input is given, so the two ways in agree about what is too big.
 */
const LARGEST_DESIGNS_FILE = 16 * 1024 * 1024;

/**
 * Whether this browser has a file picker to show at all.
 *
 * Asked before the header is drawn, so that Import can be an ordinary button where a picker exists
 * and fall back to a file input where one does not. Firefox and Safari have none.
 *
 * @returns {boolean} whether openDesignsFile can do anything
 */
export function canPickFiles() {
    return typeof window.showOpenFilePicker === 'function';
}

/**
 * Asks the player for their designs file.
 *
 * Where the browser has no picker - Firefox and Safari have none - this returns null and the caller
 * falls back to the file input, which is still in the page for exactly that reason.
 *
 * Must be called while a click is still being handled. That holds through the trip out to C# and
 * back, but it would not survive a dialog, so nothing may be asked of the player before this.
 *
 * @returns {Promise<{name: string, bytes: Uint8Array}|null>} the file, or null if there is no picker
 *   to show, the player cancelled, or what they chose is too large to be a designs file
 */
export async function openDesignsFile() {
    if (typeof window.showOpenFilePicker !== 'function') {
        return null;
    }

    try {
        const [handle] = await window.showOpenFilePicker({
            id: DESIGNS_PICKER,
            startIn: 'documents',
            types: DESIGNS_TYPES,
            excludeAcceptAllOption: false,
            multiple: false,
        });

        const file = await handle.getFile();

        if (file.size > LARGEST_DESIGNS_FILE) {
            return null;
        }

        return { name: file.name, bytes: new Uint8Array(await file.arrayBuffer()) };
    } catch {
        // Cancelling is the ordinary way to leave a file picker, and it is not a failure.
        return null;
    }
}

/**
 * Offers to write the designs file where the player keeps it.
 *
 * A real save dialog rather than a download, so the file goes back where it came from instead of
 * into Downloads for the player to move themselves. Sharing an id with the open picker is what puts
 * it in the right folder with nothing having to know the path.
 *
 * Every export asks again. Keeping the handle would let a later save skip the dialog, and it was
 * decided not to: writing the player's own designs file should be a thing they chose that time, not
 * a permission granted once.
 *
 * Which of the three things happened matters, and a boolean could not say. A dismissed dialog must
 * leave nothing behind - handing over a download after the player said no is answering a question
 * they declined to ask. A browser with no dialog at all should still get its file, and be told why
 * it arrived the older way. And a browser that has the dialog but will not open it is a third thing
 * again, worth saying out loud rather than blaming on the player.
 *
 * @param {string} name what to call it
 * @param {Uint8Array} bytes file contents
 * @returns {Promise<'saved'|'cancelled'|'unavailable'|'refused'>} what became of it
 */
export async function saveDesignsFile(name, bytes) {
    if (typeof window.showSaveFilePicker !== 'function') {
        return 'unavailable';
    }

    try {
        const handle = await window.showSaveFilePicker({
            id: DESIGNS_PICKER,
            startIn: 'documents',
            suggestedName: name,
            types: DESIGNS_TYPES,
        });

        // The browser stages this and swaps it in when the stream closes, so a tab that dies
        // half-way through leaves the file as it was.
        const writable = await handle.createWritable();
        await writable.write(bytes);
        await writable.close();

        return 'saved';
    } catch (error) {
        // AbortError is the one the player caused, and it is not a failure: they closed the dialog.
        // Everything else is the browser refusing - a policy, a site setting, a file it will not
        // open - and the caller should fall back rather than leave them with nothing.
        return error?.name === 'AbortError' ? 'cancelled' : 'refused';
    }
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
 * Ties up a whole page of popovers at once, each one the first time it is pointed at.
 *
 * bindPopover costs a call into managed code per chip. One panel beside an empire is nothing; a
 * table of twenty-five empires with six columns of them is a hundred and fifty-six calls every time
 * the rows are sorted, filtered or paged, which is most of half a second the reader waits through
 * for panels they have not opened.
 *
 * So the listening is done once, here, for everything inside a container. A chip is bound the first
 * time a pointer or the keyboard reaches it, and then shown - the hover that triggered the binding
 * would otherwise be the one hover that did nothing.
 *
 * @param {HTMLElement} root what holds the chips
 */
export function bindPopoversWhenPointed(root) {
    if (!root || root.dataset.semDeferred) {
        return;
    }

    root.dataset.semDeferred = 'on';

    const bind = event => {
        const anchor = event.target.closest?.('[aria-describedby]');

        if (!anchor || anchor.dataset.semBound === 'yes') {
            return;
        }

        const panel = document.getElementById(anchor.getAttribute('aria-describedby'));

        if (!panel) {
            return;
        }

        bindPopover(anchor, panel);

        // The hover that did the binding happened before there was anything listening for it.
        anchor.dispatchEvent(new MouseEvent('mouseenter'));
    };

    root.addEventListener('pointerover', bind);
    root.addEventListener('focusin', bind);
}

/**
 * How wide the window is, for a decision that has to be made once before anything is drawn.
 *
 * The stylesheet answers most questions about width on its own and should keep doing so. This is
 * for the one it cannot: what a panel's state should be the first time somebody sees it, which is
 * a fact the markup has to carry and CSS has no way to set.
 *
 * @returns {number} the viewport width in CSS pixels
 */
export function viewportWidth() {
    return window.innerWidth;
}

/**
 * Closes an open suggestion list when the next press lands outside it.
 *
 * The list itself is drawn or not drawn by the component, on a flag the component owns. What this
 * adds is the one way of closing it that the component cannot see: a press somewhere else on the
 * page. Escape, choosing an option and pressing the arrow again are all its own business.
 *
 * This was a blur handler on the text box, which had to go: focus moving from the box onto one of
 * the options is not the box being left, and closing on it made the list reachable by mouse and by
 * nothing else. A press is the right thing to watch instead, because it is the same event whether
 * it came from a mouse or a finger — where focus, on a phone, never moves at all.
 *
 * Rather than call back into managed code, this presses the list's own arrow, which is the button
 * that closes it. One listener serves every list on the page, and there is no per-list state to
 * keep in step.
 */
let watchingForOutsidePress = false;

export function closeListsOnOutsidePress() {
    if (watchingForOutsidePress) {
        return;
    }

    watchingForOutsidePress = true;

    // Captured, so a press is seen before anything inside the page can stop it travelling.
    document.addEventListener('pointerdown', event => {
        for (const combo of document.querySelectorAll('.sem-combo.open')) {
            if (!combo.contains(event.target)) {
                combo.querySelector('button.toggle')?.click();
            }
        }
    }, true);
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
 * A tap is the odd one out. There is no hover to leave, so a click pins the panel open until
 * something dismisses it - which is right for a chip that only describes itself, and wrong for one
 * that also opens an editor, where the press belongs to the editor and pinning would fire as well.
 * So pinning is asked for rather than assumed, and a chip that opens something asks for none.
 *
 * @param {HTMLElement} anchor what the panel describes
 * @param {HTMLElement} panel the popover itself
 * @param {boolean} [pinOnClick=true] whether a click should hold the panel open
 */
export function bindPopover(anchor, panel, pinOnClick = true) {
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

        // After showing, so the panel has been laid out and has a size to place. And again on the
        // next frame, because a panel that fills itself the first time it is pointed at has not
        // finished doing so yet - placed against an empty box it would sit where a box that size
        // belongs, which is nowhere near where this one ends up.
        place();
        requestAnimationFrame(place);
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

    if (pinOnClick) {
        anchor.addEventListener('click', event => {
            event.preventDefault();
            pinned = !pinned;

            if (pinned) {
                show();
            } else {
                panel.hidePopover();
            }
        });
    }

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

/**
 * Lets the cards in a list be dragged into a new order by their grips.
 *
 * Pointer events rather than HTML5 drag and drop, for two reasons. The card is a button and a
 * browser will not begin a drag from inside one, which is why dragging the card did nothing at all.
 * And HTML5 dragging is a mouse feature: it does not fire for a finger, on any browser, and this
 * list is a single column on a phone. One pointer path covers a mouse, a finger and a pen.
 *
 * Every card is placed by a transform while a drag is in progress. The held one follows the pointer
 * and the rest slide into the places they would take if it were dropped now, so the answer is
 * visible before the drop rather than after it. Nothing is reordered until the pointer is released:
 * the list itself does not change under the finger, only where each card is drawn.
 *
 * Positions are measured once, at the start. Reading them again during the drag would be reading
 * the transforms back, and the cards would chase their own tails.
 *
 * @param {HTMLElement} list the container holding the cards
 * @param {object} owner what to tell when a card has been moved
 */
export function enableCardReorder(list, owner) {
    if (!list || list.dataset.semReorder) {
        return;
    }

    list.dataset.semReorder = 'on';

    let cards = [];
    let places = [];
    let held = null;
    let from = -1;
    let onto = -1;
    let grabbed = { x: 0, y: 0 };

    const settle = () => {
        for (const card of cards) {
            card.style.transform = '';
            card.classList.remove('lifted');
        }

        list.classList.remove('sorting');
        cards = [];
        places = [];
        held = null;
        from = -1;
        onto = -1;
    };

    /** Where each card should be drawn, given that the held one is heading for `target`. */
    const layOut = target => {
        const order = cards.map((_, i) => i);
        order.splice(from, 1);
        order.splice(target, 0, from);

        order.forEach((was, becomes) => {
            if (was === from) {
                return;
            }

            const there = places[becomes];
            const here = places[was];

            cards[was].style.transform =
                `translate(${there.left - here.left}px, ${there.top - here.top}px)`;
        });
    };

    /** Which position the pointer is over, by distance to each place's middle. */
    const nearest = (x, y) => {
        let best = 0;
        let closest = Infinity;

        places.forEach((place, at) => {
            const dx = x - (place.left + place.width / 2);
            const dy = y - (place.top + place.height / 2);
            const away = (dx * dx) + (dy * dy);

            if (away < closest) {
                closest = away;
                best = at;
            }
        });

        return best;
    };

    list.addEventListener('pointerdown', event => {
        const grip = event.target.closest?.('[data-grip]');

        if (!grip) {
            return;
        }

        const card = grip.closest('[data-index]');

        if (!card) {
            return;
        }

        // Or the browser takes the gesture for itself: selecting text with a mouse, scrolling the
        // page with a finger. The grip also carries touch-action:none, which is the half of this
        // that a listener cannot do.
        event.preventDefault();

        cards = [...list.querySelectorAll('[data-index]')];
        places = cards.map(one => {
            const box = one.getBoundingClientRect();
            return { left: box.left, top: box.top, width: box.width, height: box.height };
        });

        held = card;
        from = cards.indexOf(card);
        onto = from;
        grabbed = { x: event.clientX, y: event.clientY };

        card.classList.add('lifted');
        list.classList.add('sorting');

        // So the rest of the gesture keeps arriving here even once the finger has left the grip,
        // which it does immediately. Not fatal if it is refused - the listeners are on the list and
        // a drag inside it still arrives, so a browser that will not capture costs precision at the
        // edges rather than the whole gesture.
        try {
            grip.setPointerCapture(event.pointerId);
        } catch {
            // Nothing to do about it, and nothing that has to stop.
        }
    });

    list.addEventListener('pointermove', event => {
        if (!held) {
            return;
        }

        held.style.transform =
            `translate(${event.clientX - grabbed.x}px, ${event.clientY - grabbed.y}px)`;

        const target = nearest(event.clientX, event.clientY);

        if (target !== onto) {
            onto = target;
            layOut(onto);
        }
    });

    list.addEventListener('pointerup', () => {
        if (!held) {
            return;
        }

        const start = from;
        const end = onto;

        settle();

        if (end >= 0 && end !== start) {
            owner.invokeMethodAsync('Reorder', start, end);
        }
    });

    // A cancelled pointer is the system taking the gesture away - a phone call, a gesture the OS
    // claimed. Nothing moves, and every card goes back where it was.
    list.addEventListener('pointercancel', settle);
}
