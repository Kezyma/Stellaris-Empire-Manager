using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sem.Ui.Services;

/// <summary>The desktop build on offer, and enough about it to decide whether to take it.</summary>
/// <param name="Name">The file's name, which is what arrives in the downloads folder.</param>
/// <param name="Address">Where to get it.</param>
/// <param name="Bytes">How large it is.</param>
/// <param name="Built">When it was built, which for a rolling release is the only version there is.</param>
public sealed record DesktopBuild(string Name, string Address, long Bytes, DateTimeOffset Built)
{
    /// <summary>
    /// The size as somebody would say it.
    /// </summary>
    /// <remarks>
    /// Said before anything else on the button's sheet, because seventy-odd megabytes is the fact
    /// most likely to change somebody's mind, and finding it out from a progress bar is finding it
    /// out too late.
    /// </remarks>
    public string Size => $"{Bytes / 1024d / 1024d:0.#} MB";
}

/// <summary>
/// What the project's releases page is offering.
/// </summary>
/// <remarks>
/// <para>
/// Registered only in the browser. The desktop app is the thing this hands out, and offering
/// somebody a download of what they are already running is the kind of detail that makes an
/// interface feel like it was not looking.
/// </para>
/// <para>
/// Asked when the button is pressed rather than on the way in. Unauthenticated GitHub allows sixty
/// requests an hour from one address, and spending one of those on every visit - for a question
/// almost nobody asks - is how a quiet afternoon turns into a page that cannot answer.
/// </para>
/// </remarks>
public sealed class Releases(HttpClient http)
{
    /// <summary>The releases page, which is where a reader goes when this cannot answer.</summary>
    public const string Page = "https://github.com/Kezyma/Stellaris-Empire-Manager/releases";

    private const string Newest =
        "https://api.github.com/repos/Kezyma/Stellaris-Empire-Manager/releases/latest";

    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    private DesktopBuild? _found;

    /// <summary>Why there is nothing to offer, where there is nothing.</summary>
    /// <remarks>
    /// One line, and every way of failing ends in one: no release yet, a refused request, a rate
    /// limit, a shape that will not parse. They differ only in what a reader could do about them,
    /// which is nothing - so they differ in wording and not in what is offered next, which is the
    /// releases page itself.
    /// </remarks>
    public string? Trouble { get; private set; }

    /// <summary>Whether the answer is still on its way.</summary>
    public bool Asking { get; private set; }

    /// <summary>
    /// The newest build, asked once and remembered.
    /// </summary>
    /// <remarks>
    /// Only a success is kept. A refusal is usually the network rather than the repository, and a
    /// visitor who presses again after their connection comes back should get an answer rather than
    /// the sentence they were given a minute ago.
    /// </remarks>
    public async Task<DesktopBuild?> NewestAsync()
    {
        if (_found is not null)
        {
            return _found;
        }

        Asking = true;
        Trouble = null;

        try
        {
            using var asking = new HttpRequestMessage(HttpMethod.Get, Newest);

            // What GitHub asks callers to say, and the one version of the shape below this reads.
            asking.Headers.Add("Accept", "application/vnd.github+json");

            using var answer = await _http.SendAsync(asking).ConfigureAwait(false);

            // Nothing published. True before the first build finishes, and after a release is taken
            // down, and it is not a fault on either side - so it says so plainly.
            if (answer.StatusCode is HttpStatusCode.NotFound)
            {
                Trouble = "There is no download yet.";

                return null;
            }

            // Nearly always the rate limit, which is per address and shared with everything else on
            // the same network. Named rather than guessed at: this app cannot tell the difference
            // between being rationed and being refused, and saying "try again shortly" covers both.
            if (!answer.IsSuccessStatusCode)
            {
                Trouble = "GitHub would not say what the newest build is. Try again shortly.";

                return null;
            }

            var release = await answer.Content
                .ReadFromJsonAsync(ReleaseJson.Default.GitHubRelease)
                .ConfigureAwait(false);

            var built = release?.Assets?.FirstOrDefault(asset =>
                asset.Name is { Length: > 0 } name
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (built is not { Name: { Length: > 0 } named, Address: { Length: > 0 } address })
            {
                Trouble = "The newest build has nothing attached to it to download.";

                return null;
            }

            // The asset's date, not the release's. The release is put up once and kept - the tag
            // moves and the file inside it is replaced - so its published date is when the first
            // build went up and stays there for ever. The file is what changes, so the file is
            // what is asked.
            return _found = new DesktopBuild(named, address, built.Bytes, built.Updated);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Trouble = "The newest build could not be looked up.";

            return null;
        }
        finally
        {
            Asking = false;
        }
    }
}

/// <summary>One release as GitHub describes it. Only the fields this app reads.</summary>
/// <remarks>
/// Which is one field. published_at looked like the build date and is not: this project keeps one
/// release and replaces what is inside it, so that date is when the first build ever went up.
/// </remarks>
public sealed class GitHubRelease
{
    /// <summary>The files attached to it.</summary>
    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

/// <summary>A file attached to a release.</summary>
public sealed class GitHubAsset
{
    /// <summary>What it is called, which is what the download is named.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>How large it is.</summary>
    [JsonPropertyName("size")]
    public long Bytes { get; set; }

    /// <summary>Where the bytes are.</summary>
    [JsonPropertyName("browser_download_url")]
    public string? Address { get; set; }

    /// <summary>When this file was last replaced, which is when it was built.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset Updated { get; set; }
}

/// <summary>
/// The shapes that come back from GitHub, read without reflection.
/// </summary>
/// <remarks>
/// Source-generated for the reason the game data's own context is: this assembly is published into
/// a trimmed WebAssembly bundle, where a reflecting serialiser is both a build warning and a way to
/// find out at runtime that a property was trimmed away.
/// </remarks>
[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class ReleaseJson : JsonSerializerContext;
