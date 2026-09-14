using Sem.Ui.Services;

namespace Sem.Desktop.Tests;

/// <summary>
/// What stands in when no designs file could be found.
/// </summary>
/// <remarks>
/// A player who has installed Stellaris but never launched it has no game data folder, so this is
/// what the window registers. It has to keep enough of the real host's promises that the designer
/// still runs and the failure, when it comes, is a sentence rather than a crash.
/// </remarks>
public sealed class UnavailableFileExchangeTests
{
    /// <summary>Saving says what is wrong and what to do about it, rather than failing quietly.</summary>
    [Fact]
    public async Task SavingSaysThereIsNowhereToSave()
    {
        var files = new UnavailableFileExchange();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => files.SaveAsync("designs.txt", [1, 2, 3]));

        Assert.Contains("Run Stellaris once", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// It remembers unsaved work, so the window asks before closing over it.
    /// </summary>
    /// <remarks>
    /// The close guard used to ask the real host by type, so with this one in place a player could
    /// build an empire and close the window on it without being asked anything. They can still
    /// reach that work through Export, which is what made it a real loss rather than a theoretical
    /// one.
    /// </remarks>
    [Fact]
    public async Task ItRemembersUnsavedWorkSoTheWindowCanAsk()
    {
        var files = new UnavailableFileExchange();

        Assert.False(files.HasUnsavedWork);

        await files.WarnBeforeLeavingAsync(true);

        Assert.True(files.HasUnsavedWork);
    }

    /// <summary>
    /// A shared link still points at the published site.
    /// </summary>
    /// <remarks>
    /// Inherited from the interface this answered null, and the share button then built a link
    /// against the web view's own origin - an address only this process can reach. The designs file
    /// being missing has nothing to do with it.
    /// </remarks>
    [Fact]
    public void ASharedLinkStillPointsAtTheSite()
    {
        Assert.Equal(
            "https://kezyma.github.io/Stellaris-Empire-Manager/",
            new UnavailableFileExchange().ShareBaseUri);
    }

    /// <summary>It still calls the button Save, because that is what it would do if it could.</summary>
    [Fact]
    public void ItStillSavesInPlaceAsFarAsTheHeaderIsConcerned()
    {
        var files = new UnavailableFileExchange();

        Assert.True(files.SavesInPlace);
        Assert.Equal("Save", files.SaveVerb);
    }
}
