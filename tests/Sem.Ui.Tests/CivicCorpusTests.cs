using System.Text.Json;
using Sem.Designs;
using Sem.GameData;
using Sem.Rules;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the rules beside this actually come to, read against every civic the game ships.
/// </summary>
/// <remarks>
/// <para>
/// The hand-written cases in <see cref="CivicReachTests"/> prove each rule on a shape built to show
/// it. This proves there is no fourth shape nobody thought of, which for a walk over compiled script
/// is the failure that matters: every condition here was written by somebody at Paradox, and the
/// ones that break a reader's model are the ones nobody would invent.
/// </para>
/// <para>
/// It reads the extracted database committed to the repository rather than the installation, so it
/// runs on a machine with no game on it - the same way <see cref="RulerNameCorpusTests"/> reads the
/// extracted text.
/// </para>
/// </remarks>
public sealed class CivicCorpusTests
{
    /// <summary>
    /// The count of civics and origins no player can reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fifty-three and nineteen. <c>docs/hidden-content.md</c> records forty-eight and sixteen from
    /// an in-game survey, and the seven this finds on top of that are all ones that survey had no
    /// reason to look for: it was checking for content gated on being a fallen empire or a primitive,
    /// and these are gated on already holding something the designer cannot give you. Every one is
    /// named in the test below, and the game hides all of them - which is the next test.
    /// </para>
    /// <para>
    /// A change in either direction is worth stopping for. Too many and something reachable has been
    /// hidden from the wiki; too few and a civic nobody can take is shown as though they could.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public void TheOnesNoPlayerCanReachAreTheOnesTheGameNeverOffers()
    {
        var database = Database();
        Skip.If(database is null, "Extracted data is missing. Run: dotnet run --project src/Sem.Cli -- extract --web");

        var reach = CivicReach.Across(database!.Civics);
        var closed = database.Civics.Where(c => !reach[c.Key].EverOffered).ToList();

        Assert.Equal(53, closed.Count(c => !c.IsOrigin));
        Assert.Equal(19, closed.Count(c => c.IsOrigin));
    }

    /// <summary>
    /// A closed entry names a kind of country only where that is what closed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Forty-seven civics are shut out by a <c>country_type</c> and say so, which is what lets a card
    /// read "only for fallen empires" rather than only "you cannot have this".
    /// </para>
    /// <para>
    /// The other twenty-five say nothing, and must. Nineteen origins are refused outright with no
    /// country mentioned at all. The six civics are the Khan's and the Emperor's, every one of them
    /// granted by an event: three require themselves, and <c>civic_diadochi</c> and
    /// <c>civic_great_khans_legacy</c> require the Khan's Vision, which is itself out of reach.
    /// </para>
    /// <para>
    /// <c>civic_great_khans_vision</c> is why the country types are worked out by asking a second
    /// time with the country forgotten rather than simply collected. Its condition permits an
    /// ordinary empire's country type and then requires the civic itself, so a card that read the
    /// country clause would announce it as belonging to awakened marauders - when what shuts the
    /// player out is the event.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public void AClosedEntryNamesAKindOfCountryOnlyWhereThatIsWhatClosedIt()
    {
        var database = Database();
        Skip.If(database is null, "Extracted data is missing.");

        var reach = CivicReach.Across(database!.Civics);

        var closed = database.Civics
            .Select(c => (c.Key, c.IsOrigin, Reach: reach[c.Key]))
            .Where(c => !c.Reach.EverOffered)
            .ToList();

        Assert.Equal(47, closed.Count(c => c.Reach.CountryTypes.Count > 0));

        Assert.Equal(
            [
                "civic_diadochi",
                "civic_galactic_sovereign",
                "civic_galactic_sovereign_megacorp",
                "civic_great_khans_legacy",
                "civic_great_khans_vision",
                "civic_psionic_sovereign",
            ],
            closed.Where(c => !c.IsOrigin && c.Reach.CountryTypes.Count == 0)
                .Select(c => c.Key)
                .Order(StringComparer.Ordinal));

        Assert.All(closed.Where(c => c.IsOrigin), c => Assert.Empty(c.Reach.CountryTypes));
    }

    /// <summary>
    /// Nothing the designer offers today is called out of reach.
    /// </summary>
    /// <remarks>
    /// The strongest check of the three, and the one that would have caught the flat walk: the
    /// designer's own list of civics is built by the rules engine against a blank empire, so
    /// everything in it is demonstrably reachable. Anything this called closed that the designer
    /// offers would be a civic the wiki hides and the editor lets you pick.
    /// </remarks>
    [SkippableFact]
    public void NothingTheDesignerOffersIsCalledOutOfReach()
    {
        var database = Database();
        Skip.If(database is null, "Extracted data is missing.");

        var reach = CivicReach.Across(database!.Civics);

        var rules = new EmpireRules(database);
        var blank = rules.CreateContext(
            EmpireDesignsFile.CreateEmpty().Add("scratch"),
            database.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal));

        var contradicted = rules.GetCivicOptions(blank)
            .Concat(rules.GetOriginOptions(blank))
            .Where(o => o.Visible)
            .Select(o => o.Key)
            .Where(key => !reach[key].EverOffered);

        Assert.Empty(contradicted);
    }

    /// <summary>
    /// The packs read off a civic are packs the game actually ships.
    /// </summary>
    /// <remarks>
    /// A name misread would filter to nothing and look like a civic behind no pack at all, which is
    /// indistinguishable on screen from one that is free.
    /// </remarks>
    [SkippableFact]
    public void EveryPackACivicNamesIsOneTheGameShips()
    {
        var database = Database();
        Skip.If(database is null, "Extracted data is missing.");

        var shipped = database!.Dlc.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        var unknown = database.Civics
            .SelectMany(c => ContentPacks.Named(c.Playable))
            .Distinct(StringComparer.Ordinal)
            .Where(name => !shipped.Contains(name));

        Assert.Empty(unknown);
    }

    /// <summary>The extracted database as committed, or nothing where it has not been built.</summary>
    private static GameDatabase? Database()
    {
        if (Repository() is not { } root)
        {
            return null;
        }

        var path = Path.Combine(root, "src", "Sem.Web", "wwwroot", "gamedata", "gamedb.json");

        return File.Exists(path)
            ? JsonSerializer.Deserialize(File.ReadAllBytes(path), GameDataJsonContext.Default.GameDatabase)
            : null;
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
