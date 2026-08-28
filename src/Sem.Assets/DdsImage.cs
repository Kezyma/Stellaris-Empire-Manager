using System.Buffers.Binary;

namespace Sem.Assets;

/// <summary>
/// A decoded DDS texture as straight 32-bit pixels.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Pixels">
/// Pixel data, four bytes each in blue, green, red, alpha order, which is what both SkiaSharp and
/// a browser canvas expect on a little-endian machine.
/// </param>
public sealed record DdsImage(int Width, int Height, byte[] Pixels)
{
    /// <summary>The colour at a pixel.</summary>
    public (byte B, byte G, byte R, byte A) this[int x, int y]
    {
        get
        {
            var offset = ((y * Width) + x) * 4;
            return (Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
        }
    }
}

/// <summary>
/// Reads the DDS textures Stellaris ships.
/// </summary>
/// <remarks>
/// Only what the game actually uses is supported, which is less than the format allows: across the
/// fourteen thousand textures in an installation, none carry the extended header, so the classic
/// 124-byte one is the only case. Roughly three quarters are uncompressed 32-bit, most of the rest
/// are DXT5, and a few are DXT1, DXT3 or 24-bit without alpha.
/// </remarks>
public static class DdsReader
{
    private const uint Magic = 0x20534444; // "DDS "
    private const int HeaderSize = 124;
    private const int PixelDataOffset = 128;

    private const uint FourCcDxt1 = 0x31545844;
    private const uint FourCcDxt3 = 0x33545844;
    private const uint FourCcDxt5 = 0x35545844;
    private const uint FourCcDx10 = 0x30315844;

    private const uint FlagsFourCc = 0x4;
    private const uint FlagsRgb = 0x40;
    private const uint FlagsAlphaPixels = 0x1;

    /// <summary>Whether the bytes begin with a DDS header this can read.</summary>
    public static bool IsDds(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= PixelDataOffset && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == Magic;

    /// <summary>Decodes a texture's top mip level.</summary>
    public static DdsImage Read(ReadOnlySpan<byte> bytes)
    {
        if (!IsDds(bytes))
        {
            throw new InvalidDataException("Not a DDS file.");
        }

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (headerSize != HeaderSize)
        {
            throw new InvalidDataException($"Unexpected DDS header size {headerSize}.");
        }

        var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
        var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"DDS reports a {width} by {height} image.");
        }

        // The pixel format sits at offset 76; its flags and FourCC decide everything below.
        var pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[80..]);
        var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(bytes[84..]);
        var data = bytes[PixelDataOffset..];

        if ((pixelFlags & FlagsFourCc) != 0)
        {
            if (fourCc == FourCcDx10)
            {
                throw new NotSupportedException(
                    "DDS files with an extended header are not supported; Stellaris ships none.");
            }

            return DecodeBlockCompressed(fourCc, width, height, data);
        }

        if ((pixelFlags & FlagsRgb) == 0)
        {
            throw new NotSupportedException($"Unsupported DDS pixel format flags 0x{pixelFlags:X}.");
        }

