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

Two of them are the flag. The other two are the galaxy map: the empire's border, and the territory
inside it. Which is which was argued about for a long time on the strength of what other people had
written, so it was checked against Stellaris **v4.4.6** on disk — the shader, the interface files,
the localisation, and the pixels of the artwork itself.

Read that evidence carefully, because it answers a narrower question than it looks like it does.

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

## Slots 3 and 4 are the map: border and fill

The third is multiplied against the blue channel above, so on the face of it it is a flag colour. It
is not, because **nothing is drawn in that channel**: across all **63** backgrounds the game ships in
`flags/backgrounds/`, the brightest pixel in any blue channel is **11 of 255** — noise, not a shape.
Whatever slot 3 holds, the flag looks the same. And slot 4's line is **commented out**, so it cannot
change a flag either.

Neither is idle. They are read by the galaxy map, and the flag's shader is simply the wrong file to
have looked in — which is the mistake this document made in its first version, where it concluded
that "slot 4 is unused" from evidence that only showed it unused *by the flag*. **Slot 3 draws the
empire's border and slot 4 fills the territory inside it.**

What the files do say, and which is consistent with that:

- The game ships a word for the third — `TERTIARY_COLOR`, "Tertiary Colors" — with no control
  anywhere in `interface/` that uses it. There is no `QUATERNARY_COLOR` at all.
- Of the **53** empires the game ships, **32** write `null` in slot 3 and **17** write `black`, but
  **four** set it to a colour of their own — two `orange`, one `green`, one `blue` — on backgrounds
  with no blue channel at all. That is only worth doing if something other than the flag reads it.
- Across the **303** colour blocks the game ships — prescripted countries, and every `colors = { }`
  in `common/` and `events/` — slot 4 is `null` **301** times. Which is what a slot looks like when
  the default is what nearly everybody wants: a fill the game derives from the border.
- Every one of the **226** `randomizable_combo` entries in `flags/colors.txt` carries **three**
  values, not four — a flag's two and a border.

So the app offers two flag colours in the flag editor, and the border and the fill together as the
empire's **Map colours**, drawn in the map half of the pair the game keeps against every name.

### A warning worth leaving here

An earlier version of this app kept a private "tag" colour in slot 4, on the strength of the
paragraph above minus its last two sentences. It was wrong, and the way it was wrong is worth
remembering: every fact in it was correct, and the conclusion did not follow from them. "The flag
shader does not read this" is not "nothing reads this", and the only file that had been searched was
the one that draws flags.

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

None of that can tell you what the **map** does with slots 3 and 4, because none of it is the map's
code. That was settled in game, by setting them and looking.
