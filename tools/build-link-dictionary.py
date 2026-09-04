"""Builds the frozen dictionary a v2 share link encodes against.

Run: python tools/build-link-dictionary.py

The point of this file is that its output is *frozen*. A link encodes a choice as its position in
these lists, so a position may never change meaning. The script is therefore append-only by
construction: it reads whatever is already committed, keeps every entry in the order it is already
in, and adds only keys that are not there yet. Re-running it after a game update is safe; deleting
the output and regenerating from scratch is not, and would silently repoint every link ever shared.

That is also why the strings are carried rather than looked up in gamedb.json at runtime. A key the
game later removes still has to decode, and gamedb.json is re-extracted whenever the game changes.
"""
import io
import json
import os
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAMEDB = os.path.join(ROOT, 'src', 'Sem.Web', 'wwwroot', 'gamedata', 'gamedb.json')
LOC = os.path.join(ROOT, 'src', 'Sem.Web', 'wwwroot', 'gamedata', 'loc', 'en.json')
OUT = os.path.join(ROOT, 'src', 'Sem.Ui', 'Services', 'LinkDictionary.v2.txt')

# The order here is the wire order: table N is the Nth line group. Never reorder it.
GROUPS = [
    'species_trait', 'leader_trait', 'civic', 'origin', 'ethic', 'authority', 'government',
    'planet_class', 'room', 'name_list', 'portrait', 'graphical_culture', 'advisor_voice',
    'initializer', 'flag_category', 'flag_colour', 'leader_class', 'ship_set', 'arkship',
    'species_class', 'flag_set', 'species_stem', 'name_word', 'name_key', 'flag_file', 'field',
    'token', 'char_prefix', 'char_suffix', 'shape',
]

# What the designer writes that is neither a game key nor anything the player typed: the four
# genders, the placeholder an unused flag colour slot holds, the four templates a generated name is
# built from, and the third thing spawn_enabled can say - "always", which yes and no are spelled
# for by the format itself and which was costing six literal bytes until a real link showed it.
# Ten strings that turn up in almost every design, so they are worth a group of their own.
TOKENS = [
    'not_set', 'male', 'female', 'indeterminable', 'null', 'always',
    '%ADJ%', '%ADJECTIVE%', '%LEADER_1%', '%LEADER_2%',
]


