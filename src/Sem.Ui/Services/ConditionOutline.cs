using Sem.GameData;

namespace Sem.Ui.Services;

/// <summary>How a condition's parts fit together.</summary>
public enum ConditionJoin
{
    /// <summary>Not a group: one thing being asked about.</summary>
    Leaf,

    /// <summary>Every one of the parts must hold.</summary>
    All,

    /// <summary>At least one of the parts must hold.</summary>
    Any,
}

/// <summary>
/// One condition, shaped for reading rather than for evaluating.
/// </summary>
/// <remarks>
/// <para>
/// The game shows these as an indented list with the joins written out, and that is the shape worth
/// copying: a condition three levels deep run together into one sentence - "not ethic Gestalt
/// Consciousness and not authority Corporate and Oligarchic Authority and not Xenophobe or Fanatic
/// Xenophobe" - is a sentence nobody can parse, because the reader has to reconstruct the nesting
/// from the word order and the word order does not carry it.
/// </para>
/// <para>
/// So the structure survives to the page and the page indents it. <see cref="ConditionWriter"/> is
/// still the right answer where there is one line to fill - a swap saying when it applies, a reason
/// an option is blocked - and this is the right answer where there is a panel.
/// </para>
/// </remarks>
/// <param name="Join">Whether this is one thing, all of several, or any of several.</param>
/// <param name="Wanted">
/// For one thing: whether it is being asked for or asked against. A group is always positive - a
/// negation is pushed down to the leaves as it is built, which is what turns "not all of these" into
/// "any of these, not held".
/// </param>
public sealed record ConditionOutline(ConditionJoin Join, bool Wanted)
{
    /// <summary>The parts, for a group.</summary>
    public IReadOnlyList<ConditionOutline> Parts { get; init; } = [];

    /// <summary>
    /// What is being asked about, where it is a thing the game draws.
    /// </summary>
    /// <remarks>
    /// A civic, an ethic, an authority, a trait, a world, a content pack. Shown as the chip it is
    /// shown as everywhere else, so a reader recognises it from the picker without reading it.
    /// </remarks>
    public EmpireChoice? Chip { get; init; }

    /// <summary>What is being asked about, where there is no chip for it - said in words.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// Whether this says something about the condition rather than naming something to hold.
    /// </summary>
    /// <remarks>
    /// One case, and it needs the distinction: a condition nothing can satisfy is a statement, not a
    /// subject. Drawn like every other bullet it came out as "must not have: never", which is a
    /// sentence disagreeing with itself - so it gets the words and neither the tick nor the cross.
    /// </remarks>
    public bool Plain { get; init; }
}

/// <summary>
/// Turns the game's compiled conditions into something a page can indent.
/// </summary>
/// <param name="localizer">What everything is called.</param>
/// <param name="database">Where the artwork and the names are looked up.</param>
public sealed class ConditionReader(Localizer localizer, GameDatabase database)
{
    private readonly Localizer _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

    private readonly GameDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    /// <summary>
    /// Reads a condition, or returns nothing where it says nothing.
    /// </summary>
    /// <param name="requirement">The condition, or nothing.</param>
    /// <returns>The outline, or null where the condition asks nothing at all.</returns>
    public ConditionOutline? Read(Requirement? requirement) => Build(requirement, wanted: true);

    private ConditionOutline? Build(Requirement? requirement, bool wanted)
    {
        switch (requirement)
        {
            case null:
                return null;

            // A condition that always holds says nothing worth a bullet. One that never does says
            // the only thing about the option that matters, so it gets one - as a statement rather
            // than as a thing to have or not have. All sixteen of the hidden origins are written
            // this way, and it is the whole story about them.
            case AlwaysRequirement always:
                return always.Value == wanted
                    ? null
                    : new ConditionOutline(ConditionJoin.Leaf, false) { Text = "Never", Plain = true };

            case NotRequirement not:
                return Build(not.Item, !wanted);

            // De Morgan, so that a negation never has to be carried on a group. "Not all of these"
            // is "any of these, not held", and written that way every bullet on the page reads the
            // same: a tick or a cross against one thing.
            case AllRequirement all:
                return Group(all.Items, wanted ? ConditionJoin.All : ConditionJoin.Any, wanted);

            case AnyRequirement any:
                return Group(any.Items, wanted ? ConditionJoin.Any : ConditionJoin.All, wanted);

            case SelectionRequirement selection:
                return new ConditionOutline(ConditionJoin.Leaf, wanted) { Chip = Chip(selection) };

            case DlcRequirement dlc:
                return new ConditionOutline(ConditionJoin.Leaf, wanted) { Chip = Pack(dlc.Name) };

            // A plain field, and in this corpus only ever is_nomadic. The value decides the mark as
            // much as the polarity does: "is_nomadic = no", asked for, is a cross against being
            // nomadic. Drawn as its own words it read "Is Nomadic is No", which is a sentence in
            // the middle of a column of chips and one negation harder to read than it needs to be.
            case FieldRequirement field when Field(field) is { } chip:
                return new ConditionOutline(
                    ConditionJoin.Leaf,
                    wanted == field.Value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    Chip = chip,
                };

            default:
                return new ConditionOutline(ConditionJoin.Leaf, wanted) { Text = Words(requirement) };
        }
    }

