using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the wiki has to say about one civic, before any of it reaches a page.
/// </summary>
/// <remarks>
/// Every test in this project is service-level, so anything the wiki works out inside a component
/// has no coverage and cannot be given any. The decisions here are the ones worth holding still:
/// which of the five conditions gets which words when the game states none, what a card says about
/// something no player can have, and what a typed word is matched against.
/// </remarks>
public sealed class CivicShelfTests
{
    private static CivicShelf Shelf(params CivicDefinition[] civics)
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    Civics = civics,
                    Dlc =
                    [
                        new DlcDefinition("utopia", "Utopia", null, null, true),
                        new DlcDefinition("overlord", "Overlord", null, null, false),
                    ],
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["civic_named"] = "Beacon of Liberty",
                    ["civic_named_desc"] = "A shining example.",
                },
                "assets"));

        session.StartEmptyFile();
        return new CivicShelf(session);
    }

    private static CivicRow Row(CivicDefinition civic) =>
        Assert.Single(Shelf(civic).Civics);

    /// <summary>A civic is read out of the database into everything a page needs.</summary>
    [Fact]
    public void ACivicIsReadIntoItsNameAndItsProse()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        Assert.Equal("Beacon of Liberty", row.Name);
        Assert.Equal("A shining example.", row.Description);
    }

    /// <summary>
    /// One the game names with nothing at all still gets a name.
    /// </summary>
    /// <remarks>
    /// The game ships <c>civic_caravaneer_caravansary</c> with an empty string for its name, and the
    /// localiser's own fallback does not fire for it: the key is there, so there is nothing missing
    /// to fall back from. Unnamed, it sorted to the front of the list and drew a card with an icon,
    /// a badge and no title.
    /// </remarks>
    [Fact]
    public void OneTheGameLeavesBlankIsStillNamed()
    {
        var row = Row(new CivicDefinition("civic_nameless", IsOrigin: false));

        Assert.Equal("Nameless", row.Name);
    }

    /// <summary>
    /// A typed word is matched against the key as well as the words on screen.
    /// </summary>
    /// <remarks>
    /// Somebody who found a civic named in a save file, a mod or a wiki elsewhere has a word in mind
    /// that no screen here ever shows them.
    /// </remarks>
    [Fact]
    public void AKeyIsSearchableEvenThoughNothingShowsIt()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        Assert.Contains("civic_named", row.Text, StringComparison.Ordinal);
        Assert.Contains("Beacon of Liberty", row.Text, StringComparison.Ordinal);
        Assert.Contains("shining", row.Text, StringComparison.Ordinal);
    }

    /// <summary>All five conditions are there, in the game's own order, whether stated or not.</summary>
    /// <remarks>
    /// The game means something different by each and keeps them apart, so a card that dropped the
    /// silent ones would leave a reader unable to tell "adds nothing" from "not shown".
    /// </remarks>
    [Fact]
    public void AllFiveConditionsAreThere()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        Assert.Equal(
            ["Needs", "Offered to", "Allowed when", "Added by reform", "Dropped by reform"],
            row.Conditions.Select(c => c.Heading));

        Assert.All(row.Conditions, c => Assert.False(c.Stated));
    }

    /// <summary>
    /// Each silent condition gets words of its own rather than one word used five times.
    /// </summary>
    /// <remarks>
    /// "Needs: Always" says the opposite of what it means. The five ask different questions, so
    /// what stands in for silence has to differ with them.
    /// </remarks>
    [Fact]
    public void EachSilentConditionGetsItsOwnWords()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        Assert.Equal(
            ["Nothing", "Any empire", "Always", "Always", "Always"],
            row.Conditions.Select(c => c.Otherwise));
    }

    /// <summary>A condition the game does state comes back as something to indent.</summary>
    [Fact]
    public void AStatedConditionComesBackAsAnOutline()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Playable = new DlcRequirement("Utopia"),
        });

        var needs = row.Conditions[0];

        Assert.True(needs.Stated);
        Assert.Equal("Utopia", needs.Outline!.Chip!.Name);
        Assert.True(needs.Outline.Wanted);
    }

    /// <summary>A pack the reader has and one they do not are told apart.</summary>
    [Fact]
    public void APackYouHaveAndOneYouDoNotAreToldApart()
    {
        Assert.True(Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Playable = new DlcRequirement("Utopia"),
        }).Owned);

        Assert.False(Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Playable = new DlcRequirement("Overlord"),
        }).Owned);
    }

    /// <summary>
    /// A pack that rules a civic out is carried as that, not as one it needs.
    /// </summary>
    /// <remarks>
    /// Corporate Dominion in miniature. Its whole condition is <c>NOT = { has_dlc = Megacorp }</c> -
    /// it is the civic for an oligarchy that cannot be a megacorp - and read without the polarity it
    /// wore a badge telling the reader to buy the one pack that takes it away from them. Owning the
    /// pack has to read as the gate being unmet, which is the opposite of what it means for every
    /// other pack on the page.
    /// </remarks>
    [Fact]
    public void APackThatRulesACivicOutIsNotOneItNeeds()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Playable = new NotRequirement(new DlcRequirement("Utopia")),
        });

        var pack = Assert.Single(row.Packs);

        Assert.Equal("Utopia", pack.Name);
        Assert.False(pack.Wanted);
        Assert.True(pack.Held);
        Assert.False(pack.Satisfied);
        Assert.False(row.Owned);
    }

    /// <summary>And not owning that one is what makes the civic available.</summary>
    [Fact]
    public void NotOwningAPackThatRulesACivicOutSuitsIt()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Playable = new NotRequirement(new DlcRequirement("Overlord")),
        });

        Assert.True(row.Owned);
        Assert.True(Assert.Single(row.Packs).Satisfied);
    }

    /// <summary>One behind no pack at all is owned, rather than being neither.</summary>
    [Fact]
    public void OneBehindNoPackIsOwned()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        Assert.Empty(row.Packs);
        Assert.True(row.Owned);
    }

    /// <summary>
    /// One shut out by the kind of country says which, in words rather than in keys.
    /// </summary>
    /// <remarks>
    /// The game writes <c>fallen_empire</c>, and a card reading "only fallen_empire" is a line of
    /// script in the middle of a page of prose.
    /// </remarks>
    [Fact]
    public void OneShutOutByTheCountrySaysWhichInWords()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new SelectionRequirement(SelectionCategory.CountryType, "fallen_empire"),
        });

        Assert.Equal("Not for players", row.ClosedShort);
        Assert.Contains("Fallen Empire", row.ClosedWhy!, StringComparison.Ordinal);
        Assert.DoesNotContain("fallen_empire", row.ClosedWhy!, StringComparison.Ordinal);
    }

    /// <summary>And one shut out by an event says that instead.</summary>
    /// <remarks>
    /// Two different answers, because they are two different facts: one names a kind of empire the
    /// civic belongs to, the other says there is no empire it belongs to at all.
    /// </remarks>
    [Fact]
    public void OneShutOutByAnEventSaysSo()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new SelectionRequirement(SelectionCategory.Civics, "civic_named"),
        });

        Assert.Equal("By event only", row.ClosedShort);
    }

    /// <summary>One anybody can take says nothing about being shut out.</summary>
    [Fact]
    public void OneAnybodyCanTakeSaysNothingAboutIt()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        Assert.Null(row.ClosedShort);
        Assert.Null(row.ClosedWhy);
    }

    /// <summary>
    /// What it asks an empire to be is filed by heading, and what it rules out is not.
    /// </summary>
    /// <remarks>
    /// Filing a ruled-out choice as a requirement puts every mutually exclusive civic on its
    /// opposite's list - so a reader asking which civics want a militarist empire would be handed
    /// the ones that refuse one.
    /// </remarks>
    [Fact]
    public void WhatItAsksForIsFiledAndWhatItRulesOutIsNot()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new AllRequirement(
            [
                new SelectionRequirement(SelectionCategory.Authority, "auth_democratic"),
                new NotRequirement(new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist")),
            ]),
        });

        Assert.Equal(["auth_democratic"], row.Wanting(SelectionCategory.Authority).Select(c => c.Key));
        Assert.Empty(row.Wanting(SelectionCategory.Ethics));
    }

    /// <summary>
    /// The modifiers a swap grants count as bonuses, not only the always-on ones.
    /// </summary>
    /// <remarks>
    /// A civic whose whole value is a swap for gestalt empires would otherwise be filed as granting
    /// nothing. The trait budget learned this the hard way and its own comment records it: reading
    /// only the always-on modifiers "lost every bonus the game states inside a swap".
    /// </remarks>
    [Fact]
    public void AModifierInsideASwapIsStillABonus()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Effects = new EffectSet
            {
                Modifiers = new Dictionary<string, double>(StringComparer.Ordinal) { ["pop_growth_speed"] = 0.1 },
                Conditional =
                [
                    new ConditionalEffects(
                        new AlwaysRequirement(true),
                        new Dictionary<string, double>(StringComparer.Ordinal) { ["unity_produces_add"] = 2 }),
                ],
            },
        });

        Assert.Equal(["pop_growth_speed", "unity_produces_add"], row.Bonuses.Select(b => b.Key).Order());
    }

    /// <summary>Civics and origins are the same record, and come back on different shelves.</summary>
    [Fact]
    public void CivicsAndOriginsAreToldApart()
    {
        var shelf = Shelf(
            new CivicDefinition("civic_named", IsOrigin: false),
            new CivicDefinition("origin_named", IsOrigin: true));

        Assert.Equal(["civic_named"], shelf.Civics.Select(r => r.Key));
        Assert.Equal(["origin_named"], shelf.Origins.Select(r => r.Key));
    }

    /// <summary>
    /// The shelf is read once, however many times it is asked for.
    /// </summary>
    /// <remarks>
    /// Three hundred and fifty-eight entries through five condition trees with a localisation lookup
    /// each, and the page reads it on every keystroke in the search box. This is the same thing
    /// <see cref="EmpireOptionsTests"/> holds still about the filter card's own lists, and for the
    /// same reason: the class says it is built once, and saying so is not the same as being so.
    /// </remarks>
    [Fact]
    public void TheShelfIsReadOnceAndHeld()
    {
        var shelf = Shelf(
            new CivicDefinition("civic_named", IsOrigin: false),
            new CivicDefinition("origin_named", IsOrigin: true));

        Assert.Same(shelf.Civics, shelf.Civics);
        Assert.Same(shelf.Origins, shelf.Origins);
    }
}
