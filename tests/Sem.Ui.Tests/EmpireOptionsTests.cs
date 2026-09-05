using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What a filter heading offers, worked out once rather than on every reading.
/// </summary>
/// <remarks>
/// Each of these is a pass over one of the game's collections, through the rules, with a name looked
/// up for every key that survives - and the filter card reads seven of them to draw a single tab.
/// They were expression-bodied properties, so all of that ran again every time one was read, which
/// the class's own documentation had said for a long time that it did not.
///
/// Two of them ran twice within one drawing of one card: a species is asked for its class and for
/// its second species' class, and for its traits and its second species' traits, and each pair names
/// the same property.
///
/// This is the one part of that work a test can hold still. Everything else about the filter card
/// is only checked by somebody looking at the page, so it is worth having the part that can be
/// checked actually checked. The database here is empty, which does not matter: what is being
/// asserted is that the same list comes back, not what is in it.
/// </remarks>
public sealed class EmpireOptionsTests
{
    /// <summary>Every heading, so a new one added without a field is caught rather than assumed.</summary>
    public static TheoryData<string, Func<EmpireOptions, IReadOnlyList<EmpireChoice>>> Headings =>
        new()
        {
            { "Ethics", o => o.Ethics },
            { "Civics", o => o.Civics },
            { "Origins", o => o.Origins },
            { "Authorities", o => o.Authorities },
            { "SpeciesClasses", o => o.SpeciesClasses },
            { "Traits", o => o.Traits },
            { "RulerTraits", o => o.RulerTraits },
            { "Portraits", o => o.Portraits },
            { "Homeworlds", o => o.Homeworlds },
            { "StartingSystems", o => o.StartingSystems },
            { "Rooms", o => o.Rooms },
            { "Shipsets", o => o.Shipsets },
            { "Advisors", o => o.Advisors },
            { "RulerClasses", o => o.RulerClasses },
            { "NameLists", o => o.NameLists },
            { "FlagSets", o => o.FlagSets },
            { "Genders", o => o.Genders },
            { "Spawning", o => o.Spawning },
        };

    [Theory]
    [MemberData(nameof(Headings))]
    public void AHeadingIsWorkedOutOnceAndHeld(
        string heading,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>> read)
    {
        var options = new EmpireOptions(Session());

        Assert.True(
            ReferenceEquals(read(options), read(options)),
            $"{heading} was worked out again on the second reading.");
    }

    /// <summary>
    /// The pair that costs the most, asked for the way the Species tab asks for it.
    /// </summary>
    /// <remarks>
    /// Traits and second species traits name one property, and so do the two species classes. This
    /// is the case the card actually hits, written out so that it fails for a reason somebody can
    /// read rather than as one row of the theory above.
    /// </remarks>
    [Fact]
    public void TheSpeciesTabAsksForTraitsTwiceAndPaysOnce()
    {
        var options = new EmpireOptions(Session());

        Assert.Same(options.Traits, options.Traits);
        Assert.Same(options.SpeciesClasses, options.SpeciesClasses);
    }

    private static DesignSession Session()
    {
        var session = new DesignSession(Data());
        session.StartEmptyFile();
        session.CreateEmpire(file => file.Add("Test"));
        return session;
    }

    private static Sem.Ui.Services.GameData Data() => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
            Archetypes = [new ArchetypeDefinition("BIOLOGICAL", 2, 5, false)],
            SpeciesClasses = [new SpeciesClassDefinition("HUM", "BIOLOGICAL")],
        },
        new Dictionary<string, string>(),
        "assets");
}
