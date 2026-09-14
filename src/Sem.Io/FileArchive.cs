namespace Sem.Io;

/// <summary>
/// Keeps copies of what a replacement wrote over, somewhere the game will never look.
/// </summary>
/// <remarks>
/// <para>
/// The second of the two ways back from a save. <see cref="SafeFile.DatedBackupPath"/> puts a copy
/// beside the file, where the player can find it; this puts one in the app's own folder, where
/// nothing else will tidy it away. Both keep the same thing - the bytes that were there a moment
/// ago - and the app's own copy matters most when the dated sibling is switched off, which is what
/// happens while the file is being kept in step and saves happen several times a minute.
/// </para>
/// <para>
/// Here rather than beside the host that calls it, for two reasons. It belongs with
/// <see cref="SafeFile"/>, whose policy it writes through and whose dated backup it complements. And
/// the host that used to own it is a WPF assembly that no test project can reference, which is how
/// it went unnoticed for so long that this archive held copies of the wrong side of every save: the
/// bytes being written rather than the ones being replaced. A copy of the replacement is not a way
/// back from it.
/// </para>
/// </remarks>
/// <param name="file">What the copies are written through, so the policy still applies.</param>
/// <param name="root">The folder to keep them in.</param>
public sealed class FileArchive(SafeFile file, string root)
{
    /// <summary>How many copies of one file are kept before the oldest is dropped.</summary>
    public const int Keeps = 20;

    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));

    /// <summary>The folder the copies are kept in.</summary>
    public string Root { get; } = !string.IsNullOrWhiteSpace(root)
        ? root
        : throw new ArgumentException("An archive needs somewhere to keep things.", nameof(root));

    /// <summary>
    /// Keeps a copy of what a file holds now, before something replaces it.
    /// </summary>
    /// <param name="path">The file about to be replaced, which names the copy.</param>
    /// <param name="moment">When, for a caller that needs to say; otherwise now.</param>
    /// <remarks>
    /// <para>
    /// The file is read here rather than handed in, and that is the whole shape of this class. The
    /// bug it replaces was a caller passing the bytes it was about to write instead of the ones
    /// already there, and no amount of testing <em>this</em> would have caught that - a method that
    /// writes whatever it is given is only ever as right as its caller. Reading the file makes the
    /// wrong thing impossible to ask for.
    /// </para>
    /// <para>
    /// It costs one read of a small file per save, on a path that has just read the same file to
    /// decide whether anything else had written it. That is the price of the guarantee and it is
    /// worth paying.
    /// </para>
    /// <para>
    /// Never throws, and keeps nothing when there is no file yet. An archive is a courtesy the
    /// player did not ask for and cannot see, and a save that failed because the courtesy could not
    /// be paid would be the worst of both: the empires unwritten, and an error naming a folder in
    /// application data that has nothing to do with them.
    /// </para>
    /// </remarks>
    public void KeepReplaced(string path, DateTime? moment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var of = Path.GetFileName(path);
        var kept = Path.Combine(Root, $"{moment ?? DateTime.Now:yyyyMMdd-HHmmss}-{of}");

        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            _file.WriteAllBytes(kept, SafeFile.ReadAllBytes(path));
            Prune(of);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Including the policy's own refusal, which is an IOException, and a folder that cannot
            // be created at all - locked down by a corporate rule, or by whatever else is watching
            // this machine's application data.
        }
    }

    /// <summary>
    /// Drops the oldest copies of one file, keeping <see cref="Keeps"/> of them.
    /// </summary>
    /// <remarks>
    /// By name rather than by creation time. The name carries the moment, sortable, written by the
    /// line above - and Windows will hand back a creation time from a file that used to have the
    /// same name, which is a rule nobody expects and one that would make this keep the wrong twenty.
    /// Per file as well as by name, so one crowded file cannot push out the copies of another.
    /// </remarks>
    private void Prune(string of)
    {
        try
        {
            var stale = new DirectoryInfo(Root)
                .GetFiles($"*-{of}")
                .OrderByDescending(kept => kept.Name, StringComparer.Ordinal)
                .Skip(Keeps);

            foreach (var old in stale)
            {
                old.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that grew past twenty is not worth a word to anybody.
        }
    }
}
