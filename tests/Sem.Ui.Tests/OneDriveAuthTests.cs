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
        var verifier = await session.ReadForTabAsync("sem.cloud.verifier");

        Assert.NotNull(verifier);
        Assert.DoesNotContain(verifier!, address, StringComparison.Ordinal);
        Assert.Contains("code_challenge=" + Uri.EscapeDataString(Challenge(verifier!)), address, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", address, StringComparison.Ordinal);
    }

    /// <summary>
    /// Signing out leaves nothing behind, including from a sign-in that never finished.
    /// </summary>
    /// <remarks>
    /// The abandoned one is the case worth pinning. Leaving for the provider and coming back by the
    /// back button skips the only code that clears the verifier and the state, so before this they
    /// outlived the session they belonged to and sat in storage until the next sign-in.
    /// </remarks>
    [Fact]
    public async Task SigningOutLeavesNothingBehindEvenFromASignInThatNeverFinished()
    {
        var (auth, _, session) = Built();

        await auth.BeginAsync();

        Assert.NotNull(await session.ReadForTabAsync("sem.cloud.verifier"));
        Assert.NotNull(await session.ReadForTabAsync("sem.cloud.state"));

        await auth.SignOutAsync();

        Assert.Null(await session.ReadForTabAsync("sem.cloud.verifier"));
        Assert.Null(await session.ReadForTabAsync("sem.cloud.state"));
        Assert.Null(await session.ReadAsync("sem.cloud.refresh"));
        Assert.False(await auth.SignedInAsync());
    }

    /// <summary>
    /// One browser: every tab shares the lasting half, and each keeps its own of the other.
    /// </summary>
    /// <remarks>
    /// Two separate stores would not model this at all - they would share nothing, and a test of
    /// two tabs interfering could not fail however wrong the code was.
    /// </remarks>
    private sealed class Browser
    {
        private readonly Dictionary<string, string> _shared = new(StringComparer.Ordinal);

        public OneTab Tab() => new(_shared);

        public sealed class OneTab(Dictionary<string, string> shared) : ITokenStore
        {
            private readonly Dictionary<string, string> _thisTab = new(StringComparer.Ordinal);

            public Task<string?> ReadAsync(string key) => Task.FromResult(shared.GetValueOrDefault(key));

            public Task WriteAsync(string key, string? value) => Put(shared, key, value);

            public Task<string?> ReadForTabAsync(string key) =>
                Task.FromResult(_thisTab.GetValueOrDefault(key));

            public Task WriteForTabAsync(string key, string? value) => Put(_thisTab, key, value);

            /// <summary>Empties this tab's half, which is what a discarded tab comes back as.</summary>
            public void Discarded() => _thisTab.Clear();

            private static Task Put(Dictionary<string, string> into, string key, string? value)
            {
                if (value is null)
                {
                    into.Remove(key);
                }
                else
                {
                    into[key] = value;
                }

                return Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// A tab that came back with its own storage emptied can still finish its sign-in.
    /// </summary>
    /// <remarks>
    /// Safari on a phone discards a tab when it wants the memory, and a sign-in is exactly when it
    /// gets the chance: the app is in the background while a password is typed somewhere else. Kept
    /// only in the tab, that handshake could never be finished - so it is kept in both places, and
    /// the shared copy is consulted only when the tab has nothing.
    /// </remarks>
    [Fact]
    public async Task ATabThatWasDiscardedCanStillFinish()
    {
        var browser = new Browser();
        var tab = browser.Tab();
        var auth = new OneDriveAuth(
            new HttpClient(new Handler((_, _) => Granting("token-1", "refresh-1"))),
            tab, ClientId, Redirect);

        await auth.BeginAsync();

        var began = await tab.ReadForTabAsync("sem.cloud.state");
        Assert.NotNull(began);

        // The phone took the memory back while the password was being typed.
        tab.Discarded();

        Assert.Null(await tab.ReadForTabAsync("sem.cloud.state"));
        Assert.True(await auth.CompleteAsync(
            $"{Redirect}?code=the-code&state={Uri.EscapeDataString(began!)}"));

        // And it is spent in both places, so neither can answer anything a second time.
        Assert.Null(await tab.ReadAsync("sem.cloud.state"));
        Assert.Null(await tab.ReadAsync("sem.cloud.verifier"));
    }

    /// <summary>
    /// A second tab signing in does not spoil the first tab's sign-in.
    /// </summary>
    /// <remarks>
    /// The bug this exists to stop, and it was mine. The verifier and the state began in
    /// sessionStorage, which is one tab's; moving the refresh token to localStorage so a session
    /// would outlive a tab took those two along with it, and localStorage is shared by every tab on
    /// the origin. So a second tab beginning a sign-in wrote its state over the first tab's, and
    /// the first tab came back to find somebody else's and was refused - correctly, and for a
    /// reason nobody could see. Anyone who keeps the app open in a tab and opens another to try
    /// again reproduces it every time.
    /// </remarks>
    [Fact]
    public async Task ASecondTabSigningInDoesNotSpoilTheFirst()
    {
        // One browser, two tabs: the lasting half is genuinely shared between them, which is what
        // makes this test able to fail. Two unrelated stores would share nothing and pass whatever
        // the code did.
        var handler = new Handler((_, _) => Granting("token-1", "refresh-1"));
        var browser = new Browser();
        var firstTab = browser.Tab();
        var secondTab = browser.Tab();

        var first = new OneDriveAuth(new HttpClient(handler), firstTab, ClientId, Redirect);
        var second = new OneDriveAuth(new HttpClient(handler), secondTab, ClientId, Redirect);

        await first.BeginAsync();
        var began = await firstTab.ReadForTabAsync("sem.cloud.state");

        // The other tab starts its own, which used to overwrite what the first one was waiting on.
        await second.BeginAsync();

        Assert.NotNull(began);
        Assert.Equal(began, await firstTab.ReadForTabAsync("sem.cloud.state"));

        // So the first tab's return is still the answer to the question it asked.
        Assert.True(await first.CompleteAsync(
            $"{Redirect}?code=the-code&state={Uri.EscapeDataString(began!)}"));
    }

    /// <summary>
    /// The session outlives the tab; the half-finished sign-in does not.
    /// </summary>
    /// <remarks>
    /// Both halves of the arrangement in one place, because they are only correct together: the
    /// refresh token is kept where a new tab can find it, and the handshake where no other tab can
    /// reach it.
    /// </remarks>
    [Fact]
    public async Task TheSessionIsSharedAndTheHandshakeIsNot()
    {
        var (auth, _, session) = Built();

        await auth.BeginAsync();

        // Kept in both, and the tab's is the one that answers - which is what the two-tab test
        // above pins. The shared copy exists for a tab that comes back with nothing.
        Assert.NotNull(await session.ReadForTabAsync("sem.cloud.verifier"));
        Assert.NotNull(await session.ReadForTabAsync("sem.cloud.state"));
        Assert.NotNull(await session.ReadAsync("sem.cloud.verifier"));
        Assert.NotNull(await session.ReadAsync("sem.cloud.state"));

        await auth.CompleteAsync($"{Redirect}?code=c&state={Uri.EscapeDataString(
            (await session.ReadForTabAsync("sem.cloud.state"))!)}");

        // And the token that came of it is the half a new tab is meant to find.
        Assert.NotNull(await session.ReadAsync("sem.cloud.refresh"));
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
