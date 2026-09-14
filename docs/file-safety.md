# File safety

This project reads a Stellaris installation and rewrites the player's empire designs file. Both
are irreplaceable to the person running it: an install can be re-downloaded, but a designs file
full of hand-built empires cannot. The rule below is therefore enforced in code, not by convention.

## The rule

**Development never modifies the real game install or the real empire presets.** All work happens
against copies in the gitignored `sandbox/` directory.

## How it is enforced

Every write in the solution goes through `SafeFile` (in `Sem.Io`), which consults a `WritePolicy`
before touching the disk. A policy holds two sets of roots:

- **Allowed roots** — writes are permitted here and nowhere else. An empty set denies everything.
- **Forbidden roots** — writes are refused here regardless of what is allowed. Forbidden always wins.

`SandboxLayout.CreateDevelopmentPolicy()` builds the policy used during development and by tests:

| Location | Writable |
| --- | --- |
| `sandbox/` | yes |
| Process temp directory | yes |
| `%LocalAppData%\StellarisEmpireManager` | yes |
| The detected Stellaris installation | **never** |
| The detected Stellaris game data folder | **never** |
| Anywhere else | no |

A refused write throws `ForbiddenWriteException` naming the path, the reason and the policy. It
does not fall back, warn, or partially succeed.

Path comparisons resolve symlinks and junctions before testing containment. This matters here:
the development machine's Documents folder is redirected into OneDrive, so a plain string prefix
check would not recognise the real game data folder at all.

Reads are deliberately unrestricted, and always open files with `FileShare.ReadWrite | FileShare.Delete`
so this process never blocks the game, OneDrive or a virus scanner, and never fails because one of
them holds a handle.

## Filling the sandbox

```bash
dotnet run --project src/Sem.Cli -- devsync
```

This copies, one way only:

- the player's `user_empire_designs_v3.4*.txt` files (live and dated backups) plus small metadata
  files, into `sandbox/userdata/`;
- `launcher-settings.json`, all of `prescripted_countries/`, and every DLC descriptor, into
  `sandbox/gamefiles/`.

Bulk game data is not mirrored. Extraction reads it in place, which is safe because reads cannot
alter anything, and copying gigabytes would only add a way to get out of sync.

`devsync` refuses to run with the sandbox as its source, so it cannot be inverted into a command
that overwrites real files.

## Writing to real files

Four paths deliberately reach the player's real designs file, and all four are opt-in:

1. **The shipped desktop application.** It runs under `WritePolicy.ForApplication()` and adds the
   designs file's directory only once the user has chosen it. Every save then goes through
   `SafeFile.ReplaceAtomically`: content is staged beside the target, verified, and swapped in with
   `File.Replace`. Two copies of what it replaced are kept - see below.
2. **The `deploy-design` CLI command** used for in-game verification, which archives the existing
   file before copying an export over it.
3. **A file at a cloud provider, once connected to one.** See below; it is not the browser's Export
   and its guarantees are different.
4. **The web app's Export, through the browser's save dialog.** The weakest of the four, and
   deliberately the narrowest.

Neither of the first two is reachable from the development policy.

### The two copies the desktop keeps, and when there is only one

`SafeFile.DatedBackupPath` puts a copy beside the designs file, named after it and stamped with the
moment; `FileArchive` puts one in the app's own folder under `%LocalAppData%`, keeping the newest
twenty. Both hold **what the save replaced**, not what it wrote.

That last sentence is worth its emphasis, because for a long time the archive held the other side:
`Archive(contents)` was handed the incoming bytes, so the folder recorded what each save created and
never what it destroyed. `FileArchive` reads the file itself now rather than being told what is in
it, which is why there is no longer a wrong thing to pass, and `tests/Sem.Core.Tests/Io/` covers it.

**The dated sibling is suppressed while write-as-you-go is on.** Saving then happens several times a
minute and one dated file per save would bury the folder the game keeps its saves in, so
`SessionHost.KeepsBackup` turns it off and the archive is the whole of the way back. That is the
arrangement the bug above made worthless, and the reason it mattered more than it looked.

### What a cloud save is, and is not

Connecting to a file at a provider points Save at that file, and it is the player's real designs file
- usually the same one, reached through the folder Windows redirects into OneDrive. `Sem.Ui` cannot
use `SafeFile` or `WritePolicy` here and would gain nothing if it could: the write is an HTTP request
to somebody else's storage, not a path on a disk. What it does have instead:

- **A dated copy beside it**, written before the save (`CloudFileExchange`), which is the provider's
  equivalent of the sibling backup.
- **A refusal to write over a change it has not seen.** The version stamp the file had when this app
  last read or wrote it is sent as `If-Match`; the provider rejects the write if the file has moved
  since, and the app asks what to do rather than overwriting. The same promise the desktop makes by
  reading the file back and comparing.
- **No archive of the last twenty**, and no atomic swap - the provider's own write is what it is.

### What the browser's write is, and is not

A page cannot reach the disk. It can only ask the browser to show a save dialog, and is then handed
a writer for the one file the person named in it — the browser is the guard, and it is not this
project's code. Export asks every time: no file handle is kept between saves, so there is no
standing permission to write anything, and dismissing the dialog writes nothing and leaves the work
marked unsaved.

**None of the guarantees above apply to it** — not the desktop's, and not the cloud path's either.
There is no dated sibling backup, no archive of the last twenty, no conflict check, no `SafeFile` and
no `WritePolicy`: `Sem.Ui` does not reference `Sem.Io` and a WebAssembly build could not use it if it
did. What the browser does provide is that the write is staged and committed when the stream closes,
so a tab that dies mid-save leaves the file as it was.

That is worth stating plainly rather than leaving implied: the desktop app is still the safe way to
write this file, a cloud file is the careful way to do it from a tab, and the web app's Export is a
convenience that trades those protections for not having to find the folder by hand. Anyone who would be sorry to lose a designs file should keep a
copy of their own, which is what the site's own README says.

## Tests

`tests/Sem.Core.Tests/Io/` covers the guard directly, including the sibling-prefix trap
(`data` must not appear to contain `data-backup`), relative traversal out of an allowed root, and
a real-data test asserting that the actual installation and game data folder on this machine are
unwritable. `FileArchiveTests` covers the copies: that what is kept is what was replaced and not
what replaced it, that twenty survive and the oldest goes, and that an archive which cannot be
written does not stop the save. Tests tagged `Category=RealData` skip cleanly when Stellaris is not installed.
