using Sem.Ui.Components;

namespace Sem.Ui.Services;

/// <summary>
/// One heading a list can be narrowed by: what it is called, and what a row holds under it.
/// </summary>
/// <remarks>
/// <para>
/// A table rather than a control per heading, because every one is the same control asking the same
/// question of a different field. Adding one is a line in whichever table declares them.
/// </para>
/// <para>
/// Generic in the row because the empires are not the only list worth narrowing. The wiki asks the
/// same questions of a civic that the front page asks of an empire - any within a heading, all
/// across them, with a switch for the headings that can hold several - and a second copy of that
/// would be two places that can disagree about what a tick means.
/// </para>
/// </remarks>
/// <typeparam name="TRow">What the heading answers about.</typeparam>
public sealed record Facet<TRow>
    where TRow : class
{
    /// <summary>
    /// Private, so a heading can only be declared through one of the three below.
    /// </summary>
    /// <remarks>
    /// Which is the whole guarantee. Left public, a heading could be written out by hand with
    /// neither arity set and nothing would say so - and that omission is exactly the mistake this
    /// arrangement exists to prevent, twice made. Choosing a factory is choosing an answer.
    /// </remarks>
    private Facet(
        string key,
        string label,
        Func<TRow, IReadOnlyList<EmpireChoice>> values,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions,
        string group)
    {
        Key = key;
        Label = label;
        Values = values;
        Fixed = fixedOptions;
        Group = group;
    }

    /// <summary>What the choice is remembered under, and what the column shares with it.</summary>
    public string Key { get; }

    /// <summary>What the heading is called in the filter card.</summary>
    public string Label { get; }

    /// <summary>What a row holds under it, which may be none, one or several.</summary>
    public Func<TRow, IReadOnlyList<EmpireChoice>> Values { get; }

    /// <summary>
    /// Every option there is, for a heading whose options are a shelf of the game rather than
    /// whatever the rows in front of the reader happen to hold - and nothing for the rest.
    /// </summary>
    /// <remarks>
    /// Still keyed on <see cref="EmpireOptions"/> rather than reduced to a plain thunk, because the
    /// tables that declare headings are static and a thunk would have to close over a session. A
    /// list that already holds every one of a thing needs none of this: the rows are the whole
    /// shelf, and the union the filter card takes of them is the same answer.
    /// </remarks>
    public Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? Fixed { get; }

    /// <summary>
    /// Which tab of the filter card it sits on. Twenty-eight controls in one grid is a wall to read
    /// rather than a card to use.
    /// </summary>
    public string Group { get; }

    /// <summary>
    /// Whether the heading has two answers and wants a dropdown rather than a list of ticks.
    /// </summary>
    /// <remarks>
    /// Yes, no, or neither. Ticking both is the same as ticking neither, which is a thing a set of
    /// tick boxes lets you do and a reader has to work out for themselves - so these say All, Yes
    /// and No and only one at a time.
    /// </remarks>
    public bool YesNo { get; private init; }

    /// <summary>Whether more than one can be held at once, which is what makes "all" worth offering.</summary>
    /// <remarks>
    /// <para>
    /// An empire has one authority and any number of civics. Asking for all of two authorities is a
    /// question with no answer, so the headings that can only hold one are not offered the choice.
    /// </para>
    /// <para>
    /// Declared by the heading rather than matched against a list of keys, which is what this and
    /// <see cref="YesNo"/> both used to be. A written-out list has to be revisited whenever a
    /// heading is added and twice it was not: second species traits went without the choice for as
    /// long as they existed, and so did all three of the plan's headings when they arrived. Worse,
    /// the tests that look like they guard it cannot - every one of them reads the same property
    /// under test, so a heading left out of a list is a heading they all agree about.
    /// </para>
    /// <para>
    /// So the arity lives in the declaration, through <see cref="One"/>, <see cref="Many"/> and
    /// <see cref="Asked"/>. Adding a heading means choosing one of the three, which is the decision
    /// that was being forgotten, and there is no longer a second place that can disagree.
    /// </para>
    /// </remarks>
    public bool Several { get; private init; }

    /// <summary>
    /// A heading a row holds exactly one of, or none.
    /// </summary>
    /// <param name="key">What the choice is remembered under.</param>
    /// <param name="label">What it is called in the filter card.</param>
    /// <param name="value">The one it holds, where it holds one.</param>
    /// <param name="fixedOptions">Every option there is, for a heading whose options are a setting.</param>
    /// <param name="group">Which tab it sits on.</param>
    /// <returns>The heading.</returns>
    public static Facet<TRow> One(
        string key,
        string label,
        Func<TRow, EmpireChoice?> value,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions,
        string group)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Facet<TRow>(key, label, row => Some(value(row)), fixedOptions, group);
    }

    /// <summary>
    /// A heading a row may hold any number of.
    /// </summary>
    /// <param name="key">What the choices are remembered under.</param>
    /// <param name="label">What it is called in the filter card.</param>
    /// <param name="values">Everything it holds.</param>
    /// <param name="fixedOptions">Every option there is, for a heading whose options are a setting.</param>
    /// <param name="group">Which tab it sits on.</param>
    /// <returns>The heading.</returns>
    public static Facet<TRow> Many(
        string key,
        string label,
        Func<TRow, IReadOnlyList<EmpireChoice>> values,
        Func<EmpireOptions, IReadOnlyList<EmpireChoice>>? fixedOptions,
        string group) =>
        new(key, label, values, fixedOptions, group) { Several = true };

    /// <summary>
    /// A heading whose answer is yes or no.
    /// </summary>
    /// <param name="key">What the answer is remembered under.</param>
    /// <param name="label">What it is called in the filter card.</param>
    /// <param name="held">Whether the row answers yes.</param>
    /// <param name="group">Which tab it sits on.</param>
    /// <returns>The heading.</returns>
    public static Facet<TRow> Asked(
        string key,
        string label,
        Func<TRow, bool> held,
        string group)
    {
        ArgumentNullException.ThrowIfNull(held);

        return new Facet<TRow>(key, label, row => YesOrNo(held(row)), fixedOptions: null, group)
        {
            YesNo = true,
        };
    }

    internal static IReadOnlyList<EmpireChoice> Some(EmpireChoice? choice) =>
        choice is null ? [] : [choice];

    /// <summary>
    /// A heading whose answer is yes or no, as the one choice a row holds under it.
    /// </summary>
    /// <remarks>
    /// Which makes it the same kind of heading as every other: ticking Yes asks for the rows that
    /// are, ticking No for the ones that are not, and ticking neither - or both - asks for all of
    /// them. Three answers out of the control the others already use, rather than another kind of
    /// control that only these would want.
    /// </remarks>
    private static IReadOnlyList<EmpireChoice> YesOrNo(bool held) =>
        [held ? Yes : No];

    private static readonly EmpireChoice Yes = new("yes", "Yes", null, null);

    private static readonly EmpireChoice No = new("no", "No", null, null);
}
