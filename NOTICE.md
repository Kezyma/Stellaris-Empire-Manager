# Notice and attribution

Stellaris Empire Manager is an unofficial, non-commercial fan project. It is not affiliated with,
authorised by, sponsored by, or endorsed by Paradox Interactive AB.

Stellaris and all associated game data, text, icons, artwork and trademarks are the property of
Paradox Interactive AB.

## What this repository contains

Only original source code, licensed under the MIT Licence (see `LICENSE`).

**No Stellaris game files, extracted data, or artwork are committed to this repository.** The
`.gitignore` excludes the `sandbox/` working directory and every extracted-data output path.

## How the applications obtain game content

- **Desktop.** Reads the game files already present in the user's own legitimate installation.
  Nothing is redistributed; the application only reads what the user already owns.
- **Web.** Ships a pre-extracted data set so the designer can run in a browser. This is limited to
  what the tool needs to function — identifiers, rules, localised strings, and low-resolution
  icons and thumbnails — and is served separately from the application itself so it can be
  withdrawn without breaking the app, which then falls back to placeholder imagery.

## The published site

The source repository holds no game content and is not the thing that is deployed. The site is built
from a local Stellaris installation by `scripts/deploy-pages.ps1` and pushed to a separate `gh-pages`
branch, which does carry the extracted data and artwork. That branch is the only place game content
appears, and replacing rather than adding to it on each deploy keeps exactly one copy there.

## Before public release

Review Paradox Interactive's current User Agreement and user-generated content policy, and seek
their confirmation, before pushing that branch or publishing any other site that serves extracted
game assets.

## Third-party components

All bundled dependencies are used under permissive licences (MIT or Apache-2.0). Their notices are
included in the published build output.
