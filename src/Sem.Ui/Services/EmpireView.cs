using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// One of the empire's choices, with everything needed to describe it.
/// </summary>
/// <param name="Key">The key the design stores.</param>
/// <param name="Name">What it is called, which is not always its key made readable - an advisor
/// voice borrows an ethic's or a trait's name, so its key says nothing about what it is called.</param>
/// <param name="Icon">Its artwork, where the game has any.</param>
/// <param name="Effects">What it does, so a chip can say so without a second lookup.</param>
public sealed record EmpireChoice(string Key, string Name, string? Icon, EffectSet? Effects);

/// <summary>
/// Everything a card needs to know about an empire, worked out once.
/// </summary>
/// <remarks>
/// <para>
/// These are the lookups that turn what a design stores - a string of keys - into the things that
/// can be drawn: the room, the world through its window, the portraits, the icon and the effects
/// behind each chip, and the sentences under them. Every one of them is a question about the same
/// design, and none of them is a question about how it is laid out.
/// </para>
/// <para>
/// Here rather than inside a card because there are two cards now. The showcase reads an empire and
/// the lite editor edits one, they arrange the same facts differently, and the facts themselves must
/// not be worked out twice - a second copy would drift, which is exactly what happened when the
/// ethics grid was hand-copied from the option grid and quietly missed all three of its fixes.
/// </para>
/// <para>
/// The context and the report are computed once and kept, because building a context walks every
/// government the game defines and a card asks for it several times per render. Call
/// <see cref="Refresh"/> when the design changes underneath.
/// </para>
/// </remarks>
public sealed class EmpireView(DesignSession session, EmpireDesign design)
{
    private readonly DesignSession _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly EmpireDesign _design = design ?? throw new ArgumentNullException(nameof(design));

    private DesignContext? _context;
    private ValidationReport? _report;

    /// <summary>The empire being described.</summary>
    public EmpireDesign Design => _design;

    private GameDatabase Database => _session.Data.Database;

    /// <summary>What the rules make of the empire, kept rather than rebuilt.</summary>
    public DesignContext Context =>
        _context ??= _session.Rules.CreateContext(_design, _session.OwnedDlc);

    /// <summary>What is wrong with it, kept for the same reason.</summary>
    public ValidationReport Report =>
        _report ??= _session.Rules.Validate(Context, _design);

    /// <summary>Throws away what was worked out, for when the design has changed.</summary>
    public void Refresh()
    {
        _context = null;
        _report = null;
    }

    public RoomDefinition? Room =>
        Database.Rooms.FirstOrDefault(r => r.Key == _design.Room);

    /// <summary>
    /// The world through the window: the homeworld the empire actually starts on.
    /// </summary>
    /// <remarks>
    /// An origin can override the choice - Void Dwellers begin on a habitat whatever the picker
    /// said - and the context has already worked that out.
    /// </remarks>
    public PlanetClassDefinition? World =>
        Database.PlanetClasses.FirstOrDefault(p => p.Key == Context.EffectivePlanetClass);

    public GraphicalCultureDefinition? City =>
        Database.GraphicalCultures.FirstOrDefault(c => c.Key == _design.CityGraphicalCulture);

    public GraphicalCultureDefinition? Shipset =>
        Database.GraphicalCultures.FirstOrDefault(c => c.Key == _design.GraphicalCulture);

    public AdvisorVoiceDefinition? Advisor =>
        Database.AdvisorVoices.FirstOrDefault(v => v.Key == _design.AdvisorVoiceType);

    /// <summary>The arkship a nomad begins aboard, when the design names one.</summary>
    public ArkshipDefinition? Arkship =>
        Database.Arkships.FirstOrDefault(a => a.Key == _design.ShipSize);

    /// <summary>
    /// What the game calls a set of artwork, falling back to its readable key.
    /// </summary>
    /// <remarks>
    /// The game names a set by its key shouted, and names only the two Biogenesis added. Read the
    /// stored key directly and the row shows <c>mammalian_01</c>.
    /// </remarks>
    public string? CultureName(string? key) => key is { Length: > 0 }
        ? _session.Localizer.Text(key.ToUpperInvariant(), Localizer.Prettify(key))
        : null;

