# Stellaris Empire Manager

Build, edit and manage Stellaris empire designs — as a Windows desktop app pointed at your own
game install, and as a browser app that never uploads your files anywhere.

> Unofficial fan project. Not affiliated with or endorsed by Paradox Interactive.
> See [NOTICE.md](NOTICE.md).

## What it does

- Extracts every empire-design option from the game files: species classes, portraits, traits,
  ethics, authorities, civics, origins, name lists, homeworlds, starting systems, flags, rooms,
  city sets, advisor voices — along with their icons and localised text.
- Enforces the same rules the game does. Trait point and pick budgets, ethics costs, civic
  requirements, origin incompatibilities and DLC gating are all evaluated live, and blocked options
  explain themselves with the game's own tooltip text.
- Reads and writes `user_empire_designs_v3.4.txt` faithfully, preserving fields it does not
  understand so a future patch or a mod cannot silently cost you an empire.
- Lets you start from any of the 53 built-in prescripted empires and save the result as your own.

## Status

Early development. See [the milestone plan](docs/) for what is built and what is next.

| Milestone | Status |
| --- | --- |
| M0 — Repository bootstrap and file-safety guard | Done |
| M1 — Clausewitz parser and byte-exact writer | Done |
| M2 — Empire design model and mappers | Done |
| M3 — Game data extraction | Next |
| M4 — Rules and validation engine | |
| M5 — Icons, flags and image pipeline | |
| M6 — Designer UI and web app | |
| M7 — Desktop application | |
| M8 — Portrait rendering | |
| M9 — Hardening and first release | |

## Repository layout

```
src/Sem.Clausewitz   Paradox script tokenizer, lossless tree, byte-exact writer
src/Sem.Designs      Empire design model and the user/prescripted file mappers
src/Sem.GameData     Extracted game database contracts and the requirement expression AST
src/Sem.Rules        Validation, budgets and derived values
src/Sem.Io           All filesystem access, behind the write guard
src/Sem.Assets       DDS decoding, PNG encoding, flag and icon baking
src/Sem.MeshBake     Portrait mesh parsing and software rendering
src/Sem.Extraction   The staged extraction pipeline
src/Sem.Ui           Shared Blazor designer components
src/Sem.Web          Blazor WebAssembly host (static site, no backend)
src/Sem.Desktop      WPF + BlazorWebView host (Windows)
src/Sem.Cli          Development and build tooling
```

## Getting started

Requires the .NET 10 SDK. On Windows, with Stellaris installed:

```bash
dotnet run --project src/Sem.Cli -- devsync
```

That copies your real game files into a gitignored `sandbox/` directory, which is the only place
development is permitted to write. Then:

```bash
dotnet test
```

**Your game install and your real empire designs are never modified during development.** This is
enforced in code, not by convention — see [docs/file-safety.md](docs/file-safety.md).

## Licence

Source code is MIT licensed (see [LICENSE](LICENSE)). Game data and artwork extracted from
Stellaris remain the property of Paradox Interactive and are not distributed in this repository.
