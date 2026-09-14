using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// How a modifier is written out for somebody to read.
/// </summary>
/// <remarks>
/// Every number the designer shows about what a choice does comes through here, and two of the
/// decisions are ones a reader would never question and could not check: whether 0.2 means twenty
/// per cent or two tenths, and whether a number going up is good news. Both are answered from the
/// extracted table rather than guessed, and both are easy to get backwards.
/// </remarks>
public sealed class ModifierFormatterTests
{
    private static ModifierFormatter Formatter(params (string Key, ModifierInfo Info)[] known) =>
        new(
            new Localizer(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mod_pop_growth_speed"] = "Pop Growth Speed",
                ["mod_country_influence_produces_add"] = "Monthly Influence",
                ["MOD_ARMY_DAMAGE_MULT"] = "Army Damage",
                ["leader_trait_intelligence"] = "Intelligence",
            }),
            new GameDatabase
            {
                SchemaVersion = GameDatabase.CurrentSchemaVersion,
                GameVersion = "test",
                ExtractorVersion = "test",
                Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2, CityPopLevel = 4 },
                Modifiers = known.ToDictionary(m => m.Key, m => m.Info, StringComparer.Ordinal),
            });

    /// <summary>A share of a whole is shown as a percentage, not as the fraction behind it.</summary>
    /// <remarks>
    /// The game writes +10%, and the number it writes it with is 0.1. Shown as it is stored, every
    /// growth and damage modifier in the designer would read as a tenth of what the game says.
    /// </remarks>
    [Fact]
    public void AShareIsShownAsAPercentage()
    {
        var formatter = Formatter(("pop_growth_speed", new ModifierInfo(true, true, false, 2, true)));

        Assert.Equal("+10%", formatter.Format("pop_growth_speed", 0.1).Value);
    }

    /// <summary>And a flat amount is shown as itself.</summary>
    [Fact]
    public void AFlatAmountIsShownAsItself()
    {
        var formatter = Formatter(
            ("country_influence_produces_add", new ModifierInfo(false, true, false, 2, true)));

        Assert.Equal("+3", formatter.Format("country_influence_produces_add", 3).Value);
    }

    /// <summary>A whole number carries no decimals, because the game shows none.</summary>
    /// <remarks>
    /// Trailing zeroes read as precision the number has not got: +10% rather than +10.00%.
    /// </remarks>
    [Fact]
    public void AWholeNumberCarriesNoTrailingZeroes()
    {
        var formatter = Formatter(("pop_growth_speed", new ModifierInfo(true, true, false, 2, true)));

        Assert.Equal("+25%", formatter.Format("pop_growth_speed", 0.25).Value);
        Assert.DoesNotContain(".", formatter.Format("pop_growth_speed", 0.25).Value, StringComparison.Ordinal);
    }

    /// <summary>A loss is signed the way a loss is, which is to say not signed at all.</summary>
    [Fact]
    public void ALossCarriesItsOwnMinusAndNoPlus()
    {
        var formatter = Formatter(("pop_growth_speed", new ModifierInfo(true, true, false, 2, true)));

        Assert.Equal("-15%", formatter.Format("pop_growth_speed", -0.15).Value);
    }

    /// <summary>More of a good thing reads as good, and less of it as bad.</summary>
    [Fact]
    public void MoreOfAGoodThingReadsAsGood()
    {
        var formatter = Formatter(("pop_growth_speed", new ModifierInfo(true, true, false, 2, true)));

        Assert.Equal(ModifierTone.Good, formatter.Format("pop_growth_speed", 0.1).Tone);
        Assert.Equal(ModifierTone.Bad, formatter.Format("pop_growth_speed", -0.1).Tone);
    }

    /// <summary>
    /// And less of a cost reads as good, which is the one a plain sign test gets backwards.
    /// </summary>
    /// <remarks>
    /// Anything an empire pays is better when it is lower, so a negative influence cost is an
    /// improvement. Coloured by its sign it would be shown in the colour of a loss, which tells the
    /// player the opposite of what the choice does.
    /// </remarks>
    [Fact]
    public void LessOfACostReadsAsGood()
    {
        var cost = Formatter(("country_influence_produces_add", new ModifierInfo(false, false, false, 2, true)));

        Assert.Equal(ModifierTone.Good, cost.Format("country_influence_produces_add", -1).Tone);
        Assert.Equal(ModifierTone.Bad, cost.Format("country_influence_produces_add", 1).Tone);
    }

    /// <summary>Something that is neither good nor bad is shown in neither colour.</summary>
    [Fact]
    public void SomethingNeitherGoodNorBadIsNeutral()
    {
        var formatter = Formatter(("pop_growth_speed", new ModifierInfo(true, true, true, 2, true)));

        Assert.Equal(ModifierTone.Neutral, formatter.Format("pop_growth_speed", 0.1).Tone);
        Assert.Equal(ModifierTone.Neutral, formatter.Format("pop_growth_speed", -0.1).Tone);
    }

    /// <summary>
    /// A modifier the game names with a prefixed key is found by it.
    /// </summary>
    /// <remarks>
    /// The game writes these three ways - a lowercase prefixed key, an uppercase one, or the
    /// modifier's own name with no prefix at all - and all three have to be tried, because which
    /// one a given modifier uses is not something the key itself says.
    /// </remarks>
    [Fact]
    public void ALabelIsFoundByEitherPrefixedSpelling()
    {
        var formatter = Formatter();

        Assert.Equal("Pop Growth Speed", formatter.Label("pop_growth_speed"));
        Assert.Equal("Army Damage", formatter.Label("army_damage_mult"));
    }

    /// <summary>
    /// And a modifier the game names without a prefix is found too.
    /// </summary>
    /// <remarks>
    /// Intelligence is written this way and has no prefixed form anywhere, so a lookup that only
    /// tried the prefixes would show it as its own key.
    /// </remarks>
    [Fact]
    public void ALabelIsFoundWithNoPrefixAtAll()
    {
        Assert.Equal("Intelligence", Formatter().Label("leader_trait_intelligence"));
    }

    /// <summary>
    /// One nothing in the game names is made readable rather than shown as written.
    /// </summary>
    /// <remarks>
    /// Which is what a mod's own modifier arrives as. The game does the same thing with one it does
    /// not recognise, and the alternative is a row of script in the middle of a panel of prose.
    /// </remarks>
    [Fact]
    public void AModifierNobodyNamesIsStillMadeReadable()
    {
        var written = Formatter().Label("some_mod_invented_this");

        Assert.DoesNotContain("_", written, StringComparison.Ordinal);
        Assert.Contains("invented", written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A set comes back in a settled order, so a list does not reshuffle as you type.</summary>
    [Fact]
    public void ASetIsOrderedByWhatTheReaderSees()
    {
        var formatter = Formatter();

        var written = formatter.Format(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["leader_trait_intelligence"] = 1,
            ["pop_growth_speed"] = 0.1,
            ["army_damage_mult"] = 0.2,
        });

        Assert.Equal(["Army Damage", "Intelligence", "Pop Growth Speed"], written.Select(m => m.Label));
    }

    /// <summary>A modifier that changes nothing is left out rather than shown as zero.</summary>
    [Fact]
    public void SomethingThatChangesNothingIsLeftOut()
    {
        var written = Formatter().Format(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["pop_growth_speed"] = 0,
            ["army_damage_mult"] = 0.2,
        });

        Assert.Equal(["Army Damage"], written.Select(m => m.Label));
    }
}
