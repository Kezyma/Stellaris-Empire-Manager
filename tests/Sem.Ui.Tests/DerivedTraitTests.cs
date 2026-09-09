using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// The traits an empire's own choices impose on its founders, written into the design.
/// </summary>
/// <remarks>
/// The game forces these and, in its own words, verifies them "only for empire designs" - which is
/// exactly what this app writes. They were computed for the picker, which shows them greyed among
/// the chosen traits and will not let them go, and reached the file only when something else was
/// edited: an empire copied off the game's own shelf opened with a problem against it naming a
/// trait nothing in the editor could add, because the picker refuses to let a forced trait be
/// touched. So it is written when the empire is made, not when it is next changed.
/// </remarks>
public sealed class DerivedTraitTests
{
    [Fact]
    public void AnEmpireIsMadeCarryingTheTraitsItsChoicesForce()
    {
        var session = new DesignSession(Data());
        session.StartEmptyFile();

        session.CreateEmpire(file =>
        {
            var design = file.Add("Test");
            design.Species.Class = "LITH";

            return design;
        });

        Assert.Contains("trait_lithoid", session.Current!.Species.Traits);
    }

    /// <summary>And nothing is imposed on an empire whose choices impose nothing.</summary>
    [Fact]
    public void AndNothingOnOneThatForcesNone()
    {
        var session = new DesignSession(Data());
        session.StartEmptyFile();

        session.CreateEmpire(file =>
        {
            var design = file.Add("Test");
            design.Species.Class = "MAM";

            return design;
        });

        Assert.Empty(session.Current!.Species.Traits);
    }

    /// <summary>
    /// A file that predates any of this is brought up to date as it is opened.
    /// </summary>
    /// <remarks>
    /// The half that was missed the first time. Deriving on creation covers a new empire and one
    /// copied off the game's shelf, and does nothing at all for the empires already in the player's
    /// file - which are the ones they open. Left alone, such an empire reported a trait it must have
    /// and did not, on every visit, with no way to act on it: the picker shows a forced trait greyed
    /// and refuses to let it go, so it will not let one be added either.
    /// </remarks>
    [Fact]
    public void AFileIsBroughtUpToDateAsItOpens()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        file.Add("Old").Species.Class = "LITH";

        var session = new DesignSession(Data());
        session.Open(file, "user_empire_designs_v3.4.txt");

        Assert.Contains("trait_lithoid", session.File!.Designs[0].Species.Traits);
        Assert.True(session.HasUnwrittenFileChanges, "The file gained a trait and has changed.");
    }

    /// <summary>
    /// And one that was already complete opens reporting nothing, so the desktop does not offer to
    /// write back a file it has not touched.
    /// </summary>
    [Fact]
    public void AndOneThatWasAlreadyRightIsLeftAlone()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("Current");
        design.Species.Class = "LITH";
        design.Species.SetTraits(["trait_lithoid"]);

        var session = new DesignSession(Data());
        session.Open(file, "user_empire_designs_v3.4.txt");

        Assert.Equal(["trait_lithoid"], session.File!.Designs[0].Species.Traits);
        Assert.False(session.HasUnwrittenFileChanges, "Nothing changed, so nothing is outstanding.");
    }

    /// <summary>A world with two classes on it, one of which insists on a trait.</summary>
    private static Sem.Ui.Services.GameData Data() => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
            SpeciesClasses =
            [
                new SpeciesClassDefinition("MAM", "BIOLOGICAL"),
                new SpeciesClassDefinition("LITH", "BIOLOGICAL")
                {
                    ForcedTrait = "trait_lithoid",
                },
            ],
            Traits =
            [
                new TraitDefinition("trait_lithoid", TraitKind.Species),
            ],
        },
        new Dictionary<string, string>(),
        "assets");
}
