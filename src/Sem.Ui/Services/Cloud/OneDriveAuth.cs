using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Sem.Ui.Services.Cloud;

/// <summary>
/// Signing in to a personal Microsoft account, from a page with no server behind it.
/// </summary>
/// <remarks>
/// <para>
/// Authorisation code with PKCE, written out rather than taken from a library. It is about a hundred
/// lines and needs no JavaScript at all - .NET has the random bytes and the hash, the address bar is
/// the redirect, and HttpClient is the exchange - where MSAL would be a quarter of a megabyte of
/// minified third-party script vendored into a repository that has exactly one script of its own.
/// Keeping <c>script-src 'self'</c> literally true is worth more here than the edge cases a broker
/// library handles for applications that have several accounts and a native shell.
/// </para>
/// <para>
/// No secret is involved and none could be: anything shipped to a browser is readable. What refuses
/// an impostor is the redirect allowlist held by Microsoft, and the verifier below - a random value
/// kept in this tab, of which only the hash is sent out, so a code intercepted on the way back
/// cannot be exchanged by whoever intercepted it. See docs/cloud-setup.md.
/// </para>
/// <para>
/// The refresh token is kept by <see cref="ITokenStore"/>, which puts it in localStorage so that a
/// sign-in outlives the tab it was made in. That leaves it on the machine until the player
/// disconnects, and the remark on <see cref="BrowserTokenStore"/> is where that trade is argued.
/// </para>
/// </remarks>
public sealed class OneDriveAuth
{
    /// <summary>
    /// Personal accounts, which is what a Stellaris player has.
    /// </summary>
    /// <remarks>
    /// <c>/consumers</c> rather than <c>/common</c>: the registration is for personal Microsoft
    /// accounts, and pointing at common would offer work and school sign-ins that the application
    /// would then refuse after the password had been typed.
    /// </remarks>
    private const string Authority = "https://login.microsoftonline.com/consumers/oauth2/v2.0";

    /// <summary>
    /// What is asked for.
    /// </summary>
    /// <remarks>
    /// Files.ReadWrite because Microsoft offers nothing narrower to a personal account - the
    /// Selected scopes are work-and-school only and are not for calling Graph - so the consent
    /// screen says more than this app intends to do, and the interface has to say so instead.
    /// offline_access is what makes the session outlast the first hour.
    /// </remarks>
    private const string Scopes = "Files.ReadWrite offline_access";

    private const string VerifierKey = "sem.cloud.verifier";
    private const string StateKey = "sem.cloud.state";
    private const string RefreshKey = "sem.cloud.refresh";

    private readonly HttpClient _client;
    private readonly ITokenStore _session;
    private readonly string _clientId;
    private readonly string _redirectUri;

    private string? _token;
    private DateTimeOffset _expires;

