# The four flag colours

A design's `empire_flag` block always writes four colours:

```
colors=
{
	"ship_steel"
	"red"
	"black"
	"null"
}
```

Two of them are the flag. One is the empire's colour on the galaxy map. One does nothing. Which is
which was argued about for a long time on the strength of what other people had written, so it was
settled instead by reading Stellaris **v4.4.6** on disk — the shader, the interface files, the
localisation, and the pixels of the artwork itself. Everything below is from there.

The app acts on this: the flag editor offers the first two, the empire's own settings offer the
third under **Map colour**, and the fourth holds a tag of the player's own
(`EmpireFlag.TagSlot`).

---

## Slots 1 and 2 are the flag

A background is not a picture. It is **three shapes packed into one file's red, green and blue
channels**, and the shader tints each channel with one of the first three colours:

```
float4 vColor = float4( 0, 0, 0, 1 );
vColor += BackgroundColor[0] * vBG.r;
vColor += BackgroundColor[1] * vBG.g;
vColor += BackgroundColor[2] * vBG.b;
vColor = saturate( vColor );
//vColor += BackgroundColor[3] * vBG.a;
```
— `gfx/FX/flag_sprite.shader`, the only thing in the whole of `gfx/` that reads a flag's colours.

The game's own designer offers a picker for each of the first two and nothing else:

```
buttonType={ name = "primary_color"   buttonText = "PRIMARY_COLOR"   ... }
buttonType={ name = "secondary_color" buttonText = "SECONDARY_COLOR" ... }
```
— `interface/customize_species_editors.gui`

## Slot 3 is the map colour, not a third flag colour

The shader multiplies it against the blue channel, so on the face of it slot 3 *is* a flag colour.
It is not, because **nothing is drawn in that channel**. Across all **63** backgrounds the game
ships in `flags/backgrounds/`, the brightest pixel in any blue channel is **11 of 255** — noise, not
a shape. Whatever slot 3 is set to, the flag looks the same.

It is not vestigial, though:

- The game ships a word for it — `TERTIARY_COLOR`, "Tertiary Colors" — with no control anywhere in
  `interface/` that uses it, commented out or otherwise.
- Of the **53** empires the game ships, **32** write `null` here and **17** write `black`, but
  **four** set it to a colour of their own — two `orange`, one `green`, one `blue` — on backgrounds
  with no blue channel at all. That is only worth doing if something other than the flag reads it.
- The game's **226** `randomizable_combo` entries in `flags/colors.txt` name a third colour, and
  **175** of them name `black`.
- An empire that leaves it empty takes its map colour from the flag's primary, which is why the
  United Nations of Earth are blue on the map with `"blue" "black" "null" "null"` written down.

## Slot 4 is unused

- The shader line for it is **commented out**, and sits after the `saturate` clamp.
- There is no `QUATERNARY_COLOR` string in the game's localisation and no control for it.
- Across the **303** colour blocks the game ships — prescripted countries, and every `colors = { }`
  in `common/` and `events/` — the slot is `null` **301** times. The two exceptions are `"red"` on
  the Pyrrag'Thul Planet Forgers (`"orange" "red" "orange" "red"`) and `"black"` on a distant-stars
  event, and neither has any consequence.
- Every one of the **226** `randomizable_combo` entries carries **three** values, not four.

That is what makes it safe to keep a tag there: it survives into the file and back out again, the
game ignores it, and the flag is unchanged. `PrescriptedConverter.CopyFlag` clears it when starting
a design from one of the game's empires, so the Pyrrag'Thul's stray `"red"` does not arrive as a tag
nobody chose.

---

## How to check this again after a patch

The shader, which is the whole argument for slots 1 and 2:

```bash
grep -n "BackgroundColor" gfx/FX/flag_sprite.shader
```

The designer's controls, and the words the game has for the slots:

```bash
grep -rn "PRIMARY_COLOR\|SECONDARY_COLOR\|TERTIARY_COLOR\|QUATERNARY_COLOR" interface/ localisation/english/
```

Whether any background has gained a blue channel — the claim slot 3 rests on. Read each `.dds` in
`flags/backgrounds/` and take the maximum blue value across every pixel; anything above single
digits means a third shape now exists and the third colour would be drawing it.

And what the shipped empires put in each slot:

```bash
grep -rn -A 6 "colors=" common/prescripted_countries/
```
