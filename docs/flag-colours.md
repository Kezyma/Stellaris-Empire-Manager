# The six flag colours

A design's `empire_flag` block writes six colours, and since Stellaris 4.5 it may carry a switch
beside them:

```
empire_flag=
{
	icon={ category="human" file="flag_human_4.dds" }
	background={ category="backgrounds" file="flag_BG_31.dds" }
	colors=
	{
		"red"
		"red"
		"blue"
		"yellow"
		"red"
		"red"
	}
	use_map_color=yes
}
```

Three of them are the flag, one is the fleet, and two are the galaxy map. **This was worked out twice
from evidence and got wrong both times.** In 4.5 the game finally documents it, so this page quotes
the game rather than reasoning towards an answer.

---

## What each slot is

From the header of `flags/colors.txt`, lines 94-117, verbatim:

```
#   randomizable_combo = { slot0 slot1 slot2 slot3 slot4 slot5 }
#
#   slot0  Primary color    Drives the red channel of the flag background texture.
#           Also the derived map border color and the fallback ship entity
#            tint when no ship color is explicitly set.
#   slot1  Secondary color  Drives the green channel of the flag background texture.
#           Also the derived map territory fill color (the most visually distinct
#           secondary swatch relative to slot0).
#   slot2  Tertiary color   Drives the blue channel of the flag background texture.
#           No special gameplay role beyond flag rendering.
#   slot3  Ship color       Inert in the flag shader (alpha channel line commented out).
#           When use_ship_color = yes, this swatch's ship = rgb value is used as the
#           entity tint on all ships and structures. Mirrors slot0 in all combos so
#           randomized empires default to the primary swatch's ship color.
#   slot4  Map border color Inert in the flag shader. When use_map_color = yes, this
#           swatch's map = rgb value overrides the map border color. Mirrors slot0.
#   slot5  Map fill color   Inert in the flag shader. When use_map_color = yes, this
#           swatch's map = rgb value overrides the map territory fill. Mirrors slot1.
```

Said again, independently, by the localisation
(`localisation/english/main_1_l_english.yml:3353-3364`):

```
 PRIMARY_COLOR:0 "Primary Flag Color"
 SECONDARY_COLOR:0 "Secondary Flag Color"
 TERTIARY_COLOR:0 "Tertiary Color"
 BORDER_COLOR: "Map Border Color"
 FILL_COLOR: "Map Fill Color"
 USE_MAP_COLOR: "Independent Map Color"
 USE_MAP_COLOR_TOOLTIP: "When enabled, this empire's galaxy map border and fill use the selected
   colors. When disabled, they are derived from the Primary and Secondary Flag colors."
 SHIP_COLOR_TOOLTIP: "When enabled, ships will use the selected color. When disabled, ships will
   use the Empire's primary Flag color."
```

Note that 4.5 **renamed** the first two — they were "Primary Colors" and "Secondary Colors" and are
now "Primary/Secondary **Flag** Color", to tell them from the new map ones.

And by the game's own parser help, which lives as a string in `stellaris.exe` at `0x24a0edb`:

```
# Four color swatches (keys from flags/colors.txt). Slots: 0=primary (red), 1=secondary (green),
# 2=tertiary (blue), 3=ship color (alpha, shader-inert). Use null for unused slots.
colors = { <key> <key> <key> <key> }
use_ship_color = yes/no # default no
# When yes, slot 3 swatch ship= value is used for ships Slot 3 must be a real swatch.
```

## Each colour is three colours

`flags/colors.txt` keeps **three** RGB values against every one of its 72 names, and they differ:

| | flag | map | ship |
|---|---|---|---|
| `red` | 158 22 22 | 151 14 18 | 255 57 36 |
| `frog_green` | 168 218 39 | 209 241 126 | — |

So a swatch shown in the wrong column is the wrong colour. The app draws the map pickers in `map`
and the ship picker in `ship`, and ships are lit, which is why their column is much the brightest.
48 of the 72 have identical `flag` and `map` values, which is exactly why the error is easy to miss.

## How it is written

