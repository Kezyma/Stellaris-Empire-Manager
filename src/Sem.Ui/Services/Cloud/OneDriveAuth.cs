using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// <para>
/// The verifier and the state are kept the other way, for this tab alone, and the difference is
/// load-bearing. They are one sign-in's half-finished handshake; shared across tabs, a second tab
/// beginning one overwrites what the first is waiting on, and the first comes back to find an
/// answer to somebody else's question and is refused.
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
    public async Task<string> BeginAsync(bool afresh = false)
    {
        var verifier = Random(64);
        var state = Random(16);

        // Both places, and the return leg prefers this tab's. Safari on a phone discards a tab it
        // decides it needs the memory for, and a sign-in is exactly when that happens: the app is
        // in the background while somebody types a password at Microsoft. A discarded tab comes
        // back with its own storage emptied, and a handshake kept only there could never be
        // finished. The shared copy is the fallback for that, and nothing more - see CompleteAsync.
        await _session.WriteForTabAsync(VerifierKey, verifier).ConfigureAwait(false);
        await _session.WriteForTabAsync(StateKey, state).ConfigureAwait(false);
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

        // Asked for by name when the last attempt came to nothing.
        //
        // Somebody who ticked "keep me signed in" is sent straight back by Microsoft without a page
        // being drawn, and an instant round trip is a harder thing for a browser to carry a handshake
        // through than one with a person typing in the middle of it. select_account puts the page
        // back: the session at Microsoft is untouched, they pick the same account, and the trip takes
        // long enough to be an ordinary navigation again. It also gives somebody a way to reach a
        // different account, which was not otherwise possible once one was remembered.
        if (afresh)
        {
            query["prompt"] = "select_account";
        }

        return $"{Authority}/authorize?" + string.Join(
            '&',
            query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    /// <summary>
    /// Why the last exchange was refused, where one was and the endpoint said.
    /// </summary>
    /// <remarks>
    /// Held rather than thrown, because the callers of this are dialogs and headers with somebody
    /// standing in front of them, and a message is what they can use. Cleared by the next attempt
    /// that gets as far as asking.
    /// </remarks>
    public string? Refusal { get; private set; }

    /// <summary>
    /// What went wrong on this side, where the provider is not the one that said no.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Refusal"/> because the two want opposite sentences. A browser
    /// that would not keep the verifier - storage switched off, or full - fails the return leg
    /// looking exactly like a refusal, and telling somebody that OneDrive turned them down sends
    /// them to check the one thing that is working.
    /// </remarks>
    public string? Trouble { get; private set; }

    /// <summary>The readable half of a refusal from the token endpoint, where there is one.</summary>
    private static async Task<string?> SaidAsync(HttpResponseMessage response)
    {
        try
        {
            var refused = await response.Content
                .ReadFromJsonAsync(CloudJson.Default.TokenRefusal)
                .ConfigureAwait(false);

            if (refused?.Description is { Length: > 0 } said)
            {
                // One line of it. Microsoft's runs to a paragraph with a trace id on the end.
                var lines = said.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

                return lines.Length > 0 ? lines[0].Trim() : refused.Error;
            }

            return refused?.Error;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException)
        {
            // A refusal that will not parse is still a refusal; it just cannot say why.
            return null;
        }
    }

    /// <summary>
    /// What the provider said when it refused, where that is what it did.
    /// </summary>
    /// <remarks>
    /// The description rather than the code, because the code is for us and the description is the
    /// only part that tells somebody what to do about it. Microsoft's runs to several lines with a
    /// trace id on the end, so the first line is taken and the rest left for the console.
    /// </remarks>
    public static string? RefusalIn(string address)
    {
        var query = Parsed(address);

        if (!query.TryGetValue("error", out var code) || code.Length == 0)
        {
            return null;
        }

        if (!query.TryGetValue("error_description", out var said) || said.Length == 0)
        {
            return code;
        }

        // Plus for space, which is the form encoding a query is written in and not the one
        // UnescapeDataString undoes. Done here rather than for every parameter because the two
        // that matter - the code and the state - must come back exactly as they were sent, and
        // this one is prose that nobody can read with the spaces still written as punctuation.
        var line = said.Replace('+', ' ')
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        return line.Length > 0 ? line[0].Trim() : code;
    }

    /// <summary>
    /// Whether this address is the provider answering, whether it said yes or no.
    /// </summary>
    /// <param name="address">The address the app was loaded at, query and all.</param>
    /// <returns>True when the provider put an answer of either kind in the address.</returns>
    /// <remarks>
    /// Both halves count. A refusal comes back as <c>error</c> rather than <c>code</c>, and a page
    /// that only recognises the happy one leaves the refusal sitting in the address to be handed
    /// to the next load.
    /// </remarks>
    public static bool IsReturn(string address) =>
        !string.IsNullOrWhiteSpace(address)
        && Parsed(address) is { } query
        && (query.ContainsKey("code") || query.ContainsKey("error"));

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

        Trouble = null;

        var query = Parsed(address);

        if (!query.TryGetValue("code", out var code) || code.Length == 0)
        {
            return false;
        }

        // This tab's copy first, and the shared one only if this tab has none.
        //
        // The order is the whole of it. Preferring this tab's is what stops a second tab that began
        // its own sign-in from answering for this one - that was the bug, and localStorage being
        // shared by every tab on the origin was the cause. Falling back to the shared copy is for
        // the tab that came back with nothing, which on a phone means it was discarded while the
        // password was being typed. Where both exist they agree unless another tab has been signing
        // in, and in that case this tab's is the right one by construction.
        var kept = await _session.ReadForTabAsync(StateKey).ConfigureAwait(false);
        var shared = kept is null ? await _session.ReadAsync(StateKey).ConfigureAwait(false) : null;
        var expected = kept ?? shared;

        var verifier = await _session.ReadForTabAsync(VerifierKey).ConfigureAwait(false)
            ?? await _session.ReadAsync(VerifierKey).ConfigureAwait(false);

        // Spent, wherever they were found. Both copies go, so neither can answer anything again.
        await _session.WriteForTabAsync(StateKey, null).ConfigureAwait(false);
        await _session.WriteForTabAsync(VerifierKey, null).ConfigureAwait(false);
        await _session.WriteAsync(StateKey, null).ConfigureAwait(false);
        await _session.WriteAsync(VerifierKey, null).ConfigureAwait(false);

        var keptState = expected is { Length: > 0 };
        var keptVerifier = verifier is { Length: > 0 };

        // Every way this can fail says which one it was. They used to share a sentence, and a
        // sentence that covers four unrelated faults tells the person reading it nothing they can
        // act on and tells whoever they report it to even less.
        if (!keptState && !keptVerifier)
        {
            // Nothing at all from the leg that left here: a browser refusing to keep site data, or
            // a different browser finishing what this one started. The sign-in itself went through,
            // and nothing the provider did is wrong.
            Trouble = "This browser did not keep the sign-in it started, so it could not be "
                + "finished. Allow site data for this page, then connect again.";

            return false;
        }

        // Written as the patterns rather than the flags so that what survives is known to be there
        // from here down, which is what the request below is built out of.
        if (expected is not { Length: > 0 } || verifier is not { Length: > 0 })
        {
            // Half of it. Both are written one after the other before anything leaves, so this is
            // storage dropping one of them rather than anything about the flow.
            Trouble = "Only part of the sign-in this browser started was still here when it came "
                + "back, so it could not be finished. Connect again.";

            return false;
        }

        // Trimmed on both sides before comparing. A state is base64url - letters, digits, dash and
        // underscore - so whitespace at either end cannot be part of one, and removing it cannot
        // make two different states look alike. It can only forgive something that put a stray
        // character on the end of one of them, which is what the first report of this looked like:
        // two values whose visible halves were identical and which compared unequal anyway.
        var carried = query.TryGetValue("state", out var found) ? found.Trim() : null;

        if (carried is null || !string.Equals(carried, expected.Trim(), StringComparison.Ordinal))
        {
            // An answer to a question this tab is no longer asking: a second sign-in started over
            // the first, or a code arriving that nobody here asked for. Not spent either way.
            //
            // The two states used to be printed here, whole, with their lengths. That was put in
            // to find one bug on a phone, which has no console to open, and it found it: a stray
            // character on the end of a state that made two identical-looking values compare
            // unequal. The trim above is the fix, and the diagnostic has gone with the bug - a
            // sentence carrying two nonces is not one anybody should have to read.
            Trouble = "The answer that came back was for a different sign-in, so it was not used. "
                + "Connect again.";

            return false;
        }

        var redeemed = await RedeemAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _redirectUri,
            ["code_verifier"] = verifier,
        }).ConfigureAwait(false);

        // A refusal has already said why in its own words. Anything else that got this far and came
        // back false never reached Microsoft at all.
        if (!redeemed && Refusal is null)
        {
            Trouble = "Microsoft could not be reached to finish signing in. Check your connection, "
                + "then connect again.";
        }

        return redeemed;
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
        await _session.WriteForTabAsync(VerifierKey, null).ConfigureAwait(false);
        await _session.WriteForTabAsync(StateKey, null).ConfigureAwait(false);

        // And where these two used to be kept, which is not where they are put any more. A browser
        // that ran the older build still has a pair sitting in the shared store, and nothing else
        // will ever come back for them - so disconnecting, which is the one thing that promises to
        // leave nothing behind, takes them too.
        await _session.WriteAsync(VerifierKey, null).ConfigureAwait(false);
        await _session.WriteAsync(StateKey, null).ConfigureAwait(false);
    }

    private async Task<bool> RedeemAsync(Dictionary<string, string> form)
    {
        form["client_id"] = _clientId;
        form["scope"] = Scopes;
        Refusal = null;

        try
        {
            using var response = await _client
                .PostAsync($"{Authority}/token", new FormUrlEncodedContent(form))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Kept before the session is dropped, because dropping it is also what clears the
                // way to ask again and somebody is owed a reason first. A code that will not
                // exchange looks identical from outside to one that never arrived.
                Refusal = await SaidAsync(response).ConfigureAwait(false);

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

    /// <summary>
    /// Enough of a value to compare two of them by eye, for a message somebody has to read.
    /// </summary>
    /// <remarks>
    /// Eight characters of a hundred and twenty-eight bits. Two that differ will differ here, and
    /// showing the whole thing would put twenty-two characters of noise in a sentence.
    /// </remarks>
    private static string Short(string? value) =>
        value is not { Length: > 0 } ? "nothing" : value.Length <= 8 ? value : value[..8];

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