        return DecodeUncompressed(bytes, width, height, data, (pixelFlags & FlagsAlphaPixels) != 0);
    }

    private static DdsImage DecodeBlockCompressed(uint fourCc, int width, int height, ReadOnlySpan<byte> data)
    {
        var format = fourCc switch
        {
            FourCcDxt1 => BCnEncoder.Shared.CompressionFormat.Bc1WithAlpha,
            FourCcDxt3 => BCnEncoder.Shared.CompressionFormat.Bc2,
            FourCcDxt5 => BCnEncoder.Shared.CompressionFormat.Bc3,
            _ => throw new NotSupportedException($"Unsupported DDS compression '{FourCcName(fourCc)}'."),
        };

        var decoder = new BCnEncoder.Decoder.BcDecoder();
        var colors = decoder.DecodeRaw(data.ToArray(), width, height, format);

        var pixels = new byte[width * height * 4];
        for (var i = 0; i < colors.Length && i < width * height; i++)
        {
            var color = colors[i];
            var offset = i * 4;
            pixels[offset] = color.b;
            pixels[offset + 1] = color.g;
            pixels[offset + 2] = color.r;
            pixels[offset + 3] = color.a;
        }

        return new DdsImage(width, height, pixels);
    }

    private static DdsImage DecodeUncompressed(
        ReadOnlySpan<byte> file,
        int width,
        int height,
        ReadOnlySpan<byte> data,
        bool hasAlpha)
    {
        var bitCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(file[88..]);
        var redMask = BinaryPrimitives.ReadUInt32LittleEndian(file[92..]);
        var greenMask = BinaryPrimitives.ReadUInt32LittleEndian(file[96..]);
        var blueMask = BinaryPrimitives.ReadUInt32LittleEndian(file[100..]);
        var alphaMask = BinaryPrimitives.ReadUInt32LittleEndian(file[104..]);

        var bytesPerPixel = bitCount / 8;
        if (bytesPerPixel is < 2 or > 4)
        {
            throw new NotSupportedException($"Unsupported DDS bit depth {bitCount}.");
        }

        var required = (long)width * height * bytesPerPixel;
        if (data.Length < required)
        {
            throw new InvalidDataException(
                $"DDS is truncated: {data.Length} bytes of pixel data for a {width} by {height} image.");
        }

        var red = new ChannelMask(redMask);
        var green = new ChannelMask(greenMask);
        var blue = new ChannelMask(blueMask);
        // A zero-width mask yields nothing, which is how a channel that is simply not there is
        // distinguished from one that happens to be zero.
        var alpha = new ChannelMask(hasAlpha ? alphaMask : 0);

        var pixels = new byte[width * height * 4];

        for (var i = 0; i < width * height; i++)
        {
            var source = i * bytesPerPixel;

            var value = bytesPerPixel switch
            {
                4 => BinaryPrimitives.ReadUInt32LittleEndian(data[source..]),
                3 => (uint)(data[source] | (data[source + 1] << 8) | (data[source + 2] << 16)),
                _ => BinaryPrimitives.ReadUInt16LittleEndian(data[source..]),
            };

            var offset = i * 4;
            pixels[offset] = blue.Extract(value);
            pixels[offset + 1] = green.Extract(value);
            pixels[offset + 2] = red.Extract(value);

            // Flag backgrounds are 24-bit with no alpha channel at all, and must come out opaque
            // rather than invisible.
            pixels[offset + 3] = alpha.HasChannel ? alpha.Extract(value) : (byte)255;
        }

        return new DdsImage(width, height, pixels);
    }

    private static string FourCcName(uint fourCc) =>
        new([(char)(fourCc & 0xFF), (char)((fourCc >> 8) & 0xFF), (char)((fourCc >> 16) & 0xFF), (char)(fourCc >> 24)]);

    /// <summary>
    /// One colour channel's position and width within a packed pixel, scaled up to eight bits.
    /// </summary>
    private readonly struct ChannelMask
    {
        private readonly uint _mask;
        private readonly int _shift;
        private readonly int _bits;

        public ChannelMask(uint mask)
        {
            _mask = mask;

            if (mask == 0)
            {
                _shift = 0;
                _bits = 0;
                return;
            }

            _shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
            _bits = System.Numerics.BitOperations.PopCount(mask);
        }

        /// <summary>False when the pixel format has no such channel.</summary>
        public bool HasChannel => _bits > 0;

        public byte Extract(uint value)
        {
            if (_bits == 0)
            {
                return 0;
            }

            var raw = (value & _mask) >> _shift;

            // Widen a narrow channel so that its maximum becomes 255 rather than, say, 31.
            var max = (1u << _bits) - 1;
            return (byte)(raw * 255 / max);
        }
    }
}
