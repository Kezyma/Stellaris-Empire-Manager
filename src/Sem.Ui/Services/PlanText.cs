using Sem.Designs;

namespace Sem.Ui.Services;

/// <summary>
/// Turns a plan into the prose that goes in a biography, and reads one back out.
/// </summary>
/// <remarks>
/// <para>
/// The prose is the storage. The game has no field for a plan and drops anything it does not model,
/// but it does model both biographies - so a plan written as sentences survives, and is shown to the
/// player in game rather than sitting in the file as machine text nobody can read.
/// </para>
/// <para>
/// Read back by name rather than by any hidden marker. That is only safe because names resolve
/// uniquely among the choices one empire could actually make: the two cybernetics trees share a name
/// in every language the game ships, as do the several Galactic Wonders, but no empire can be
/// offered both of any such pair. Resolution therefore goes through what this empire may take, not
/// through a flat list of everything.
/// </para>
/// <para>
/// The labels are fixed words rather than the game's own, which costs a French player a line of
/// English and buys two things. Room: the headings were most of what a third line of civics needed,
/// and the budget below is tight enough in a long language to lose items over it. And recognition:
/// a plan written in a German game is still read as a plan by an English one, where before the
/// heading and the names both had to match and neither did.
/// </para>
/// </remarks>
public sealed class PlanText(Localizer localizer)
{
    /// <summary>
    /// How much prose a biography will hold.
    /// </summary>
    /// <remarks>
    /// The game cuts a biography short and does it without saying so. The only measurement we have
    /// is a real one: a species biography written from a 626-character description came back at 476,
    /// cut mid-phrase. So the budget is set below that with room to spare, and a plan too long for it
    /// drops whole items rather than handing the game something to cut in the middle of a name - a
    /// half-written name is the one input that could be read as the wrong thing.
    /// </remarks>
    public const int Budget = 440;

    /// <summary>
    /// The word a biography starts with when it is a plan.
    /// </summary>
    /// <remarks>
    /// What tells a plan from somebody's writing, and what a plan that has decided nothing consists
    /// of. Both jobs used to be done by a line naming the ascension path, which cost four times as
    /// much and said something the perks below it already said.
    /// </remarks>
    public const string Marker = "Plan";

    private const string TreesLabel = "Traditions";

    private const string PerksLabel = "Perks";

    private const string CivicsLabel = "Civics";

    /// <summary>
    /// What stands between the parts.
    /// </summary>
    /// <remarks>
    /// A line each, which is how it reads best in the box the game shows. It spent a while as a bar
    /// on one line, because a quoted value in the game's format used to end at the first line break
    /// and a plan on four lines came back from its own share link as no design at all - silently,
    /// which is what a link that will not parse looks like. The parser was taught to keep a line
    /// break in the same pass, so that reason is gone.
    /// </remarks>
    public const string Separator = "\n";

    /// <summary>
    /// Writes a plan as the biography that carries it.
    /// </summary>
    /// <remarks>
    /// The marker always, then a line per part that has anything in it.
    /// </remarks>
    public string Write(EmpirePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return Fit(Lines(plan));
    }

    /// <summary>The three parts, named in the player's own language, before anything is dropped.</summary>
    private List<Line> Lines(EmpirePlan plan) =>
    [
        new(TreesLabel, [.. plan.Trees.Select(k => localizer.Text(k))]),
        new(PerksLabel, [.. plan.Perks.Select(k => localizer.Text(k))]),
        new(CivicsLabel, [.. plan.Civics.Select(k => localizer.Text(k))]),
    ];

    /// <summary>
    /// How long the plan would be if nothing were dropped to make it fit.
    /// </summary>
    /// <remarks>
    /// What the meter in the editor reads. Asking <see cref="Write"/> instead gives the length after
    /// shortening, which by construction is never over the budget - so the warning could not fire on
    /// the one occasion it exists for.
    /// </remarks>
    public int Measure(EmpirePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return Render(Lines(plan)).Length;
    }

