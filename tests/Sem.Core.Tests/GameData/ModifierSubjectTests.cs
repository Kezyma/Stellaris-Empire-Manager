using Sem.GameData;

namespace Sem.Core.Tests.GameData;

/// <summary>
/// The entity a modifier's key names when its own label will not.
/// </summary>
/// <remarks>
/// <para>
/// Two projects read this and neither can see the other: the extractor decides which localisation
/// entries survive pruning, and the designer looks the entity up in what survived. They disagreed.
/// The mechanism for putting a shroud patron's name back was written, tested by eye, and
/// commented - and the entry it reads was never seeded, so every one of these modifiers read as
/// "Add Attunement with an Unknown Entity" with the code to say otherwise sitting right there.
/// </para>
/// <para>
/// Nothing could notice, because the substitution fails silently by design: a modifier naming no
/// entity is meant to be left alone. That is why the rule lives in one place now, and why this is
/// the test for it rather than a test of either side.
/// </para>
/// </remarks>
public sealed class ModifierSubjectTests
{
    /// <summary>The shape the game writes for a flat addition.</summary>
    [Fact]
    public void AnAddedAttunementNamesItsPatron()
    {
        Assert.Contains(
            "the_eater_of_worlds",
            ModifierSubject.Candidates("add_attunement_the_eater_of_worlds"),
            StringComparer.Ordinal);
    }

    /// <summary>The shape it writes for a multiplier, where the patron leads the key.</summary>
    [Fact]
    public void AMultipliedAttunementNamesItsPatron()
    {
        Assert.Contains(
            "the_cradle_of_souls",
            ModifierSubject.Candidates("the_cradle_of_souls_attunement_mult"),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// And the monthly shape, which drops the article the entry keeps.
    /// </summary>
    /// <remarks>
    /// <c>eater_of_worlds_monthly_attunement_add</c> against the entry <c>the_eater_of_worlds</c>.
    /// Offering only what the key says would miss it, so the stem is offered both ways and whichever
    /// is a real entry wins.
    /// </remarks>
    [Fact]
    public void AMonthlyAttunementNamesItsPatronDespiteTheMissingArticle()
    {
        Assert.Contains(
            "the_eater_of_worlds",
            ModifierSubject.Candidates("eater_of_worlds_monthly_attunement_add"),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// An ordinary modifier names nothing, which is what keeps this from doing harm.
    /// </summary>
    /// <remarks>
    /// Whatever comes back is looked up in the shipped text and dropped if it is not there, so a
    /// false candidate costs a failed lookup and nothing else. Offering none at all for the great
    /// majority of modifiers is still worth having: it is the difference between a rule about
    /// attunement and a rule about every key in the game.
    /// </remarks>
    [Theory]
    [InlineData("pop_growth_speed")]
    [InlineData("country_influence_produces_add")]
    [InlineData("army_damage_mult")]
    public void AnOrdinaryModifierNamesNothing(string key) =>
        Assert.Empty(ModifierSubject.Candidates(key));

    /// <summary>A key that is nothing but the affix names nothing rather than an empty stem.</summary>
    /// <remarks>
    /// An empty candidate would be looked up, and an empty key is exactly the kind of thing a
    /// localisation table can answer by accident.
    /// </remarks>
    [Theory]
    [InlineData("add_attunement_")]
    [InlineData("_attunement_mult")]
    public void AKeyThatIsNothingButTheAffixNamesNothing(string key) =>
        Assert.Empty(ModifierSubject.Candidates(key));
}
