# Special empire flags

The field the designer calls **Special empire flags**, and the game writes as `flag="empire_human_1"`,
has nothing to do with the flag you draw. It names an entry in
`common/prescripted_flags/00_default_empire_flags.txt`, and each entry is a list of **country
flags** — the scripted boolean markers events and triggers read with `has_country_flag`. Setting one
is how a design tells the rest of the game's scripts "I am that empire".

```
empire_human_1 = {
	flags = { human_1 custom_start_screen }
}
```

There are **21** entries, setting **22** distinct country flags between them. Seven of the twenty-one
carry a flag the game actually reads. The other fourteen are set and never looked at.

Everything below was established by reading Stellaris **v4.4.6** on disk: the definitions, then every
occurrence of each flag name across `common/`, `events/`, `map/` and the rest of the script tree.

The game's own empire creator has **no control for this** — `interface/customize_species_editors.gui`
contains nothing that sets it, so it reaches a design only by being one of the game's own empires or
by being edited in a tool like this one.

---

## The seven that do something

### `empire_human_1` — United Nations of Earth

Sets `human_1` and `custom_start_screen`.

- **Counts as a human empire.** The scripted trigger `is_human_prescripted_empire` is defined as
  `human_1 OR human_2 OR human_3` (`common/scripted_triggers/00_scripted_triggers.txt:4679`) and is
  used **37 times across 12 files**. That is the widest single consequence of any flag here.
- **Sol and the Lost Colony.** The system initializers check it: if a `human_2` or `human_3` empire
  exists and no `human_1` empire does, the game spawns Sol with an **NPC United Nations of Earth**
  and flags it `human_1` and `lost_colony_parent` (`common/solar_system_initializers/sol_initializers.txt`).
  Being `human_1` yourself is what stops that from happening.
- **Bespoke first contact** with the other human empires — the Commonwealth of Man, the Gundersen
  Research Society and the Federated Theian Preservers each have a scripted introduction with the UNE
  rather than the generic one (`events/on_action_events_1.txt`).
- **A hostile opening opinion** where it meets the lithoid humans: the pair get
  `opinion_hostile_first_contact_hungry`
  (`common/scripted_triggers/02_scripted_triggers_first_contact.txt`).
- **Its own opening narration**, `START_SCREEN_UNE` — whose trigger is `is_nomadic = no` as well as
  the flag, so a nomadic empire carrying this preset gets neither the UNE's narration nor, because of
  `custom_start_screen`, the generic one.

### `empire_human_2` — Commonwealth of Man

Sets `human_2` and `custom_start_screen`.

- Counts as a human empire, as above.
- **Triggers the Sol spawn**: with no `human_1` empire in the galaxy, the initializers create Sol and
  an NPC UNE as this empire's lost-colony parent.
- Bespoke first contact with the UNE.
- **Changes what other empires call you before first contact.** It adds weight 10 to one
  pre-communications name format, `"{<un_second> {<com_first>}}"`
  (`common/random_names/00_pre_communications_names.txt:297`).
- Its own opening narration, `START_SCREEN_CM`, and it suppresses the Lost Colony nomads narration.

### `empire_human_3` — Gundersen Research Society

Sets `human_3` and `custom_start_screen`.

- Counts as a human empire, as above.
- Takes part in the same Sol / lost-colony logic as `human_2`.
- Bespoke first contact with the UNE.
- **Makes one archaeological site visible.** A dig site in
  `common/archaeological_site_types/15_nomads_arc_sites.txt` has `visible = { has_country_flag = human_3 }`,
  so only this empire is shown it.

### `empire_human_lithoid` — Federated Theian Preservers

Sets `human_lithoid` and `custom_start_screen`.

- **Not** part of `is_human_prescripted_empire`, so it misses the 37 checks the three above share.
- Bespoke first contact with the UNE, in both directions.
- The hostile opening opinion with the UNE described above.
- Its own opening narration, `START_SCREEN_LITHOID` and `START_SCREEN_LITHOID_HUMAN`.

### `empire_human_plantoid` — Blooms of Gaea

Sets `human_plantoid`. **Note it does not set `custom_start_screen`** — the only human preset that
does not, so this empire gets the generic opening narration.

- **Counts as human for event text without being human.** It is checked beside
  `is_human_prescripted_empire` in envoy events, two federation event files and situation events, in
  the form `OR = { is_human_prescripted_empire = yes  has_country_flag = human_plantoid }`. The
  effect is that a plantoid empire descended from humans gets the human-flavoured branches of those
  events.

### `empire_necrophage_start_empire` — Pasharti Absorbers

Sets `necrophage_start_empire`. One use, and it is conditional on something a player never satisfies:

