using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>
/// Everything an empire the player builds could be given, heading by heading.
/// </summary>
/// <remarks>
/// <para>
/// The lists a filter offers should be the lists the editor offers. Narrower than that - only what
/// the empires in front of the reader happen to hold - and a heading quietly says the rest do not
/// exist; wider, and it is the game's whole database, of which the great majority is not something
/// an empire can be given at all: 398 traits, 546 portraits, 358 civics, 170 government types.
/// </para>
/// <para>
/// So every heading asks whoever already answers the question. The pickers do it through the rules,
/// against an empire with nothing chosen yet, and take what is visible rather than what is enabled -
/// visible is "an empire may have this", enabled is "this one may have it now", and a filter wants
/// the first. The few shelves with no rule behind them are read the way their picker reads them.
/// </para>
/// <para>
/// Built once per set of rows and held: each of these is a pass over a database collection, and the
/// card draws them on every keystroke in the search box.
/// </para>
/// </remarks>
public sealed class EmpireOptions(DesignSession session)
{
    private readonly DesignSession _session = session;

    private DesignContext? _blank;

    private readonly RequirementEvaluator _evaluator = new();

    /*
       One field per heading, because each of these was worked out again every single time it was
       read. The class has said since it was written that they are built once and held, and they
       were not: every one is a pass over a game collection, through the rules, and then a name
       looked up for each key that survived - and the card reads seven of them to draw one tab.

       Two of them twice over. A species is asked for its class and its second species' class, and
       for its traits and its second species' traits, and each pair names the same property - so the
       Species tab walked the game's eleven hundred traits twice to draw one card.

       Held rather than built up front, because the reader has asked nothing yet when the list first
       draws, and Portraits and Traits are the two most expensive things here.
    */
    private IReadOnlyList<EmpireChoice>? _ethics;
    private IReadOnlyList<EmpireChoice>? _civics;
    private IReadOnlyList<EmpireChoice>? _origins;
    private IReadOnlyList<EmpireChoice>? _authorities;
    private IReadOnlyList<EmpireChoice>? _speciesClasses;
    private IReadOnlyList<EmpireChoice>? _traits;
    private IReadOnlyList<EmpireChoice>? _rulerTraits;
    private IReadOnlyList<EmpireChoice>? _portraits;
    private IReadOnlyList<EmpireChoice>? _homeworlds;
    private IReadOnlyList<EmpireChoice>? _startingSystems;
    private IReadOnlyList<EmpireChoice>? _rooms;
    private IReadOnlyList<EmpireChoice>? _shipsets;
    private IReadOnlyList<EmpireChoice>? _advisors;
    private IReadOnlyList<EmpireChoice>? _rulerClasses;
    private IReadOnlyList<EmpireChoice>? _nameLists;
    private IReadOnlyList<EmpireChoice>? _flagSets;
    private IReadOnlyList<EmpireChoice>? _genders;
    private IReadOnlyList<EmpireChoice>? _spawning;
    private IReadOnlyList<EmpireChoice>? _flagColors;
    private IReadOnlyList<EmpireChoice>? _mapColors;

    private GameDatabase Database => _session.Data.Database;

    private Localizer Loc => _session.Localizer;

    /// <summary>
    /// All seventy-two of the game's colours, in the shade the thing being filtered is drawn in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two lists over one set of names, because the map draws 24 of the 72 in a different shade from
    /// the flag and a filter showing the wrong one would be showing a colour the reader will never
    /// see on screen. 48 of the 72 have the same flag and map value, which is what makes getting it
    /// wrong survivable and so worth being careful about. There is no third list: see the note
    /// beside <see cref="Swatches.MapShade"/> for why ships are not drawn in the ship column.
    /// </para>
    /// <para>
    /// Not narrowed by the rules the way the other lists are. A colour has no requirements - every
    /// empire may hold every one of them - so the whole palette is the answer, in the order the game
    /// writes it rather than alphabetically, since that order groups the browns, the reds and the
    /// blues together and a list of colours sorted by name is a list nobody can scan.
    /// </para>
    /// </remarks>
    public IReadOnlyList<EmpireChoice> FlagColors => _flagColors ??= Palette(Swatches.FlagShade);

