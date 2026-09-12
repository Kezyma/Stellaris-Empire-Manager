using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What a government calls its ruler, and which of the two forms a given ruler is shown.
/// </summary>
/// <remarks>
/// A hundred and twenty-eight of the game's hundred and seventy governments write a female form of
/// the title, it was extracted, and it was kept through pruning so the text would be there to show -
/// and then nothing ever read it. Every female ruler who had not been given a title by hand was
/// called Emperor.
/// </remarks>
public sealed class RulerTitleTests
{
    private static GovernmentTypeDefinition Empire => new("gov_empire", 1, 0)
    {
        RulerTitleKey = "RT_EMPEROR",
        RulerTitleFemaleKey = "RT_EMPRESS",
    };

    [Fact]
    public void AFemaleRulerIsGivenTheFemaleForm() =>
        Assert.Equal("RT_EMPRESS", Empire.RulerTitleFor("female"));

    [Theory]
    [InlineData("male")]
    [InlineData("not_set")]
    [InlineData("indeterminable")]
    [InlineData(null)]
    public void EveryOtherRulerIsGivenThePlainForm(string? gender) =>
        Assert.Equal("RT_EMPEROR", Empire.RulerTitleFor(gender));

    /// <summary>
    /// Forty-two governments name no female form, and a ruler of one is not left without a title.
    /// </summary>
    [Fact]
    public void AGovernmentWithNoFemaleFormFallsBackToThePlainOne()
    {
        var plain = new GovernmentTypeDefinition("gov_plain", 1, 0) { RulerTitleKey = "RT_PRESIDENT" };

        Assert.Equal("RT_PRESIDENT", plain.RulerTitleFor("female"));
    }

    /// <summary>The card reads it the same way, which is where anyone would have noticed.</summary>
    [Fact]
    public void TheCardShowsTheFemaleTitleForAFemaleRuler()
    {
        var session = Session();
        session.Edit(design => design.Ruler.Gender = "female");

        var view = new EmpireView(session, session.Current!);

        Assert.Equal("Empress", view.RulerTitle);
    }

    [Fact]
    public void TheCardShowsThePlainTitleOtherwise()
    {
        var session = Session();
        session.Edit(design => design.Ruler.Gender = "male");

        var view = new EmpireView(session, session.Current!);

        Assert.Equal("Emperor", view.RulerTitle);
    }

    /// <summary>A title written by hand is the ruler's own, whichever gender they are.</summary>
    [Fact]
    public void ATitleGivenByHandOutranksBoth()
    {
        var session = Session();
        session.Edit(design =>
        {
            design.Ruler.Gender = "female";
            design.Ruler.GetOrAddTitle().SetLiteral("First Citizen");
        });

        var view = new EmpireView(session, session.Current!);

        Assert.Equal("First Citizen", view.RulerTitle);
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
            Authorities = [new AuthorityDefinition("auth_despotic")],
            GovernmentTypes = [Empire],
        },
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RT_EMPEROR"] = "Emperor",
            ["RT_EMPRESS"] = "Empress",
        },
        "assets");
}
