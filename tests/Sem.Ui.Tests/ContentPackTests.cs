using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Which packs a civic depends on, and which way round.
/// </summary>
/// <remarks>
/// <para>
/// The way round is the whole of it. The game writes both directions in the same grammar and one
/// civic in the corpus is written the second way: Corporate Dominion's entire condition is
/// <c>NOT = { has_dlc = Megacorp }</c>, because it is the civic for an oligarchy that cannot be a
/// megacorp. Read without the polarity it wore a badge telling the reader to buy the one pack that
/// takes it away from them.
/// </para>
/// <para>
/// That reading was <c>AndNested().OfType&lt;DlcRequirement&gt;()</c> - the same flat walk
/// <see cref="CivicReach"/> already carries three paragraphs about avoiding, made a second time
/// twenty lines from the walk written to avoid it. Both go through <c>Selections</c> now, and this
/// is what holds that still.
/// </para>
/// </remarks>
public sealed class ContentPackTests
{
    /// <summary>A pack a civic asks for is one it needs.</summary>
    [Fact]
    public void APackAskedForIsOneItNeeds()
    {
        var gate = Assert.Single(ContentPacks.Gating(new DlcRequirement("Utopia")));

        Assert.Equal("Utopia", gate.Name);
        Assert.True(gate.Wanted);
    }

    /// <summary>A pack a civic rules out is not.</summary>
    [Fact]
    public void APackRuledOutIsNotOneItNeeds()
    {
        var gate = Assert.Single(ContentPacks.Gating(new NotRequirement(new DlcRequirement("Megacorp"))));

        Assert.Equal("Megacorp", gate.Name);
        Assert.False(gate.Wanted);
    }

    /// <summary>A pack ruled out three levels down is still ruled out.</summary>
    /// <remarks>
    /// Which is what a flat walk cannot see: it finds the name at any depth and has nothing to say
    /// about the two negations it passed on the way.
    /// </remarks>
    [Fact]
    public void PolarityIsCarriedThroughTheNesting()
    {
        var gate = Assert.Single(ContentPacks.Gating(
            new AllRequirement([new AnyRequirement([new NotRequirement(new DlcRequirement("Megacorp"))])])));

        Assert.False(gate.Wanted);
    }

    /// <summary>Two negations cancel, as they do in the script.</summary>
    [Fact]
    public void TwoNegationsCancel()
    {
        Assert.True(Assert.Single(ContentPacks.Gating(
            new NotRequirement(new NotRequirement(new DlcRequirement("Utopia"))))).Wanted);
    }

    /// <summary>A condition naming no pack names none.</summary>
    [Fact]
    public void AConditionNamingNoPackNamesNone()
    {
        Assert.Empty(ContentPacks.Gating(null));
        Assert.Empty(ContentPacks.Gating(new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist")));
    }

    /// <summary>A pack named twice is listed once.</summary>
    [Fact]
    public void APackNamedTwiceIsListedOnce()
    {
        Assert.Single(ContentPacks.Gating(
            new AllRequirement([new DlcRequirement("Utopia"), new DlcRequirement("Utopia")])));
    }

    /// <summary>A pack it needs has to be owned.</summary>
    [Fact]
    public void APackItNeedsHasToBeOwned()
    {
        IReadOnlyList<PackGate> gates = [new PackGate("Utopia", true)];

        Assert.True(ContentPacks.Satisfied(gates, Owning("Utopia")));
        Assert.False(ContentPacks.Satisfied(gates, Owning()));
    }

    /// <summary>
    /// And a pack it rules out has to be not owned, which is the half that was backwards.
    /// </summary>
    /// <remarks>
    /// Answered the naive way - every named pack has to be owned - the civic for an oligarchy that
    /// is not a megacorp was shown as available to exactly the players who cannot take it, and
    /// unavailable to the ones who can.
    /// </remarks>
    [Fact]
    public void APackItRulesOutHasToBeUnowned()
    {
        IReadOnlyList<PackGate> gates = [new PackGate("Megacorp", false)];

        Assert.True(ContentPacks.Satisfied(gates, Owning()));
        Assert.False(ContentPacks.Satisfied(gates, Owning("Megacorp")));
    }

    /// <summary>A civic behind no pack at all suits everybody.</summary>
    [Fact]
    public void NoPacksAtAllSuitsEverybody()
    {
        Assert.True(ContentPacks.Satisfied([], Owning()));
    }

    private static IReadOnlySet<string> Owning(params string[] packs) =>
        new HashSet<string>(packs, StringComparer.Ordinal);
}
