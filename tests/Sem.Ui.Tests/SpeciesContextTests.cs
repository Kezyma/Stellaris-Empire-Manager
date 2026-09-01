using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Which species the rules are being asked about.
/// </summary>
/// <remarks>
/// An origin may call for a second species, and it is judged by different rules from the founders'.
/// Telling the two apart used to compare the <see cref="SpeciesDesign"/> objects themselves, and a
/// design builds a fresh view every time the property is read — so the comparison was false for the
/// founders as surely as for anybody else, and every species in the application was judged as the
/// second one. The Machine class was the visible symptom: the game only allows it as a second
/// species under one particular origin, so the founders could never be machines.
/// </remarks>
public sealed class SpeciesContextTests
{
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

    private static DesignSession Session()
    {
        var session = new DesignSession(Data());
        session.StartEmptyFile();
        session.CreateEmpire(file => file.Add("Test"));
        return session;
    }

    /// <summary>
    /// Two reads of the same species give two views of one block. Written down because it is the
    /// trap: the views are not equal, and everything that tells one species from another has to
    /// compare what they are views of.
    /// </summary>
    [Fact]
    public void ReadingASpeciesTwiceGivesTwoViewsOfTheSameBlock()
    {
        var design = Session().Current!;

        Assert.NotSame(design.Species, design.Species);
        Assert.Same(design.Species.Block, design.Species.Block);
    }

    [Fact]
    public void TheFounderSpeciesIsNotJudgedAsTheSecondOne()
    {
        var session = Session();

        Assert.False(session.ContextFor(session.Current!.Species)!.IsSecondarySpecies);
    }

    [Fact]
    public void ButAnActualSecondSpeciesIs()
    {
        var session = Session();
        var second = session.Current!.AddSecondarySpecies();

        Assert.True(session.ContextFor(second)!.IsSecondarySpecies);
    }

    /// <summary>
    /// Asking which species this is does not write one into the file.
    /// </summary>
    /// <remarks>
    /// The question used to be answered by comparing the species' block against the founders', and
    /// reading the founders' block makes one where a design has not got one. Three controls ask it,
    /// on every render, so a design missing the field — an older file, a mod's, a hand-edited one —
    /// gained an empty <c>species = { }</c> from being looked at, and a file nobody touched no
    /// longer came back as it went in.
    /// </remarks>
    [Fact]
    public void AskingAboutASpeciesWritesNothing()
    {
        var session = Session();
        var before = session.File!.Document.ToText();

        session.ContextFor(session.Current!.Species);

        Assert.Equal(before, session.File.Document.ToText());
        Assert.DoesNotContain("species", before, StringComparison.Ordinal);
    }
}
