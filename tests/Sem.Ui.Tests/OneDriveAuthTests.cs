using System.Net;
using System.Reflection;
using System.Text;
using Sem.Ui.Services.Cloud;

namespace Sem.Ui.Tests;

/// <summary>
/// Signing in to a personal Microsoft account from a page with no server behind it.
/// </summary>
/// <remarks>
/// The flow is written out in this repository rather than taken from a library, so the parts a
/// library would have got right are the parts worth testing: that the challenge really is the hash
/// of the verifier, that the verifier itself never leaves, and that a code arriving with the wrong
/// state is dropped rather than spent.
/// </remarks>
public sealed class OneDriveAuthTests
{
    private const string ClientId = "3275b739-5f92-47b4-9210-3f1def80ec25";
    private const string Redirect = "https://kezyma.github.io/Stellaris-Empire-Manager/";

    /// <summary>Answers whatever the test says, and keeps what it was asked.</summary>
    private sealed class Handler(Func<HttpRequestMessage, string, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        public List<Uri?> Urls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Bodies.Add(body);
            Urls.Add(request.RequestUri);

            return answer(request, body);
        }
    }

    private static HttpResponseMessage Granting(string access, string? refresh, int seconds = 3600)
    {
        var refreshPart = refresh is null ? string.Empty : $",\"refresh_token\":\"{refresh}\"";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"access_token\":\"{access}\",\"expires_in\":{seconds}{refreshPart}}}",
                Encoding.UTF8,
                "application/json"),
        };
    }

    private static (OneDriveAuth Auth, Handler Handler, NoTokenStore Session) Built(
        Func<HttpRequestMessage, string, HttpResponseMessage>? answer = null)
    {
        var handler = new Handler(answer ?? ((_, _) => Granting("token-1", "refresh-1")));
        var session = new NoTokenStore();

        return (new OneDriveAuth(new HttpClient(handler), session, ClientId, Redirect), handler, session);
    }

    private static string Challenge(string verifier) =>
        (string)typeof(OneDriveAuth)
            .GetMethod("Challenge", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [verifier])!;

    /// <summary>
    /// The challenge is the base64url SHA-256 of the verifier, to the letter.
    /// </summary>
    /// <remarks>
    /// The example from RFC 7636 itself. This is the one piece of cryptography in the app and the
    /// one place a library would have been safer, so it is pinned against the specification's own
    /// numbers rather than against whatever this code happens to produce. Ordinary base64 fails it:
    /// the padding has to go and two characters have to be swapped.
    /// </remarks>
    [Fact]
    public void TheChallengeIsTheHashTheSpecificationSays()
    {
        Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    /// <summary>The address carries the hash, and never the thing it is a hash of.</summary>
    [Fact]
    public async Task TheVerifierStaysInTheTab()
    {
        var (auth, _, session) = Built();

        var address = await auth.BeginAsync();
        var verifier = await session.ReadAsync("sem.cloud.verifier");

        Assert.NotNull(verifier);
        Assert.DoesNotContain(verifier!, address, StringComparison.Ordinal);
        Assert.Contains("code_challenge=" + Uri.EscapeDataString(Challenge(verifier!)), address, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", address, StringComparison.Ordinal);
    }

    /// <summary>And the rest of what a sign-in needs, in the address rather than anywhere else.</summary>
    [Fact]
    public async Task TheAddressSaysWhoIsAskingAndWhereToComeBack()
    {
        var (auth, _, _) = Built();

        var address = await auth.BeginAsync();

        Assert.StartsWith("https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?", address, StringComparison.Ordinal);
        Assert.Contains("client_id=" + ClientId, address, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=" + Uri.EscapeDataString(Redirect), address, StringComparison.Ordinal);
        Assert.Contains("response_type=code", address, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("Files.ReadWrite offline_access"), address, StringComparison.Ordinal);

        // No secret, because a page cannot keep one. See docs/cloud-setup.md.
        Assert.DoesNotContain("client_secret", address, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A code that comes back with the right state is exchanged, and the session kept.</summary>
    [Fact]
    public async Task ComingBackWithTheRightStateSignsIn()
    {
        var (auth, handler, session) = Built();

        var address = await auth.BeginAsync();
        var state = Uri.UnescapeDataString(address.Split("state=")[1].Split('&')[0]);

        Assert.True(await auth.CompleteAsync($"{Redirect}?code=the-code&state={Uri.EscapeDataString(state)}"));
        Assert.Equal("token-1", await auth.TokenAsync());
        Assert.Equal("refresh-1", await session.ReadAsync("sem.cloud.refresh"));

        // The verifier went out exactly once, to the token endpoint, and is now forgotten.
        Assert.Contains("code_verifier=", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Null(await session.ReadAsync("sem.cloud.verifier"));
    }

    /// <summary>
    /// A code arriving with somebody else's state is not spent.
    /// </summary>
    /// <remarks>
    /// The guard against being handed a sign-in nobody in this tab asked for. Dropping it costs a
    /// second click; redeeming it would attach a stranger's account to the session.
    /// </remarks>
    [Fact]
    public async Task ACodeWithTheWrongStateIsDropped()
    {
        var (auth, handler, _) = Built();

        await auth.BeginAsync();

        Assert.False(await auth.CompleteAsync($"{Redirect}?code=the-code&state=not-the-one"));
        Assert.Empty(handler.Bodies);
    }

    /// <summary>And so is one arriving when nothing was asked at all.</summary>
    [Fact]
    public async Task ACodeNobodyAskedForIsDropped()
    {
        var (auth, handler, _) = Built();

        Assert.False(await auth.CompleteAsync($"{Redirect}?code=the-code&state=anything"));
        Assert.Empty(handler.Bodies);
    }

    /// <summary>An ordinary load, with no code in the address, is not a sign-in and says so.</summary>
    [Fact]
    public async Task AnAddressWithNoCodeIsNotASignIn()
    {
        var (auth, handler, _) = Built();

        Assert.False(await auth.CompleteAsync(Redirect));
        Assert.False(await auth.CompleteAsync(Redirect + "?d=something-shared"));
        Assert.Empty(handler.Bodies);
    }

    /// <summary>A token that has run out is renewed without anybody being asked again.</summary>
    [Fact]
    public async Task AnExpiredTokenIsRenewedQuietly()
    {
        var granted = 0;
        var (auth, handler, _) = Built((_, body) =>
            Granting(body.Contains("refresh_token", StringComparison.Ordinal) ? "token-2" : "token-1",
                     "refresh-1",
                     // The first is already past its grace period, so the next call must renew.
                     seconds: ++granted == 1 ? 30 : 3600));

        var address = await auth.BeginAsync();
        var state = Uri.UnescapeDataString(address.Split("state=")[1].Split('&')[0]);
        await auth.CompleteAsync($"{Redirect}?code=c&state={Uri.EscapeDataString(state)}");

        Assert.Equal("token-2", await auth.TokenAsync());
        Assert.Contains("grant_type=refresh_token", handler.Bodies[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal to renew ends the session rather than being retried for ever.
    /// </summary>
    /// <remarks>
    /// Refusing a refresh token means it is over - revoked, expired, the password changed - and the
    /// answer is to ask the player, not to ask Microsoft again every fifteen seconds.
    /// </remarks>
    [Fact]
    public async Task ARefusedRenewalEndsTheSession()
    {
        var calls = 0;
        var (auth, _, session) = Built((_, body) =>
        {
            calls++;

            return body.Contains("refresh_token", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : Granting("token-1", "refresh-1", seconds: 30);
        });

        var address = await auth.BeginAsync();
        var state = Uri.UnescapeDataString(address.Split("state=")[1].Split('&')[0]);
        await auth.CompleteAsync($"{Redirect}?code=c&state={Uri.EscapeDataString(state)}");

        Assert.Null(await auth.TokenAsync());
        Assert.Null(await session.ReadAsync("sem.cloud.refresh"));
        Assert.False(await auth.SignedInAsync());

        // And asking again does not send Microsoft another refusal to refuse.
        Assert.Null(await auth.TokenAsync());
        Assert.Equal(2, calls);
    }

    /// <summary>With nothing kept, there is no token and nothing is asked of anybody.</summary>
    [Fact]
    public async Task NoSessionMeansNoToken()
    {
        var (auth, handler, _) = Built();

        Assert.Null(await auth.TokenAsync());
        Assert.False(await auth.SignedInAsync());
        Assert.Empty(handler.Bodies);
    }
}
