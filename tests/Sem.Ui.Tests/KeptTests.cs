using System.Text;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// How a designs file survives being kept in a browser's store, which takes text and not bytes.
/// </summary>
/// <remarks>
/// The file used to go in through <c>Encoding.UTF8.GetString</c> and come back out as text, which is
/// not a round trip for either of the two encodings the parser accepts. A byte-order mark survived
/// as a character the lexer read as a stray token; bytes that are not valid UTF-8 came back as the
/// replacement character and were gone for good. Both cases are the player's own file.
/// </remarks>
public sealed class KeptTests
{
    private static byte[] Decode(string kept) =>
        typeof(SessionHost).Assembly
            .GetType("Sem.Ui.Services.Kept")!
            .GetMethod("TryDecode")!
            .Invoke(null, [kept]) as byte[]
        ?? throw new InvalidOperationException("The stored value was not read back as bytes.");

    private static string Encode(byte[] contents) =>
        (string)typeof(SessionHost).Assembly
            .GetType("Sem.Ui.Services.Kept")!
            .GetMethod("Encode")!
            .Invoke(null, [contents])!;

    [Fact]
    public void AFileComesBackAsTheBytesItWentInAs()
    {
        var contents = Encoding.UTF8.GetBytes("\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n}\r\n");

        Assert.Equal(contents, Decode(Encode(contents)));
    }

    /// <summary>
    /// A mark at the start is part of the file and not part of the text. Kept as a character it
    /// reappeared ahead of the first empire, and the lexer read it as a token of its own.
    /// </summary>
    [Fact]
    public void AByteOrderMarkSurvivesRatherThanBecomingACharacter()
    {
        var contents = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("\"Empire\"=\r\n{\r\n}\r\n"))
            .ToArray();

        Assert.Equal(contents, Decode(Encode(contents)));
    }

    /// <summary>
    /// The parser falls back to Latin-1 for a file that is not valid UTF-8, so those bytes reach the
    /// store. Read as UTF-8 every one of them became U+FFFD, which is the player's file destroyed.
    /// </summary>
    [Fact]
    public void BytesThatAreNotValidUtf8SurviveToo()
    {
        var contents = new byte[] { 0x22, 0xE9, 0xF1, 0xFF, 0x22, 0x3D, 0x7B, 0x7D };

        Assert.Equal(contents, Decode(Encode(contents)));
    }

    /// <summary>
    /// Anything kept by a version that stored text is still read as text, because the alternative is
    /// losing whatever somebody had open when they updated.
    /// </summary>
    [Fact]
    public void TextKeptByAnOlderVersionIsRecognisedAsText()
    {
        var older = typeof(SessionHost).Assembly
            .GetType("Sem.Ui.Services.Kept")!
            .GetMethod("TryDecode")!
            .Invoke(null, ["\"Empire\"=\r\n{\r\n}\r\n"]);

        Assert.Null(older);
    }
}
