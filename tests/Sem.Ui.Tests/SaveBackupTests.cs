using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Whether a save keeps a copy of the file it writes over, and who gets asked.
/// </summary>
/// <remarks>
/// Two halves that fail in different ways. The setting has to default to keeping one, because the
/// first time anybody thinks about a backup is the time they needed it - and it has to survive the
/// visit, or it is not a setting. The flag has to reach a host that wants it and be harmless to one
/// that does not, which is what the default on the interface is for.
/// </remarks>
public sealed class SaveBackupTests
{
    /// <summary>An unanswered question means keep one.</summary>
    [Fact]
    public void ABackupIsKeptUntilSomebodySaysOtherwise() =>
        Assert.True(new Preferences().KeepsBackup);

    /// <summary>And the answer is remembered, either way round.</summary>
    [Fact]
    public void TheAnswerIsRemembered()
    {
        var preferences = new Preferences();

        preferences.SetKeepsBackup(false);
        Assert.False(preferences.KeepsBackup);

        preferences.SetKeepsBackup(true);
        Assert.True(preferences.KeepsBackup);
    }

    /// <summary>A host that cares is handed the answer.</summary>
    [Fact]
    public async Task AHostThatKeepsBackupsIsToldWhetherToKeepThisOne()
    {
        var files = new BackingUpHost();

        await ((IFileExchange)files).SaveAsync("designs.txt", [1], backUp: false);

        Assert.Equal(false, files.LastAsked);
    }

    /// <summary>
    /// And a host that does not still saves.
    /// </summary>
    /// <remarks>
    /// The browser implements only the two-argument call, and the third argument has nothing to
    /// mean there: a save dialog writes where the player pointed it and replaces nothing they did
    /// not name. The default on the interface is what keeps that from being a missing method.
    /// </remarks>
    [Fact]
    public async Task AHostWithNothingToBackUpSavesAnyway()
    {
        var files = new PlainHost();

        var outcome = await ((IFileExchange)files).SaveAsync("designs.txt", [1], backUp: true);

        Assert.Equal(SaveOutcome.Saved, outcome);
        Assert.Equal(1, files.Saves);
    }

    private sealed class BackingUpHost : IFileExchange
    {
        public bool? LastAsked { get; private set; }

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
            SaveAsync(fileName, contents, backUp: true);

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp)
        {
            LastAsked = backUp;

            return Task.FromResult(SaveOutcome.Saved);
        }
    }

    private sealed class PlainHost : IFileExchange
    {
        public int Saves { get; private set; }

        public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents)
        {
            Saves++;

            return Task.FromResult(SaveOutcome.Saved);
        }
    }
}
