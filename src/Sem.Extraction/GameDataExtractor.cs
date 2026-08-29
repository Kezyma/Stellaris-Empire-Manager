using System.Reflection;
using System.Text.Json;
using Sem.Extraction.Extractors;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction;

/// <summary>
/// Builds a <see cref="GameDatabase"/> from a Stellaris installation.
/// </summary>
/// <remarks>
/// The installation is only ever read. Nothing here writes, and the write guard in
/// <see cref="Sem.Io"/> refuses writes into a game folder in any case.
/// </remarks>
public sealed class GameDataExtractor(LayeredContent content)
{
    /// <summary>
    /// Version of the produced database's shape. Raise it when the shape changes, so a cache built
    /// by an older version is rebuilt rather than misread.
    /// </summary>
    public const int SchemaVersion = 1;

    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>The installation this reads from, when a layer is backed by one.</summary>
    private string? InstallRoot =>
        _content.Layers.OfType<DirectoryContentSource>().LastOrDefault()?.Root;

    /// <summary>
    /// The game's text, read once and shared.
    /// </summary>
    /// <remarks>
    /// Extraction needs this before the database exists, because the random name pools are stored as
    /// the words themselves rather than as keys. Ten thousand names would otherwise appear twice,
    /// once as a key in the database and again as text beside it, for no benefit — nothing ever
    /// needs a name's key once it has the name.
    /// </remarks>
    private Dictionary<string, string> Localisation =>
        _localisation ??= LocalisationExtractor.Extract(_content, _language);

    private Dictionary<string, string>? _localisation;
    private string _language = "english";

    /// <summary>
    /// The images the last extraction referred to, ready to be converted by
    /// <see cref="AssetBaker"/>. Empty until <see cref="Extract"/> has run.
    /// </summary>
    public AssetCatalog Assets { get; private set; } = new(content);

    /// <summary>Builds a database from an installation directory.</summary>
    public static GameDatabase ExtractFrom(string installRoot, IProgress<string>? progress = null) =>
        new GameDataExtractor(LayeredContent.ForInstall(installRoot)).Extract(progress);

    /// <summary>
    /// Reads the installation and produces everything the designer needs.
    /// </summary>
    /// <remarks>
    /// Order matters. Shared variables and scripted triggers are loaded first because the rest of
    /// the content refers to them, and a condition compiled before its trigger is known would be
    /// recorded as unrecognised.
    /// </remarks>
    public GameDatabase Extract(IProgress<string>? progress = null)
    {
        var loader = new ScriptLoader(_content);
        var requirements = new RequirementCompiler();

        Report("Reading sprite definitions");
        var sprites = SpriteCatalog.Read(_content);

        var assets = new AssetCatalog(_content, sprites);
        Assets = assets;

        Report("Reading shared variables and triggers");
        loader.LoadVariables();
        requirements.LoadScriptedTriggers(loader);

        Report("Reading content packs and defines");
        var dlc = MetadataExtractor.ExtractDlc(loader);
        var defines = MetadataExtractor.ExtractDefines(loader);

        Report("Reading species");
        var archetypes = SpeciesExtractor.ExtractArchetypes(loader);
        var speciesClasses = SpeciesExtractor.ExtractSpeciesClasses(loader, requirements);

        Report("Reading ethics and traits");
        var ethics = EthicsExtractor.Extract(loader, requirements, assets);
        var traits = TraitsExtractor.Extract(loader, requirements, assets);

        Report("Reading governments");
        var authorities = GovernmentExtractor.ExtractAuthorities(loader, requirements, assets);
        var civics = GovernmentExtractor.ExtractCivics(loader, requirements, assets);
        var governmentTypes = GovernmentExtractor.ExtractGovernmentTypes(loader, requirements);

        Report("Reading worlds and starting systems");
        var planetClasses = WorldExtractor.ExtractPlanetClasses(loader, requirements, assets);
        var initializers = WorldExtractor.ExtractInitializers(loader);

        Report("Reading portraits");
        var portraitCategories = PortraitExtractor.ExtractCategories(loader);
        var portraitSets = PortraitExtractor.ExtractSets(loader, requirements);
        var portraits = PortraitExtractor.ExtractPortraits(loader);

        Report("Reading appearance options");
        var rooms = CosmeticsExtractor.ExtractRooms(loader, assets);
        var graphicalCultures = CosmeticsExtractor.ExtractGraphicalCultures(loader, requirements, assets);
        var advisorVoices = CosmeticsExtractor.ExtractAdvisorVoices(loader, requirements, assets);
        Report("Reading names");
        var nameLists = CosmeticsExtractor.ExtractNameLists(loader, requirements, Localisation);
        var speciesNames = NameExtractor.ExtractSpeciesNames(loader, Localisation);

        Report("Reading flags");
        var flagCategories = FlagExtractor.ExtractCategories(loader, assets);
        var flagColors = FlagExtractor.ExtractColors(loader);

        Report("Reading built-in empires");
        var prescripted = MetadataExtractor.ExtractPrescriptedEmpires(loader, requirements);
        var template = MetadataExtractor.ExtractNewEmpireTemplate(loader);

        Report("Reading modifier display settings");
        var modifiers = DescribeModifiers(ethics, traits, authorities, civics);

        return new GameDatabase
        {
            SchemaVersion = SchemaVersion,
            GameVersion = ReadGameVersion() ?? "unknown",
            ExtractorVersion = typeof(GameDataExtractor).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? "0.0.0",
            Defines = defines,
            Dlc = dlc,
            Archetypes = archetypes,
            SpeciesClasses = speciesClasses,
            Traits = traits,
            Modifiers = modifiers,
            SpeciesNames = speciesNames,
            Ethics = ethics,
            Authorities = authorities,
            Civics = civics,
            GovernmentTypes = governmentTypes,
            PlanetClasses = planetClasses,
            PortraitCategories = portraitCategories,
            PortraitSets = portraitSets,
            Portraits = portraits,
            NameLists = nameLists,
            Initializers = initializers,
            AdvisorVoices = advisorVoices,
            Rooms = rooms,
            GraphicalCultures = graphicalCultures,
            FlagCategories = flagCategories,
            FlagColors = flagColors,
            PrescriptedEmpires = prescripted,
            NewEmpireTemplate = template,
            UnrecognisedTriggers = requirements.Unrecognised
                .OrderByDescending(p => p.Value)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            UnrecognisedEffectConditions = requirements.UnrecognisedInEffects
                .OrderByDescending(p => p.Value)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
        };

        void Report(string message) => progress?.Report(message);
    }