    public string? RulerPortrait =>
        PortraitArtwork.For(Database, _design.Ruler.Portrait, _design.Ruler.Gender)
        ?? SpeciesPortrait;

    public string? SpeciesPortrait =>
        PortraitArtwork.For(Database, _design.Species.Portrait, _design.Species.Gender);

    public AuthorityDefinition? Authority =>
        Database.Authorities.FirstOrDefault(a => a.Key == _design.Authority);

    /// <summary>An origin is a civic, as the game files have it, so it is looked up among them.</summary>
    public CivicDefinition? Origin =>
        Database.Civics.FirstOrDefault(c => c.Key == _design.Origin);

    /// <summary>The government the game would name this empire's, derived rather than stored.</summary>
    public string Government =>
        _session.Rules.DeriveGovernment(Context) is { } government
            ? _session.Localizer.Text(government.Key)
            : _session.Localizer.Text(_design.Authority);

    /// <summary>The ruler, as a line: their name and the title they hold, where they hold one.</summary>
    public string Ruler
    {
        get
        {
            var name = _session.Localizer.RulerName(_design.Ruler, string.Empty);

            var title = _design.Ruler.Title is { } held
                ? _session.Localizer.Name(held, string.Empty)
                : string.Empty;

            return string.Join(", ", new[] { name, title }.Where(p => p.Length > 0));
        }
    }

    /// <summary>The homeworld's name and its class, as one line.</summary>
    public string Homeworld =>
        string.Join(
            " · ",
            new[]
            {
                _session.Localizer.Name(_design.PlanetName, string.Empty),
                _session.Localizer.Text(Context.EffectivePlanetClass),
            }.Where(p => p.Length > 0));

    /// <summary>The starting system, which most empires leave to the galaxy generator.</summary>
    public string StartingSystem =>
        _design.Initializer is { Length: > 0 } initializer
            ? _session.Localizer.Text(
                Database.Initializers.FirstOrDefault(i => i.Key == initializer)?.NameKey ?? initializer)
            : string.Empty;

    /// <summary>
    /// What to call a set of scripted country flags.
    /// </summary>
    /// <remarks>
    /// The game gives these no names at all, and "Empire Human 1" says nothing. What does say
    /// something is whose flags they are, so the empires that carry the set name it. Static and
    /// here rather than inside the picker, because the card reads the held set's name and the
    /// picker writes the same name against every option, and two spellings of one set would be a
    /// difference nobody could explain.
    /// </remarks>
    public static string FlagSetName(DesignSession session, EmpireFlagSet set)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(set);