    /// <summary>
    /// A group of parts, with the ones that say nothing dropped and a lone survivor unwrapped.
    /// </summary>
    /// <remarks>
    /// Both simplifications earn their place on the real corpus. The game writes
    /// <c>NOT = { AND = { has_ethic = gestalt } }</c> where it means "not a gestalt", and drawn
    /// literally that is two levels of indentation and a heading over a single bullet. Dropping the
    /// empty parts matters for the same reason: half of what an <c>AND</c> holds is often an
    /// <c>always = yes</c> that exists to make the script tidy.
    /// </remarks>
    private ConditionOutline? Group(IReadOnlyList<Requirement> items, ConditionJoin join, bool wanted)
    {
        List<ConditionOutline> parts = [];

        foreach (var built in items.Select(i => Build(i, wanted)).OfType<ConditionOutline>())
        {
            // A group of the same kind nested directly inside this one is the same question asked
            // twice. Lifting its parts here keeps the indentation to the nesting that means
            // something.
            if (built.Join == join)
            {
                parts.AddRange(built.Parts);
            }
            else
            {
                parts.Add(built);
            }
        }

        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => new ConditionOutline(join, true) { Parts = parts },
        };
    }

    /// <summary>
    /// The chip for whatever a selection names, looked up by what kind of thing it is.
    /// </summary>
    /// <remarks>
    /// Every category the game can ask about, because an unhandled one falls through to its own key
    /// prettified - which reads as a name and is not one. The ones with no artwork still get a chip:
    /// what makes it a chip is being a named thing rather than a phrase, and a country type with no
    /// icon is still Fallen Empire rather than a sentence about country types.
    /// </remarks>
    public EmpireChoice Chip(SelectionRequirement selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var key = selection.Key;

        // The artwork and what the thing does, together, because the chip this feeds opens a panel
        // on hover and a chip with neither opens an empty one. Four of the ten categories the game
        // asks about carry no artwork at all - a species class, an archetype, a graphical culture
        // and a country type are script rather than content - and they still get a chip, because
        // what makes it a chip is being a named thing rather than a phrase.
        var (icon, effects) = selection.Category switch
        {
            SelectionCategory.Ethics => (_database.Ethic(key)?.Icon, _database.Ethic(key)?.Effects),
            SelectionCategory.Authority => (_database.Authority(key)?.Icon, _database.Authority(key)?.Effects),
            SelectionCategory.Civics or SelectionCategory.Origin =>
                (_database.Civic(key)?.Icon, _database.Civic(key)?.Effects),
            SelectionCategory.Traits => (_database.Trait(key)?.Icon, _database.Trait(key)?.Effects),
            SelectionCategory.PreferredPlanetClass => (_database.PlanetClass(key)?.Icon, null),
            SelectionCategory.AscensionPerk =>
                (_database.AscensionPerk(key)?.Icon, _database.AscensionPerk(key)?.Effects),
            SelectionCategory.TraditionTree => (_database.TraditionTree(key)?.Icon, null),
            _ => (null, null),
        };

        return new EmpireChoice(key, Named(selection), icon, effects);
    }

    /// <summary>
    /// The chip for a plain field, where the game has artwork for one.
    /// </summary>
    /// <remarks>
    /// One field in the whole corpus: <c>is_nomadic</c>, on eighty-six civics. The game draws it as
    /// a toggle beside the authorities wearing <c>GFX_toggle_nomad</c>, so that is what it wears
    /// here. Anything else falls back to words, which is right - a field this does not know is a
    /// field with nothing to draw.
    /// </remarks>
    private EmpireChoice? Field(FieldRequirement field) =>
        string.Equals(field.Field, "is_nomadic", StringComparison.Ordinal)
            ? new EmpireChoice(
                field.Field,
                _localizer.Text("IS_NOMADIC", "Nomadic"),
                _database.Icons.GetValueOrDefault("GFX_toggle_nomad"),
                null)
            : null;

    /// <summary>
    /// What a selection is called.
    /// </summary>
    /// <remarks>
    /// Most of these the game names under their own key. The ones that do not are the ones with no
    /// artwork either - an archetype and a country type are script rather than content - so they
    /// fall through to being made readable, which is what the game itself does with a key it has no
    /// words for. Asked twice, because a key can be present and empty: one civic ships that way and
    /// drew a card with no title until it was caught.
    /// </remarks>
    private string Named(SelectionRequirement selection) =>
        _localizer.Text(selection.Key, Localizer.Prettify(selection.Key)) is { Length: > 0 } named
            ? named
            : Localizer.Prettify(selection.Key);

    /// <summary>A content pack, wearing the badge the pack bar already wears.</summary>
    public EmpireChoice Pack(string name)
    {
        var pack = _database.Dlc.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));

        return new EmpireChoice(name, _localizer.Text(pack?.NameKey, name), pack?.Icon, null);
    }

    /// <summary>
    /// A condition with nothing to draw, said as plainly as it can be.
    /// </summary>
    /// <remarks>
    /// A named check about the design as a whole, a plain field, a count of what a plan holds, and
    /// whatever a future patch introduces that the extractor did not recognise. None of them is a
    /// thing with a picture, and all of them are worth showing rather than silently dropping - a
    /// bullet list missing the one condition that actually blocks you is worse than no list.
    /// </remarks>
    private string Words(Requirement requirement) => requirement switch
    {
        PredicateRequirement predicate => Localizer.Prettify(predicate.Name),

        FieldRequirement field =>
            $"{Localizer.Prettify(field.Field)} is {Localizer.Prettify(field.Value)}",

        CountRequirement count =>
            $"{Localizer.Prettify(count.Of.ToString())} {Compared(count.Comparison)} {count.Value}",

        UnknownRequirement unknown => Localizer.Prettify(unknown.Name),

        _ => Localizer.Prettify(requirement.GetType().Name),
    };

    private static string Compared(CountComparison comparison) => comparison switch
    {
        CountComparison.Above => "more than",
        CountComparison.Below => "fewer than",
        CountComparison.AtLeast => "at least",
        CountComparison.AtMost => "at most",
        _ => "exactly",
    };
}