```
if = {
	limit = {
		is_ai = yes
		has_country_flag = necrophage_start_empire
		has_ethic = ethic_fanatic_xenophobe
		has_ethic = ethic_militarist
	}
	force_remove_civic = civic_cutthroat_politics
	force_add_civic = civic_fanatic_purifiers
}
```
— `events/game_start.txt:2317`

So when the game runs this design **as an AI empire** and it is fanatic xenophobe and militarist, it
swaps Cutthroat Politics for Fanatic Purifiers. **A player-controlled empire carrying this flag gets
nothing at all**, because of `is_ai = yes`.

### `empire_blorg` — Blorg Commonality

Sets `prescripted_blorg`. One use, and it is a suppression:

`events/astral_planes_events.txt:5180` — the country event `astral_planes.6095` will not fire for an
empire if **any playable country has this flag**. So having a Blorg empire in the galaxy stops that
event happening to everyone else. The event's other conditions concern the `fun12` portrait and a
`mercedes_spawned` global flag, which is to say: it exists so the Blorg's own character is not
duplicated elsewhere.

---

## `custom_start_screen`, which four of them share

Not an effect of its own so much as a switch. The game builds its opening narration from parts in
`common/start_screen_messages/00_start_screen_messages.txt`, each with a trigger. The flag is checked
**53 times** there, and **52 of those are suppressions** of the form:

```
trigger = {
	ideal_planet_class = pc_continental
	NOR = {
		has_country_flag = custom_start_screen
		is_hive_empire = yes
		is_machine_empire = yes
	}
}
```

So the flag means *this empire has narration of its own, do not print the generic kind*. It is set by
the four human presets that have bespoke text, and separately by **53 origins** in
`common/governments/civics/00_origins.txt`, which have their own opening lines for the same reason.

The one non-suppressing use is `START_SCREEN_POST_APOCALYPTIC_MACHINES`, which *requires* the flag —
almost certainly aimed at the origin-set instances rather than these.

**Consequence worth knowing:** putting one of the four human presets on an empire that is not
otherwise a human empire silences the generic opening narration and puts that empire's in its place.
The replacement carries its own conditions, though — `START_SCREEN_UNE` also wants `is_nomadic = no`
— so a combination that fails them ends up with no opening narration at all, the generic one having
been switched off by a flag whose replacement never fires.

---

## The fourteen that do nothing

Each of these defines exactly one country flag, and **that flag appears nowhere else in the game
files**. Setting them is inert in v4.4.6.

| Preset | Country flag | Empire it belongs to |
|---|---|---|
| `empire_the_voor` | `the_voor` | Voor Technocracy |
| `empire_zhardmehka` | `zhardmehka` | Bloomwrought Wildscape of Tellon |
| `empire_djunnetic` | `djunnetic` | Djunnetic Dominion |
| `empire_yatunan_radicals` | `the_yatunan_radicals` | Yatunan Radicals |
| `empire_basidrix_cyber_ecclesia` | `the_basidrix_cyber_ecclesia` | Basidrix Cyber Ecclesia |
| `empire_lacertan_techno_protectorate` | `the_lacertan_techno_protectorate` | Lacertan Techno-Protectorate |
| `empire_free_sunbuilt_uplifters` | `the_free_sunbuilt_uplifters` | Sunbuilt Uplifters |
| `empire_kilik_cooperative` | `kilik_cooperative` | Kilik Cooperative |
| `empire_orbis` | `orbis` | Orbis Customer Synergies |
| `empire_chinorr_combine` | `chinorr_combine` | Chinorr Combine |
| `empire_mindwardens` | `prescripted_mindwardens` | Krijdex Wardenship |
| `empire_tankbound` | `prescripted_tankbound` | Oracularity of Noerm |
| `empire_zrobots` | `prescripted_zrobots` | Yorphian Extractorate |
| `empire_pyrragthul` | `prescripted_pyrragthul` | Pyrrag'Thul Planet Forgers |

Three of these — `zhardmehka`, `djunnetic` and `orbis` — do turn up a second time in
`prescripted_countries/`, but only as the **block key of the empire itself**, which happens to be the
same word. Not a check.

They are best read as reservations: the flag exists so a future event can ask for it, and nothing
asks yet. Setting one costs nothing and does nothing.

---

## How to check this again after a patch

The definitions:

```bash
cat "common/prescripted_flags/00_default_empire_flags.txt"
```

And for any one flag, everything that reads it — the pattern below catches `has_`, `set_` and
`remove_country_flag` alike, and the lookarounds stop `human_1` matching `human_10`:

```bash
grep -rnE "country_flag = human_1([^0-9A-Za-z_]|$)" common events
```

A flag whose only hit is `common/prescripted_flags/` is inert.