def collect():
    db = json.load(io.open(GAMEDB, encoding='utf-8-sig'))
    loc = json.load(io.open(LOC, encoding='utf-8-sig'))

    def keys(name, pred=None):
        out = []
        for e in db.get(name) or []:
            if isinstance(e, dict) and e.get('key') and (pred is None or pred(e)):
                out.append(e['key'])
            elif isinstance(e, str) and pred is None:
                out.append(e)
        return sorted(set(out))

    found = {
        'species_trait': keys('traits', lambda t: t.get('kind') == 0),
        'leader_trait': keys('traits', lambda t: t.get('kind') != 0),
        'civic': keys('civics', lambda c: not c.get('isOrigin')),
        'origin': keys('civics', lambda c: c.get('isOrigin')),
        'ethic': keys('ethics'),
        'authority': keys('authorities'),
        'government': keys('governmentTypes'),
        'planet_class': keys('planetClasses'),
        'room': keys('rooms'),
        'name_list': keys('nameLists'),
        'portrait': keys('portraits'),
        'graphical_culture': keys('graphicalCultures'),
        'advisor_voice': keys('advisorVoices'),
        'initializer': keys('initializers'),
        'flag_category': keys('flagCategories'),
        'flag_colour': keys('flagColors'),
        'leader_class': keys('leaderClasses'),
        'ship_set': keys('shipSets'),
        'arkship': keys('arkships'),
        'species_class': keys('speciesClasses'),
        'flag_set': keys('empireFlagSets'),
    }

    # One entry per species, not four. The design writes SPEC_x, SPEC_x_pl, SPEC_x_planet and
    # SPEC_x_system; the stem plus a two-bit suffix says all four, so 2,064 keys cost 523 entries.
    stems = set()
    for s in db.get('speciesNames') or []:
        k = s.get('nameKey')
        if k:
            stems.add(k)
    found['species_stem'] = sorted(stems)

    found['name_word'] = sorted({p['word'] for g in db.get('empireNameParts') or []
                                 for p in g.get('parts') or []})

    # Planet and system names a design can point at.
    found['name_key'] = sorted(k for k in loc if k.startswith('NAME_'))

    # Every emblem and background the game ships, which a flag names by file rather than by key.
    files = set()
    for c in db.get('flagCategories') or []:
        for f in c.get('files') or []:
            if isinstance(f, str):
                files.add(f)
    found['flag_file'] = sorted(files)

    # Every field name a design block can hold, so a key costs an index rather than its letters.
    found['field'] = sorted({
        'key', 'ship_prefix', 'species', 'secondary_species', 'name', 'adjective', 'authority',
        'flag', 'flags', 'government', 'is_nomadic', 'advisor_voice_type', 'planet_name',
        'planet_class', 'ship_size', 'system_name', 'initializer', 'graphical_culture',
        'city_graphical_culture', 'empire_flag', 'ruler', 'spawn_as_fallen',
        'ignore_portrait_duplication', 'room', 'spawn_enabled', 'ethic', 'civics', 'origin',
        'class', 'portrait', 'species_name', 'species_plural', 'species_adjective', 'species_bio',
        'name_list', 'gender', 'trait', 'literal', 'variables', 'value', 'icon', 'background',
        'colors', 'category', 'file', 'ruler_title', 'heir_title', 'ruler_title_female',
        'heir_title_female', 'texture', 'evolution_mask', 'attachment', 'clothes', 'leader_class',
        'custom_biography', 'full_names', 'first_name', 'second_name', 'use_full_regnal_name',
    })

    found['token'] = list(TOKENS)

    # What may sit directly inside what. A design is 93 nodes and almost all of them are one of a
    # handful of things their parent can hold - a species has nine possible children, the variables
    # of a generated name have one - so a key costs two or three bits here against the seven it
    # takes to name one of the fifty-nine fields in general.
    #
    # Written out rather than read from a design, because the shapes are the app's own model and a
    # generator that learned them from whatever designs happened to be on the machine would freeze
    # a different table on every machine. A child missing from a list costs length, not correctness:
    # the encoder falls back to naming the field in full.
    #
    # "name" holds both shapes because a name means one thing under an empire and another under a
    # ruler; merging the two costs a bit and saves tracking where in the tree we are.
    found['shape'] = [
        # The design's own block. It cannot be found by its parent's name, because that name is
        # whatever the empire is called, so the encoder asks for this one by depth instead.
        '<empire>>key|ship_prefix|species|secondary_species|name|adjective|authority|flag|flags'
        '|government|is_nomadic|advisor_voice_type|planet_name|planet_class|ship_size|system_name'
        '|initializer|graphical_culture|city_graphical_culture|empire_flag|ruler|spawn_as_fallen'
        '|ignore_portrait_duplication|room|spawn_enabled|ethic|civics|origin',
        'species>class|portrait|species_name|species_plural|species_adjective|species_bio'
        '|name_list|gender|trait',
        'secondary_species>class|portrait|species_name|species_plural|species_adjective'
        '|species_bio|name_list|gender|trait',
        'ruler>gender|name|portrait|texture|evolution_mask|attachment|clothes|custom_biography'
        '|ruler_title|heir_title|ruler_title_female|heir_title_female|trait|leader_class',
        'name>key|literal|variables|full_names|first_name|second_name|use_full_regnal_name',
        'value>key|literal|variables',
        'variables>key|value',
        'empire_flag>icon|background|colors',
        'icon>category|file',
        'background>category|file',
        'adjective>key|literal|variables',
        'ship_prefix>key|literal|variables',
        'planet_name>key|literal|variables',
        'system_name>key|literal|variables',
        'species_name>key|literal|variables',
        'species_plural>key|literal|variables',
        'species_adjective>key|literal|variables',
        'ruler_title>key|literal|variables',
        'heir_title>key|literal|variables',
        'ruler_title_female>key|literal|variables',
        'heir_title_female>key|literal|variables',
        'custom_biography>key|literal|variables',
        'full_names>key|literal|variables',
        'first_name>key|literal|variables',
        'second_name>key|literal|variables',
    ]

    # Ruler names picked from the dropdown, which is how most rulers are named. There are 10,076 of
    # these keys and they all read <LIST>_CHR_<Word>, so the table holds the 69 prefixes and the
    # 7,667 endings apart: 62 KB rather than 190, and a name costs a pair of indices.
    prefixes, suffixes = set(), set()
    for k in loc:
        cut = -1
        for mark in ('_CHR_', '_CHA_', '_SHP_'):
            at = k.find(mark)
            if at >= 0:
                cut = at + len(mark)
                break
        if cut > 0:
            prefixes.add(k[:cut])
            suffixes.add(k[cut:])

    found['char_prefix'] = sorted(prefixes)
    found['char_suffix'] = sorted(suffixes)

    return found


def read_existing():
    if not os.path.exists(OUT):
        return {}
    kept, group = {}, None
    for line in io.open(OUT, encoding='utf-8'):
        line = line.rstrip('\n')
        if line.startswith('# '):
            continue
        if line.startswith('['):
            group = line[1:-1]
            kept[group] = []
        elif line and group is not None:
            kept[group].append(line)
    return kept


def main():
    found = collect()
    kept = read_existing()

    merged, added = {}, 0
    for g in GROUPS:
        have = kept.get(g, [])
        seen = set(have)
        extra = [k for k in found.get(g, []) if k not in seen]
        added += len(extra)
        merged[g] = have + extra          # append only; never reorder

    lines = ['# Frozen. Append-only: a position in these lists is what a v2 link stores, so an',
             '# entry may never move or be removed. Regenerate with tools/build-link-dictionary.py.',
             '']
    for g in GROUPS:
        lines.append('[' + g + ']')
        lines.extend(merged[g])
        lines.append('')

    text = '\n'.join(lines)
    io.open(OUT, 'w', encoding='utf-8', newline='\n').write(text)

    raw = text.encode('utf-8')
    print('wrote', os.path.relpath(OUT, ROOT))
    print('  entries added this run:', added)
    print('  %-18s %6s' % ('group', 'count'))
    for g in GROUPS:
        print('  %-18s %6d' % (g, len(merged[g])))
    print('  total entries:', sum(len(v) for v in merged.values()))
    print('  raw %d bytes, deflated %d bytes' % (len(raw), len(zlib.compress(raw, 9))))


if __name__ == '__main__':
    main()
