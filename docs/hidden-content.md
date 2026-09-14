# Content the game hides

The game's own empire designer draws less than the game will accept. Some content is switched off in
the creator and still works perfectly well in a design; some is switched off because naming it breaks
the empire. **Nothing in the files tells the two apart** - both are a boolean in a data file, and in
one case they are even spelled the same way. So each kind was tested in the game before being
offered here, and this page records what came back.

The test is the same every time: put one design in `user_empire_designs_v3.4.txt` that differs from a
known-good empire in exactly one field, start Stellaris, and look for the empire in the list.

## What works, and is offered

### Flag emblems - `show_in_designer = no`

Three categories carry it in their `flags/<category>/usage.txt`: `enclaves` (3 emblems), `pre_ftl`
(10) and `special` (27). The game's own scripts hand these out by writing exactly the category and
file name a design stores - see `common/scripted_effects/enclave_effects.txt`, which writes
`icon = { category = "special" file = "salvagers.dds" }` - and an empire built with one loads.

They are extracted like any other category and marked `IsOffered = false`, and the flag picker puts
them below a divider. The game ships no `FLAG_CATEGORY_` string for them, because nothing was ever
meant to display them, so their headings are ours.

### Name lists - `selectable = { always = no }`

Sixteen of the eighty: `default`, `bio_ship`, `AI`, `CETANA`, `Cybrex`, `Extradimensional`, `GDF`,
`graygoo`, `IA`, `Prethoryn`, `PRT1`, `Tiyanki`, `SpaceAmoeba`, `CrystallineEntity`, `Voidworm`,
`Cutholoid`. Each is fully populated with ship, planet, leader and species names. Verified in game
with `name_list = "Prethoryn"` on an ordinary empire: it plays.

They are grouped at the end of the name list dropdown under a heading of their own.

## What does not work, and is not offered

### Shipsets - `selectable = { always = no }`

**This is the trap.** Thirty graphical cultures carry the same key as the name lists above, and the
data looks identical from the outside: the models exist, seven of them declare their own `ship_kinds`
and so build genuinely distinct fleets, and we already bake a preview for all fifty-two.

It does not work. Tested with `graphical_culture = "fallen_empire_01"` and again with
`city_graphical_culture = "fallen_empire_01"`; either one left the empire unplayable.

So `selectable = { always = no }` means two different things depending on what it is attached to,
and a graphical culture is the case where it is a hard gate rather than a curtain. The filter in
`ShipsetTab` is correct as written and should stay. **If this looks like an oversight later, it is
not - it is this test.**

### Species classes - `playable = { always = no }`

Nineteen classes carry it, and an empire naming one does not appear in the list. Confirmed with a
design using `class = "MSI_SLAVER"` and `portrait = "msi_slaver1"`, which is in the game's files as
a First Contact enclave and is not a prescripted empire: the design sits in the file and the game
does not offer it.

This also settles the portraits. A portrait is only reachable if a category names its set, and
twenty sets are named by no category - nineteen of them hang off one of those unplayable classes.
The twentieth is `robots`, whose class `ROBOT` is gated on `playable = { has_global_flag = game_started }`,
a flag that is by definition unset while an empire is being designed; and its portrait list is
identical to that of `machines`, which is offered already. **There is no hidden portrait sitting on
a class a player can pick.**

## What the designer cannot reach, by a third route

### Civics gated on already having something - `potential = { has_civic = <itself> }`

Found while building the wiki, by reading the conditions rather than by playing. Six civics and three
origins are out of reach for a reason the survey below was not looking for: not the kind of country
you are, but something you would have to be holding already.

Four of them ask for themselves outright. `civic_galactic_sovereign`, its megacorp twin,
`civic_psionic_sovereign` and `civic_great_khans_vision` each carry
`potential = { has_civic = <that same civic> }` and `can_add_later = no`, which is how the game says
"only an empire I have already granted this to". Two more, `civic_diadochi` and
`civic_great_khans_legacy`, ask for the Khan's Vision, so they are out of reach because it is. The
three `origin_legendary_leader_*` origins are a ring: each asks for any one of the three, and nothing
can put the first one in your hand.

**Not tested in game, and it does not need to be.** Nothing here is being offered - the question is
only what the wiki labels, and the app's own rules engine already hides every one of the nine when
asked for a blank empire's options. That agreement is what `CivicCorpusTests` pins.

`CivicReach` is where the rule lives. It reads the whole shelf at once for this reason: a civic that
is out of reach because another one is cannot be judged on its own, and neither can a ring.

## Checked and found complete

No gap between the installation and what is offered: flag backgrounds (63), flag colours (72),
ethics (17), advisor voices (27), species archetypes (6), leader classes, starting ruler traits (34 -
the twenty-four marked `initial = no` are gated on an origin, and the game offers them once that
origin is picked, which is what we do), civics (none are `always = no`; forty-seven of the fifty-three
that never appear require a country type a player can never be, and the other six are the section
above), origins (nineteen hidden - sixteen marked unplayable outright, three the ring above), species
traits, homeworlds (eighteen reachable), portrait categories, prescripted empires and starting
systems.

## Adding to this page

If you find something else the designer hides, test it before offering it, and write the result here
either way. A refusal recorded is worth as much as a success: the shipsets above look like an easy
win in the data and cost an evening to disprove.
