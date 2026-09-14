using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>
/// A content pack a civic depends on, and which way round.
/// </summary>
/// <param name="Name">The pack, exactly as the game spells it.</param>
/// <param name="Wanted">True where the pack must be owned, false where owning it rules the civic out.</param>
public sealed record PackGate(string Name, bool Wanted);

/// <summary>
/// Which content packs a condition depends on.
/// </summary>
/// <remarks>
/// Around two hundred of the three hundred and fifty-eight civics and origins are behind a pack, and
/// the check is left in the data on purpose: which packs are owned is a fact about the person using
/// the app rather than about the game files, so it is answered at runtime and the web build has to
/// let somebody say so.
/// </remarks>
public static class ContentPacks
{
    /// <summary>
    /// Every pack a condition turns on, once each, in the order they read in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which way round matters, and a flat walk over the nested conditions gets it backwards.
    /// Corporate Dominion's whole condition is <c>NOT = { has_dlc = Megacorp }</c> - it is the civic
    /// for an oligarchy that <em>cannot</em> be a megacorp - and asking
    /// <c>AndNested().OfType&lt;DlcRequirement&gt;()</c> for its packs answers "Megacorp", which put
    /// a badge on the card saying you need the one pack that takes it away from you.
    /// </para>
    /// <para>
    /// The same mistake <see cref="CivicReach"/> already carries a paragraph about, made a second
    /// time twenty lines away from the walk written to avoid it. Both go through
    /// <see cref="Selections"/> now.
    /// </para>
    /// <para>
    /// Order is the order they appear rather than alphabetical, because a civic behind two packs
    /// almost always names the one that introduced it first, and that is the one a reader is
    /// looking for.
    /// </para>
    /// </remarks>
    /// <param name="requirement">The condition to read, or nothing.</param>
    /// <returns>The packs, each with whether it must be owned or must not.</returns>
    public static IReadOnlyList<PackGate> Gating(Requirement? requirement) =>
    [
        .. Selections.Leaves(requirement)
            .Where(l => l.Leaf is DlcRequirement)
            .Select(l => new PackGate(((DlcRequirement)l.Leaf).Name, l.Wanted))
            .DistinctBy(p => p.Name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Whether the packs somebody owns satisfy every one of these gates.
    /// </summary>
    /// <remarks>
    /// Both directions, which is the point of reading the polarity in the first place. A pack the
    /// civic needs has to be owned; a pack it rules out must not be. Answered the naive way, the
    /// civic for an oligarchy that is not a megacorp was shown as available to exactly the players
    /// who cannot take it.
    /// </remarks>
    /// <param name="gates">What the civic asks about packs.</param>
    /// <param name="owned">What the reader has.</param>
    /// <returns>True when every gate is satisfied.</returns>
    public static bool Satisfied(IReadOnlyList<PackGate> gates, IReadOnlySet<string> owned)
    {
        ArgumentNullException.ThrowIfNull(gates);
        ArgumentNullException.ThrowIfNull(owned);

        return gates.All(gate => owned.Contains(gate.Name) == gate.Wanted);
    }
}
