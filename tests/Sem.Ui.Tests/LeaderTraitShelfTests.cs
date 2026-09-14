using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// The one wiki shelf built from a file of the wiki's own rather than from the designer's data.
/// </summary>
/// <remarks>
/// Two things here are unlike every other shelf. The text arrives with the records, because the
/// app's own localisation is pruned to what the database reaches and a leader trait is not in the
/// database; and a third of them have no name at all in any language the game ships, being tiers a
/// leader earns while a game is running, so the heading has to be inherited from what they replace.
/// </remarks>
public sealed class LeaderTraitShelfTests
{
    private static readonly WikiPackStamp Stamp =
        new("test", LeaderTraitPack.CurrentSchemaVersion);

    /// <summary>A session with nothing in it, since the shelf is built from the pack alone.</summary>
    private static WikiShelves Shelves(params (string Key, string Name)[] known)
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    Dlc = [new DlcDefinition("paragon", "Galactic Paragons", null, null, true)],
                    LeaderClasses =
                    [
                        new LeaderClassDefinition("scientist", "scientist")
                        {
                            Icon = "icons/leaders/scientist.png",
                        },
                    ],
                },
                known.ToDictionary(k => k.Key, k => k.Name, StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();
        return new WikiShelves(session);
    }

    private static LeaderTraitPack Pack(
        IReadOnlyList<LeaderTraitDefinition> traits,
        params (string Key, string Text)[] text) => new()
        {
            Stamp = Stamp,
            Traits = traits,
            Text = text.ToDictionary(t => t.Key, t => t.Text, StringComparer.Ordinal),
        };

    /// <summary>A trait the pack names is called what the pack calls it.</summary>
    [Fact]
    public void ATraitIsNamedFromTheTextThatTravelledWithIt()
    {
        var row = Assert.Single(Shelves()
            .LeaderTraits(Pack(
                [new LeaderTraitDefinition("leader_trait_carefree")],
                ("leader_trait_carefree", "Carefree"),
                ("leader_trait_carefree_desc", "Happy to let the results speak.")))
            .Rows);

        Assert.Equal("Carefree", row.Name);
        Assert.Equal("Happy to let the results speak.", row.Description);
    }

    /// <summary>
    /// And one the game never named takes the name of the tier it replaces.
    /// </summary>
    /// <remarks>
    /// Two hundred and thirty-four of the game's leader traits are in this position. Left alone they
    /// would be a third of the page showing a raw key, or nothing at all.
    /// </remarks>
    [Fact]
    public void ANamelessTierTakesTheNameOfWhatItReplaces()
    {
        var rows = Shelves()
            .LeaderTraits(Pack(
                [
                    new LeaderTraitDefinition("leader_trait_wrecker") { Tier = 1 },
                    new LeaderTraitDefinition("leader_trait_wrecker_2")
                    {
                        Tier = 2,
                        Replaces = ["leader_trait_wrecker"],
                    },
                    new LeaderTraitDefinition("leader_trait_wrecker_3")
                    {
                        Tier = 3,
                        Replaces = ["leader_trait_wrecker_2"],
                    },
                ],
                ("leader_trait_wrecker", "Wrecker")))
            .Rows;

        Assert.All(rows, r => Assert.Equal("Wrecker", r.Name));
        Assert.Equal(["1", "2", "3"], rows.Select(r => r.Fact("Tier")!.Text));
    }

    /// <summary>
    /// A chain that ends outside the pack is followed into the app's own text.
    /// </summary>
    /// <remarks>
    /// A handful of these tiers replace a starting-ruler trait, which is in the database and so is
    /// named in the ordinary localisation rather than in the pack.
    /// </remarks>
    [Fact]
    public void AChainEndingAtARulerTraitIsStillNamed()
    {
        var row = Assert.Single(Shelves(("trait_ruler_charismatic", "Charismatic"))
            .LeaderTraits(Pack(
                [
                    new LeaderTraitDefinition("trait_ruler_charismatic_2")
                    {
                        Tier = 2,
                        Replaces = ["trait_ruler_charismatic"],
                    },
                ]))
            .Rows);

        Assert.Equal("Charismatic", row.Name);
    }

    /// <summary>
    /// A pair that replace each other does not go round for ever.
    /// </summary>
    /// <remarks>
    /// Not something the game ships, and the reason the walk is bounded: a cycle here would hang the
    /// page rather than draw a bad name, which is a far worse failure.
    /// </remarks>
    [Fact]
    public void ACycleInTheChainStops()
    {
        var rows = Shelves()
            .LeaderTraits(Pack(
                [
                    new LeaderTraitDefinition("leader_trait_a") { Replaces = ["leader_trait_b"] },
                    new LeaderTraitDefinition("leader_trait_b") { Replaces = ["leader_trait_a"] },
                ]))
            .Rows;

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
    }

    /// <summary>What a leader trait says about itself beyond what it does.</summary>
    [Fact]
    public void ATraitSaysWhoMayHoldItAndWhatSortItIs()
    {
        var row = Assert.Single(Shelves()
            .LeaderTraits(Pack(
                [
                    new LeaderTraitDefinition("leader_trait_carefree")
                    {
                        LeaderClasses = ["scientist"],
                        Sort = "veteran",
                        Rarity = "common",
                        RequiredDlc = "Galactic Paragons",
                    },
                ],
                ("leader_trait_carefree", "Carefree")))
            .Rows);

        Assert.Equal(["scientist"], row.Fact("Class")!.Chips.Select(c => c.Key));
        Assert.Equal("Veteran", row.Fact("Sort")!.Text);
        Assert.Equal("Common", row.Fact("Rarity")!.Text);
        Assert.Equal("Galactic Paragons", Assert.Single(row.Packs).Name);
    }

    /// <summary>
    /// A tier of zero says nothing rather than saying nought.
    /// </summary>
    /// <remarks>
    /// Two hundred and twenty-five of them are not part of a chain at all, and a "0" in a column of
    /// ones and twos reads as a rank rather than as an absence.
    /// </remarks>
    [Fact]
    public void ATraitOutsideAnyChainSaysNoTier()
    {
        var row = Assert.Single(Shelves()
            .LeaderTraits(Pack(
                [new LeaderTraitDefinition("leader_trait_loose")],
                ("leader_trait_loose", "Loose")))
            .Rows);

        Assert.Null(row.Fact("Tier"));
    }

    /// <summary>A leader class chip wears its own badge.</summary>
    [Fact]
    public void AClassChipWearsItsBadge()
    {
        var row = Assert.Single(Shelves()
            .LeaderTraits(Pack(
                [new LeaderTraitDefinition("leader_trait_carefree") { LeaderClasses = ["scientist"] }],
                ("leader_trait_carefree", "Carefree")))
            .Rows);

        var chip = Assert.Single(row.Fact("Class")!.Chips);

        Assert.Equal("icons/leaders/scientist.png", chip.Icon);
    }

    /// <summary>
    /// A chip naming another trait wears that trait's picture and is named the way its row is.
    /// </summary>
    /// <remarks>
    /// These keys are in the wiki's own file and in no collection the database has, so a chip built
    /// the ordinary way found nothing: no picture, an empty panel, and the key prettified for a name
    /// - "Leader Trait Adventurous Spirit 2" sitting beside a row headed "Adventurous Spirit".
    /// </remarks>
    [Fact]
    public void AChipNamingAnotherTraitIsDrawnAsThatTraitIs()
    {
        var rows = Shelves()
            .LeaderTraits(Pack(
                [
                    new LeaderTraitDefinition("leader_trait_wrecker")
                    {
                        Tier = 1,
                        Icon = "icons/traits/wrecker.png",
                    },
                    new LeaderTraitDefinition("leader_trait_wrecker_2")
                    {
                        Tier = 2,
                        Replaces = ["leader_trait_wrecker"],
                        Icon = "icons/traits/wrecker_2.png",
                    },
                    new LeaderTraitDefinition("leader_trait_rival")
                    {
                        Opposites = ["leader_trait_wrecker_2"],
                    },
                ],
                ("leader_trait_wrecker", "Wrecker"),
                ("leader_trait_rival", "Rival")))
            .Rows;

        // The tier that replaces one: its chip names the tier below, which the game did name.
        var replaces = Assert.Single(
            rows.First(r => r.Key == "leader_trait_wrecker_2").Fact("Replaces")!.Chips);

        Assert.Equal("Wrecker", replaces.Name);
        Assert.Equal("icons/traits/wrecker.png", replaces.Icon);

        // And one ruling out a tier the game never named takes the name of what that tier replaces,
        // rather than the key prettified.
        var rules = Assert.Single(rows.First(r => r.Key == "leader_trait_rival").Fact("Rules out")!.Chips);

        Assert.Equal("Wrecker", rules.Name);
        Assert.Equal("icons/traits/wrecker_2.png", rules.Icon);
    }

    /// <summary>The shelf says which keys it answers for, so those chips can be links.</summary>
    [Fact]
    public void TheShelfNamesTheKeysOnlyItKnows()
    {
        var shelf = Shelves().LeaderTraits(Pack(
            [new LeaderTraitDefinition("leader_trait_carefree")],
            ("leader_trait_carefree", "Carefree")));

        Assert.Contains("leader_trait_carefree", shelf.Entries);
        Assert.NotNull(shelf.Reader);
    }

    /// <summary>
    /// A file that could not be fetched leaves an empty shelf rather than no shelf.
    /// </summary>
    /// <remarks>
    /// The page then says "nothing matches" rather than spinning on a loading message for ever,
    /// which is what an absent shelf would have given it.
    /// </remarks>
    [Fact]
    public void AMissingPackLeavesAnEmptyShelfRatherThanNone()
    {
        var shelf = Shelves().LeaderTraits(null);

        Assert.Equal("Leader Traits", shelf.Title);
        Assert.Empty(shelf.Rows);
    }
}
