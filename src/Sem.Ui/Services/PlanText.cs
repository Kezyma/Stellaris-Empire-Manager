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
/// The labels carry the meaning of each line, so a name that could be read two ways is settled by
/// which line it is on. Two of the three are the game's own words, which means they are already
/// translated; the third has no key in the game and keeps ours.
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
    /// drops whole items from the end rather than handing the game something to cut in the middle of
    /// a name - a half-written name is the one input that could be read as the wrong thing.
    /// </remarks>
    public const int Budget = 440;

    /// <summary>The game has no word for this one, so it keeps ours.</summary>
    private const string PathFallback = "Ascension path";

    private string PathLabel => localizer.Heading("SEM_ASCENSION_PATH", PathFallback);

    private string TreesLabel => localizer.Heading("TRADITIONS", "Traditions");

    private string PerksLabel => localizer.Heading("ASCENSION_PERKS", "Ascension Perks");

    /// <summary>What one of the game's ascension paths is called, in the player's own language.</summary>
    public string PathName(PlanPath path) => path switch
    {
        PlanPath.Unset => localizer.Heading("SEM_PLAN_UNSET", "Not decided"),
        PlanPath.None => localizer.Heading("SEM_PLAN_NO_PATH", "No ascension"),
        _ => PlanPaths.NameKey(path) is { } key ? localizer.Text(key) : path.ToString(),
    };

    /// <summary>
    /// Writes a plan as the biography that carries it.
    /// </summary>
    /// <remarks>
    /// A line per part that has anything in it, and the path line always - which is what a plan
    /// still being made has instead of nothing, and what lets it be read back before its first perk
    /// is chosen.
    /// </remarks>
    public string Write(EmpirePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Always, even when nothing is decided. The path is worked out from the perks, so a plan
        // being built has no path until the first one is chosen - and without this line there would
        // be nothing in the biography to read the plan back from, so it would look like an empire
        // nobody was planning at all.
        var lines = new List<string> { $"{PathLabel}: {PathName(plan.Path)}" };

        if (plan.Trees.Count > 0)
        {
            lines.Add($"{TreesLabel}: {string.Join(", ", plan.Trees.Select(t => localizer.Text(t)))}");
        }

        if (plan.Perks.Count > 0)
        {
            lines.Add($"{PerksLabel}: {string.Join(", ", plan.Perks.Select(p => localizer.Text(p)))}");
        }

        return Fit(lines);
    }

    /// <summary>
    /// Brings the whole thing inside what a biography will hold, by dropping from the end.
    /// </summary>
    /// <remarks>
    /// The perks line goes first, being both the longest and the most granular: a plan that has lost
    /// its perks still says which path and which trees, which is the shape of the decision. Dropping
    /// a whole line is deliberate - the alternative is letting the game cut wherever it likes, which
    /// would leave a name half-written and readable as a different one.
    /// </remarks>
    private static string Fit(List<string> lines)
    {
        while (lines.Count > 0 && string.Join('\n', lines).Length > Budget)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Reads a plan out of a biography, or nothing when the text is somebody's actual writing.
    /// </summary>
    /// <remarks>
    /// Null and an empty plan are different answers, which is why this returns one that can be null.
    /// A biography holding "Ascension path: Not decided" is a plan that has decided nothing yet, and
    /// a biography holding somebody's prose is not a plan at all - and the difference decides whether
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

        var path = PlanPath.Unset;
        List<string> trees = [];
        List<string> perks = [];
        var understood = false;

        foreach (var line in biography.ReplaceLineEndings("\n").Split('\n'))
        {
            var at = line.IndexOf(':', StringComparison.Ordinal);

            if (at < 0)
            {
                continue;
            }

            var heading = line[..at].Trim();
            var written = line[(at + 1)..].Trim();

            if (Same(heading, PathLabel) || Same(heading, PathFallback))
            {
                // Only a path this recognises counts. A line reading "Ascension path: something
                // nobody has heard of" is somebody's own writing that happens to start with those
                // words, and reading it as a plan would replace what they wrote.
                if (ReadPath(written) is { } found)
                {
                    path = found;
                    understood = true;
                }
            }
            else if (Same(heading, TreesLabel))
            {
                trees = [.. Names(written).Select(vocabulary.Tree).OfType<string>()];
                understood |= trees.Count > 0;
            }
            else if (Same(heading, PerksLabel))
            {
                perks = [.. Names(written).Select(vocabulary.Perk).OfType<string>()];
                understood |= perks.Count > 0;
            }
        }

        return understood ? new EmpirePlan(path, trees, perks) : null;
    }

    /// <summary>Whether this biography is a plan rather than something the player wrote.</summary>
    public bool IsPlan(string? biography, PlanVocabulary vocabulary) =>
        Read(biography, vocabulary) is not null;

    /// <summary>
    /// The path a line names, or nothing when it names none of them.
    /// </summary>
    /// <remarks>
    /// "Not decided" is one of the answers rather than the absence of one: it is what a plan says
    /// while its perks are still being chosen, and recognising it is what keeps such a plan readable.
    /// </remarks>
    private PlanPath? ReadPath(string written)
    {
        foreach (var candidate in PlanPaths.All)
        {
            if (Same(written, PathName(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

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
