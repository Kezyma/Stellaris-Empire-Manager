using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sem.Io;

/// <summary>
/// A short print of the game files an extraction read, for noticing that they have changed.
/// </summary>
/// <remarks>
/// <para>
/// The version in <c>launcher-settings.json</c> answers "has the game been patched", which is most
/// of it and not all of it: a hotfix that does not move the version, a verified-files repair, a
/// hand-edited define, and later a mod added or removed all change what extraction would produce
/// and leave that string exactly where it was.
/// </para>
/// <para>
/// Metadata rather than contents. The folders below hold nearly twenty gigabytes; hashing that on
/// every launch would cost more than the rebuild it is meant to avoid. Names, sizes and write times
/// are one directory walk - about a second for thirty-five thousand files - and they move whenever
/// anything is written, which is the question being asked.
/// </para>
/// <para>
/// It errs towards rebuilding. Steam repairing a file it decides is damaged, or restoring one from
/// a backup, moves a write time without changing a byte, and the cost of that is one rebuild
/// nobody needed. The other way round - a change that went unnoticed - costs the player a designer
/// quietly offering a version of the game they are not running.
/// </para>
/// <para>
/// One edit it cannot see, said plainly: a file rewritten to exactly the same length inside the
/// same filesystem timestamp tick moves neither the size nor the write time, and prints the same.
/// Reaching that takes a change of identical length applied within a few milliseconds of the last
/// one - which a patch does not do, because a patch writes a real time, and a person does not do,
/// because a person is not that fast. Closing it would mean reading twenty gigabytes on every
/// launch to catch something nothing has been observed to do.
/// </para>
/// </remarks>
public static class InstallFingerprint
{
    /// <summary>
    /// The folders whose contents decide what an extraction produces.
    /// </summary>
    /// <remarks>
    /// Wider than the game's own <c>checksum_manifest.txt</c>, deliberately. That one names script
    /// only - <c>common</c>, <c>events</c> and <c>map</c> - because a checksum for multiplayer is
    /// about rules agreeing, and two players may differ on artwork without disagreeing about
    /// anything. This app draws the artwork, so the folders it is drawn from count too.
    /// </remarks>
    private static readonly string[] Folders =
    [
        "common",
        "events",
        "map",
        "gfx",
        "flags",
        "interface",
        "localisation",
        "prescripted_countries",
    ];

    /// <summary>
    /// Prints what these installation roots hold, or null when they cannot be read.
    /// </summary>
    /// <param name="roots">Every layer with files on disk, lowest first. Order does not matter.</param>
    /// <returns>A short hex print, or null where the walk failed.</returns>
    /// <remarks>
    /// Null is "cannot say" and not "nothing changed", so a caller must decide what to do about it
    /// rather than read it as agreement.
    /// </remarks>
    public static string? Of(params string[] roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        try
        {
            var entries = new List<string>(capacity: 1 << 15);

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                foreach (var folder in Folders)
                {
                    Gather(root, folder, entries);
                }
            }

            // Sorted, because a directory walk makes no promise about order and two walks of the
            // same unchanged folder that disagreed about it would print differently every launch.
            entries.Sort(StringComparer.Ordinal);

            using var print = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            foreach (var entry in entries)
            {
                print.AppendData(Encoding.UTF8.GetBytes(entry));
            }

            return Convert.ToHexString(print.GetHashAndReset())[..16].ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Adds one folder's files to the list, as name, size and write time.</summary>
    private static void Gather(string root, string folder, List<string> entries)
    {
        var directory = new DirectoryInfo(Path.Combine(root, folder));

        if (!directory.Exists)
        {
            return;
        }

        // EnumerateFiles rather than a path walk with a stat on each: the enumeration already
        // carries the length and the write time, so this is one pass over the directory data.
        foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var relative = file.FullName[(directory.FullName.Length + 1)..].Replace('\\', '/');

            entries.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{folder}/{relative}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"));
        }
    }
}
