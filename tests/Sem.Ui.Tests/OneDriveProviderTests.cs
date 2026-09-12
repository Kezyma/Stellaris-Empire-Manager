using System.Net;
using System.Text;
using Sem.Ui.Services.Cloud;

namespace Sem.Ui.Tests;

/// <summary>
/// Reaching one file in a OneDrive through Graph.
/// </summary>
/// <remarks>
/// What is worth pinning here is what goes out on the wire, because none of it can be checked by
/// reading the code back: that a write carries If-Match and so cannot silently win a race, that a
/// refusal is told apart from a conflict, and that nothing is sent at all when there is no session
/// to send it with.
/// </remarks>
public sealed class OneDriveProviderTests
{
    private sealed class Graph(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? IfMatch, bool Bearer)> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add((
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.TryGetValues("if-match", out var match) ? string.Join(",", match) : null,
                request.Headers.Authorization is { Scheme: "Bearer", Parameter.Length: > 0 }));

            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string Item =
        """
        {"id":"item-1","name":"user_empire_designs_v3.4.txt","eTag":"\"tag-7\"","size":1234,
         "lastModifiedDateTime":"2026-09-12T10:11:12Z",
         "parentReference":{"id":"folder-9","path":"/drive/root:/KEZYMA-DT/Documents/Paradox Interactive/Stellaris"}}
        """;

    /// <summary>An auth that has been through a sign-in, so the provider has a token to send.</summary>
    private static async Task<OneDriveAuth> SignedInAsync()
    {
        var granting = new Graph(_ => Json("""{"access_token":"token-1","expires_in":3600,"refresh_token":"r"}"""));
        var auth = new OneDriveAuth(
            new HttpClient(granting), new NoSessionStore(), "client", "https://example.invalid/");

        var address = await auth.BeginAsync();
        var state = Uri.UnescapeDataString(address.Split("state=")[1].Split('&')[0]);
        await auth.CompleteAsync($"https://example.invalid/?code=c&state={Uri.EscapeDataString(state)}");

        return auth;
    }

    private static async Task<(OneDriveProvider Provider, Graph Wire)> Built(
        Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var wire = new Graph(answer);

        return (new OneDriveProvider(new HttpClient(wire), await SignedInAsync()), wire);
    }

    /// <summary>A search turns Graph's items into something a chooser can show.</summary>
    [Fact]
    public async Task SearchingGivesBackTheFilesAndWhereTheySit()
    {
        var (provider, wire) = await Built(_ => Json($$"""{"value":[{{Item}}]}"""));

        var found = await provider.FindAsync("user_empire_designs");
        var file = Assert.Single(found);

        Assert.Equal("item-1", file.Id);
        Assert.Equal("user_empire_designs_v3.4.txt", file.Name);

        // The readable half of Graph's path, which is what tells two machines' files apart.
        Assert.Equal("/KEZYMA-DT/Documents/Paradox Interactive/Stellaris", file.Folder);

        Assert.Contains("search(q='user_empire_designs')", Uri.UnescapeDataString(wire.Sent[0].Url), StringComparison.Ordinal);
        Assert.True(wire.Sent[0].Bearer);
    }

    /// <summary>Reading gives back the bytes, and the version they were read at.</summary>
    [Fact]
    public async Task ReadingGivesTheBytesAndTheVersion()
    {
        var (provider, _) = await Built(request =>
            request.RequestUri!.ToString().EndsWith("/content", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }
                : Json(Item));

        var read = await provider.ReadAsync("item-1");

        Assert.Equal([1, 2, 3], read?.Contents);
        Assert.Equal("\"tag-7\"", read?.Stamp.Version);
        Assert.Equal(1234, read?.Stamp.Size);
    }

    /// <summary>
    /// A write carries the version it believes is there, so the provider can refuse.
    /// </summary>
    /// <remarks>
    /// The header is the whole promise. Without it a save is a race that the last writer wins, which
    /// on two devices editing one designs file is how an evening goes missing.
    /// </remarks>
    [Fact]
    public async Task AWriteSaysWhatItExpectsToBeOverwriting()
    {
        var (provider, wire) = await Built(_ => Json(Item));

        var (outcome, stamp) = await provider.WriteAsync("item-1", [9], "\"tag-6\"");

        Assert.Equal(CloudWrite.Written, outcome);
        Assert.Equal("\"tag-7\"", stamp?.Version);

        var put = wire.Sent.Single(s => s.Method == "PUT");
        Assert.Equal("\"tag-6\"", put.IfMatch);
        Assert.EndsWith("/me/drive/items/item-1/content", put.Url, StringComparison.Ordinal);
    }

    /// <summary>And a refusal on those grounds is a conflict, not a failure.</summary>
    [Fact]
    public async Task AFileThatMovedOnComesBackAsAConflict()
    {
        var (provider, _) = await Built(_ => new HttpResponseMessage(HttpStatusCode.PreconditionFailed));

        var (outcome, _) = await provider.WriteAsync("item-1", [9], "\"tag-6\"");

        Assert.Equal(CloudWrite.Conflicted, outcome);
    }

    /// <summary>Anything else it says no to is a refusal, which means something different.</summary>
    [Fact]
    public async Task AnythingElseIsARefusal()
    {
        var (provider, _) = await Built(_ => new HttpResponseMessage(HttpStatusCode.InsufficientStorage));

        var (outcome, _) = await provider.WriteAsync("item-1", [9], "\"tag-6\"");

        Assert.Equal(CloudWrite.Refused, outcome);
    }

    /// <summary>With no session, nothing is sent and nothing pretends to have worked.</summary>
    [Fact]
    public async Task WithNoSessionNothingGoesOut()
    {
        var wire = new Graph(_ => Json(Item));
        var auth = new OneDriveAuth(
            new HttpClient(wire), new NoSessionStore(), "client", "https://example.invalid/");
        var provider = new OneDriveProvider(new HttpClient(wire), auth);

        Assert.Empty(await provider.FindAsync("anything"));
        Assert.Null(await provider.ReadAsync("item-1"));
        Assert.Equal(CloudWrite.Refused, (await provider.WriteAsync("item-1", [1], null)).Outcome);
        Assert.Empty(wire.Sent);
    }

    /// <summary>The dated copy is written into the folder the chosen file is already in.</summary>
    [Fact]
    public async Task TheBackupGoesBesideTheFileItIsACopyOf()
    {
        var (provider, wire) = await Built(_ => Json(Item));

        var kept = await provider.WriteBesideAsync(
            new CloudFile("item-1", "user_empire_designs_v3.4.txt", "/Stellaris"),
            "user_empire_designs_v3.4_260912_101112.txt",
            [1]);

        Assert.True(kept);

        var put = wire.Sent.Single(s => s.Method == "PUT");

        Assert.Contains("/me/drive/items/folder-9:/", put.Url, StringComparison.Ordinal);
        Assert.Contains("user_empire_designs_v3.4_260912_101112.txt", Uri.UnescapeDataString(put.Url), StringComparison.Ordinal);
    }
}
