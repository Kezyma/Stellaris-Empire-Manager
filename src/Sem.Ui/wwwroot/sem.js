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