    /// <inheritdoc cref="FlagColors"/>
    public IReadOnlyList<EmpireChoice> MapColors => _mapColors ??= Palette(Swatches.MapShade);

    private IReadOnlyList<EmpireChoice> Palette(
        Func<FlagColorDefinition, (byte R, byte G, byte B)> shade) =>
        [.. Database.FlagColors
            .Select(c => EmpireRow.Coloured(_session, c.Key, shade))
            .OfType<EmpireChoice>()];

    /// <summary>
    /// An empire with nothing chosen, which is what the pickers are asked about.
    /// </summary>
    /// <remarks>
    /// The same scratch empire the front page builds to judge which of the game's own are playable.
    /// It carries the player's content packs, so a heading offers what they own and no more.
    /// </remarks>
    private DesignContext Blank => _blank ??= _session.Rules.CreateContext(
        EmpireDesignsFile.CreateEmpty().Add("scratch"),
        _session.OwnedDlc);

    public IReadOnlyList<EmpireChoice> Ethics => _ethics ??=
        Named(Visible(_session.Rules.GetEthicOptions(Blank)), key =>
        {
            var ethic = Database.Ethic(key);
            return new EmpireChoice(key, Loc.Text(key), ethic?.Icon, ethic?.Effects);
        });

    public IReadOnlyList<EmpireChoice> Civics => _civics ??=
        Named(Visible(_session.Rules.GetCivicOptions(Blank)), key => Civic(key));

