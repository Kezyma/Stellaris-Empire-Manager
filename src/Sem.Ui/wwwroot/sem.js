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
export function saveFile(name, bytes, mediaType) {
    const blob = new Blob([bytes], { type: mediaType || 'text/plain' });
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
 * Offers a file that is not the designs file.
 *
 * Deliberately not the designs picker. That one carries an id, which is how the browser remembers
 * the folder and reopens in the player's Stellaris directory - exactly where a part of a collection
 * or a picture of an empire must not default to. This asks with no id and no startIn, so the
 * browser offers wherever it would ordinarily offer, and the two never share a memory.
 *
 * @param {string} name suggested file name
 * @param {Uint8Array} bytes file contents
 * @param {string} mediaType what it is, for the picker and the blob
 * @param {string} extension the suffix the picker will accept, with its dot
 * @param {string} description what to call the kind of file in the dialog
 * @returns {Promise<'saved'|'cancelled'|'unavailable'|'refused'>} what became of it
 */
export async function exportFile(name, bytes, mediaType, extension, description) {
    if (typeof window.showSaveFilePicker !== 'function') {
        return 'unavailable';
    }

    try {
        const handle = await window.showSaveFilePicker({
            suggestedName: name,
            types: [{ description, accept: { [mediaType]: [extension] } }],
        });

        const writable = await handle.createWritable();
        await writable.write(bytes);
        await writable.close();

        return 'saved';
    } catch (error) {
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
    // Null means forget it, which is what signing out of a provider does. Without this branch
    // setItem stores the four characters "null", and a token read back as those is a session the
    // app believes in and cannot use.
    if (value === null || value === undefined) {
        localStorage.removeItem(key);
        return;
    }

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
 * Puts an open number box away when the next press lands somewhere else.
 *
 * The obvious way to do this is the focus leaving the box, and that is what it did at first. It is
 * not reliable enough: blur does not bubble, and focusout depends on the document having the
 * window's focus at all - so a box opened, focused and then left alone stayed a box.
 *
 * One listener for the page, and the flag that says so belongs to the module. It used to belong to
 * the table's own box, and that box is thrown away and built again whenever the filter empties the
 * table - so each new one carried no flag and added another listener to the document that nothing
 * ever took away, every one of them holding a detached table and a handle on managed code that had
 * since been disposed. An element's flag is a true statement about listeners hanging off that
 * element, which is what the popover binding above uses it for: those die when it does. It says
 * nothing whatever about a listener on the document.
 *
 * Nothing is called back into managed code either, for the same reason the suggestion lists press
 * their own arrow: the box already closes itself on Escape, so pressing Escape at it says what
 * needs saying and leaves nothing to keep in step.
 */
let watchingForSpinnerPress = false;

export function closeSpinnerOnOutsidePress() {
    if (watchingForSpinnerPress) {
        return;
    }

    watchingForSpinnerPress = true;

    // Captured, so a press is seen before anything inside the page can stop it travelling.
    document.addEventListener('pointerdown', event => {
        // The cell rather than the box, so the arrows a number box draws inside itself are a press
        // on the thing being typed into rather than a press somewhere else.
        if (event.target.closest?.('td.index')) {
            return;
        }

        for (const open of document.querySelectorAll('td.index input')) {
            open.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        }
    }, true);
}

/**
 * Drops the labels off a table's chips when the table would otherwise scroll.
 *
 * A chip with an icon says most of what it says in the picture; the word beside it is what makes a
 * column of them three times as wide. So the words go first, and only once there is no room - a
 * table that fits keeps them, and one that has been narrowed until it fits gets them back.
 *
 * Measured with the labels on, every time, so the answer never depends on the answer before it.
 * That is what stops it flickering between the two: the question asked is always "does the full
 * table fit", not "does the table as it currently is".
 *
 * The box is watched rather than the table inside it. The box is as wide as the page gives it and
 * does not change when the table within grows, so nothing here can set off the observer that called
 * it - which a naive version of this does, for ever.
 *
 * @param {HTMLElement} wrap the scrolling box around the table
 */
export function fitTableToWidth(wrap) {
    if (!wrap) {
        return;
    }

    if (wrap.semFit) {
        wrap.semFit();
        return;
    }

    const fit = () => {
        const table = wrap.querySelector('table');

        if (!table) {
            return;
        }

        wrap.classList.remove('compact');

        // A pixel of slack, because a table measured against its own box is comparing two numbers
        // that rounding can put either side of each other.
        if (table.scrollWidth > wrap.clientWidth + 1) {
            wrap.classList.add('compact');
        }
    };

    wrap.semFit = fit;
    new ResizeObserver(fit).observe(wrap);
    fit();
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

    const place = () => placeUnder(anchor, panel);

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

    const { hide, stay } = lingerWhilePointedAt(anchor, panel, show, () => pinned);

    anchor.addEventListener('focus', () => { stay(); show(); });
    anchor.addEventListener('blur', () => { pinned = false; hide(); });

    if (pinOnClick) {
        anchor.addEventListener('click', event => {
            event.preventDefault();
            stay();
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
            stay();
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
 * Keeps a panel open while the pointer is on its anchor or inside the panel itself.
 *
 * A panel that goes the moment the pointer leaves the chip is a panel nobody can reach into. Most
 * of them only want reading and it never came up - and then one was longer than its own height,
 * grew a scrollbar, and there was no way to get at it: the bar is inside the panel, and the pointer
 * cannot arrive there without leaving the chip.
 *
 * So closing waits a moment and either of the two being entered calls the wait off. The delay is
 * also what makes the few pixels between them crossable; without it the panel went in the frame
 * before the pointer landed anywhere.
 *
 * @param {HTMLElement} anchor what the panel hangs off
 * @param {HTMLElement} panel the popover itself
 * @param {() => void} show what opening it means, which differs between the two kinds
 * @param {() => boolean} held whether something is holding it open regardless of the pointer
 * @returns {{hide: () => void, stay: () => void}}
 */
function lingerWhilePointedAt(anchor, panel, show, held) {
    let closing = 0;

    const stay = () => clearTimeout(closing);

    const hide = () => {
        stay();

        closing = setTimeout(() => {
            if (!held() && panel.matches(':popover-open')) {
                panel.hidePopover();
            }
        }, 220);
    };

    anchor.addEventListener('mouseenter', () => {
        stay();
        show();
    });

    anchor.addEventListener('mouseleave', hide);

    // Entering the panel only cancels the closing. Showing again would place it again, and it is
    // under the pointer by then - it would move out from under the hand reaching for it.
    panel.addEventListener('mouseenter', stay);
    panel.addEventListener('mouseleave', hide);

    return { hide, stay };
}

/**
 * Puts a panel under the thing it belongs to, inside the viewport.
 *
 * Below by preference, above where the room below will not take it, and whichever is roomier when
 * neither will. Nothing about this is left to the browser: a popover is drawn in the top layer,
 * which has no idea what it was opened from.
 *
 * @param {HTMLElement} anchor what the panel hangs off
 * @param {HTMLElement} panel the popover itself
 */
function placeUnder(anchor, panel) {
    const at = anchor.getBoundingClientRect();
    const box = panel.getBoundingClientRect();
    const margin = 8;

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
}

/**
 * Hangs a list off a chip, the way a dropdown hangs off its control.
 *
 * Same top layer, same placing and the same lingering as a description panel.
 *
 * A hover and a press mean different things and are kept apart. A hover lasts exactly as long as
 * the hover: look away and the list goes. A press holds it open until something puts it away -
 * pressing the chip again, pressing anywhere else on the page, or Escape - which is what a tap
 * needs, having no hover to end, and what anyone wanting to read down the list without keeping the
 * pointer inside it wants too.
 *
 * The press away is the browser's own doing: an auto popover light-dismisses on an outside press.
 * All that is needed here is to let go of the pin when it does, or the next hover would find the
 * list still held open by a press nobody remembers making.
 *
 * @param {HTMLElement} anchor the chip the list belongs to
 * @param {HTMLElement} panel the list itself
 */
export function bindDropdown(anchor, panel) {
    if (!anchor || !panel || anchor.dataset.semDropdown === 'yes') {
        return;
    }

    anchor.dataset.semDropdown = 'yes';

    let pinned = false;

    const show = () => {
        if (!panel.matches(':popover-open')) {
            panel.showPopover();
        }

        placeUnder(anchor, panel);
        requestAnimationFrame(() => placeUnder(anchor, panel));
    };

    const { stay } = lingerWhilePointedAt(anchor, panel, show, () => pinned);

    const put = () => {
        stay();
        pinned = false;
        panel.hidePopover();
    };

    anchor.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();

        if (pinned) {
            put();
            return;
        }

        stay();
        pinned = true;
        show();
    });

    anchor.addEventListener('focus', () => { stay(); show(); });

    // Nothing on blur, unlike a description panel: the focus leaving this chip is usually the focus
    // moving into the list, which is where it should be able to go. Escape and a press elsewhere are
    // the ways out.
    anchor.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            put();
        }
    });

    // Closed for the browser's own reasons - a press outside, or another popover opening - and the
    // pin has to let go with it.
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
    // dataset rather than the element itself, because a reference to an element that was never
    // rendered does not arrive as nothing - it arrives as an object with none of an element's
    // properties, and asking that one for a dataset is what threw.
    if (!list?.dataset || list.dataset.semReorder) {
        return;
    }

    list.dataset.semReorder = 'on';

    let cards = [];
    let places = [];
    let held = null;
    let from = -1;
    let onto = -1;
    let grabbed = { x: 0, y: 0 };
    let pointer = { x: 0, y: 0 };

    // Where the list was scrolled to when the drag began, and what is doing the scrolling. The
    // places below are measured once, in viewport coordinates, and a list that scrolls underneath
    // the drag moves every card out from under them - so the distance the box has travelled since
    // is subtracted back out rather than everything being measured again. Measuring again would
    // read the cards where they have been shifted to, which is not where they belong.
    let scroller = null;
    let scrolledFrom = { left: 0, top: 0 };

    /** How far the list has scrolled since the drag started. */
    const drift = () => scroller
        ? { x: scroller.scrollLeft - scrolledFrom.left, y: scroller.scrollTop - scrolledFrom.top }
        : { x: 0, y: 0 };

    /** The nearest thing around an element that scrolls, which may be the element itself. */
    const scrollerOf = start => {
        for (let el = start; el && el !== document.body; el = el.parentElement) {
            const overflow = getComputedStyle(el).overflowY;

            if (overflow === 'auto' || overflow === 'scroll') {
                return el;
            }
        }

        return null;
    };

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
        scroller = null;

        document.removeEventListener('scroll', track, { capture: true });
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
        pointer = { x: event.clientX, y: event.clientY };
        scroller = scrollerOf(list);
        scrolledFrom = scroller
            ? { left: scroller.scrollLeft, top: scroller.scrollTop }
            : { left: 0, top: 0 };

        card.classList.add('lifted');
        list.classList.add('sorting');

        // Only for the length of the gesture. On the document and in the capture phase because a
        // scroll event does not bubble - the box that scrolls may be this list or something around
        // it, and only capture hears both - and taken off again in settle, because a listener left
        // on the document outlives the list it was closed over. The plan's tabs build a new list
        // each time one is chosen, so a permanent one here was a new listener per tab press.
        document.addEventListener('scroll', track, { capture: true, passive: true });

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

    /**
     * Draws the held card under the pointer and works out which place it is over, both corrected
     * for however far the list has scrolled since the drag began.
     *
     * Called on pointer movement and on scrolling, because a list scrolled by the wheel - or by a
     * finger dragging near its edge - moves the cards without the pointer moving at all. Without
     * that the drag went on pointing at the places the cards used to be in, and dropped one several
     * rows from where it looked.
     */
    const track = () => {
        if (!held) {
            return;
        }

        const { x: dx, y: dy } = drift();

        // The held card is inside the box that scrolled, so it has already been carried along with
        // it; adding the drift back puts it under the pointer, which did not move.
        held.style.transform =
            `translate(${pointer.x - grabbed.x + dx}px, ${pointer.y - grabbed.y + dy}px)`;

        // The places went the other way, so the pointer is compared against where they were rather
        // than where they now are. layOut needs no such correction: it works in differences between
        // places, and a shift they all share cancels.
        const target = nearest(pointer.x + dx, pointer.y + dy);

        if (target !== onto) {
            onto = target;
            layOut(onto);
        }
    };

    list.addEventListener('pointermove', event => {
        if (!held) {
            return;
        }

        pointer = { x: event.clientX, y: event.clientY };
        track();
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

/**
 * Draws a piece of the page into a PNG.
 *
 * The empire card is worth keeping and worth showing somebody, and neither is served by asking the
 * player to take a screenshot: what they would get is whatever their window happened to be, with the
 * buttons and the totals column in it, cut off wherever the scroll had stopped.
 *
 * So the card is redrawn rather than photographed. It is cloned, laid out in an iframe as wide as a
 * desktop window, and rasterised through an SVG foreignObject - which is the one way a browser will
 * render its own HTML into an image. Two things follow from that and both matter:
 *
 *   - An SVG used as an image loads nothing from outside itself. Every picture in the clone has to
 *     be a data URI before it goes in, and that includes the masks a flag is cut and coloured with,
 *     which are url() values inside style attributes rather than <img> elements.
 *
 *   - The media queries inside it read the SVG's own width, not the window's. That is what makes the
 *     result the same on a phone as on a monitor: the frame is stated here, the card is laid out
 *     against it, and the picture is cropped back to the card afterwards.
 *
 * The whole card is cloned, not the part of it wanted. Almost every rule that shapes what is
 * inside is written against the card as an ancestor - ".sem-lite .face-button" and its neighbours -
 * so cloning the inner column alone silently lost them, and the first version came out with a box
 * drawn round every title, portrait and flag: the plain button styling, showing through where the
 * card's own reset should have been. Cloning the card keeps them, and brings its background, its
 * border and its padding with it, which is the frame the picture wants anyway.
 *
 * @param {string} selector the card to draw
 * @param {string} omit what to take out of it, as a selector
 * @param {number} card how wide the part being drawn is, inside the card's own padding
 * @param {number} frame how wide to pretend the window is, so the wide arrangement applies
 * @param {number} across how many pixels wide the finished picture should be
 * @returns {Promise<Uint8Array|null>} the PNG, or null where there was nothing to draw
 */
export async function captureCard(selector, omit, card, frame, across) {
    const source = document.querySelector(selector);

    if (!source) {
        return null;
    }

    const styles = appStyles();
    const clone = source.cloneNode(true);

    for (const marker of clone.querySelectorAll(omit)) {
        marker.remove();
    }

    // The card lays itself out in two columns, one of which has just been taken out of it.
    clone.style.display = 'block';

    lastMisses = await inlineImages(clone);

    const stage = document.createElement('iframe');
    stage.setAttribute('aria-hidden', 'true');
    stage.style.cssText =
        'position:fixed;left:-20000px;top:0;border:0;visibility:hidden;' +
        'width:' + frame + 'px;height:100px';
    document.body.appendChild(stage);

    try {
        const doc = stage.contentDocument;
        doc.open();
        doc.write('<!doctype html><html><head><meta charset="utf-8"></head><body></body></html>');
        doc.close();

        const sheet = doc.createElement('style');
        sheet.textContent = styles;
        doc.head.appendChild(sheet);
        doc.body.style.margin = '0';

        const holder = doc.createElement('div');
        holder.style.cssText = 'width:' + card + 'px;' + typography();
        holder.appendChild(doc.importNode(clone, true));
        doc.body.appendChild(holder);

        // The card's own frame, measured rather than stated: the width asked for is the width of
        // what is being drawn, and the padding and border around it are the stylesheet's business.
        const drawnCard = holder.firstElementChild;
        const edges = doc.defaultView.getComputedStyle(drawnCard);
        const sides =
            parseFloat(edges.paddingLeft) + parseFloat(edges.paddingRight) +
            parseFloat(edges.borderLeftWidth) + parseFloat(edges.borderRightWidth);

        const width = Math.ceil(card + sides);
        holder.style.width = width + 'px';

        const height = Math.ceil(holder.getBoundingClientRect().height);

        // The root's own font size, because rem resolves against whatever the root turns out to be
        // and inside an image that is the <svg> rather than an <html> the stylesheet can reach.
        const root = getComputedStyle(document.documentElement).fontSize;

        const svg =
            '<svg xmlns="http://www.w3.org/2000/svg" width="' + frame + '" height="' + height + '"' +
            ' style="font-size:' + root + '">' +
            '<foreignObject x="0" y="0" width="' + frame + '" height="' + height + '">' +
            '<div xmlns="http://www.w3.org/1999/xhtml" style="' +
            attribute('width:' + frame + 'px;' + typography()) + '">' +
            '<style><![CDATA[' + styles + ']]></style>' +
            new XMLSerializer().serializeToString(holder) +
            '</div></foreignObject></svg>';

        // A data: URL, and it has to be one. An SVG handed over as a blob: URL loads and draws,
        // and then taints the canvas it was drawn on - the document inside it is treated as
        // foreign however same-origin the blob was - so toBlob throws SecurityError and there is
        // no picture at all. Only a data: URL is origin-clean here, which is why every library
        // that rasterises a DOM node uses one. The size that made a blob tempting is dealt with
        // where it belongs, in what goes into the document: see shrink().
        const drawn = await load('data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg));

        // Laid out at the card's full width and scaled to the width asked for, rather than laid
        // out at that width: the arrangement has to be the wide one wherever the picture is taken,
        // which is the whole reason for the frame above, and a card laid out at 720 would be the
        // narrow one instead.
        const canvas = document.createElement('canvas');
        canvas.width = across;
        canvas.height = Math.round(height * (across / width));

        const context = canvas.getContext('2d');

        // The card's own ground, painted first: the panels are drawn on it, and a picture with holes
        // in it is one that reads differently on every background it is put in front of.
        context.fillStyle = variable('--bg-panel') || '#151b24';
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.drawImage(drawn, 0, 0, width, height, 0, 0, canvas.width, canvas.height);

        const blob = await new Promise(done => canvas.toBlob(done, 'image/png'));

        return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
    } finally {
        stage.remove();
    }
}

/** A value safe to put inside a double-quoted XML attribute, which a font stack is not. */
function attribute(value) {
    return value
        .split('&').join('&amp;')
        .split('<').join('&lt;')
        .split('"').join('&quot;');
}

/**
 * What the page's own body says about text, as a style attribute.
 *
 * Neither the iframe's holder nor the picture's wrapper is a body, and the rules that set the app's
 * typeface, size and colour are written against one. Left to inherit, the picture came out in the
 * browser's default serif at the browser's default size - a card in a typeface the app does not use
 * and a fifth larger than it should be, because html's own 81.25% went with it.
 *
 * Stated on both, so the height measured in the iframe is the height the picture actually needs.
 */
function typography() {
    const body = getComputedStyle(document.body);

    return 'font-family:' + body.fontFamily +
        ';font-size:' + body.fontSize +
        ';font-weight:' + body.fontWeight +
        ';line-height:' + body.lineHeight +
        ';color:' + body.color;
}

/**
 * How many pictures the last capture could not embed.
 *
 * Kept here and read by a second call rather than returned with the bytes, because a Uint8Array
 * crosses into managed code as a byte array only when it is the whole answer - put inside an object
 * it is serialised as JSON and arrives as a map of indices. The count is diagnostic and small; the
 * bytes are neither.
 */
let lastMisses = 0;

/** What the last capture could not embed, for a caller deciding whether to say so. */
export function captureMisses() {
    return lastMisses;
}

/**
 * The app's own stylesheet, read back out of the page rather than fetched again.
 *
 * Every rule, media queries and all, because the arrangement the picture wants is the one those
 * queries decide. They are re-evaluated against the frame stated above rather than against the
 * window, which is the whole point of laying the clone out somewhere of a known width.
 */
function appStyles() {
    let text = '';

    for (const sheet of document.styleSheets) {
        let rules;

        try {
            rules = sheet.cssRules;
        } catch {
            // A stylesheet from another origin. None of ours are, and anything else is not the
            // app's to copy.
            continue;
        }

        for (const rule of rules) {
            text += rule.cssText + '\n';
        }
    }

    return text;
}

/** One of the palette's values, as the page has resolved it. */
function variable(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

/**
 * Turns every picture in a clone into a data URI.
 *
 * Both kinds. The img elements are the rooms, the portraits and the emblems; the url() values in
 * style attributes are the masks a flag is cut and coloured with, which are computed per empire and
 * so cannot live in the stylesheet.
 *
 * A picture that cannot be read is left as it was rather than the whole export being abandoned: a
 * card missing one icon is worth more than no card at all. But it is counted and the count handed
 * back, because a picture that is quietly wrong is worse than one that says so - a whole room going
 * missing looked like a mystery for exactly as long as this said nothing.
 *
 * @returns {Promise<number>} how many pictures could not be embedded
 */
async function inlineImages(root) {
    const held = new Map();
    let missing = 0;

    const fetched = url => {
        if (!held.has(url)) {
            held.set(url, encode(url));
        }

        return held.get(url);
    };

    const jobs = [];

    for (const picture of root.querySelectorAll('img')) {
        // Nothing is scrolled into view here, so a lazy image would never be asked for.
        picture.removeAttribute('loading');

        const source = picture.getAttribute('src');

        if (source) {
            jobs.push(fetched(new URL(source, document.baseURI).href).then(data => {
                if (data) {
                    picture.setAttribute('src', data);
                } else {
                    missing += 1;
                }
            }));
        }
    }

    for (const element of root.querySelectorAll('[style]')) {
        const style = element.getAttribute('style');

        if (!style || !style.includes('url(')) {
            continue;
        }

        const found = [...style.matchAll(/url\(\s*(["']?)([^"')]+)\1\s*\)/g)];

        jobs.push(Promise.all(found.map(match =>
            fetched(new URL(match[2], document.baseURI).href)))
            .then(datas => {
                // Rebuilt from where the matches were rather than by replacing their text. A
                // substring replace runs over a string that already holds inserted base64, so one
                // asset URL that happened to be a substring of another - or of a payload - would
                // corrupt the attribute. Nothing in the app does that today; the arrangement that
                // cannot is worth the few extra lines.
                let rewritten = '';
                let at = 0;

                found.forEach((match, i) => {
                    const data = datas[i];

                    if (!data) {
                        missing += 1;
                        return;
                    }

                    rewritten += style.slice(at, match.index) + 'url("' + data + '")';
                    at = match.index + match[0].length;
                });

                element.setAttribute('style', rewritten + style.slice(at));
            }));
    }

    await Promise.all(jobs);

    return missing;
}

/** One file as a data URI, or nothing where it could not be read. */
async function encode(url) {
    try {
        const response = await fetch(url);

        if (!response.ok) {
            return null;
        }

        const blob = await response.blob();

        return await shrink(blob) ?? await read(blob);
    } catch {
        return null;
    }
}

/** The size above which a picture is re-encoded rather than embedded as it was shipped. */
const HEAVY = 48 * 1024;

/** The widest a re-encoded picture is drawn, which is wider than the card it goes on. */
const BROADEST = 1200;

/**
 * A heavy picture encoded small, or nothing where it is not worth it or cannot be done.
 *
 * The whole card becomes one document and that document becomes one data: URL, so what is embedded
 * is charged twice: base64 inflates it by a third, and the URL is then the length of the lot. The
 * rooms are 264 KB apiece and a city band reaches 535 KB - a megabyte or two per card before
 * encoding - and a phone handed a data: URL that long refuses it silently, drawing nothing where
 * the room should be and leaving the void behind it showing. Everything else in the card is a few
 * kilobytes and survives, which is exactly the shape the bug had.
 *
 * PNG at full resolution is the wrong format for a photograph of a city, which is what these are.
 * Re-encoded as WebP with alpha kept, a room lands around a twentieth of its shipped size, and it
 * is drawn at most 1200 across - wider than the 952 the card gives it, so nothing visible softens.
 *
 * Decoded straight from the blob rather than through an object URL, which keeps the canvas
 * origin-clean: createImageBitmap on a Blob has no origin to inherit.
 */
async function shrink(blob) {
    if (blob.size < HEAVY || !blob.type.startsWith('image/') || blob.type === 'image/svg+xml') {
        return null;
    }

    if (typeof createImageBitmap !== 'function') {
        return null;
    }

    let bitmap;

    try {
        bitmap = await createImageBitmap(blob);
    } catch {
        return null;
    }

    try {
        const scale = Math.min(1, BROADEST / bitmap.width);
        const canvas = document.createElement('canvas');

        canvas.width = Math.max(1, Math.round(bitmap.width * scale));
        canvas.height = Math.max(1, Math.round(bitmap.height * scale));

        const context = canvas.getContext('2d');
        context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);

        const small = canvas.toDataURL('image/webp', 0.9);

        // A browser with no WebP encoder hands back a PNG instead, which for these is bigger than
        // what arrived. Taken only when it is actually smaller.
        return small.startsWith('data:image/webp') && small.length < blob.size * 1.37 ? small : null;
    } catch {
        return null;
    } finally {
        bitmap.close();
    }
}

/** A blob as a data URI, exactly as it arrived. */
function read(blob) {
    return new Promise(done => {
        const reader = new FileReader();
        reader.onload = () => done(reader.result);
        reader.onerror = () => done(null);
        reader.readAsDataURL(blob);
    });
}

/** An image, once the browser has finished with it. */
function load(source) {
    return new Promise((done, failed) => {
        const image = new Image();
        image.onload = () => done(image);
        image.onerror = () => failed(new Error('The picture could not be drawn.'));
        image.src = source;
    });
}
