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
| M3 — Game data extraction | Done |
| M4 — Rules and validation engine | Done |
| M5 — Icons, flags and image pipeline | Done |
| M6 — Designer UI and web app | Done |
| M7 — Desktop application | Done |
| M8 — Portrait rendering | Done |
| M9 — Hardening and first release | Next |

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

To read your installation into the game database the designer runs on:

```bash
dotnet run --project src/Sem.Cli -- extract
```

To check the rules engine against the game's own built-in empires:

```bash
dotnet run --project src/Sem.Cli -- validate
```

To run the desktop app, which finds your installation and designs file by itself:

```bash
dotnet run --project src/Sem.Desktop
```

To run the web app locally, extract into it and start the site:

```bash
dotnet run --project src/Sem.Cli -- extract --web
```

```bash
dotnet run --project src/Sem.Web
```

**Your game install and your real empire designs are never modified during development.** This is
enforced in code, not by convention — see [docs/file-safety.md](docs/file-safety.md).

## How it fits together

The game's files are read once into a database of everything the designer needs: every option, the
conditions that gate it, its icon, and its description. The desktop app builds that from your own
installation and caches it; the web app ships one built at publish time. Both then run the same
designer, which is why the two behave identically.

Conditions are the interesting part. Stellaris expresses them in three different grammars, and all
three compile into one expression tree that is evaluated against whatever you have currently
chosen. When an option is unavailable, the explanation shown is the one the game's own script
attaches to that condition, so you read the same words the game would tell you.

Portraits are models rather than pictures, so there is nothing to copy out. They are drawn during
extraction by a small renderer that reads Paradox's model format.

Nothing is written back except by an explicit save, and only ever to your designs file.

## Verifying a change

Automated tests prove the file is written correctly; only the game can prove it accepts what was
written. [docs/in-game-test.md](docs/in-game-test.md) is the checklist for that.

## Licence

Source code is MIT licensed (see [LICENSE](LICENSE)). Game data and artwork extracted from
Stellaris remain the property of Paradox Interactive and are not distributed in this repository.