- Both switches live **inside `empire_flag`**, after the `colors` block.
- `use_map_color=no` and `use_ship_color=no` are **never written**. Absence is the off state. The
  app therefore writes `yes` or removes the key, and never writes `no` — a file that round-trips
  byte for byte is what `Sem.Designs` promises, and a stray `=no` would appear in every empire that
  has never touched the switch.
- When off, the unused slots hold the quoted literal `"null"`. The list is always six entries.
- Files written before 4.5 have **four**. They are read as they stand, and grown to six the first
  time something is written — which is what the game itself did on first load.

## The one thing still unknown

When slot 1 is too close to slot 0, the game substitutes slot 2 for the fill. The rule is real — it
can be watched happening in the 4.5 editor's preview chips — but **the threshold is not in the game
files**. Searched and closed:

- `flags/colors.txt` gives the slot map and no threshold.
- `common/defines/00_defines.txt` is byte-identical to 4.4.6; there is no similarity define.
- All 65 `*COLOR*` define names in `stellaris.exe` were enumerated. None relates — so it is not a
  define with a code default either.
- Binary string searches for "similar", "visually distinct", "falls back", "derived from" return
  nothing related.
- The editor panel is declarative: each preview chip is a `GFX_flag_swatch_border` background plus a
  `GFX_flag_colorpicker` icon, tinted by name from C++. `flag_colorpicker.dds` is 20x20 of pure
  opaque white.

Two things bound it. The 226 `randomizable_combo` rows are the game's own hand-authored slot0/slot1
pairs, and the closest are `teal`/`dark_teal` at **36.7** apart in RGB; if the test fired at that
distance the game would substitute on its own generated flags constantly, so the threshold is
**below 36.7** and near-identity is the likely trigger. And because the chips are flat white
underneath, a sampled screen pixel is the answer to the byte — `border_color_preview` sits at
`{ x = 776 y = 107 }` and `fill_color_preview` at `{ x = 776 y = 136 }`, both 18x18.

Until it is settled the app derives the fill from slot 1 unconditionally, which is right whenever
slots 0 and 1 differ. Probe colours for settling it want a wide `map`/`flag` divergence:
`frog_green` (99 apart), `brown` (61), `dark_brown` (60).

---

## Two warnings worth leaving here

**The first.** An earlier version of this app kept a private "tag" colour in what it called slot 4,
on the strength of the flag shader not reading it. Every fact in that argument was correct and the
conclusion did not follow: "the flag shader does not read this" is not "nothing reads this", and the
only file that had been searched was the one that draws flags.

**The second.** The fix for that was to call slots 2 and 3 the map's border and fill. That was also
wrong — they are the tertiary flag colour and the ship tint, and the map's own pair did not exist
until 4.5 added slots 4 and 5. The evidence was again all true: nothing is drawn in any background's
blue channel (across all 63, the brightest blue pixel is 11 of 255), the fourth shader line is
commented out, and setting those slots really did change the map. What it could not show was *which*
slot the map was reading, because that is not in any file — and the answer was neither of them.

The lesson both times is the same. Where the game does not state something, the app should say it
does not know, rather than infer it from what the game does state about something else. That is why
the section above stops where it stops.

## How to check this again after a patch

The slot map, which is now documented rather than deduced:

```bash
sed -n '90,120p' flags/colors.txt
```

The designer's controls and the words the game has for them:

```bash
grep -rn "PRIMARY_COLOR\|SECONDARY_COLOR\|TERTIARY_COLOR\|BORDER_COLOR\|FILL_COLOR\|USE_MAP_COLOR" \
    interface/ localisation/english/
```

Whether a new switch or slot has appeared, across the whole install:

```bash
grep -rln "use_map_color\|use_ship_color\|use_as_border_color" common/ interface/ gfx/ flags/
```

And what the game writes for a real empire — the only place the player-design format is visible at
all, since prescripted empires still carry four colours:

```bash
grep -A 12 "empire_flag=" "$USER_DOCUMENTS/Paradox Interactive/Stellaris/user_empire_designs_v3.4.txt"
```
