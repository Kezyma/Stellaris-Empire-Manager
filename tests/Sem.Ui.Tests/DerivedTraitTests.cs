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
