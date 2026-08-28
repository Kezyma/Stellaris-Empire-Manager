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
        var ethics = EthicsExtractor.Extract(loader);
        var traits = TraitsExtractor.Extract(loader);

        Report("Reading governments");
        var authorities = GovernmentExtractor.ExtractAuthorities(loader, requirements);
        var civics = GovernmentExtractor.ExtractCivics(loader, requirements);
        var governmentTypes = GovernmentExtractor.ExtractGovernmentTypes(loader, requirements);

        Report("Reading worlds and starting systems");
        var planetClasses = WorldExtractor.ExtractPlanetClasses(loader, requirements);
        var initializers = WorldExtractor.ExtractInitializers(loader);

        Report("Reading portraits");
        var portraitCategories = PortraitExtractor.ExtractCategories(loader);
        var portraitSets = PortraitExtractor.ExtractSets(loader, requirements);
        var portraits = PortraitExtractor.ExtractPortraits(loader);

        Report("Reading appearance options");
        var rooms = CosmeticsExtractor.ExtractRooms(loader);
        var graphicalCultures = CosmeticsExtractor.ExtractGraphicalCultures(loader, requirements);
        var advisorVoices = CosmeticsExtractor.ExtractAdvisorVoices(loader, requirements);
        var nameLists = CosmeticsExtractor.ExtractNameLists(loader, requirements);

        Report("Reading flags");
        var flagCategories = FlagExtractor.ExtractCategories(loader);
        var flagColors = FlagExtractor.ExtractColors(loader);

        Report("Reading built-in empires");
        var prescripted = MetadataExtractor.ExtractPrescriptedEmpires(loader, requirements);

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
            UnrecognisedTriggers = requirements.Unrecognised
                .OrderByDescending(p => p.Value)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
        };

        void Report(string message) => progress?.Report(message);
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
        var all = LocalisationExtractor.Extract(_content, language);
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
