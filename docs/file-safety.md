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

Two paths deliberately reach the player's real designs file, and both are opt-in:

1. **The shipped desktop application.** It runs under `WritePolicy.ForApplication()` and adds the
   designs file's directory only once the user has chosen it. Every save then goes through
   `SafeFile.ReplaceAtomically`: content is staged beside the target, verified, swapped in with
   `File.Replace`, and the previous version is kept as a backup.
2. **The `deploy-design` CLI command** used for in-game verification, which archives the existing
   file before copying an export over it.

Neither is reachable from the development policy.

## Tests

`tests/Sem.Core.Tests/Io/` covers the guard directly, including the sibling-prefix trap
(`data` must not appear to contain `data-backup`), relative traversal out of an allowed root, and
a real-data test asserting that the actual installation and game data folder on this machine are
unwritable. Tests tagged `Category=RealData` skip cleanly when Stellaris is not installed.
