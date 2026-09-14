namespace Sem.Ui.Services;

/// <summary>
/// A stand-in for something the installation has no picture for.
/// </summary>
/// <remarks>
/// Here rather than in the two components that draw one, which had it character for character
/// twice. It is also the kind of thing no test could reach while it lived in a <c>.razor</c> file:
/// every test in this project is service-level, so logic whose only callers are components has no
/// coverage and cannot be given any without a component-testing framework.
/// </remarks>
public static class Initials
{
    /// <summary>
    /// One or two letters standing for a name.
    /// </summary>
    /// <param name="name">What the thing is called.</param>
    /// <returns>
    /// The first letters of the first two words, the first two letters of a single word, or a
    /// question mark for a name with no words in it at all.
    /// </returns>
    public static string Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{words[0][0]}{words[1][0]}".ToUpperInvariant(),
        };
    }
}
