using System.Text.Json;
using Sem.GameData;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Every modifier the game states, labelled out of the text that actually ships.
/// </summary>
/// <remarks>
/// <para>
/// The hand-written cases beside this prove the formatter's rules against a table built to show
/// them. This proves the rules have something to read, which is a different thing and the one that
/// went wrong: the code to name a shroud patron was written, commented and correct, and the
/// localisation entry it looks the name up in was pruned out of the shipped file. So five separate
/// modifiers all read as "Add Attunement with an Unknown Entity" - in the designer, in every
/// tooltip, and on the wiki - with the fix sitting in the file the whole time.
/// </para>
/// <para>
/// Nothing could catch that from one side. The extractor's tests see a pruned file that contains
/// everything the extractor thought to seed; the formatter's tests see a table written by hand. Only
/// the two together say whether the app can name what it draws.
/// </para>
/// </remarks>
public sealed class ModifierLabelCorpusTests
{
    /// <summary>
    /// No modifier on a civic reads as an unknown entity.
    /// </summary>
    /// <remarks>
    /// The game's own wording until a game has met the patron, and faithful - but a designer is not
    /// a game in progress, and five anonymous rows that differ only in their numbers tell a reader
    /// nothing about which is which.
    /// </remarks>
    [SkippableFact]
    public void NoModifierIsLabelledAsAnUnknownEntity()
    {
        var read = Reading();
        Skip.If(read is null, "Extracted data is missing. Run: dotnet run --project src/Sem.Cli -- extract --web");

        var anonymous = Modifiers(read!)
            .Where(key => read!.Formatter.Label(key).Contains("Unknown Entity", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(anonymous);
    }

    /// <summary>
    /// And the patrons are named, each one differently.
    /// </summary>
    /// <remarks>
    /// The other half of the same question. A label that stopped saying "an Unknown Entity" by
    /// losing the words rather than by replacing them would pass the test above and still tell the
    /// reader nothing, so this asks for the names themselves.
    /// </remarks>
    [SkippableFact]
    public void EachShroudPatronIsNamed()
    {
        var read = Reading();
        Skip.If(read is null, "Extracted data is missing.");

        var formatter = read!.Formatter;

        Assert.Contains("Eater of Worlds", formatter.Label("add_attunement_the_eater_of_worlds"), StringComparison.Ordinal);
        Assert.Contains("Cradle of Souls", formatter.Label("the_cradle_of_souls_attunement_mult"), StringComparison.Ordinal);
        Assert.Contains("Composer of Strands", formatter.Label("add_attunement_the_composer_of_strands"), StringComparison.Ordinal);
        Assert.Contains("Instrument of Desire", formatter.Label("add_attunement_the_instrument_of_desire"), StringComparison.Ordinal);
    }

    /// <summary>Every modifier any civic or origin states, the ones inside a swap included.</summary>
    private static IEnumerable<string> Modifiers(Shipped read) =>
        read.Database.Civics
            .SelectMany(c => c.Effects.Modifiers.Keys
                .Concat(c.Effects.Conditional.SelectMany(x => x.Modifiers.Keys)))
            .Distinct(StringComparer.Ordinal);

    /// <summary>The database and the text as committed, read together the way a host reads them.</summary>
    private sealed record Shipped(GameDatabase Database, ModifierFormatter Formatter);

    /// <summary>
    /// The committed data, through a whole session rather than through a hand-built localiser.
    /// </summary>
    /// <remarks>
    /// A session, because this is a test about two projects agreeing and a shortcut here would
    /// quietly change what is being asked. Handed only the entries, the localiser has no table of
    /// what the game's script falls back to - so <c>[This.GetEaterColor]</c> resolves to nothing at
    /// all rather than to "an Unknown Entity", the label loses the words the swap looks for, and the
    /// test fails for a reason no player would ever meet. What a host builds is what is read here.
    /// </remarks>
    private static Shipped? Reading()
    {
        if (Repository() is not { } root)
        {
            return null;
        }

        var folder = Path.Combine(root, "src", "Sem.Web", "wwwroot", "gamedata");
        var database = Path.Combine(folder, "gamedb.json");
        var text = Path.Combine(folder, "loc", "en.json");

        if (!File.Exists(database) || !File.Exists(text))
        {
            return null;
        }

        var read = JsonSerializer.Deserialize(File.ReadAllBytes(database), GameDataJsonContext.Default.GameDatabase)!;
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(text))!;
        var session = new DesignSession(new Sem.Ui.Services.GameData(read, entries, "assets"));

        return new Shipped(read, session.Modifiers);
    }

    private static string? Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "sandbox")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
