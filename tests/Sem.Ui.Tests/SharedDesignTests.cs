using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// An empire arriving by link is offered, not given.
/// </summary>
/// <remarks>
/// Opening a shared link used to add the empire to the player's file outright, which announced a
/// change to the file, which put the whole thing in browser storage before the page had finished
/// drawing. Someone who followed a link to look at an empire came back the next day still holding
/// it. The designer creates it instead, the way pressing Create and copying one of the game's own
/// empires both already did.
/// </remarks>
public sealed class SharedDesignTests
{
    private static Sem.Ui.Services.GameData Data() => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        },
        new Dictionary<string, string>(),
        "assets");

    /// <summary>A session holding one empire the player already had.</summary>
    private static DesignSession Session()
    {
        var session = new DesignSession(Data());
        session.StartEmptyFile();
        session.CreateEmpire(file => file.Add("Mine"));
        session.MarkSaved();
        return session;
    }

    /// <summary>What the designer does with a link, without the page around it.</summary>
    private static void OpenShared(DesignSession session, EmpireDesign shared) =>
        session.CreateEmpire(file =>
        {
            var key = shared.Key;

            for (var attempt = 2; file.Find(key) is not null; attempt++)
            {
                key = $"{shared.Key} {attempt}";
            }

            return file.AddCopy(shared, key);
        });

    private static EmpireDesign Gift()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("A gift");
        design.Authority = "auth_democratic";
        return design;
    }

    [Fact]
    public void AnEmpireFromALinkIsNotTheirsUntilTheySaveIt()
    {
        var session = Session();
        OpenShared(session, Gift());

        // In the file, because the designer has to have something to work on...
        Assert.Equal(2, session.File!.Designs.Count);

        // ...and offered rather than given: the Save button is what accepts it.
        Assert.True(session.IsModified);
    }

    [Fact]
    public void TurningAwayFromALinkedEmpireTakesItBackOut()
    {
        var session = Session();
        var before = session.File!.Document.ToText();

        OpenShared(session, Gift());
        session.Revert();

        Assert.Single(session.File.Designs);
        Assert.Equal(before, session.File.Document.ToText());
    }

    [Fact]
    public void SavingKeepsIt()
    {
        var session = Session();
        OpenShared(session, Gift());
        session.MarkSaved();

        session.Select(session.File!.Designs[0]);

        Assert.Equal(2, session.File.Designs.Count);
        Assert.Contains(session.File.Designs, d => d.Key == "A gift");
    }

    /// <summary>
    /// A link to an empire whose name is taken does not overwrite it.
    /// </summary>
    [Fact]
    public void AClashingNameGetsANumber()
    {
        var session = Session();
        var gift = Gift();
        gift.Rename("Mine");

        OpenShared(session, gift);

        Assert.Equal(["Mine", "Mine 2"], session.File!.Designs.Select(d => d.Key));
    }
}
