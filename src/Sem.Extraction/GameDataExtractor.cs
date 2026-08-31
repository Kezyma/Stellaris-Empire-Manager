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
    public const int SchemaVersion = GameDatabase.CurrentSchemaVersion;

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
        var dlc = MetadataExtractor.ExtractDlc(loader, assets);
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
        var shipSets = CosmeticsExtractor.ExtractShipSets(loader);
        var leaderClasses = CosmeticsExtractor.ExtractLeaderClasses(loader, assets);
        var advisorVoices = CosmeticsExtractor.ExtractAdvisorVoices(loader, requirements, assets);
        Report("Reading names");
        var nameLists = CosmeticsExtractor.ExtractNameLists(loader, requirements, Localisation);
        var speciesNames = NameExtractor.ExtractSpeciesNames(loader, Localisation);

        Report("Reading flags");
        var flagCategories = FlagExtractor.ExtractCategories(loader, assets);
        var flagColors = FlagExtractor.ExtractColors(loader);
        var flagFrames = FlagExtractor.ExtractFrames(loader, assets);
        var arkships = CosmeticsExtractor.ExtractArkships(loader, assets);

        Report("Reading the empire name generator");
        var empireNameParts = EmpireNameExtractor.ExtractParts(loader);
        var empireNameFormats = EmpireNameExtractor.ExtractFormats(loader, requirements);

        Report("Reading built-in empires");
        var prescripted = MetadataExtractor.ExtractPrescriptedEmpires(loader, requirements);
        var template = MetadataExtractor.ExtractNewEmpireTemplate(loader);

        Report("Reading modifier display settings");
        var modifiers = DescribeModifiers(ethics, traits, authorities, civics);
        var textIcons = ExtractTextIcons(sprites, assets);
        var icons = ExtractInterfaceIcons(assets);
        var chrome = ExtractChrome(assets);

        var database = new GameDatabase
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
            TextIcons = textIcons,
            Icons = icons,
            Chrome = chrome,
            ScriptedValues = ScriptedValues(loader),
            ScriptedText = ScriptedText(loader),
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
            ShipSets = shipSets,
            LeaderClasses = leaderClasses,
            FlagCategories = flagCategories,
            FlagColors = flagColors,
            FlagFrames = flagFrames,
            Arkships = arkships,
            EmpireNameParts = empireNameParts,
            EmpireNameFormats = empireNameFormats,
            PrescriptedEmpires = prescripted,
            EmpireFlagSets = CosmeticsExtractor.ExtractEmpireFlagSets(loader, prescripted),
            NewEmpireTemplate = template,
            UnrecognisedTriggers = requirements.Unrecognised
                .OrderByDescending(p => p.Value)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            UnrecognisedEffectConditions = requirements.UnrecognisedInEffects
                .OrderByDescending(p => p.Value)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
        };

        // Which packs decide anything can only be answered once every rule has been compiled, so it
        // is settled here rather than where the packs themselves are read.
        return database with { Dlc = MarkDecidingPacks(database) };

        void Report(string message) => progress?.Report(message);
    }

    /// <summary>
    /// Says of each content pack whether owning it changes anything the designer offers.
    /// </summary>
    /// <remarks>
    /// A pack decides something when some condition, anywhere in the compiled rules, asks for it.
    /// Answering it here rather than in the interface means the interface has only to read a flag,
    /// and means the answer is settled against the same installation the rest of the data came from.
    /// </remarks>
    private static List<DlcDefinition> MarkDecidingPacks(GameDatabase database)
    {
        var named = database.Requirements()
            .SelectMany(r => r.AndNested())
            .OfType<DlcRequirement>()
            .Select(r => r.Name)
            .ToHashSet(StringComparer.Ordinal);

        return [.. database.Dlc.Select(d => d with { Decides = named.Contains(d.Name) })];
    }

    /// <summary>
    /// Collects the pictures that appear inline in the game's sentences.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game writes these as a code between pound signs — <c>£energy£</c> — and the code names a
    /// sprite. Most are the dedicated <c>GFX_text_</c> ones, but a sentence may equally call for a
    /// modifier's own icon, so the code is tried both ways.
    /// </para>
    /// <para>
    /// Which codes exist is decided by reading the text rather than by taking every sprite in the
    /// game: the codes are what has to be resolved, and the text is where they are.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The numbers the script names rather than writes, which the text refers to as well.
    /// </summary>
    /// <remarks>
    /// The loader has already gathered them, since a cost or a weight is as often a variable as a
    /// literal. Only the ones that resolve to a number are kept: a few name a whole block or another
    /// piece of script, and those mean nothing in a sentence.
    /// </remarks>
    private static Dictionary<string, double> ScriptedValues(ScriptLoader loader)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (name, _) in loader.Variables)
        {
            if (loader.ResolveNumber(name) is { } number)
            {
                values[name.TrimStart('@')] = number;
            }
        }

        return values;
    }

    /// <summary>
    /// What each scripted phrase in the game's display text falls back to.
    /// </summary>
    /// <remarks>
    /// A <c>defined_text</c> is a name, a list of conditional branches, and a default. Every branch
    /// asks about a game in progress — whether a patron has been met, whether a war is on — so at
    /// design time the default is the one that applies, and it is a localisation key like any
    /// other. Entries with no default are left out: the game shows nothing for those either.
    /// </remarks>
    private static Dictionary<string, string> ScriptedText(ScriptLoader loader)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in loader.LoadEntries("common/scripted_loc", recursive: true))
        {
            if (entry.Key == "defined_text" &&
                entry.Body.GetString("name") is { Length: > 0 } name &&
                entry.Body.GetString("default") is { Length: > 0 } fallback)
            {
                texts[name] = fallback;
            }
        }

        return texts;
    }

    private Dictionary<string, string> ExtractTextIcons(SpriteCatalog sprites, AssetCatalog assets)
    {
        var icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in IconCodes(Localisation.Values))
        {
            var destination = $"icons/text/{code}.png";

            // Shown at the size of a letter, so there is no reason to carry a large picture.
            const int LetterHeight = 32;

            var path = assets.RegisterSprite($"GFX_text_{code}", destination, LetterHeight)
                       ?? assets.RegisterSprite($"GFX_{code}", destination, LetterHeight)

                       // Not every icon is declared as a sprite. A modifier's own picture is found
                       // by its name, which is how the game finds it when nothing declares one.
                       ?? assets.RegisterFirst(
                           [
                               $"gfx/interface/icons/modifiers/{code}.dds",
                               $"gfx/interface/icons/modifiers/mod_{code}.dds",
                               $"gfx/interface/icons/resources/{code}.dds",
                           ],
                           destination,
                           LetterHeight);

            if (path is { Length: > 0 })
            {
                icons[code] = path;
            }
        }

        return icons;
    }

    /// <summary>
    /// Pictures the designer's own controls borrow from the game.
    /// </summary>
    /// <remarks>
    /// Named one at a time rather than swept up wholesale: these belong to no option, so nothing
    /// else would ever ask for them, and a control that wants one should have to say so.
    /// </remarks>
    private static Dictionary<string, string> ExtractInterfaceIcons(AssetCatalog assets)
    {
        string[] wanted =
        [
            // The four the species editor offers for gender.
            "GFX_button_gender_all",
            "GFX_button_male",
            "GFX_button_female",
            "GFX_button_no_gender",

            // The die the game puts beside every name it will suggest one of.
            "GFX_button_randomize",

            // The toggle that makes an empire nomadic, which sits beside its authority.
            "GFX_toggle_nomad",
        ];

        var icons = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var sprite in wanted)
        {
            if (assets.RegisterSprite(sprite, $"icons/ui/{sprite}.png", maxDimension: 32) is { } path)
            {
                icons[sprite] = path;
            }
        }

        // Whether an empire may turn up in a game as somebody else's neighbour is a button rather
        // than a list: one picture of three, clicked round. The game keys a frame of a sprite by
        // putting the number after a bar, as its own text does, so the three are keyed that way and
        // a control asks for the one matching the state it is drawing.
        for (var frame = 1; frame <= SpawnSettingFrames; frame++)
        {
            var key = $"{SpawnSetting}|{frame}";

            if (assets.RegisterSprite(SpawnSetting, $"icons/ui/{SpawnSetting}_{frame}.png", frame: frame) is { } path)
            {
                icons[key] = path;
            }
        }

        return icons;
    }

    /// <summary>
    /// The surfaces the game's own empire designer is drawn on.
    /// </summary>
    /// <remarks>
    /// Every one of these is named by <c>interface/customize_species_editors.gui</c>, which is the
    /// designer's own layout file, and the counts beside them are how often it asks for each. They
    /// are listed rather than swept up because that file names sixty-six sprites and most are icons
    /// of an ethic or a civic that already travel with the option they belong to; what is left, and
    /// what nothing else in this app would ever ask for, is the furniture.
    ///
    /// Sizes are left alone. A cornered tile is meant to be drawn at whatever size it is given, and
    /// shrinking one to fit a maximum would shrink the border it must not stretch along with it.
    /// </remarks>
    private static Dictionary<string, ChromeSprite> ExtractChrome(AssetCatalog assets)
    {
        string[] wanted =
        [
            // The panels. dark_area_cut_8 is the designer's workhorse, behind 47 of its containers.
            "GFX_tiles_dark_area_cut_8",
            "GFX_subwindow_tile_plain_solid",
            "GFX_clean_frame_area",
            "GFX_planet_bg_tile",
            "gfx_message_bg",

            // The frames that mark something out: chosen, or highlighted, or simply bounded.
            "GFX_tiles_frame",
            "GFX_tiles_frame_extra_light",
            "GFX_orange_frame_tile",
            "GFX_glow_tile_orange",
            "GFX_glow_tile_orange_no_padding",

            // The buttons, each a strip of frames for resting, hovered and pressed.
            "GFX_galactic_object_button",
            "GFX_standard_button_142_34_button",
            "GFX_standard_button_240_24",
            "GFX_dropdown_button_104",
            "GFX_dropdown_item_104",
            "GFX_button_left",
            "GFX_button_right",

            // The rest of the furniture: rules between sections, the hexagonal ground a planet
            // sits on, the tile an arkship is listed in, and the marker on a chosen ethic.
            "GFX_line",
            "GFX_line_medium",
            "GFX_hex_bg",
            "GFX_ship_design_entry_bg_arkships",
            "GFX_ethic_selected",
            "GFX_gamesetup_gov_sel",
        ];

        var chrome = new Dictionary<string, ChromeSprite>(StringComparer.Ordinal);

        foreach (var name in wanted)
        {
            if (assets.Sprites.Resolve(name) is not { } sprite ||
                assets.RegisterSprite(name, $"icons/chrome/{name}.png") is not { } path)
            {
                continue;
            }

            chrome[name] = new ChromeSprite(path, sprite.BorderSize.X, sprite.BorderSize.Y);
        }

        return chrome;
    }

    /// <summary>The button the game's setup screen puts the spawn setting on.</summary>
    private const string SpawnSetting = "GFX_button_empire_spawn_setting";

    /// <summary>
    /// How many pictures that button has, being its three states.
    /// </summary>
    /// <remarks>
    /// <c>noOfFrames = 3</c> in <c>interface/game_setup/main.gfx</c>. Which frame stands for which
    /// state is not written anywhere: the game picks by index in code. Taken here in the order the
    /// game words them — allowed, forbidden, forced — and confirmed by looking.
    /// </remarks>
    private const int SpawnSettingFrames = 3;

    /// <summary>
    /// Every icon code the game's text refers to.
    /// </summary>
    /// <remarks>
    /// A code may carry a frame number after a vertical bar, naming a variant of the same picture,
    /// which is the same icon as far as this is concerned.
    /// </remarks>
    private static IEnumerable<string> IconCodes(IEnumerable<string> text)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in text)
        {
            var start = value.IndexOf('£');

            while (start >= 0)
            {
                var end = value.IndexOf('£', start + 1);

                if (end < 0)
                {
                    break;
                }

                var code = value[(start + 1)..end].Split('|')[0];

                if (code.Length > 0)
                {
                    codes.Add(code);
                }

                start = value.IndexOf('£', end + 1);
            }
        }

        return codes;
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
        return reachableFrom is null
            ? all
            : LocalisationPruner.Prune(
                reachableFrom, all, LocalisationExtractor.AlwaysKeptKeys(_content, _language));
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