    /// <summary>
    /// Every ascension perk and tradition tree a plan could name.
    /// </summary>
    /// <remarks>
    /// The whole list rather than what a blank empire may take, unlike the headings above. Those
    /// narrow a design's own choices, where an option nobody could pick is noise; these narrow a
    /// list of empires, and an empire in it may be any shape at all - so a perk only a gestalt can
    /// take still has to be offered to the reader looking for gestalts that plan it.
    /// </remarks>
    public IReadOnlyList<EmpireChoice> AscensionPerks => _ascensionPerks ??=
    [
        .. Database.AscensionPerks
            .Select(p => new EmpireChoice(p.Key, Loc.Text(p.NameKey), p.Icon, p.Effects))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture),
    ];

    private IReadOnlyList<EmpireChoice>? _ascensionPerks;

    /// <summary>Every civic a government reform could ever arrive at.</summary>
    /// <remarks>
    /// Not <see cref="Civics"/>, which is what a blank empire may take and so leaves out every
    /// gestalt one. That list narrows what an empire <em>is</em>, and a blank empire is a fair
    /// stand-in for one being designed; this narrows a list of empires that may be any shape at
    /// all, so a civic only a hive mind can hold still has to be offered to the reader looking for
    /// hive minds that plan it.
    /// </remarks>
    public IReadOnlyList<EmpireChoice> PlannableCivics => _plannableCivics ??=
    [
        .. Database.Civics
            .Where(c => !c.IsOrigin)
            .Select(c => Civic(c.Key))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture),
    ];

    private IReadOnlyList<EmpireChoice>? _plannableCivics;

    public IReadOnlyList<EmpireChoice> TraditionTrees => _traditionTrees ??=
    [
        .. Database.TraditionTrees
            .Select(t => new EmpireChoice(t.Key, Loc.Text(t.NameKey), t.Icon, null))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture),
    ];

    private IReadOnlyList<EmpireChoice>? _traditionTrees;

    public IReadOnlyList<EmpireChoice> Origins => _origins ??=
        Named(Visible(_session.Rules.GetOriginOptions(Blank)), key =>
        {
            var origin = Database.Civic(key);
            return new EmpireChoice(
                key,
                Loc.Text(origin?.NameKey, Localizer.Prettify(key)),
                origin?.Icon,
                origin?.Effects);
        });

    public IReadOnlyList<EmpireChoice> Authorities => _authorities ??=
        Named(Visible(_session.Rules.GetAuthorityOptions(Blank)), key =>
        {
            var authority = Database.Authority(key);
            return new EmpireChoice(
                key,
                Loc.Text(authority?.NameKey, Localizer.Prettify(key)),
                authority?.Icon,
                authority?.Effects);
        });

    public IReadOnlyList<EmpireChoice> SpeciesClasses => _speciesClasses ??=
        Named(Visible(_session.Rules.GetSpeciesClassOptions(Blank)), key =>
            new EmpireChoice(key, Loc.Text(key), null, null));

    public IReadOnlyList<EmpireChoice> Traits => _traits ??=
        Named(Visible(_session.Rules.GetSpeciesTraitOptions(Blank)), key => Trait(key));

    public IReadOnlyList<EmpireChoice> RulerTraits => _rulerTraits ??=
        Named(Visible(_session.Rules.GetRulerTraitOptions(Blank)), key => Trait(key));

    /// <summary>Every likeness the picker offers, which is its categories flattened.</summary>
    public IReadOnlyList<EmpireChoice> Portraits => _portraits ??=
        Named(
            _session.Rules.GetPortraitOptions(Blank)
                .SelectMany(group => group.Portraits)
                .Where(o => o.Visible)
                .Select(o => o.Key),
            key => new EmpireChoice(
                key,
                Loc.Text(key, Localizer.Prettify(key)),
                PortraitArtwork.For(Database, key, null),
                null));

    /// <summary>
    /// Where an empire may begin, which is a world for most of them and a ship for a nomad.
    /// </summary>
    /// <remarks>
    /// The rules answer in planet classes, and for a nomad they answer with the ark world - but a
    /// nomad's row carries the arkship instead, because the class underneath says pc_ark whichever
    /// of the three it is and a picture of the ark world says nothing about an empire whose whole
    /// point is that it does not live on one. So the two were in different key spaces, and the
    /// heading offered one arkship: the one an empire in the list happened to be flying.
    /// </remarks>
    public IReadOnlyList<EmpireChoice> Homeworlds => _homeworlds ??=
        Named(
            _session.Rules.GetHomeworldOptions(Blank).Concat(Database.Arkships.Select(a => a.Key)),
            key => Database.Arkship(key) is { } ark
                ? new EmpireChoice(
                    ark.Key,
                    Loc.Text(ark.NameKey, Localizer.Prettify(ark.Key)),
                    ark.Preview is { Length: > 0 } render ? render : ark.Icon,
                    EffectSet.None)
                {
                    Description = ark.DescriptionKey,
                }
                : new EmpireChoice(
                    key,
                    Loc.Text(key),
                    Database.PlanetClass(key)?.Icon,
                    null));

    public IReadOnlyList<EmpireChoice> StartingSystems => _startingSystems ??=
        Named(_session.Rules.GetStartingSystemOptions(Blank), key =>
        {
            var start = Database.Initializer(key);
            return new EmpireChoice(key, Loc.Text(start?.NameKey, Localizer.Prettify(key)), null, null);
        });

    /// <summary>The rooms the picker offers, which is the ones the game marks as choosable.</summary>
    public IReadOnlyList<EmpireChoice> Rooms => _rooms ??=
        Named(
            Database.Rooms.Where(r => r.IsOffered).Select(r => r.Key),
            key => new EmpireChoice(key, Loc.Text(key, Localizer.Prettify(key)), null, null));

    /// <summary>
    /// The shipsets the picker offers.
    /// </summary>
    /// <remarks>
    /// Three conditions, all of them the picker's. Being in the rules' own list is the least of
    /// them and was for a while the only one taken, which offered all fifty-two graphical cultures:
    /// the pirates, the swarm, the fallen empires, the arkships and the sets that only dress a city
    /// - twenty-five things no empire can be given. A set has to model ships of its own, and it has
    /// to be one the game says may be selected.
    /// </remarks>
    public IReadOnlyList<EmpireChoice> Shipsets => _shipsets ??=
        Named(
            Database.GraphicalCultures
                .Where(c => _session.Rules.Database.GraphicalCultures.Contains(c))
                .Where(c => c.ShipCategory is not null)
                .Where(c => _evaluator.IsSatisfied(c.Selectable, Blank))
                .Select(c => c.Key),
            key =>
            {
                var set = Database.GraphicalCulture(key);
                return new EmpireChoice(
                    key,
                    Loc.Text(key.ToUpperInvariant(), Localizer.Prettify(key)),
                    set?.ShipPreview,
                    EffectSet.None)
                {
                    Description = set?.DescriptionKey,
                };
            });

    public IReadOnlyList<EmpireChoice> Advisors => _advisors ??=
        Named(
            Database.AdvisorVoices.Select(v => v.Key),
            key =>
            {
                var voice = Database.AdvisorVoice(key);
                return new EmpireChoice(
                    key, Loc.Text(voice?.NameKey, Localizer.Prettify(key)), voice?.Icon, null);
            });

    public IReadOnlyList<EmpireChoice> RulerClasses => _rulerClasses ??=
        Named(
            Database.LeaderClasses.Where(c => c.CanRule).Select(c => c.Key),
            key =>
            {
                var held = Database.LeaderClass(key);
                return new EmpireChoice(
                    key, Loc.Text(held?.NameKey, Localizer.Prettify(key)), held?.Icon, null);
            });

    public IReadOnlyList<EmpireChoice> NameLists => _nameLists ??=
        Named(
            Database.NameLists.Select(n => n.Key),
            key => new EmpireChoice(key, Loc.Text(key, Localizer.Prettify(key)), null, null));

    public IReadOnlyList<EmpireChoice> FlagSets => _flagSets ??=
        Named(
            Database.EmpireFlagSets.Select(f => f.Key),
            key =>
            {
                var set = Database.EmpireFlagSet(key);
                return new EmpireChoice(
                    key,
                    set is null ? Localizer.Prettify(key) : EmpireView.FlagSetName(_session, set),
                    null,
                    null);
            });

    /// <summary>The four genders a design may hold, named and pictured by the picker that owns them.</summary>
    public IReadOnlyList<EmpireChoice> Genders => _genders ??=
    [
        .. GenderPicker.Offered.Select(g => new EmpireChoice(
            g.Key, g.Label, Database.Icons.GetValueOrDefault(g.Icon), null))
    ];

    /// <summary>The three states of the spawn setting, likewise.</summary>
    public IReadOnlyList<EmpireChoice> Spawning => _spawning ??=
    [
        .. SpawnToggle.Offered(_session).Select(s => new EmpireChoice(s.Value, s.Name, s.Icon, null))
    ];

    private EmpireChoice Civic(string key)
    {
        var civic = Database.Civic(key);

        return new EmpireChoice(key, Loc.Text(key), civic?.Icon, civic?.Effects);
    }

    private EmpireChoice Trait(string key)
    {
        var trait = Database.Trait(key);

        return new EmpireChoice(key, Loc.Text(key), trait?.Icon, trait?.Effects);
    }

    /// <summary>What an empire may have, which is not the same as what it may have right now.</summary>
    private static IEnumerable<string> Visible(IEnumerable<OptionState> options) =>
        options.Where(o => o.Visible).Select(o => o.Key);

    private static IReadOnlyList<EmpireChoice> Named(
        IEnumerable<string> keys,
        Func<string, EmpireChoice> name) =>
    [
        .. keys
            .Distinct(StringComparer.Ordinal)
            .Select(name)
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
    ];
}
