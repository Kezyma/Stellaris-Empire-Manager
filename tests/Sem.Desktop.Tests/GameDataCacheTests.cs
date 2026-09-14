using Sem.Core.Tests;
using Sem.Extraction;

namespace Sem.Desktop.Tests;

/// <summary>
/// Whether a launch can use what it extracted last time.
/// </summary>
/// <remarks>
/// <para>
/// The difference between opening in a second and re-reading thirty-five thousand files, and the
/// answer comes back as a sentence shown to the player - so both halves matter: rebuilding when it
/// should not is a minute of somebody's evening, and not rebuilding when it should is a designer
/// quietly offering the wrong game.
/// </para>
/// <para>
/// Every case works against a cache root inside a temp directory. The real one is the machine's
/// application data, and arranging a case there would mean writing into the cache the developer is
/// actually using.
/// </para>
/// </remarks>
public sealed class GameDataCacheTests
{
    /// <summary>An install root that exists, so the fingerprint has something to read.</summary>
    private static GameDataCache Cache(TempDirectory temp) =>
        new(temp.Combine("install"), temp.Combine("cache"));

    /// <summary>Writes a database with the fields under test, and the wardrobe beside it.</summary>
    private static void Extracted(
        GameDataCache cache,
        int? schema = null,
        string? extractor = null,
        string? gameVersion = "v4.5.0",
        string? fingerprint = null,
        bool wardrobe = true,
        string? raw = null)
    {
        Directory.CreateDirectory(cache.Directory);

        var fields = new List<string>
        {
            $"\"schemaVersion\": {schema ?? GameDataExtractor.SchemaVersion}",
            $"\"extractorVersion\": \"{extractor ?? GameDataExtractor.ExtractorVersion}\"",
        };

        if (gameVersion is not null)
        {
            fields.Add($"\"gameVersion\": \"{gameVersion}\"");
        }

        if (fingerprint is not null)
        {
            fields.Add($"\"installFingerprint\": \"{fingerprint}\"");
        }

        File.WriteAllText(cache.DatabasePath, raw ?? "{" + string.Join(",", fields) + "}");

        if (wardrobe)
        {
            File.WriteAllText(
                Path.Combine(cache.Directory, GameDataWriter.WardrobeFileName), "[]");
        }
    }

    /// <summary>Nothing extracted yet says so, rather than failing some later check.</summary>
    [Fact]
    public void AnEmptyCacheIsNotUsableAndSaysWhy()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("not extracted yet", reason);
    }

    /// <summary>A complete, current cache is used, and nothing is said about it.</summary>
    [Fact]
    public void ACurrentCacheIsUsable()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache);

        Assert.True(cache.IsUsable(out var reason));
        Assert.Null(reason);
    }

    /// <summary>A database written to a shape this build no longer reads is rebuilt.</summary>
    [Fact]
    public void AnOlderDataFormatIsRebuilt()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache, schema: GameDataExtractor.SchemaVersion - 1);

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("built to an older data format", reason);
    }

    /// <summary>
    /// And one built by a different extractor, which is the check that was missing entirely.
    /// </summary>
    /// <remarks>
    /// The shape can be unchanged while what was extracted into it is not - new emblems, new
    /// renders, new text. Nothing read this, so a desktop that had extracted once kept that data
    /// through every later change and reported itself perfectly up to date.
    /// </remarks>
    [Fact]
    public void AnOlderExtractorIsRebuilt()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache, extractor: "0.0.1+000000000000");

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("built by an older version of this app", reason);
    }

    /// <summary>A cache from before the ruler's wardrobe was drawn is rebuilt for it.</summary>
    /// <remarks>
    /// Checked as a file rather than believed from the version: such a cache is otherwise perfectly
    /// valid by every other test here and would never be rebuilt.
    /// </remarks>
    [Fact]
    public void ACacheWithNoWardrobeIsRebuilt()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache, wardrobe: false);

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("built before the ruler's wardrobe was drawn", reason);
    }

    /// <summary>A cache that cannot say which game it was built from is old enough to rebuild.</summary>
    [Fact]
    public void ACacheThatCannotSayWhichGameIsRebuilt()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache, gameVersion: null);

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("built before the game version was written down", reason);
    }

    /// <summary>A database that will not parse is rebuilt rather than throwing on launch.</summary>
    [Fact]
    public void AnUnreadableDatabaseIsRebuilt()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache, raw: "{ this is not json");

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("unreadable", reason);
    }

    /// <summary>
    /// A fingerprint that no longer matches the installation rebuilds.
    /// </summary>
    /// <remarks>
    /// For the things a version string never mentions: a hotfix that did not bump it, a repaired
    /// install, a hand-edited define. The install here is a real directory with a file in it, so
    /// the fingerprint has something to measure and comes back with something that is not the
    /// nonsense written into the database.
    /// </remarks>
    [Fact]
    public void AnInstallationThatChangedIsRebuilt()
    {
        using var temp = new TempDirectory();
        var install = temp.Combine("install", "common");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "defines.txt"), "NGameplay = { }");

        var cache = Cache(temp);
        Extracted(cache, fingerprint: "0000000000000000");

        Assert.False(cache.IsUsable(out var reason));
        Assert.Equal("the game files changed", reason);
    }

    /// <summary>
    /// A cache from before the fingerprint was recorded is kept rather than thrown away.
    /// </summary>
    /// <remarks>
    /// Absent is not a mismatch. The game version above has been agreeing with it all along, and
    /// the print will be written on the next rebuild whenever one is wanted for another reason.
    /// </remarks>
    [Fact]
    public void ACacheFromBeforeTheFingerprintIsKept()
    {
        using var temp = new TempDirectory();
        var cache = Cache(temp);

        Extracted(cache, fingerprint: null);

        Assert.True(cache.IsUsable(out var reason));
        Assert.Null(reason);
    }

    /// <summary>Two installations do not share a cache.</summary>
    [Fact]
    public void EachInstallationKeepsItsOwn()
    {
        using var temp = new TempDirectory();

        var one = new GameDataCache(temp.Combine("one"), temp.Combine("cache"));
        var two = new GameDataCache(temp.Combine("two"), temp.Combine("cache"));

        Assert.NotEqual(one.Directory, two.Directory);
    }
}
