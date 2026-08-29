using System.Diagnostics;
using System.Text.Json;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// The database has to survive the trip to the browser. It is written here and read there, so a
/// shape that serialises but does not deserialise would strand the web app on its loading screen
/// with nothing to show for it.
/// </summary>
public sealed class GameDatabaseSerializationTests
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>What the browser reads with: camel-case naming and case-insensitive matching.</summary>
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheWholeDatabaseSurvivesBeingWrittenAndReadBack()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var original = GameDataExtractor.ExtractFrom(InstallRoot!);
        var json = JsonSerializer.SerializeToUtf8Bytes(original, WriteOptions);

        var timer = Stopwatch.StartNew();
        var restored = JsonSerializer.Deserialize<GameDatabase>(json, ReadOptions);
        timer.Stop();

        Assert.NotNull(restored);

        // The browser does this on a single thread while the player waits, so a slow read here is
        // a stalled loading screen there.
        Assert.True(
            timer.Elapsed < TimeSpan.FromSeconds(10),
            $"Reading the database back took {timer.Elapsed.TotalSeconds:F1} seconds.");

        Assert.Equal(original.Ethics.Count, restored!.Ethics.Count);
        Assert.Equal(original.Civics.Count, restored.Civics.Count);
        Assert.Equal(original.Traits.Count, restored.Traits.Count);
        Assert.Equal(original.Portraits.Count, restored.Portraits.Count);
        Assert.Equal(original.GameVersion, restored.GameVersion);
    }

    [Fact]
    public void ConditionsKeepTheirShapeThroughSerialisation()
    {
        // The conditions are a polymorphic tree, which is the part most likely to be lost.
        Requirement original = new AllRequirement([
            new SelectionRequirement(SelectionCategory.Ethics, "ethic_militarist"),
            new NotRequirement(new AnyRequirement([
                new SelectionRequirement(SelectionCategory.Civics, "civic_a"),
                new SelectionRequirement(SelectionCategory.Civics, "civic_b"),
            ]))
            {
                FailureText = "civic_tooltip_not_both",
            },
            new DlcRequirement("Utopia"),
            new FieldRequirement("is_nomadic", "no"),
            new PredicateRequirement(DesignPredicates.IsGestalt),
            new UnknownRequirement("some_future_trigger"),
            new AlwaysRequirement(true),
        ]);

        var json = JsonSerializer.SerializeToUtf8Bytes(original, WriteOptions);
        var restored = JsonSerializer.Deserialize<Requirement>(json, ReadOptions);

        var all = Assert.IsType<AllRequirement>(restored);
        Assert.Equal(7, all.Items.Count);

        var not = Assert.IsType<NotRequirement>(all.Items[1]);
        Assert.Equal("civic_tooltip_not_both", not.FailureText);
        Assert.Equal(2, Assert.IsType<AnyRequirement>(not.Item).Items.Count);

        Assert.Equal("Utopia", Assert.IsType<DlcRequirement>(all.Items[2]).Name);
        Assert.Equal(SelectionCategory.Ethics, Assert.IsType<SelectionRequirement>(all.Items[0]).Category);
    }
}
