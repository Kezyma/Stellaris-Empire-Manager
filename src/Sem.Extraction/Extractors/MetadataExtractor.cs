using Sem.Clausewitz;
using Sem.Designs;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>Reads content packs, defines and the game's built-in empires.</summary>
internal static class MetadataExtractor
{
    /// <summary>
    /// Reads the downloadable content packs.
    /// </summary>
    /// <remarks>
    /// The <c>name</c> in a pack's descriptor is exactly the string the game's script matches on,
    /// so it is what conditions are keyed by. Which packs exist comes from the metadata file, which
    /// lists them all; which are present comes from the folders, since only installed packs have
    /// one.
    /// </remarks>
    public static List<DlcDefinition> ExtractDlc(ScriptLoader loader, AssetCatalog assets)
    {
        var installed = ReadInstalledPacks(loader);
        var results = new List<DlcDefinition>(installed.Values);
        var known = new HashSet<string>(installed.Keys, StringComparer.Ordinal);

        // Packs the player does not own still need names and categories, so they can be shown as
        // unavailable rather than vanishing from the designer entirely.
        var catalogue = loader.Load("dlc_metadata/dlc_info.txt")?.Nodes
            .FirstOrDefault(n => n.Key == "dlcs")?.Block;

        if (catalogue is not null)
        {
            foreach (var node in catalogue.Nodes)
            {
                if (node.Block is not { } body || body.GetString("name") is not { Length: > 0 } name)
                {
                    continue;
                }

                if (known.Add(name))
                {
                    results.Add(new DlcDefinition(
                        node.Key ?? name,
                        name,
                        body.GetString("localizable_name"),
                        Category: null,
                        Installed: false));
                }
            }
        }

        return [.. results
            .Select(d => d with { Icon = PackIcon(d, assets) })
            .OrderBy(d => d.Folder, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The picture a content pack is known by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game keeps these as sprites named after the pack with its spaces and punctuation taken
    /// out — <c>GFX_themachineage</c> for The Machine Age — which covers three packs in four.
    /// </para>
    /// <para>
    /// The rest are the old portrait packs, which predate the icon set. The game gives them no icon
    /// either, and the store thumbnail in each pack's folder is a wide banner rather than a badge,
    /// so those go without and are shown by name instead.
    /// </para>
    /// </remarks>
    private static string? PackIcon(DlcDefinition pack, AssetCatalog assets)
    {
        var sprite = "GFX_" + new string([.. pack.Name.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

        return assets.Sprites.Resolve(sprite) is null
            ? null
            : assets.RegisterSprite(sprite, $"icons/dlc/{pack.Folder}.png", maxDimension: 64);
    }

    /// <summary>Reads the values from the game's defines that constrain empire creation.</summary>
    public static GameDefines ExtractDefines(ScriptLoader loader)
    {
        var defines = new List<CwBlock>();

        foreach (var (_, document) in loader.LoadDirectory("common/defines"))
        {
            defines.AddRange(document.Nodes.Select(n => n.Block).OfType<CwBlock>());
        }

        foreach (var (_, document) in loader.LoadDirectory("unchecked_defines"))
        {
            defines.AddRange(document.Nodes.Select(n => n.Block).OfType<CwBlock>());
        }

        return new GameDefines
        {
            // Falling back to the values an unmodified game uses keeps the designer usable even if
            // a patch moves these, rather than leaving every budget at zero.
            EthicsPoints = FindInt(defines, "ETHOS_MAX_POINTS") ?? 3,
            CivicPoints = FindInt(defines, "GOVERNMENT_CIVIC_POINTS_BASE") ?? 2,
            DefaultCityPreviewPlanetClass =
                Find(defines, "CITY_SELECTION_DEFAULT_PLANET_CLASS")?.Trim('"'),
        };
    }

    /// <summary>
    /// Reads the game's built-in empires, enough of each to list it as a starting point.
    /// </summary>
    public static List<PrescriptedEmpireSummary> ExtractPrescriptedEmpires(
        ScriptLoader loader,
        RequirementCompiler requirements)
    {
        var results = new List<PrescriptedEmpireSummary>();

        foreach (var path in loader.Content.EnumerateFiles("prescripted_countries", "*.txt"))
        {
            PrescriptedCountriesFile file;
            try
            {
                file = PrescriptedCountriesFile.Load(loader.Content.Read(path));
            }
            catch (CwSyntaxException)
            {
                continue;
            }

            foreach (var empire in file.Empires)
            {
                // The blank template exists for the game's own "new empire" button, not as a preset.
                if (empire.IsDefaultTemplate)
                {
                    continue;
                }

                // Carried already converted into the player's own format. The game writes its
                // empires in a different shape from the file a player saves, and a browser cannot
                // reach the installation to do the conversion itself — so a player can take a copy
                // of one and edit it, which is the whole point of listing them.
                var designs = EmpireDesignsFile.CreateEmpty();
                designs.AddFromPrescripted(empire, empire.Key);

                results.Add(new PrescriptedEmpireSummary(empire.Key, Path.GetFileName(path))
                {
                    NameKey = empire.Name,
                    SpeciesClass = empire.Species?.Class,
                    Portrait = empire.Species?.Portrait,
                    Authority = empire.Authority,
                    Origin = empire.Origin,
                    Playable = requirements.CompileTriggerByName(empire.Playable),
                    FlagSet = empire.PrescriptedFlag,
                    Room = empire.Room,
                    Design = designs.Document.ToText(),
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Converts the game's blank template into the player's designs format, so a new empire starts
    /// from something playable rather than from nothing.
    /// </summary>
    public static string? ExtractNewEmpireTemplate(ScriptLoader loader)
    {
        foreach (var path in loader.Content.EnumerateFiles("prescripted_countries", "*.txt"))
        {
            PrescriptedCountriesFile file;
            try
            {
                file = PrescriptedCountriesFile.Load(loader.Content.Read(path));
            }
            catch (CwSyntaxException)
            {
                continue;
            }

            if (file.Empires.FirstOrDefault(e => e.IsDefaultTemplate) is not { } template)
            {
                continue;
            }

            var designs = EmpireDesignsFile.CreateEmpty();
            designs.AddFromPrescripted(template, "New Empire");
            return designs.Document.ToText();
        }

        return null;
    }

    private static Dictionary<string, DlcDefinition> ReadInstalledPacks(ScriptLoader loader)
    {
        var packs = new Dictionary<string, DlcDefinition>(StringComparer.Ordinal);

        foreach (var layer in loader.Content.Layers.OfType<DirectoryContentSource>())
        {
            var dlcRoot = Path.Combine(layer.Root, "dlc");
            if (!Directory.Exists(dlcRoot))
            {
                continue;
            }

            foreach (var folder in Directory.EnumerateDirectories(dlcRoot).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(folder);

                foreach (var descriptor in Directory.EnumerateFiles(folder, "*.dlc"))
                {
                    var relative = $"dlc/{name}/{Path.GetFileName(descriptor)}";
                    if (loader.Load(relative) is not { } document)
                    {
                        continue;
                    }

                    var declared = document.Nodes.FirstOrDefault(n => n.Key == "name")?.ScalarValue;
                    if (declared is { Length: > 0 })
                    {
                        packs[declared] = new DlcDefinition(
                            name,
                            declared,
                            document.Nodes.FirstOrDefault(n => n.Key == "localizable_name")?.ScalarValue,
                            document.Nodes.FirstOrDefault(n => n.Key == "category")?.ScalarValue,
                            Installed: true);
                    }
                }
            }
        }

        return packs;
    }

    private static string? Find(List<CwBlock> sections, string key) =>
        sections.Select(s => s.FindNestedString(key)).FirstOrDefault(v => v is not null);

    private static int? FindInt(List<CwBlock> sections, string key) =>
        int.TryParse(Find(sections, key), out var value) ? value : null;
}
