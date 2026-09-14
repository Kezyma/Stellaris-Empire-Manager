using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What picture stands for a species archetype.
/// </summary>
/// <remarks>
/// The game gives an archetype none: <c>ArchetypeDefinition</c> is a trait budget and a flag, so
/// chips reading "Machine" or "Lithoid" were bare words beside chips that all carried artwork. It
/// does give every species of an archetype a trait, and traits have icons - so the answer is worked
/// out from the classes rather than added to the file, which would cost a schema bump and send every
/// desktop player back through thirty-five thousand files.
/// </remarks>
public sealed class ArchetypeMarkTests
{
    /// <summary>The six archetypes and the classes that name them, as the game has them.</summary>
    private static GameDatabase Game() => new()
    {
        SchemaVersion = GameDatabase.CurrentSchemaVersion,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
        Archetypes =
        [
            new ArchetypeDefinition("BIOLOGICAL", 2, 5, IsRobotic: false),
            new ArchetypeDefinition("PRESAPIENT", 2, 5, IsRobotic: false),
            new ArchetypeDefinition("LITHOID", 2, 5, IsRobotic: false),
            new ArchetypeDefinition("MACHINE", 1, 5, IsRobotic: true),
            new ArchetypeDefinition("ROBOT", 0, 4, IsRobotic: true),
            new ArchetypeDefinition("OTHER", 0, 0, IsRobotic: false),
        ],
        SpeciesClasses =
        [
            new SpeciesClassDefinition("MAM", "BIOLOGICAL") { ForcedTrait = "trait_organic" },
            new SpeciesClassDefinition("REP", "BIOLOGICAL") { ForcedTrait = "trait_organic" },

            // Pre-Sapient is the one that needs more than the first answer found: nine of its
            // classes force the organic trait and two force something else.
            new SpeciesClassDefinition("PRE_LITHOID", "PRESAPIENT") { ForcedTrait = "trait_lithoid" },
            new SpeciesClassDefinition("PRE_MAM", "PRESAPIENT") { ForcedTrait = "trait_organic" },
            new SpeciesClassDefinition("PRE_REP", "PRESAPIENT") { ForcedTrait = "trait_organic" },

            new SpeciesClassDefinition("LITHOID", "LITHOID") { ForcedTrait = "trait_lithoid" },
            new SpeciesClassDefinition("MACHINE", "MACHINE") { ForcedTrait = "trait_machine_unit" },
            new SpeciesClassDefinition("EXD", "OTHER") { ForcedTrait = "trait_exd" },

            // As the game has it: the robot class names no trait at all.
            new SpeciesClassDefinition("ROBOT", "ROBOT"),

            // And one that is only an appearance, so names no archetype either.
            new SpeciesClassDefinition("PSIONIC", null),
        ],
        Traits =
        [
            new TraitDefinition("trait_organic", TraitKind.Species) { Icon = "icons/traits/organic.png" },
            new TraitDefinition("trait_lithoid", TraitKind.Species) { Icon = "icons/traits/lithoid.png" },
            new TraitDefinition("trait_machine_unit", TraitKind.Species) { Icon = "icons/traits/machine.png" },
            new TraitDefinition("trait_mechanical", TraitKind.Species) { Icon = "icons/traits/mechanical.png" },
            new TraitDefinition("trait_exd", TraitKind.Species) { Icon = "icons/traits/exd.png" },
        ],
    };

    /// <summary>Each archetype is known by the trait its classes force on their species.</summary>
    /// <param name="archetype">The archetype key.</param>
    /// <param name="trait">The trait it should be known by.</param>
    [Theory]
    [InlineData("BIOLOGICAL", "trait_organic")]
    [InlineData("LITHOID", "trait_lithoid")]
    [InlineData("MACHINE", "trait_machine_unit")]
    [InlineData("OTHER", "trait_exd")]
    public void AnArchetypeIsKnownByTheTraitItsSpeciesAlwaysCarry(string archetype, string trait) =>
        Assert.Equal(trait, ArchetypeMarks.Trait(Game(), archetype));

    /// <summary>
    /// And by the commonest of them where its classes disagree.
    /// </summary>
    /// <remarks>
    /// Pre-Sapient covers every phenotype the game has, so one of its classes is lithoid and one is
    /// thermophile while the rest are organic. Taking the first found would make the whole archetype
    /// a rock.
    /// </remarks>
    [Fact]
    public void AnArchetypeWhoseClassesDisagreeTakesTheCommonestAnswer() =>
        Assert.Equal("trait_organic", ArchetypeMarks.Trait(Game(), "PRESAPIENT"));

    /// <summary>
    /// The robot archetype is the one the game leaves implicit, and is named rather than guessed.
    /// </summary>
    /// <remarks>
    /// Its class forces no trait, so there is nothing to read off the classes. The game calls the
    /// archetype "Mechanical" and ships <c>trait_mechanical</c> under the same word.
    /// </remarks>
    [Fact]
    public void TheRobotArchetypeFallsBackToTheTraitTheGameNamesItAfter()
    {
        Assert.Equal("trait_mechanical", ArchetypeMarks.Trait(Game(), "ROBOT"));
        Assert.Equal("icons/traits/mechanical.png", ArchetypeMarks.Of(Game(), "ROBOT"));
    }

    /// <summary>The icon comes from that trait, which is what puts a picture on the chip.</summary>
    [Fact]
    public void TheIconIsTheTraitsOwn() =>
        Assert.Equal("icons/traits/machine.png", ArchetypeMarks.Of(Game(), "MACHINE"));

    /// <summary>And nothing is invented for a key that names no archetype.</summary>
    /// <param name="key">Something that is not an archetype.</param>
    [Theory]
    [InlineData("PSIONIC")]
    [InlineData("civic_meritocracy")]
    [InlineData("")]
    [InlineData(null)]
    public void AKeyThatIsNotAnArchetypeGetsNothing(string? key)
    {
        Assert.Null(ArchetypeMarks.Trait(Game(), key));
        Assert.Null(ArchetypeMarks.Of(Game(), key));
    }

    /// <summary>
    /// A requirement naming an archetype draws it with that picture, which is the whole point.
    /// </summary>
    /// <remarks>
    /// Sixty-six of the game's requirements ask for <c>MACHINE</c>, one for <c>LITHOID</c> and two
    /// for <c>ROBOT</c>. Every one of them was a word with a blank where the other chips in the same
    /// bullet list had artwork.
    /// </remarks>
    [Fact]
    public void AChipForAnArchetypeCarriesTheTraitsPicture()
    {
        var reader = new ConditionReader(
            new Localizer(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MACHINE"] = "Machine",
            }),
            Game());

        var chip = reader.Chip(new SelectionRequirement(SelectionCategory.SpeciesArchetype, "MACHINE"));

        Assert.Equal("Machine", chip.Name);
        Assert.Equal("icons/traits/machine.png", chip.Icon);
    }
}