        // Two of the game's empires may share both a set and a name — a species and its machine
        // counterpart — and naming the set after both says it twice.
        var empires = set.Empires
            .Select(e => session.Localizer.Text(
                session.Data.Database.PrescriptedEmpires
                    .FirstOrDefault(p => string.Equals(p.Key, e, StringComparison.Ordinal))?.NameKey,
                Localizer.Prettify(e)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return empires.Count == 0
            ? Localizer.Prettify(set.Key)
            : string.Join(", ", empires);
    }

    /// <summary>The held set's name, or nothing when the design claims no flags.</summary>
    public string? FlagSetLabel =>
        FlagSet is { } set ? FlagSetName(_session, set) : null;

    /// <summary>The named set of country flags the design carries, when it carries one.</summary>
    /// <remarks>
    /// Nothing to do with the drawn flag, despite where it sits in the file. See
    /// <c>docs/empire-flags.md</c>.
    /// </remarks>
    public EmpireFlagSet? FlagSet =>
        _design.PrescriptedFlag is { Length: > 0 } key
            ? Database.EmpireFlagSets.FirstOrDefault(f => f.Key == key)
            : null;

    public IEnumerable<EmpireChoice> Ethics =>
        _design.Ethics.Select(key =>
        {
            var ethic = Database.Ethics.FirstOrDefault(d => d.Key == key);
            return Chip(key, ethic?.Icon, ethic?.Effects);
        });

    public IEnumerable<EmpireChoice> Civics =>
        _design.Civics.Select(key =>
        {
            var civic = Database.Civics.FirstOrDefault(d => d.Key == key);
            return Chip(key, civic?.Icon, civic?.Effects);
        });

    public IEnumerable<EmpireChoice> Traits => TraitsOf(_design.Species);

    public IEnumerable<EmpireChoice> TraitsOf(SpeciesDesign species)
    {
        ArgumentNullException.ThrowIfNull(species);

        return species.Traits.Select(key =>
        {
            var trait = Database.Traits.FirstOrDefault(d => d.Key == key);
            return Chip(key, trait?.Icon, trait?.Effects);
        });
    }

    /// <summary>The ruler's traits, which are a different set from the species' own.</summary>
    public IEnumerable<EmpireChoice> RulerTraits =>
        _design.Ruler.Traits.Select(key =>
        {
            var trait = Database.Traits.FirstOrDefault(d => d.Key == key);
            return Chip(key, trait?.Icon, trait?.Effects);
        });

    /// <summary>The authority as a chip, so it can sit in a row like the ethics beside it.</summary>
    public IEnumerable<EmpireChoice> AuthorityChoice =>
        _design.Authority is { Length: > 0 } key
            ? [Chip(key, Authority?.Icon, Authority?.Effects)]
            : [];

    /// <summary>The origin as a chip.</summary>
    public IEnumerable<EmpireChoice> OriginChoice =>
        _design.Origin is { Length: > 0 } key
            ? [Chip(key, Origin?.Icon, Origin?.Effects)]
            : [];

    /// <summary>
    /// The advisor voice as a chip.
    /// </summary>
    /// <remarks>
    /// Named from the voice's own NameKey rather than from its key: the voices borrow the ethics'
    /// and traits' names, so <c>l_militarist</c> reads as Militarist only by going the long way
    /// round. It carries no effects and has no description, which is true of it - an advisor voice
    /// changes nothing about the empire.
    /// </remarks>
    public IEnumerable<EmpireChoice> AdvisorChoice =>
        _design.AdvisorVoiceType is { Length: > 0 } key && Advisor is { } voice
            ? [new EmpireChoice(key, _session.Localizer.Text(voice.NameKey, Localizer.Prettify(key)), voice.Icon, null)]
            : [];

    private EmpireChoice Chip(string key, string? icon, EffectSet? effects) =>
        new(key, _session.Localizer.Text(key), icon, effects);

    /// <summary>The empire's adjectival name, or nothing where it has none.</summary>
    public string Adjective => _session.Localizer.Name(_design.Adjective, string.Empty);

    /// <summary>The ruler's name on its own, without the title the card shows separately.</summary>
    public string RulerName => _session.Localizer.RulerName(_design.Ruler, string.Empty);

    /// <summary>
    /// What the ruler is called, falling back to what the government would call them.
    /// </summary>
    public string RulerTitle =>
        _design.Ruler.Title is { } held && _session.Localizer.Name(held, string.Empty) is { Length: > 0 } title
            ? title
            : _session.Rules.DeriveGovernment(Context)?.RulerTitleKey is { } key
                ? _session.Localizer.Text(key)
                : string.Empty;

    /// <summary>
    /// Whether these ethics, authority and civics add up to a government the game has.
    /// </summary>
    /// <remarks>
    /// The card shows all three at once and the government name they produce, so when they produce
    /// none it can say so where the choices are rather than only in the problem list.
    /// </remarks>
    public bool HasGovernment => _session.Rules.DeriveGovernment(Context) is not null;

    /// <summary>What to call a second species' traits, since two rows of "Traits" would not say.</summary>
    public string SecondName(SpeciesDesign species)
    {
        ArgumentNullException.ThrowIfNull(species);

        return $"{_session.Localizer.Name(species.Name, _session.Localizer.Text(species.Class))} · " +
            _session.Localizer.Heading("TRAITS", "Traits");
    }
}
