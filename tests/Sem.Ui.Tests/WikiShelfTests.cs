using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the wiki has to say about one entry, before any of it reaches a page.
/// </summary>
/// <remarks>
/// Every test in this project is service-level, so anything the wiki works out inside a component
/// has no coverage and cannot be given any. The decisions here are the ones worth holding still:
/// which of the five conditions gets which words when the game states none, what a card says about
/// something no player can have, and what a typed word is matched against.
/// </remarks>
public sealed class WikiShelfTests
{
    private static WikiShelves Shelves(params CivicDefinition[] civics) => Shelves(civics, []);

    private static WikiShelves Shelves(CivicDefinition[] civics, EthicDefinition[] ethics)
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
                    Ethics = ethics.Length > 0
                        ? ethics
                        : [new EthicDefinition("ethic_militarist", 1, "militarist") { Icon = "icons/militarist.png" }],
                    Dlc =
                    [
                        new DlcDefinition("utopia", "Utopia", null, null, true),
                        new DlcDefinition("overlord", "Overlord", null, null, false),
                    ],
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["civic_named"] = "Beacon of Liberty",
                    ["ethic_militarist"] = "Militarist",
                    ["civic_named_desc"] = "A shining example.",
                },
                "assets"));

        session.StartEmptyFile();
        return new WikiShelves(session);
    }

    private static WikiRow Row(CivicDefinition civic) =>
        Assert.Single(Shelves(civic).Of(WikiKind.Civics).Rows);

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

    /// <summary>
    /// The two trees the game states about an empire are read as one list.
    /// </summary>
    /// <remarks>
    /// The game keeps potential and possible apart and means something by it - failing the first
    /// hides the option, failing the second greys it out - but that is about how it refuses you
    /// rather than whether, and a reader asks one question. Two narrow columns, often half empty,
    /// become one that is not.
    /// </remarks>
    [Fact]
    public void TheTwoTreesAboutTheEmpireAreReadAsOneList()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new SelectionRequirement(SelectionCategory.Authority, "auth_democratic"),
            Possible = new NotRequirement(new SelectionRequirement(SelectionCategory.Civics, "civic_other")),
        });

        var requirements = Assert.Single(row.Conditions);

        Assert.Equal("Requirements", requirements.Heading);
        Assert.Equal(ConditionJoin.All, requirements.Outline!.Join);
        Assert.Equal(
            ["auth_democratic", "civic_other"],
            requirements.Outline.Parts.Select(p => p.Chip!.Key));
    }

    /// <summary>
    /// And where both trees say the same thing, it is said once.
    /// </summary>
    /// <remarks>
    /// Twenty-five of the three hundred and fifty-eight name the same selection in both, so without
    /// this a reader would be told twice, in the same words, that their empire must not be a gestalt.
    /// </remarks>
    [Fact]
    public void WhatBothTreesSayIsSaidOnce()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new SelectionRequirement(SelectionCategory.Authority, "auth_democratic"),
            Possible = new SelectionRequirement(SelectionCategory.Authority, "auth_democratic"),
        });

        var requirements = Assert.Single(row.Conditions);

        Assert.Equal(ConditionJoin.Leaf, requirements.Outline!.Join);
        Assert.Equal("auth_democratic", requirements.Outline.Chip!.Key);
    }

    /// <summary>A kind that states nothing carries one heading with words in place of a list.</summary>
    [Fact]
    public void AKindThatStatesNothingSaysSo()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false));

        var requirements = Assert.Single(row.Conditions);

        Assert.False(requirements.Stated);
        Assert.Equal("Any empire", requirements.Otherwise);
    }

    /// <summary>
    /// What you must own is not among them, because the pack chips already say it.
    /// </summary>
    /// <remarks>
    /// Playable is a content pack check and nothing else - all two hundred and one of them, with
    /// not one appearing in either of the other two trees - so a bullet for it would read "needs
    /// Utopia" beside a badge already saying so.
    /// </remarks>
    [Fact]
    public void WhatYouMustOwnIsLeftToThePackChips()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Playable = new DlcRequirement("Utopia"),
        });

        Assert.Equal(["Requirements"], row.Conditions.Select(c => c.Heading));
        Assert.Equal("Utopia", Assert.Single(row.Packs).Name);
    }

    /// <summary>
    /// Nor is whether a reform could add or drop it later.
    /// </summary>
    /// <remarks>
    /// True, and about a game in progress rather than about designing an empire. On most civics the
    /// two say nothing at all, so they cost every card two rows of "Always" to tell a reader nothing
    /// they came for.
    /// </remarks>
    [Fact]
    public void NorIsWhatAReformCouldDoLater()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            CanAddLater = new AlwaysRequirement(false),
            CanRemoveLater = new AlwaysRequirement(false),
        });

        Assert.Single(row.Conditions);
    }

    /// <summary>A condition the game does state comes back as something to indent.</summary>
    [Fact]
    public void AStatedConditionComesBackAsAnOutline()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist"),
        });

        var stated = Assert.Single(row.Conditions);

        Assert.True(stated.Stated);
        Assert.Equal("Militarist", stated.Outline!.Chip!.Name);
        Assert.True(stated.Outline.Wanted);
    }

    /// <summary>
    /// A heading's choices wear the artwork the bullets wear.
    /// </summary>
    /// <remarks>
    /// Written out separately they were names and nothing else, which in a dropdown of seventeen
    /// ethics is the difference between recognising one and reading the list. One lookup feeds both
    /// now, so a filter and a requirement cannot disagree about what an ethic looks like.
    /// </remarks>
    [Fact]
    public void AHeadingsChoicesWearTheSameArtworkAsTheBullets()
    {
        var row = Row(new CivicDefinition("civic_named", IsOrigin: false)
        {
            Potential = new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist"),
        });

        var choice = Assert.Single(row.Wanting(SelectionCategory.Ethics));

        Assert.Equal("Militarist", choice.Name);
        Assert.Equal("icons/militarist.png", choice.Icon);
        Assert.Equal(row.Conditions[0].Outline!.Chip!.Icon, choice.Icon);
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

        Assert.Equal("Unplayable", row.ClosedShort);
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

        Assert.Equal("Event only", row.ClosedShort);
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
        var shelves = Shelves(
            new CivicDefinition("civic_named", IsOrigin: false),
            new CivicDefinition("origin_named", IsOrigin: true));

        Assert.Equal(["civic_named"], shelves.Of(WikiKind.Civics).Rows.Select(r => r.Key));
        Assert.Equal(["origin_named"], shelves.Of(WikiKind.Origins).Rows.Select(r => r.Key));
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
        var shelves = Shelves(
            new CivicDefinition("civic_named", IsOrigin: false),
            new CivicDefinition("origin_named", IsOrigin: true));

        Assert.Same(shelves.Of(WikiKind.Civics), shelves.Of(WikiKind.Civics));
        Assert.Same(shelves.Of(WikiKind.Origins), shelves.Of(WikiKind.Origins));
    }

    /// <summary>
    /// An ethic states no conditions, so it carries none.
    /// </summary>
    /// <remarks>
    /// Not one of the seventeen has a <c>playable</c> or a <c>possible</c> between them, which is
    /// why the ethics page draws neither condition column nor a pack column: every one of them is
    /// within anybody's reach and behind nothing.
    /// </remarks>
    [Fact]
    public void AnEthicCarriesNoConditionsAndNoPacks()
    {
        var row = Ethic(new EthicDefinition("ethic_militarist", 1, "mil"));

        Assert.Empty(row.Conditions);
        Assert.Empty(row.Packs);
        Assert.True(row.Playable);
        Assert.True(row.Owned);
    }

    /// <summary>What an ethic costs is the number, and nothing else.</summary>
    /// <remarks>
    /// The budget is three for every ethic in the game, so saying it on each row is a column
    /// repeating itself seventeen times - and a number alone is what a column sorts by.
    /// </remarks>
    [Fact]
    public void AnEthicSaysItsCostAsANumber()
    {
        Assert.Equal("2", Ethic(new EthicDefinition("ethic_militarist", 2, "mil")).Fact("Cost")!.Text);
    }

    /// <summary>
    /// An ethic names the other strength of itself, under one heading for both directions.
    /// </summary>
    /// <remarks>
    /// One heading, not "Stronger form" and "Milder form". Those read better on a card and are a
    /// disaster in a table: the heading would differ by row, so the columns are the union of both
    /// and every ethic fills one and leaves the other blank down all seventeen rows.
    /// </remarks>
    [Fact]
    public void AnEthicNamesTheOtherStrengthOfItself()
    {
        var ordinary = Ethic(
            new EthicDefinition("ethic_militarist", 1, "mil")
            {
                CategoryValue = 1,
                FanaticVariant = "ethic_fanatic_militarist",
            },
            new EthicDefinition("ethic_fanatic_militarist", 2, "mil")
            {
                CategoryValue = 0,
                RegularVariant = "ethic_militarist",
            });

        Assert.Equal("Ordinary", ordinary.Fact("Intensity")!.Text);
        Assert.Equal(
            ["ethic_fanatic_militarist"],
            ordinary.Fact("Other form")!.Chips.Select(c => c.Key));
    }

    /// <summary>
    /// And it rules out the other side of its own pair, not the other strength of itself.
    /// </summary>
    /// <remarks>
    /// The game groups a pair under one category and places each on a scale within it, so the two
    /// forms of one pole sit on the same side of the middle. Reading the category alone would have
    /// Militarist ruling out Fanatic Militarist, which is the one thing in the category it is not
    /// opposed to.
    /// </remarks>
    [Fact]
    public void AnEthicRulesOutTheOtherSideOfItsOwnPair()
    {
        var militarist = Ethic(
            new EthicDefinition("ethic_militarist", 1, "mil") { CategoryValue = 1 },
            new EthicDefinition("ethic_fanatic_militarist", 2, "mil") { CategoryValue = 0 },
            new EthicDefinition("ethic_pacifist", 1, "mil") { CategoryValue = 3 },
            new EthicDefinition("ethic_fanatic_pacifist", 2, "mil") { CategoryValue = 4 },
            new EthicDefinition("ethic_xenophobe", 1, "xen") { CategoryValue = 1 });

        Assert.Equal(
            ["ethic_fanatic_pacifist", "ethic_pacifist"],
            militarist.Fact("Rules out")!.Chips.Select(c => c.Key).Order());
    }

    /// <summary>Gestalt rules out every other ethic, which is a sentence rather than a list.</summary>
    [Fact]
    public void GestaltRulesOutEverythingInWords()
    {
        var gestalt = Ethic(new EthicDefinition("ethic_gestalt_consciousness", 3, "hive") { IsGestalt = true });

        Assert.Equal("Gestalt", gestalt.Fact("Intensity")!.Text);
        Assert.Equal("Every other ethic", gestalt.Fact("Rules out")!.Text);
    }

    /// <summary>
    /// An authority the game keeps for itself is out of a player's reach.
    /// </summary>
    /// <remarks>
    /// The flag rather than a condition, and that is the rules layer's own decision restated: two
    /// authorities declare a country type in <c>potential</c>, the game's designer does not read it,
    /// and honouring it would hide Machine Intelligence from the player entitled to it.
    /// </remarks>
    [Fact]
    public void AnAuthorityTheGameKeepsForItselfIsOutOfReach()
    {
        Assert.False(Authority(new AuthorityDefinition("auth_ai") { AiOnly = true }).Playable);
        Assert.True(Authority(new AuthorityDefinition("auth_democratic")).Playable);
    }

    /// <summary>An authority says how rulers are chosen, whether there is an heir, and what it forces.</summary>
    [Fact]
    public void AnAuthoritySaysHowItIsGoverned()
    {
        var row = Authority(new AuthorityDefinition("auth_imperial")
        {
            ElectionType = "none",
            HasHeir = true,
            ForcedTraits = ["trait_hive_mind"],
        });

        Assert.Equal("None", row.Fact("Elections")!.Text);
        Assert.Equal("Yes", row.Fact("Heir")!.Text);
        Assert.Equal(["trait_hive_mind"], row.Fact("Forces")!.Chips.Select(c => c.Key));
    }

    /// <summary>And an authority behind a pack carries it, the same way a civic does.</summary>
    [Fact]
    public void AnAuthorityBehindAPackCarriesIt()
    {
        var row = Authority(new AuthorityDefinition("auth_corporate")
        {
            Playable = new DlcRequirement("Utopia"),
        });

        Assert.Equal("Utopia", Assert.Single(row.Packs).Name);
        Assert.True(row.Owned);
    }

    /// <summary>
    /// A class the game refuses outright is the only kind that is out of reach.
    /// </summary>
    /// <remarks>
    /// The rule was "unplayable unless the condition says nothing", which marked thirty-three of the
    /// game's forty-two classes shut when nineteen are: everything behind a species pack - Toxoid,
    /// Necroid, Lithoid, Plantoid, Aquatic - was being reported as something no player can ever be.
    /// A pack is a thing to buy, not a door that is closed.
    /// </remarks>
    [Fact]
    public void OnlyASpeciesClassTheGameRefusesIsOutOfReach()
    {
        Assert.False(Species(new SpeciesClassDefinition("PRE_MAM", "ART")
        {
            Playable = new AlwaysRequirement(false),
        }).Playable);

        Assert.True(Species(new SpeciesClassDefinition("TOX", "ART")).Playable);
    }

    /// <summary>And one behind a pack is playable, and says which pack.</summary>
    [Fact]
    public void ASpeciesClassBehindAPackCarriesItRatherThanBeingShut()
    {
        var row = Species(new SpeciesClassDefinition("TOX", "ART")
        {
            Playable = new DlcRequirement("Utopia"),
        });

        Assert.True(row.Playable);
        Assert.Null(row.ClosedShort);
        Assert.Equal("Utopia", Assert.Single(row.Packs).Name);
    }

    /// <summary>
    /// A class with no faces anywhere is not a choice either, whatever its own file says.
    /// </summary>
    /// <remarks>
    /// Seven of the game's classes have no portrait at all and none of them says
    /// <c>playable = { always = no }</c>, so reading refusal alone called all seven playable. The
    /// designer offers none of them: a species has to look like something.
    /// </remarks>
    [Fact]
    public void ASpeciesClassWithNoFacesIsNotAChoice()
    {
        var row = Species(new SpeciesClassDefinition("SOLARPUNK", "BIOLOGICAL"), faces: false);

        Assert.False(row.Playable);
        Assert.Equal("No portraits", row.ClosedShort);
    }

    /// <summary>And a class the game gives no archetype is artwork rather than a species.</summary>
    [Fact]
    public void AnAppearanceOnlySpeciesClassIsNotAChoice()
    {
        var row = Species(new SpeciesClassDefinition("PSIONIC", null));

        Assert.False(row.Playable);
        Assert.Equal("Appearance only", row.ClosedShort);
    }

    /// <summary>One species class, read as the wiki reads it.</summary>
    /// <param name="species">The class.</param>
    /// <param name="faces">
    /// Whether the game gives it a portrait. True for almost every class, and the default here, so
    /// that a test about packs or refusal is not quietly answering a different question.
    /// </param>
    /// <returns>Its row.</returns>
    private static WikiRow Species(SpeciesClassDefinition species, bool faces = true)
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    SpeciesClasses = [species],
                    PortraitSets = faces
                        ?
                        [
                            new PortraitSetDefinition("set", species.Key)
                            {
                                Portraits = [new PortraitEntry("face", new AlwaysRequirement(true))],
                            },
                        ]
                        : [],
                    Dlc = [new DlcDefinition("utopia", "Utopia", null, null, true)],
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();
        return Assert.Single(new WikiShelves(session).Of(WikiKind.Species).Rows);
    }

    /// <summary>A species trait says what it costs and who may take it.</summary>
    [Fact]
    public void ASpeciesTraitSaysItsCostAndItsRestrictions()
    {
        var row = Trait(new TraitDefinition("trait_intelligent", TraitKind.Species)
        {
            Cost = 2,
            AllowedArchetypes = ["BIOLOGICAL", "LITHOID"],
            Opposites = ["trait_nerve_stapled"],
        });

        Assert.Equal("2", row.Fact("Cost")!.Text);
        Assert.Equal(["BIOLOGICAL", "LITHOID"], row.Fact("Archetype")!.Chips.Select(c => c.Key));
        Assert.Equal(["trait_nerve_stapled"], row.Fact("Rules out")!.Chips.Select(c => c.Key));
    }

    /// <summary>
    /// Hidden is out of reach; not being initial is not.
    /// </summary>
    /// <remarks>
    /// The twenty-four non-initial traits are gated on an origin, and the game offers them once that
    /// origin is picked - which is what this app does too, so calling them unplayable would be a
    /// plain untruth about traits a player can have.
    /// </remarks>
    [Fact]
    public void OnlyAHiddenSpeciesTraitIsOutOfReach()
    {
        Assert.False(Trait(new TraitDefinition("trait_secret", TraitKind.Species) { Hidden = true }).Playable);
        Assert.True(Trait(new TraitDefinition("trait_late", TraitKind.Species) { Initial = false }).Playable);
    }

    /// <summary>A trait behind a pack carries it, though the game states it as a name not a condition.</summary>
    [Fact]
    public void ASpeciesTraitBehindAPackCarriesIt()
    {
        var row = Trait(new TraitDefinition("trait_toxic", TraitKind.Species) { RequiredDlc = "Utopia" });

        Assert.Equal("Utopia", Assert.Single(row.Packs).Name);
        Assert.True(row.Owned);
    }

    /// <summary>Only the species traits are on that shelf; the ruler's are their own question.</summary>
    [Fact]
    public void TheRulerTraitsAreNotOnTheSpeciesShelf()
    {
        var shelves = Shelves(
            [new TraitDefinition("trait_intelligent", TraitKind.Species),
             new TraitDefinition("trait_ruler_charismatic", TraitKind.StartingRuler)]);

        Assert.Equal(
            ["trait_intelligent"],
            shelves.Of(WikiKind.SpeciesTraits).Rows.Select(r => r.Key));
    }

    /// <summary>
    /// A homeworld chip wears the world's icon and borrows the habitability trait's prose.
    /// </summary>
    /// <remarks>
    /// The game writes no description for a planet class at all, so a chip that looked one up under
    /// the world's own key opened an empty panel. The designer's own picker has always borrowed the
    /// preference trait's; the wiki was the only place that did not.
    /// </remarks>
    [Fact]
    public void AHomeworldChipWearsTheWorldAndBorrowsItsPreferencesProse()
    {
        var chip = Assert.Single(Worlds().Fact("Homeworld")!.Chips);

        Assert.Equal("pc_ocean", chip.Key);
        Assert.Equal("icons/planets/pc_ocean.png", chip.Icon);
        Assert.Equal("trait_pc_ocean_preference_desc", chip.Description);
    }

    /// <summary>A world the game names no preference for still gets its picture.</summary>
    [Fact]
    public void AHomeworldWithNoPreferenceStillWearsTheWorld()
    {
        var chip = Assert.Single(Worlds(preference: false).Fact("Homeworld")!.Chips);

        Assert.Equal("icons/planets/pc_ocean.png", chip.Icon);
        Assert.Null(chip.Description);
    }

    /// <summary>
    /// An archetype chip borrows the words of the trait whose picture it already wears.
    /// </summary>
    /// <remarks>
    /// The game writes no description for an archetype either - there is no <c>BIOLOGICAL_desc</c> -
    /// so the picture was borrowed and the words were not, and a Biological chip opened a panel
    /// reading "Biological. No effects." Every species of the archetype carries the trait, so what it
    /// says and what it does are true of all of them.
    /// </remarks>
    [Fact]
    public void AnArchetypeChipBorrowsTheWordsOfTheTraitItWears()
    {
        var chip = Assert.Single(Archetypes().Fact("Archetype")!.Chips);

        Assert.Equal("BIOLOGICAL", chip.Key);
        Assert.Equal("icons/traits/organic.png", chip.Icon);
        Assert.Equal("trait_organic_desc", chip.Description);
        Assert.NotNull(chip.Effects);
    }

    /// <summary>One trait limited to an archetype whose classes all force the same trait.</summary>
    private static WikiRow Archetypes()
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    Archetypes = [new ArchetypeDefinition("BIOLOGICAL", 2, 5, false)],
                    SpeciesClasses =
                    [
                        new SpeciesClassDefinition("MAM", "BIOLOGICAL") { ForcedTrait = "trait_organic" },
                    ],
                    Traits =
                    [
                        new TraitDefinition("trait_intelligent", TraitKind.Species)
                        {
                            AllowedArchetypes = ["BIOLOGICAL"],
                        },
                        new TraitDefinition("trait_organic", TraitKind.Species)
                        {
                            Icon = "icons/traits/organic.png",
                            Effects = new EffectSet
                            {
                                Modifiers = new Dictionary<string, double>(StringComparer.Ordinal)
                                {
                                    ["pop_food_upkeep_mult"] = 0.1,
                                },
                            },
                        },
                    ],
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();

        return new WikiShelves(session).Of(WikiKind.SpeciesTraits).Rows
            .First(r => r.Key == "trait_intelligent");
    }

    /// <summary>One trait limited to a world, with or without a preference trait to borrow from.</summary>
    private static WikiRow Worlds(bool preference = true)
    {
        var traits = new List<TraitDefinition>
        {
            new("trait_aquatic", TraitKind.Species) { AllowedPlanetClasses = ["pc_ocean"] },
        };

        if (preference)
        {
            traits.Add(new TraitDefinition("trait_pc_ocean_preference", TraitKind.Species));
        }

        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    Traits = traits,
                    PlanetClasses =
                    [
                        new PlanetClassDefinition("pc_ocean") { Icon = "icons/planets/pc_ocean.png" },
                    ],
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();

        return new WikiShelves(session).Of(WikiKind.SpeciesTraits).Rows
            .First(r => r.Key == "trait_aquatic");
    }

    /// <summary>One species trait, read as the wiki reads it.</summary>
    private static WikiRow Trait(TraitDefinition trait) =>
        Assert.Single(Shelves([trait]).Of(WikiKind.SpeciesTraits).Rows);

    /// <summary>A session holding nothing but some traits.</summary>
    /// <summary>
    /// A world a civic adds is offered, even though its own file does not mark it a starting world.
    /// </summary>
    /// <remarks>
    /// Nine worlds carry the flag and exactly one more is reachable: the volcanic world, which seven
    /// civics and origins and the Infernal species class each add. Reading the flag alone called it
    /// unreachable - the same untruth the species classes told, a page saying no while the designer
    /// says yes.
    /// </remarks>
    [Fact]
    public void AWorldACivicAddsIsStillAHomeworld()
    {
        var rows = PlanetShelf().Of(WikiKind.Planets).Rows;

        var volcanic = rows.First(r => r.Key == "pc_volcanic");
        Assert.True(volcanic.Playable);
        Assert.Equal("With a civic", volcanic.Fact("Start here")!.Text);
        Assert.Equal(["civic_world_forgers"], volcanic.Fact("Opened by")!.Chips.Select(c => c.Key));

        // And one nothing offers stays out of reach, with the badge saying which door is shut.
        var frozen = rows.First(r => r.Key == "pc_frozen");
        Assert.False(frozen.Playable);
        Assert.Equal("Not a homeworld", frozen.ClosedShort);
    }

    /// <summary>A world with no climate says so rather than leaving the column to wander.</summary>
    /// <remarks>
    /// Thirty-eight of the sixty-nine are outside the climate system. A fact nobody states on the
    /// first row is a column drawn last, and the order of the columns is not the first row's to set.
    /// </remarks>
    [Fact]
    public void AWorldOutsideTheClimateSystemSaysNone()
    {
        var rows = PlanetShelf().Of(WikiKind.Planets).Rows;

        Assert.Equal("None", rows.First(r => r.Key == "pc_habitat").Fact("Climate")!.Text);
        Assert.Equal("Wet", rows.First(r => r.Key == "pc_ocean").Fact("Climate")!.Text);
    }

    /// <summary>A shipset is named under its key shouted, and says what the game says about it.</summary>
    /// <remarks>
    /// The one shelf whose names are not under the entry's own key. Only two of the game's
    /// fifty-two sets are named at all, so the rest fall back - and the fallback has to be the key
    /// made readable rather than the key shouted, or the page reads "HUMANOID 01".
    /// </remarks>
    [Fact]
    public void AShipsetIsNamedUnderItsKeyShoutedAndFallsBackToItReadable()
    {
        var rows = Shipsets().Shipsets(pack: null).Rows;

        var named = rows.First(r => r.Key == "biogenesis_01");
        Assert.Equal("Spinovore", named.Name);
        Assert.Equal("Born as much as built.", named.Description);

        Assert.Equal("Humanoid 01", rows.First(r => r.Key == "humanoid_01").Name);
    }

    /// <summary>And a set that flies nothing of its own says so rather than saying nothing.</summary>
    [Fact]
    public void AShipsetWithNoShipsOfItsOwnSaysSo()
    {
        var rows = Shipsets().Shipsets(pack: null).Rows;

        Assert.Equal("Grown", rows.First(r => r.Key == "biogenesis_01").Fact("Fleet")!.Text);
        Assert.Equal("Built", rows.First(r => r.Key == "humanoid_01").Fact("Fleet")!.Text);
        Assert.Equal("None of its own", rows.First(r => r.Key == "solarpunk_01").Fact("Fleet")!.Text);
    }

    /// <summary>
    /// A personality is named under the prefix the game keeps them under.
    /// </summary>
    /// <remarks>
    /// Fifty of the fifty-one are written as <c>personality_&lt;key&gt;</c> with a description
    /// beside them, which is the one thing about these that is not the usual convention.
    /// </remarks>
    [Fact]
    public void APersonalityIsNamedUnderItsPrefix()
    {
        var row = Assert.Single(Personalities().Of(WikiKind.Personalities).Rows);

        Assert.Equal("Honourbound Warriors", row.Name);
        Assert.Equal("They fight fairly.", row.Description);
        Assert.Equal("50", row.Fact("Weight")!.Text);
    }

    /// <summary>A government says what it calls whoever is in charge, in both forms.</summary>
    [Fact]
    public void AGovernmentSaysBothFormsOfBothTitles()
    {
        var row = Assert.Single(Governments().Of(WikiKind.Governments).Rows);

        Assert.Equal("Emperor", row.Fact("Ruler")!.Text);
        Assert.Equal("Empress", row.Fact("Ruler (female)")!.Text);
        Assert.Equal("Heir Apparent", row.Fact("Heir")!.Text);
        Assert.Equal("100", row.Fact("Weight")!.Text);
    }

    /// <summary>An ascension perk reads the name of its path rather than prettifying the key.</summary>
    /// <remarks>
    /// The game names these - "Ascensions", "Ambitions" - and nothing asked for the key until the
    /// column existed, so the pruner had thrown the words away and the column read
    /// "Ap Category Ascensions".
    /// </remarks>
    [Fact]
    public void AnAscensionPerkNamesItsPath()
    {
        var row = Assert.Single(AscensionPerks().Of(WikiKind.AscensionPerks).Rows);

        Assert.Equal("Ascensions", row.Fact("Path")!.Text);
    }

    /// <summary>Four worlds and a civic that opens one of them.</summary>
    private static WikiShelves PlanetShelf() =>
        Shelves(new GameDatabase
        {
            SchemaVersion = GameDatabase.CurrentSchemaVersion,
            GameVersion = "test",
            ExtractorVersion = "test",
            Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
            PlanetClasses =
            [
                new PlanetClassDefinition("pc_ocean") { Climate = "wet", IsStartingWorld = true },
                new PlanetClassDefinition("pc_volcanic") { Climate = "dry" },
                new PlanetClassDefinition("pc_frozen") { Climate = "cold" },
                new PlanetClassDefinition("pc_habitat") { ShowsCity = false },
            ],
            Civics =
            [
                new CivicDefinition("civic_world_forgers", false)
                {
                    AddedPlanetClasses = ["pc_volcanic"],
                },
            ],
        });

    /// <summary>Three sets: one named and grown, one built, one that flies nothing of its own.</summary>
    private static WikiShelves Shipsets() =>
        Shelves(
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                GraphicalCultures =
                [
                    new GraphicalCultureDefinition("biogenesis_01") { ShipCategory = "bio_ship" },
                    new GraphicalCultureDefinition("humanoid_01") { ShipCategory = "default_ship" },
                    new GraphicalCultureDefinition("solarpunk_01"),
                ],
            },
            ("BIOGENESIS_01", "Spinovore"),
            ("biogenesis_01_shipset_desc", "Born as much as built."));

    /// <summary>One personality, named the way the game names them.</summary>
    private static WikiShelves Personalities() =>
        Shelves(
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                Personalities = [new PersonalityDefinition("honorbound_warriors", 50, 0)],
            },
            ("personality_honorbound_warriors", "Honourbound Warriors"),
            ("personality_honorbound_warriors_desc", "They fight fairly."));

    /// <summary>One government, with both forms of both titles.</summary>
    private static WikiShelves Governments() =>
        Shelves(
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                GovernmentTypes =
                [
                    new GovernmentTypeDefinition("gov_imperial", 100, 0)
                    {
                        RulerTitleKey = "title_emperor",
                        RulerTitleFemaleKey = "title_empress",
                        HeirTitleKey = "title_heir",
                    },
                ],
            },
            ("gov_imperial", "Imperial"),
            ("title_emperor", "Emperor"),
            ("title_empress", "Empress"),
            ("title_heir", "Heir Apparent"));

    /// <summary>One perk, in a path the game has a name for.</summary>
    private static WikiShelves AscensionPerks() =>
        Shelves(
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                AscensionPerks =
                [
                    new AscensionPerkDefinition("ap_engineered_evolution")
                    {
                        Category = "ap_category_ascensions",
                    },
                ],
            },
            ("ap_engineered_evolution", "Engineered Evolution"),
            ("ap_category_ascensions", "Ascensions"));

    /// <summary>A session holding one database and whatever text the test needs.</summary>
    /// <param name="database">The game.</param>
    /// <param name="text">The localisation entries, as key and value.</param>
    /// <returns>The shelves.</returns>
    private static WikiShelves Shelves(
        GameDatabase database,
        params (string Key, string Value)[] text)
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                database,
                text.ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();
        return new WikiShelves(session);
    }

    private static WikiShelves Shelves(TraitDefinition[] traits)
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    Traits = traits,
                    Dlc = [new DlcDefinition("utopia", "Utopia", null, null, true)],
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();
        return new WikiShelves(session);
    }

    /// <summary>The first of some ethics, read as the wiki reads them.</summary>
    private static WikiRow Ethic(params EthicDefinition[] ethics) =>
        Shelves([], ethics).Of(WikiKind.Ethics).Rows.First(r => r.Key == ethics[0].Key);

    /// <summary>One authority, likewise.</summary>
    private static WikiRow Authority(AuthorityDefinition authority)
    {
        var session = new DesignSession(
            new Sem.Ui.Services.GameData(
                new GameDatabase
                {
                    SchemaVersion = GameDatabase.CurrentSchemaVersion,
                    GameVersion = "test",
                    ExtractorVersion = "test",
                    Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                    Authorities = [authority],
                    Traits = [new TraitDefinition("trait_hive_mind", TraitKind.Species)],
                    Dlc =
                    [
                        new DlcDefinition("utopia", "Utopia", null, null, true),
                    ],
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                "assets"));

        session.StartEmptyFile();
        return Assert.Single(new WikiShelves(session).Of(WikiKind.Authorities).Rows);
    }
}
