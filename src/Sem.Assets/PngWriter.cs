using SkiaSharp;

namespace Sem.Assets;

/// <summary>Turns decoded textures into PNG files the designer can display.</summary>
public static class PngWriter
{
    /// <summary>
    /// Encodes an image as PNG, optionally scaling it down so neither side exceeds a limit.
    /// </summary>
    /// <remarks>
    /// Scaling is only ever downward. Room backgrounds are nearly a megapixel each and the picker
    /// shows them at a third of that, so shipping them at full size would cost far more bandwidth
    /// than it buys in sharpness.
    /// </remarks>
    public static byte[] Encode(DdsImage image, int? maxDimension = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        using var bitmap = new SKBitmap();
        if (!bitmap.InstallPixels(info, PinPixels(image.Pixels, out var release), info.RowBytes, release))
        {
            throw new InvalidOperationException("Could not build a bitmap from the decoded texture.");
        }

        var scaled = Resize(bitmap, maxDimension);

        try
        {
            using var encoded = scaled.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("PNG encoding failed.");

            return encoded.ToArray();
        }
        finally
        {
            if (!ReferenceEquals(scaled, bitmap))
            {
                scaled.Dispose();
            }
        }
    }

    private static SKBitmap Resize(SKBitmap bitmap, int? maxDimension)
    {
        if (maxDimension is not { } limit || (bitmap.Width <= limit && bitmap.Height <= limit))
        {
            return bitmap;
        }

        var scale = (double)limit / Math.Max(bitmap.Width, bitmap.Height);
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        return bitmap.Resize(new SKSizeI(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? bitmap;
    }

    private static nint PinPixels(byte[] pixels, out SKBitmapReleaseDelegate release)
    {
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        release = (_, _) => handle.Free();
        return handle.AddrOfPinnedObject();
    }
}
