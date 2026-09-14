namespace Sem.Ui.Services;

/// <summary>
/// What the app needs to ask about the designs file, held until there is somewhere to ask it.
/// </summary>
/// <remarks>
/// <para>
/// The file is opened by the layout, because the way back from a provider's sign-in lands on
/// whichever address the browser was sent to and the code in it has to be spent wherever that is.
/// The questions belong somewhere else: they are about the player's empires, and a reader who came
/// to look up what Meritocracy does did not come for a modal about a file in OneDrive.
/// </para>
/// <para>
/// So the work still happens on every page and the asking waits. Whoever draws the file's own
/// controls draws these too, and until somebody does, a question simply stands - which is the right
/// answer for a question, and better than the two alternatives: asking it over the wiki, or dropping
/// it and quietly not opening a file somebody chose.
/// </para>
/// </remarks>
public sealed class FileQuestions
{
    /// <summary>Raised when something is asked or answered, so whoever draws them redraws.</summary>
    public event Action? Changed;

    private Waiting? _waiting;

    /// <summary>
    /// The file that has arrived and the question about it, or null where nothing is waiting.
    /// </summary>
    public Waiting? Arriving => _waiting;

    /// <summary>Whether a remembered file is still being fetched.</summary>
    /// <remarks>
    /// A please-wait rather than a question, and the one thing here that is dropped rather than
    /// held: what it says is "not yet", and by the time a reader reaches a page that would draw it
    /// the wait is usually over. Saying it late would be saying it about nothing.
    /// </remarks>
    public bool Opening { get; private set; }

    /// <summary>Whether a file was wanted, could not be had, and somebody has to say what to do.</summary>
    public bool Stranded { get; set; }

    /// <summary>Whether the connect-to-a-provider sheet is wanted.</summary>
    public bool Connecting { get; set; }

    /// <summary>Says a fetch is under way, or has finished.</summary>
    /// <param name="opening">Whether it is under way.</param>
    public void Fetching(bool opening)
    {
        Opening = opening;
        Announce();
    }

    /// <summary>Lets whoever draws these know they have changed.</summary>
    /// <remarks>
    /// Public because two of these are set rather than called - a sheet closes by writing false to
    /// the property it was opened by - and a setter that announced its own change would have to be
    /// a method, which reads worse at every one of those sites than this does at four.
    /// </remarks>
    public void Announce() => Changed?.Invoke();

    /// <summary>
    /// Puts a question about an arriving file, and waits for whoever is drawing questions.
    /// </summary>
    /// <param name="mine">How many empires are open now, for the question to weigh against.</param>
    /// <param name="holds">How many the arriving file holds.</param>
    /// <param name="name">What to call it - a file name, or a whole path.</param>
    /// <param name="theirs">The short form, for a sentence: "the file", "OneDrive".</param>
    /// <param name="take">The last button's words: "Take File", "Take Cloud".</param>
    /// <param name="note">What is and is not written, which differs by where the file came from.</param>
    /// <returns>What was chosen, or null where the question was dismissed.</returns>
    public Task<Arrival?> AskAsync(
        int mine, int holds, string name, string theirs, string take, string note)
    {
        var answer = new TaskCompletionSource<Arrival?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _waiting = new Waiting(mine, holds, name, theirs, take, note, answer);
        Announce();

        return answer.Task;
    }

    /// <summary>Takes the question down and lets whoever asked carry on, or stop.</summary>
    /// <param name="choice">What was chosen, or null for a dismissal.</param>
    public void Answer(Arrival? choice)
    {
        var waiting = _waiting?.Answer;

        _waiting = null;
        Announce();

        waiting?.TrySetResult(choice);
    }

    /// <summary>
    /// A file that has arrived, and the promise waiting on what to do about it.
    /// </summary>
    /// <remarks>
    /// The answer is awaited rather than acted on afterwards, because the connection is part-way
    /// through when it asks: it has read the file and has deliberately not touched the session yet,
    /// and the answer is what it does next.
    /// </remarks>
    /// <param name="Mine">How many empires are open now.</param>
    /// <param name="Holds">How many the arriving file holds.</param>
    /// <param name="Name">What to call it in the question.</param>
    /// <param name="Theirs">The short form, for a sentence.</param>
    /// <param name="Take">The last button's words.</param>
    /// <param name="Note">What is and is not written.</param>
    /// <param name="Answer">Completed by whichever answer is pressed, or by dismissal.</param>
    public sealed record Waiting(
        int Mine,
        int Holds,
        string Name,
        string Theirs,
        string Take,
        string Note,
        TaskCompletionSource<Arrival?> Answer);
}
