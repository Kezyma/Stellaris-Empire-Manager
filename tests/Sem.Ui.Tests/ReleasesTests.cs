using System.Net;
using System.Text;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// What the Desktop button asks GitHub, and what it does with each answer.
/// </summary>
/// <remarks>
/// Two hundred lines with four failure branches and no tests, which shipped two defects in a single
/// afternoon: it read the build date from a field that stops moving, and it treated "could not ask"
/// as "there is nothing to offer" - so an address that had spent its sixty requests an hour lost the
/// installer along with the size.
/// </remarks>
public sealed class ReleasesTests
{
    private sealed class Handler(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked++;

            return Task.FromResult(answer());
        }
    }

    private sealed class Refuses : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no route to host");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// One release with one file in it.
    /// </summary>
    /// <remarks>
    /// The two dates are six years apart on purpose: reading the release's instead of the file's
    /// cannot then pass by coincidence.
    /// </remarks>
    private const string OneAsset =
        "{"
        + "\"published_at\": \"2020-01-01T00:00:00Z\","
        + "\"assets\": [{"
        + "  \"name\": \"StellarisEmpireManager-win-x64.zip\","
        + "  \"size\": 80530637,"
        + "  \"browser_download_url\": \"https://example.invalid/win-x64.zip\","
        + "  \"updated_at\": \"2026-09-14T02:04:13Z\""
        + "}]}";

    private static Releases With(HttpMessageHandler handler) => new(new HttpClient(handler));

    /// <summary>
    /// The build date is the file's, not the release's.
    /// </summary>
    /// <remarks>
    /// The release is put up once and the file inside it replaced, so its published date is when the
    /// first build ever went up and stays there. Read from there, the button would have shown the
    /// same day for ever while handing out a different file each time.
    /// </remarks>
    [Fact]
    public async Task TheDateComesFromTheFileRatherThanTheReleaseAroundIt()
    {
        var releases = With(new Handler(() => Json(HttpStatusCode.OK, OneAsset)));

        var build = await releases.NewestAsync();

        Assert.NotNull(build);
        Assert.Equal(new DateTimeOffset(2026, 9, 14, 2, 4, 13, TimeSpan.Zero), build.Built);
    }

    /// <summary>And the size is said the way somebody would say it.</summary>
    [Fact]
    public async Task TheSizeIsReadableRatherThanAByteCount()
    {
        var releases = With(new Handler(() => Json(HttpStatusCode.OK, OneAsset)));

        var build = await releases.NewestAsync();

        Assert.Equal("76.8 MB", build!.Size);
        Assert.Equal("https://example.invalid/win-x64.zip", build.Address);
    }

    /// <summary>
    /// Being rationed costs the size and not the download.
    /// </summary>
    /// <remarks>
    /// Sixty requests an hour, per address, shared with everything else on that network - so this is
    /// reachable by anybody, and it was reached. A rate limit says nothing at all about whether a
    /// build exists, and taking the installer away over one is how an app appears to have lost its
    /// own download.
    /// </remarks>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ARefusedRequestKeepsTheDownloadAndLosesOnlyTheSize(HttpStatusCode status)
    {
        var releases = With(new Handler(() => new HttpResponseMessage(status)));

        Assert.Null(await releases.NewestAsync());
        Assert.True(releases.Offered);
        Assert.Contains("still the newest build", releases.Trouble, StringComparison.Ordinal);
    }

    /// <summary>Being offline is the same answer: nothing said about whether a build exists.</summary>
    [Fact]
    public async Task ANetworkThatCannotBeReachedKeepsTheDownloadToo()
    {
        var releases = With(new Refuses());

        Assert.Null(await releases.NewestAsync());
        Assert.True(releases.Offered);
        Assert.Contains("still the newest build", releases.Trouble, StringComparison.Ordinal);
    }

    /// <summary>Only GitHub saying there is nothing there takes the download away.</summary>
    [Fact]
    public async Task NoReleaseWithdrawsTheDownload()
    {
        var releases = With(new Handler(() => new HttpResponseMessage(HttpStatusCode.NotFound)));

        Assert.Null(await releases.NewestAsync());
        Assert.False(releases.Offered);
        Assert.Equal("There is no download yet.", releases.Trouble);
    }

    /// <summary>And so does a release with nothing attached to it, which says the same thing.</summary>
    [Fact]
    public async Task AReleaseWithNoFileWithdrawsTheDownload()
    {
        var releases = With(new Handler(() => Json(
            HttpStatusCode.OK,
            "{ \"published_at\": \"2026-09-14T00:00:00Z\", \"assets\": [] }")));

        Assert.Null(await releases.NewestAsync());
        Assert.False(releases.Offered);
        Assert.Contains("nothing attached", releases.Trouble, StringComparison.Ordinal);
    }

    /// <summary>An answer in a shape this does not read is a failure to ask, not a missing build.</summary>
    [Fact]
    public async Task AnAnswerThatWillNotParseKeepsTheDownload()
    {
        var releases = With(new Handler(() => Json(HttpStatusCode.OK, "not json at all")));

        Assert.Null(await releases.NewestAsync());
        Assert.True(releases.Offered);
        Assert.NotNull(releases.Trouble);
    }

    /// <summary>
    /// A success is asked for once; a refusal is not held against the next press.
    /// </summary>
    /// <remarks>
    /// Which way round matters. Caching the success is what keeps this off a rationed endpoint; not
    /// caching the refusal is what lets somebody whose connection has come back get an answer rather
    /// than the sentence they were given a minute ago.
    /// </remarks>
    [Fact]
    public async Task OnlyAnAnswerIsRemembered()
    {
        var refuse = true;
        var handler = new Handler(() => refuse
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : Json(HttpStatusCode.OK, OneAsset));

        var releases = With(handler);

        Assert.Null(await releases.NewestAsync());
        Assert.Null(await releases.NewestAsync());
        Assert.Equal(2, handler.Asked);

        refuse = false;

        Assert.NotNull(await releases.NewestAsync());
        Assert.Equal(3, handler.Asked);

        // And now it stops asking.
        Assert.NotNull(await releases.NewestAsync());
        Assert.Equal(3, handler.Asked);
    }

    /// <summary>
    /// There is a download address without asking anybody anything.
    /// </summary>
    /// <remarks>
    /// The whole point of a rolling release: one tag, one asset name, one address that answers with
    /// whatever was built last. Asking GitHub is only how the size and the date are found out, which
    /// is why none of the refusals above take the download away.
    /// </remarks>
    [Fact]
    public void ThereIsADownloadAddressWithoutAskingAnybody()
    {
        Assert.Contains("/releases/latest/download/", Releases.Download, StringComparison.Ordinal);
        Assert.EndsWith(".zip", Releases.Download, StringComparison.Ordinal);
    }
}