    /// <summary>Takes the registration this app is, and where the browser will come back to.</summary>
    public OneDriveAuth(HttpClient client, ITokenStore session, string clientId, string redirectUri)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);

        _clientId = clientId;
        _redirectUri = redirectUri;
    }

    /// <summary>Whether there is a session to act with, without going out to check.</summary>
    public async Task<bool> SignedInAsync() =>
        _token is not null || await _session.ReadAsync(RefreshKey).ConfigureAwait(false) is { Length: > 0 };

    /// <summary>
    /// Where to send the browser to sign in, having kept what the return leg will need.
    /// </summary>
    /// <remarks>
    /// The verifier is generated and kept before anything leaves, because the whole point of it is
    /// that it never does. What goes out is its hash, and Microsoft will not exchange the code it
    /// returns for a token unless it is handed back the original.
    /// </remarks>
    public async Task<string> BeginAsync()
    {
        var verifier = Random(64);
        var state = Random(16);

        await _session.WriteAsync(VerifierKey, verifier).ConfigureAwait(false);
        await _session.WriteAsync(StateKey, state).ConfigureAwait(false);

        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = _clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _redirectUri,
            ["response_mode"] = "query",
            ["scope"] = Scopes,
            ["state"] = state,
            ["code_challenge"] = Challenge(verifier),
            ["code_challenge_method"] = "S256",
        };

        return $"{Authority}/authorize?" + string.Join(
            '&',
            query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    /// <summary>
    /// Finishes a sign-in the player has come back from, if this is that return.
    /// </summary>
    /// <param name="address">The address the app was loaded at, query and all.</param>
    /// <returns>True when a session was established by this call.</returns>
    /// <remarks>
    /// The state is checked before the code is spent. It is the value this tab invented a moment
    /// ago, so a code arriving with somebody else's state - or with none - is not the answer to
    /// anything this app asked, and is dropped rather than redeemed.
    /// </remarks>
    public async Task<bool> CompleteAsync(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var query = Parsed(address);

        if (!query.TryGetValue("code", out var code) || code.Length == 0)
        {
            return false;
        }

        var expected = await _session.ReadAsync(StateKey).ConfigureAwait(false);
        await _session.WriteAsync(StateKey, null).ConfigureAwait(false);

        if (expected is not { Length: > 0 }
            || !query.TryGetValue("state", out var state)
            || !string.Equals(state, expected, StringComparison.Ordinal))
        {
            return false;
        }

        var verifier = await _session.ReadAsync(VerifierKey).ConfigureAwait(false);
        await _session.WriteAsync(VerifierKey, null).ConfigureAwait(false);

        if (verifier is not { Length: > 0 })
        {
            return false;
        }

        return await RedeemAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _redirectUri,
            ["code_verifier"] = verifier,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// An access token good for the next call, renewed quietly if the last one has run out.
    /// </summary>
    /// <returns>The token, or null where the player has to sign in again.</returns>
    public async Task<string?> TokenAsync()
    {
        // A minute's grace, so a token is not spent on a request that will arrive after it expires.
        if (_token is { Length: > 0 } && DateTimeOffset.UtcNow < _expires - TimeSpan.FromMinutes(1))
        {
            return _token;
        }

        if (await _session.ReadAsync(RefreshKey).ConfigureAwait(false) is not { Length: > 0 } refresh)
        {
            return null;
        }

        var renewed = await RedeemAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
        }).ConfigureAwait(false);

        return renewed ? _token : null;
    }

    /// <summary>Forgets the session, here and in the tab. Microsoft is not told, and need not be.</summary>
    /// <remarks>
    /// The half-finished sign-in goes too. A player who leaves for the provider and comes back by
    /// the back button leaves a verifier and a state behind them, and nothing clears those but the
    /// return leg that never ran - so they sat in storage until the next sign-in replaced them.
    /// Neither is much use to anyone on its own, since redeeming a code needs the code as well and
    /// those are short-lived and single-use. But "disconnect" should mean there is nothing left,
    /// and a stored value that outlives what it was for is the kind of thing that is only ever
    /// found later.
    /// </remarks>
    public async Task SignOutAsync()
    {
        _token = null;
        _expires = default;

        await _session.WriteAsync(RefreshKey, null).ConfigureAwait(false);
        await _session.WriteAsync(VerifierKey, null).ConfigureAwait(false);
        await _session.WriteAsync(StateKey, null).ConfigureAwait(false);
    }

    private async Task<bool> RedeemAsync(Dictionary<string, string> form)
    {
        form["client_id"] = _clientId;
        form["scope"] = Scopes;

        try
        {
            using var response = await _client
                .PostAsync($"{Authority}/token", new FormUrlEncodedContent(form))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // A refresh that is refused means the session is over rather than that something
                // went wrong this minute, so what is kept is dropped and the player signs in again.
                await SignOutAsync().ConfigureAwait(false);
                return false;
            }

            var granted = await response.Content
                .ReadFromJsonAsync(CloudJson.Default.TokenGrant)
                .ConfigureAwait(false);

            if (granted is not { AccessToken.Length: > 0 })
            {
                return false;
            }

            _token = granted.AccessToken;
            _expires = DateTimeOffset.UtcNow.AddSeconds(granted.ExpiresIn);

            if (granted.RefreshToken is { Length: > 0 } refresh)
            {
                await _session.WriteAsync(RefreshKey, refresh).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>The query of an address, which is where this flow's answer arrives.</summary>
    internal static Dictionary<string, string> Parsed(string address)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        var mark = address.IndexOf('?', StringComparison.Ordinal);

        if (mark < 0)
        {
            return found;
        }

        foreach (var pair in address[(mark + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);

            if (equals > 0)
            {
                found[Uri.UnescapeDataString(pair[..equals])] = Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return found;
    }

    /// <summary>Random bytes as base64url, which is what both the verifier and the state are.</summary>
    internal static string Random(int bytes) => Encoded(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>The hash of the verifier, which is the only part of it that is ever sent.</summary>
    internal static string Challenge(string verifier) =>
        Encoded(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>
    /// Base64url: base64 with the two characters that mean something else in a URL swapped, and the
    /// padding dropped. RFC 7636 asks for exactly this and rejects ordinary base64.
    /// </summary>
    private static string Encoded(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>What the token endpoint hands back.</summary>
/// <remarks>
/// Only the four fields that are used. The response carries more - a token type, the scopes actually
/// granted, an id token where one was asked for - and none of it changes what this does.
/// </remarks>
public sealed class TokenGrant
{
    /// <summary>The token to put on a request.</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    /// <summary>How many seconds it is good for.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>What renews it, where one was granted.</summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>What was actually granted, which may be less than was asked for.</summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