    /// <summary>
    /// Brings the whole thing inside what a biography will hold, by dropping the last item of
    /// whichever line is longest until it fits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dropping items and not lines, which is the difference between a plan that loses its last
    /// perk and one that loses all eight. It used to remove whole lines from the end, and in a long
    /// language that is what a single character over budget cost.
    /// </para>
    /// <para>
    /// From the end of each line because that is the far future: the eighth perk is the least
    /// certain thing in a plan and the first worth giving up. From the longest line because that is
    /// where the characters are, and it keeps all three parts represented instead of emptying one
    /// to spare another.
    /// </para>
    /// <para>
    /// A name is never cut in half. That is the whole reason this exists rather than letting the
    /// game truncate: a half-written name can read as a different one, and a plan that says the
    /// wrong thing is worse than a plan that says less.
    /// </para>
    /// </remarks>
    private static string Fit(List<Line> lines)
    {
        while (Render(lines).Length > Budget)
        {
            var longest = lines
                .Where(l => l.Names.Count > 0)
                .OrderByDescending(l => l.Length)
                .FirstOrDefault();

            if (longest is null)
            {
                break;
            }

            longest.Names.RemoveAt(longest.Names.Count - 1);
        }

        return Render(lines);
    }

    private static string Render(List<Line> lines) => string.Join(
        Separator,
        new[] { Marker }.Concat(lines.Where(l => l.Names.Count > 0).Select(l => l.ToString())));

    /// <summary>One line of the prose while it is still being shortened to fit.</summary>
    private sealed class Line(string label, List<string> names)
    {
        public List<string> Names { get; } = names;

        public int Length => ToString().Length;

        public override string ToString() => $"{label}: {string.Join(", ", Names)}";
    }

    /// <summary>
    /// Reads a plan out of a biography, or nothing when the text is somebody's actual writing.
    /// </summary>
    /// <remarks>
    /// Null and an empty plan are different answers, which is why this returns one that can be null.
    /// A biography holding nothing but the marker is a plan that has decided nothing yet, and a
    /// biography holding somebody's prose is not a plan at all - and the difference decides whether
    /// their writing is about to be overwritten.
    ///
    /// Everything it cannot make sense of is left out rather than guessed at. A name the empire could
    /// not take, a line whose heading is not one of the three, a tradition from a pack since turned
    /// off - each is dropped, and what is understood still comes back. A partial plan is recoverable;
    /// a wrongly-read one is not.
    /// </remarks>
    public EmpirePlan? Read(string? biography, PlanVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        if (biography is not { Length: > 0 })
        {
            return null;
        }

        List<string> trees = [];
        List<string> perks = [];
        List<string> civics = [];

        // Split on both, because a plan written while this was one line is sitting in somebody's
        // designs file already. Reading one costs nothing.
        var segments = biography.ReplaceLineEndings("\n").Split('\n', '|');

        // The marker has to come first, and used to be accepted anywhere. That was a licence to
        // take somebody's biography: prose with a line reading "Plan" in the middle of it was read
        // as a plan, which ticked a checkbox nobody ticked, turned the box read-only, and replaced
        // what they had written with generated text at the first perk chosen.
        var marked = Same(segments[0].Trim(), Marker);

        foreach (var line in segments)
        {
            var at = line.IndexOf(':', StringComparison.Ordinal);

            if (at < 0)
            {
                continue;
            }

            var heading = line[..at].Trim();
            var written = line[(at + 1)..].Trim();

            if (Same(heading, TreesLabel))
            {
                trees = [.. Names(written).Select(vocabulary.Tree).OfType<string>()];
            }
            else if (Same(heading, PerksLabel))
            {
                perks = [.. Names(written).Select(vocabulary.Perk).OfType<string>()];
            }
            else if (Same(heading, CivicsLabel))
            {
                civics = [.. Names(written).Select(vocabulary.Civic).OfType<string>()];
            }
        }

        // The marker alone decides. A biography that happens to have a line starting "Perks:" but
        // does not open with the word is somebody's own writing about their empire, and reading it
        // as a plan would be a licence to replace it.
        return marked ? new EmpirePlan(trees, perks, civics) : null;
    }

    /// <summary>Whether this biography is a plan rather than something the player wrote.</summary>
    public bool IsPlan(string? biography, PlanVocabulary vocabulary) =>
        Read(biography, vocabulary) is not null;

    private static IEnumerable<string> Names(string written) =>
        written.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0);

    /// <summary>
    /// Whether two pieces of the player's own text mean the same thing.
    /// </summary>
    /// <remarks>
    /// By the current culture rather than ordinally, because both sides are display text in the
    /// player's language, and a reader who typed a name in lower case means the name.
    /// </remarks>
    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.CurrentCultureIgnoreCase);
}
