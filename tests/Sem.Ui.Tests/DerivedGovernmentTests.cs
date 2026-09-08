using Sem.Designs;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// The government the design carries, which nobody chooses and everybody reads.
/// </summary>
/// <remarks>
/// It is what an authority, some ethics and some civics add up to, and the game writes the answer
/// down: the empires it saved carry gov_executive_committee and gov_megacorporation. The ones made
/// here all carried gov_despotic_empire, the blank template's own value, whatever they had since
/// become - because every place that shows a government derives it again and not one of them reads
/// the field, so the card and the file only ever met on the way out.
/// </remarks>
public sealed class DerivedGovernmentTests
{
    [Fact]
    public void TheDesignCarriesTheGovernmentItsChoicesAddUpTo()
    {
        var session = Session();

        session.Edit(design => design.Authority = "auth_corporate");

        Assert.Equal("gov_megacorporation", session.Current!.Government);
    }

    /// <summary>
    /// Worked out from the ethics as well, which nothing else here turns on.
    /// </summary>
    /// <remarks>
    /// The forced traits were the only thing being written back before, and no ethic forces one - so
    /// the list of things worth rebuilding a context for had no ethics in it, and an empire that
    /// changed nothing but its ethics kept the government it used to have.
    /// </remarks>
    [Fact]
    public void AnEthicOnItsOwnIsEnoughToChangeIt()
    {
        var session = Session();

        session.Edit(design => design.SetEthics(["ethic_fanatic_authoritarian"]));

        Assert.Equal("gov_despotic_empire", session.Current!.Government);

        session.Edit(design => design.SetEthics(["ethic_fanatic_egalitarian"]));

        Assert.Equal("gov_democratic_republic", session.Current!.Government);
    }

    /// <summary>
    /// An empire whose choices match nothing keeps what it had rather than losing it.
    /// </summary>
    /// <remarks>
    /// The card already says "these ethics, authority and civics do not add up to a government the
    /// game has". Writing nothing over the answer the design arrived holding would throw away the
    /// better of the two.
    /// </remarks>
    [Fact]
    public void AnEmpireMatchingNoGovernmentKeepsTheOneItHad()
    {
        // Every government here wants a corporate authority, and this empire has the template's
        // democratic one - so there is no answer to write and the design's own is left alone.
        var session = Session(Only(new GovernmentTypeDefinition("gov_megacorporation", 10, 0)
        {
            Possible = new FieldRequirement("authority", "auth_corporate"),
        }));

        session.Edit(design => design.Government = "gov_something_the_design_came_with");
        session.Edit(design => design.PlanetClass = "pc_ocean");

        Assert.Equal("gov_something_the_design_came_with", session.Current!.Government);
    }

    /// <summary>A session holding one empire, and three governments to tell apart.</summary>
    private static DesignSession Session(Sem.Ui.Services.GameData? data = null)
    {
        var session = new DesignSession(data ?? Data());
        session.StartEmptyFile();
        session.CreateEmpire(file => file.Add("Test"));

        return session;
    }

    /// <summary>The same world with one government in it, for the empire that matches none.</summary>
    private static Sem.Ui.Services.GameData Only(GovernmentTypeDefinition government) => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
            GovernmentTypes = [government],
        },
        new Dictionary<string, string>(),
        "assets");

    private static Sem.Ui.Services.GameData Data() => new(
        new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
            Ethics =
            [
                new EthicDefinition("ethic_fanatic_authoritarian", 2, "authoritarian"),
                new EthicDefinition("ethic_fanatic_egalitarian", 2, "egalitarian"),
            ],
            Authorities = [new AuthorityDefinition("auth_corporate"), new AuthorityDefinition("auth_despotic")],
            GovernmentTypes =
            [
                // Weighted as the game weights them: the specific answer outbids the general one
                // wherever it applies, and the general one is what is left when nothing else fits.
                new GovernmentTypeDefinition("gov_despotic_empire", 1, 0),
                new GovernmentTypeDefinition("gov_megacorporation", 10, 1)
                {
                    Possible = new FieldRequirement("authority", "auth_corporate"),
                },
                new GovernmentTypeDefinition("gov_democratic_republic", 10, 2)
                {
                    Possible = new SelectionRequirement(
                        SelectionCategory.Ethics,
                        "ethic_fanatic_egalitarian"),
                },
            ],
        },
        new Dictionary<string, string>(),
        "assets");
}
