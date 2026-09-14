using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Whose face a ruler wears, and which picture that is.
/// </summary>
/// <remarks>
/// Four components call this and nothing in the suite could reach it - every test here is
/// service-level. Both of the falling-back rules exist because they were once wrong in ways nobody
/// could see from the code: the ruler was drawn as whatever the species happened to look like, and
/// a ruler set to female was drawn as whatever her people were set to.
/// </remarks>
public sealed class PortraitArtworkTests
{
    /// <summary>The value a design holds when nobody has said which gender.</summary>
    private const string NoPreference = "not_set";

    private static GameDatabase Database(params PortraitDefinition[] portraits) => new()
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        Portraits = portraits,
    };

    private static EmpireDesign Design()
    {
        var file = EmpireDesignsFile.CreateEmpty();

        return file.Add("Test");
    }

    /// <summary>A ruler with a face of their own wears it.</summary>
    [Fact]
    public void ARulerWithTheirOwnLikenessKeepsIt()
    {
        var design = Design();
        design.Species.Portrait = "hum1";
        design.Ruler.Portrait = "hum4";

        Assert.Equal("hum4", PortraitArtwork.RulerPortrait(design));
    }

    /// <summary>
    /// And one with none is drawn as their own people.
    /// </summary>
    /// <remarks>
    /// The key falls back, not the picture. Falling back after the picture had been chosen left
    /// everything downstream with nothing to choose from, and the ruler was drawn as whatever the
    /// species happened to look like.
    /// </remarks>
    [Fact]
    public void ARulerWithNoLikenessOfTheirOwnTakesTheSpeciesOne()
    {
        var design = Design();
        design.Species.Portrait = "hum1";
        design.Ruler.Portrait = string.Empty;

        Assert.Equal("hum1", PortraitArtwork.RulerPortrait(design));
    }

    /// <summary>A ruler's own gender chooses their face.</summary>
    [Fact]
    public void ARulerWithAGenderIsDrawnByIt()
    {
        var design = Design();
        design.Species.Gender = "male";
        design.Ruler.Gender = "female";

        Assert.Equal("female", PortraitArtwork.RulerGender(design));
    }

    /// <summary>
    /// "No preference" is a question to pass on rather than an answer.
    /// </summary>
    /// <remarks>
    /// Treated as an answer, a ruler's gender did nothing at all unless they also had a portrait of
    /// their own.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData(NoPreference)]
    public void ARulerWithNoGenderOfTheirOwnTakesTheSpeciesOne(string held)
    {
        var design = Design();
        design.Species.Gender = "male";
        design.Ruler.Gender = held;

        Assert.Equal("male", PortraitArtwork.RulerGender(design));
    }

    /// <summary>A group resolves to the face it actually wears.</summary>
    /// <remarks>
    /// Anything keyed by the likeness rather than by the group - the wardrobe is, since a group has
    /// no pieces of its own - needs the name of the face and not the group's.
    /// </remarks>
    [Fact]
    public void AGroupResolvesToOneOfItsFaces()
    {
        var database = Database(
            new PortraitDefinition("humanoid") { ResolvesTo = "hum1" },
            new PortraitDefinition("hum1") { Thumbnail = "faces/hum1.png" });

        Assert.Equal("hum1", PortraitArtwork.Resolve(database, "humanoid", null));
    }

    /// <summary>
    /// A key naming nothing comes back as itself.
    /// </summary>
    /// <remarks>
    /// A design may name a likeness from a version this app did not read, and the caller's own
    /// lookup is what says so - swallowing it here would lose the only evidence.
    /// </remarks>
    [Fact]
    public void AKeyNamingNoPortraitIsHandedBack() =>
        Assert.Equal("from_a_later_patch", PortraitArtwork.Resolve(Database(), "from_a_later_patch", null));

    /// <summary>Nothing asked for is nothing resolved.</summary>
    [Fact]
    public void NothingResolvesToNothing()
    {
        Assert.Null(PortraitArtwork.Resolve(Database(), null, null));
        Assert.Null(PortraitArtwork.For(Database(), null, null));
    }

    /// <summary>A likeness with a picture of its own is drawn with it.</summary>
    [Fact]
    public void APortraitWithAPictureUsesIt()
    {
        var database = Database(new PortraitDefinition("hum1") { Thumbnail = "faces/hum1.png" });

        Assert.Equal("faces/hum1.png", PortraitArtwork.For(database, "hum1", null));
    }

    /// <summary>A group with none borrows one from the face it resolves to.</summary>
    [Fact]
    public void AGroupWithNoPictureBorrowsItsFaces()
    {
        var database = Database(
            new PortraitDefinition("humanoid") { ResolvesTo = "hum1" },
            new PortraitDefinition("hum1") { Thumbnail = "faces/hum1.png" });

        Assert.Equal("faces/hum1.png", PortraitArtwork.For(database, "humanoid", null));
    }

    /// <summary>A group offering a face per gender hands back the one asked for.</summary>
    [Fact]
    public void AGroupOffersTheFaceForTheGenderAsked()
    {
        var database = Database(
            new PortraitDefinition("humanoid")
            {
                ResolvesTo = "hum1",
                Members = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["female"] = "hum5",
                },
            },
            new PortraitDefinition("hum1") { Thumbnail = "faces/hum1.png" },
            new PortraitDefinition("hum5") { Thumbnail = "faces/hum5.png" });

        Assert.Equal("hum5", PortraitArtwork.Resolve(database, "humanoid", "female"));
        Assert.Equal("faces/hum5.png", PortraitArtwork.For(database, "humanoid", "female"));

        // And a gender it lists nothing for falls back to the group's default.
        Assert.Equal("hum1", PortraitArtwork.Resolve(database, "humanoid", "male"));
    }

    /// <summary>And a key the database does not know has no picture to offer.</summary>
    [Fact]
    public void AnUnknownKeyHasNoPicture() =>
        Assert.Null(PortraitArtwork.For(Database(), "from_a_later_patch", null));
}