    /// <summary>
    /// Works out how to display every modifier the options actually use.
    /// </summary>
    /// <remarks>
    /// Only the modifiers reachable from the designer are described, which is a few hundred rather
    /// than the several thousand the game defines. That keeps the database small and, more usefully,
    /// makes the number of them that had to be guessed at a figure small enough to act on.
    /// </remarks>
    private Dictionary<string, ModifierInfo> DescribeModifiers(
        IEnumerable<EthicDefinition> ethics,
        IEnumerable<TraitDefinition> traits,
        IEnumerable<AuthorityDefinition> authorities,
        IEnumerable<CivicDefinition> civics)
    {
        var catalog = ModifierCatalog.Read(_content, InstallRoot);
        var observed = new Dictionary<string, List<double>>(StringComparer.Ordinal);

        foreach (var effects in ethics.Select(e => e.Effects)
                     .Concat(traits.Select(t => t.Effects))
                     .Concat(authorities.Select(a => a.Effects))
                     .Concat(civics.Select(c => c.Effects)))
        {
            Record(effects.Modifiers);

            foreach (var conditional in effects.Conditional)
            {
                Record(conditional.Modifiers);
            }
        }

        return observed.ToDictionary(
            p => p.Key,
            p => catalog.Describe(p.Key, p.Value),
            StringComparer.Ordinal);

        void Record(IReadOnlyDictionary<string, double> modifiers)
        {
            foreach (var (key, value) in modifiers)
            {
                if (!observed.TryGetValue(key, out var values))
                {
                    observed[key] = values = [];
                }

                values.Add(value);
            }
        }
    }

    /// <summary>
    /// Reads a language's text into a flat lookup of key to display string.
    /// </summary>
    /// <remarks>
    /// Kept separate from the database because it is large, and because a language can be swapped
    /// without re-reading anything else.
    /// </remarks>
    /// <param name="language">The language folder to read.</param>
    /// <param name="reachableFrom">
    /// When supplied, keeps only the text this database can display, following references between
    /// entries. English in full is about 150,000 entries, almost all of it event and dialogue text
    /// the designer never shows.
    /// </param>
    public Dictionary<string, string> ExtractLocalisation(
        string language = "english",
        GameDatabase? reachableFrom = null)
    {
        if (!string.Equals(language, _language, StringComparison.OrdinalIgnoreCase))
        {
            _language = language;
            _localisation = null;
        }

        var all = Localisation;
        return reachableFrom is null ? all : LocalisationPruner.Prune(reachableFrom, all);
    }

    /// <summary>Reads the game's version from the launcher settings the installation ships.</summary>
    private string? ReadGameVersion()
    {
        if (!_content.Contains("launcher-settings.json"))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(_content.Read("launcher-settings.json"));
            return document.RootElement.TryGetProperty("rawVersion", out var version)
                ? version.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
