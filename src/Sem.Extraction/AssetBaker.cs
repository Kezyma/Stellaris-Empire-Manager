using Sem.Assets;
using Sem.Extraction.Extractors;
using Sem.Io;

namespace Sem.Extraction;

/// <summary>What came of converting the images an extraction referred to.</summary>
/// <param name="Written">How many images were produced.</param>
/// <param name="Bytes">Their total size.</param>
/// <param name="Failures">Images that could not be converted, with the reason.</param>
/// <param name="ByFolder">
/// Counts and sizes per output folder. The web build has to fetch all of this, so it is worth
/// seeing where the weight is.
/// </param>
public sealed record BakeReport(
    int Written,
    long Bytes,
    IReadOnlyList<string> Failures,
    IReadOnlyList<FolderSize> ByFolder);

/// <summary>How much one folder of extracted images weighs.</summary>
/// <param name="Folder">The folder, relative to the assets root.</param>
/// <param name="Files">How many images it holds.</param>
/// <param name="Bytes">Their total size.</param>
public sealed record FolderSize(string Folder, int Files, long Bytes);

/// <summary>
/// Converts the game's textures into PNG files the designer can display.
/// </summary>
/// <remarks>
/// A texture that fails to convert is recorded and skipped rather than thrown, because one odd
/// icon should not cost the player every other one. The database already points at where each
/// image will be, so a missing file simply shows as a gap.
/// </remarks>
public sealed class AssetBaker(LayeredContent content, SafeFile file)
{
    private readonly LayeredContent _content = content ?? throw new ArgumentNullException(nameof(content));
    private readonly SafeFile _file = file ?? throw new ArgumentNullException(nameof(file));

    /// <summary>Converts every image in a catalogue into the output directory.</summary>
    public BakeReport Bake(
        AssetCatalog catalog,
        string outputDirectory,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var written = 0;
        long bytes = 0;
        var failures = new List<string>();
        var folders = new Dictionary<string, (int Files, long Bytes)>(StringComparer.Ordinal);
        var requests = catalog.Requests.ToList();

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];

            if (i % 250 == 0)
            {
                progress?.Report($"Converting images ({i} of {requests.Count})");
            }

            try
            {
                var image = DdsReader.Read(_content.Read(request.Source));

                if (request.Frame is { } frame)
                {
                    image = DdsImageOps.Frame(image, frame.Frame, frame.FrameCount);
                }

                if (request.Channel is { } channel)
                {
                    image = DdsImageOps.AlphaFromChannel(image, channel);
                }

                var png = PngWriter.Encode(image, request.MaxDimension);

                _file.WriteAllBytes(Path.Combine(outputDirectory, request.Destination), png);
                written++;
                bytes += png.Length;

                var folder = request.Destination.Replace('\\', '/').Split('/')[0];
                var current = folders.GetValueOrDefault(folder);
                folders[folder] = (current.Files + 1, current.Bytes + png.Length);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                failures.Add($"{request.Source}: {ex.Message}");
            }
        }

        var composites = catalog.Composites.ToList();

        if (composites.Count > 0)
        {
            progress?.Report($"Stacking layered icons ({composites.Count})");
        }

        foreach (var request in composites)
        {
            try
            {
                DdsImage? stacked = null;

                foreach (var layer in request.Layers)
                {
                    var image = DdsReader.Read(_content.Read(layer.Source));

                    if (layer.Frame is { } frame)
                    {
                        image = DdsImageOps.Frame(image, frame.Frame, frame.FrameCount);
                    }

                    if (layer.Tint is { } tint)
                    {
                        image = DdsImageOps.Tint(image, tint);
                    }

                    stacked = stacked is null ? image : DdsImageOps.Over(stacked, image);
                }

                if (stacked is null)
                {
                    continue;
                }

                var png = PngWriter.Encode(stacked);

                _file.WriteAllBytes(Path.Combine(outputDirectory, request.Destination), png);
                written++;
                bytes += png.Length;

                var folder = request.Destination.Replace('\\', '/').Split('/')[0];
                var current = folders.GetValueOrDefault(folder);
                folders[folder] = (current.Files + 1, current.Bytes + png.Length);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
            {
                failures.Add($"{request.Destination}: {ex.Message}");
            }
        }

        return new BakeReport(
            written,
            bytes,
            failures,
            [.. folders.OrderByDescending(f => f.Value.Bytes)
                .Select(f => new FolderSize(f.Key, f.Value.Files, f.Value.Bytes))]);
    }

}
