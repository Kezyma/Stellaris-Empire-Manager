using Sem.GameData;

namespace Sem.Extraction.Tests;

/// <summary>
/// The face a content pack borrows when the game gave it no badge of its own.
/// </summary>
/// <remarks>
/// Three of the packs the content bar shows are a single species portrait and nothing else, and the
/// game ships no icon for any of them, so the bar drew them as two grey letters. Each now wears the
/// portrait it adds — found through the condition that gates it, because nothing in the keys says
/// which pack they came from: Rick The Cube adds <c>cyb_machine</c>.
///
/// Against a fixture rather than the installation, because the answer depends on a thumbnail that
/// only exists once the portraits have been drawn, which is after extraction has finished.
/// </remarks>
public sealed class PackFaceTests
{
    [Fact]
    public void APackWithNoBadgeWearsTheOnePortraitItGates()
    {
        var lent = GameDataExtractor.LendFaces(World("Rick The Cube Species Portrait", "cyb_machine"));

        Assert.Equal("portraits/cyb_machine.png", Pack(lent, "Rick The Cube Species Portrait").Icon);
    }

    /// <summary>A pack that already has one keeps it, whatever it gates.</summary>
    [Fact]
    public void APackWithABadgeKeepsIt()
    {
        var world = World("Rick The Cube Species Portrait", "cyb_machine");
        world = world with
        {
            Dlc = [.. world.Dlc.Select(d => d with { Icon = "icons/dlc/dlc035.png" })],
        };

        var lent = GameDataExtractor.LendFaces(world);

        Assert.Equal("icons/dlc/dlc035.png", Pack(lent, "Rick The Cube Species Portrait").Icon);
    }

    /// <summary>
    /// And a pack that gates several gets none, since there is no one face that is the pack.
    /// </summary>
    /// <remarks>
    /// The species packs are the case: a dozen portraits each, and a badge of their own anyway.
    /// </remarks>
    [Fact]
    public void APackThatGatesSeveralBorrowsNothing()
    {
        var lent = GameDataExtractor.LendFaces(
            World("Necroids Species Pack", "nec1", "nec2"));

        Assert.Null(Pack(lent, "Necroids Species Pack").Icon);
    }

    /// <summary>A portrait nothing could draw lends nothing, rather than lending a broken path.</summary>
    [Fact]
    public void AnUndrawnPortraitLendsNothing()
    {
        var world = World("Stargazer Species Portrait", "stargazer_01");
        world = world with { Portraits = [new PortraitDefinition("stargazer_01")] };

        var lent = GameDataExtractor.LendFaces(world);

        Assert.Null(Pack(lent, "Stargazer Species Portrait").Icon);
    }

    private static DlcDefinition Pack(IEnumerable<DlcDefinition> packs, string name) =>
        packs.Single(d => d.Name == name);

    /// <summary>One pack, and the portraits it gates, each drawn to a thumbnail.</summary>
    private static GameDatabase World(string pack, params string[] portraits) => new()
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        Dlc = [new DlcDefinition("dlc999_test", pack, null, "content_pack", Installed: true)],
        Portraits =
        [
            .. portraits.Select(p => new PortraitDefinition(p) { Thumbnail = $"portraits/{p}.png" })
        ],
        PortraitSets =
        [
            new PortraitSetDefinition("set", "MAM")
            {
                Portraits = [.. portraits.Select(p => new PortraitEntry(p, new DlcRequirement(pack)))],
            },
        ],
    };
}
