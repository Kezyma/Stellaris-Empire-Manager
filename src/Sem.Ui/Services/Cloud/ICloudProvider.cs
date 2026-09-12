namespace Sem.Ui.Services.Cloud;

/// <summary>
/// One file at a provider, as a chooser lists it.
/// </summary>
/// <param name="Id">The provider's own handle for it, which is what everything else is asked by.</param>
/// <param name="Name">The file's name, shown to the player and used when the file is opened.</param>
/// <param name="Folder">Where it sits, for telling two files of the same name apart.</param>
/// <remarks>
/// Named by id rather than by path throughout. A path is not a stable way to find a file at any of
/// these providers - OneDrive's backed-up Documents folder sits under a device name, so the same
/// file is at a different path on the player's other machine - and a rename would break a path that
/// had been remembered.
/// </remarks>
public sealed record CloudFile(string Id, string Name, string Folder);

/// <summary>
/// One row in a folder listing, which may be a folder itself.
/// </summary>
/// <param name="Id">The provider's handle, to descend into or to open.</param>
/// <param name="Name">What to show.</param>
/// <param name="IsFolder">Whether going into it lists more, or choosing it opens a file.</param>
/// <param name="Size">How large a file is; nothing for a folder.</param>
public sealed record CloudEntry(string Id, string Name, bool IsFolder, long Size);

/// <summary>
/// A provider offered in the chooser, and whether it can be picked.
/// </summary>
/// <param name="Name">What it is called.</param>
/// <param name="Ready">Whether choosing it does anything.</param>
/// <param name="Why">
/// A word about its state where there is one to give - that it is the one signed in to, or the
/// reason it cannot be chosen. Null where the name says everything.
/// </param>
public sealed record CloudChoice(string Name, bool Ready, string? Why);

/// <summary>
/// What a provider says about a file without handing over its contents.
/// </summary>
/// <param name="Version">
/// Whatever the provider changes when the file changes, and compares against when told not to
/// overwrite. An ETag for OneDrive; a head revision for Drive.
/// </param>
/// <param name="Modified">When it last changed, for showing rather than for deciding.</param>
/// <param name="Size">How large it is, likewise.</param>
public sealed record CloudStamp(string Version, DateTimeOffset Modified, long Size);

/// <summary>What became of a write.</summary>
public enum CloudWrite
{
    /// <summary>The bytes are in the file.</summary>
    Written,

    /// <summary>Somebody else wrote it first, and nothing was overwritten.</summary>
    Conflicted,

    /// <summary>The provider would not, and said so - signed out, out of space, offline.</summary>
    Refused,
}

/// <summary>
/// Reading and writing one file at a cloud provider.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately small, and deliberately not about signing in: an implementation is handed to
/// <see cref="CloudFileExchange"/> already able to act, and what it took to get there - a token, a
/// popup, a refresh - is its own business and the connection's. This is the part that two providers
/// can share, so it is the part that is an interface.
/// </para>
/// <para>
/// Nothing here throws for the ordinary failures. A file that has gone, a provider that refuses and
/// a session that has expired are all things the player may do or have done to them, and each
/// answers with null or a <see cref="CloudWrite"/> rather than an exception - because the caller is
/// a save button, and a save button has to say something either way.
/// </para>
/// </remarks>
public interface ICloudProvider
{
    /// <summary>What to call this provider in the interface.</summary>
    string Name { get; }

    /// <summary>
    /// Whether there is still a session to act with, without going out to find out.
    /// </summary>
    /// <remarks>
    /// Asked after something has already failed, to tell the two reasons apart. A provider that
    /// refuses a write because the session ended and one that refuses because the account is full
    /// need different things said to the player, and only this can tell which happened.
    /// </remarks>
    Task<bool> SignedInAsync();

    /// <summary>
    /// What is in a folder, so somebody can find their file by looking rather than by guessing.
    /// </summary>
    /// <param name="folderId">The folder to open, or null for the top of the drive.</param>
    /// <param name="cancellationToken">Dropped when the dialog goes.</param>
    Task<IReadOnlyList<CloudEntry>> ListAsync(
        string? folderId, CancellationToken cancellationToken = default);

    /// <summary>Files whose name matches, for somebody who would rather not go looking.</summary>
    Task<IReadOnlyList<CloudFile>> FindAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>The file's contents and the stamp they were read at, or null if it is not there.</summary>
    Task<(byte[] Contents, CloudStamp Stamp)?> ReadAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the file, refusing if it has moved on since the version given.
    /// </summary>
    /// <param name="id">The file to write, as the provider named it.</param>
    /// <param name="contents">What it should hold.</param>
    /// <param name="ifVersion">
    /// The version the caller believes is there. Null writes regardless, which is what a first
    /// write does; anything else is a promise not to overwrite somebody's work unseen.
    /// </param>
    /// <param name="cancellationToken">Dropped when the exchange stops.</param>
    Task<(CloudWrite Outcome, CloudStamp? Stamp)> WriteAsync(
        string id,
        byte[] contents,
        string? ifVersion,
        CancellationToken cancellationToken = default);

    /// <summary>What the file is now, without fetching it. The cheap call, made on a timer.</summary>
    Task<CloudStamp?> StatAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a new file beside an existing one, for the dated copy a save keeps.
    /// </summary>
    /// <remarks>
    /// Beside rather than at a path, for the reason the record above gives: the provider knows
    /// which folder its own file is in, and the caller should not have to.
    /// </remarks>
    Task<bool> WriteBesideAsync(
        CloudFile file,
        string name,
        byte[] contents,
        CancellationToken cancellationToken = default);
}
