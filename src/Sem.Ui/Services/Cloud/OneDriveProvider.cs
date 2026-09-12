using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Sem.Ui.Services.Cloud;

/// <summary>
/// One file in the player's OneDrive, reached through Microsoft Graph.
/// </summary>
/// <remarks>
/// <para>
/// Everything is addressed by item id rather than by path, which matters more here than it sounds.
/// OneDrive's backed-up Documents folder sits under a device name - so the same designs file is at
/// <c>/THIS-PC/Documents/...</c> on one machine and somewhere else on the next - and a path
/// remembered from one visit would be wrong after a rename. The id survives both.
/// </para>
/// <para>
/// Nothing here throws for the ordinary failures, because the caller is a save button. A session
/// that has expired, a file that has gone and a provider that says no all answer with null or a
/// <see cref="CloudWrite"/>, and the interface says so.
/// </para>
/// </remarks>
public sealed class OneDriveProvider(HttpClient client, OneDriveAuth auth) : ICloudProvider
{
    private const string Graph = "https://graph.microsoft.com/v1.0";

    /// <summary>What is asked for about an item, so a response is not a whole DriveItem.</summary>
    private const string Fields = "id,name,size,eTag,lastModifiedDateTime,parentReference";

    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly OneDriveAuth _auth = auth ?? throw new ArgumentNullException(nameof(auth));

    /// <inheritdoc />
    public string Name => "OneDrive";

    /// <inheritdoc />
    /// <remarks>
    /// Graph's own search, across the whole drive, because the file cannot be found by path - see
    /// the class remark. What comes back is offered to the player to choose from rather than
    /// guessed at, since more than one machine backing up to one account means more than one file
    /// of this name is entirely ordinary.
    /// </remarks>
    public async Task<IReadOnlyList<CloudFile>> FindAsync(
        string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var escaped = Uri.EscapeDataString(query.Replace("'", "''", StringComparison.Ordinal));

        var found = await ReadAsync(
            $"{Graph}/me/drive/root/search(q='{escaped}')?$select={Fields}&$top=50",
            CloudJson.Default.GraphItems,
            cancellationToken).ConfigureAwait(false);

        return found?.Value is not { } items
            ? []
            : [.. items.Where(i => i.Id is { Length: > 0 } && i.Name is { Length: > 0 })
                .Select(i => new CloudFile(i.Id!, i.Name!, Folder(i)))];
    }

    /// <inheritdoc />
    /// <remarks>
    /// In two steps rather than one, and deliberately. Asking the content endpoint for the bytes
    /// answers with a redirect to whichever content host the account lives on, and following it
    /// carries the session's token to a host that has no business holding it - besides which the
    /// host's name varies per account, so a policy naming where the page may connect cannot name it
    /// exactly. The item's own download link is asked for instead and fetched with no Authorization
    /// header at all: it carries a short-lived permission of its own, for this one file.
    /// </remarks>
    public async Task<(byte[] Contents, CloudStamp Stamp)?> ReadAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // No $select on this one, deliberately. The download link is an annotation rather than a
        // field, and asking for a list of fields drops it - Graph answers 200 with everything that
        // was asked for and no link, which looks exactly like a file that cannot be read. The whole
        // item is a little larger and always carries it.
        var item = await ReadAsync(
            $"{Graph}/me/drive/items/{Uri.EscapeDataString(id)}",
            CloudJson.Default.GraphItem,
            cancellationToken).ConfigureAwait(false);

        if (Stamped(item) is not { } stamp || item?.DownloadUrl is not { Length: > 0 } link)
        {
            return null;
        }

        // No token on this one. The link is the permission.
        using var request = new HttpRequestMessage(HttpMethod.Get, link);
        using var response = await Send(request, cancellationToken).ConfigureAwait(false);

        if (response is not { IsSuccessStatusCode: true })
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        return (bytes, stamp);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CloudEntry>> ListAsync(
        string? folderId, CancellationToken cancellationToken = default)
    {
        var where = folderId is { Length: > 0 } folder
            ? $"items/{Uri.EscapeDataString(folder)}"
            : "root";

        var found = await ReadAsync(
            $"{Graph}/me/drive/{where}/children?$select=id,name,size,folder&$top=200&$orderby=name",
            CloudJson.Default.GraphItems,
            cancellationToken).ConfigureAwait(false);

        if (found?.Value is not { } items)
        {
            return [];
        }

        // Folders first, then files, each by name - which is how every file browser has ever done
        // it, and the order Graph does not promise even when asked.
        return
        [
            .. items
                .Where(i => i.Id is { Length: > 0 } && i.Name is { Length: > 0 })
                .Select(i => new CloudEntry(i.Id!, i.Name!, i.Folder is not null, i.Size))
                .OrderByDescending(e => e.IsFolder)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <inheritdoc />
    /// <remarks>
    /// If-Match is what makes this a promise rather than a race. Graph answers 412 when the file has
    /// moved on, and nothing is written - which is the whole reason the designs file is worth
    /// keeping at a provider rather than emailing to yourself.
    /// </remarks>
    public async Task<(CloudWrite Outcome, CloudStamp? Stamp)> WriteAsync(
        string id, byte[] contents, string? ifVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(contents);

        using var request = await RequestAsync(
            HttpMethod.Put, $"{Graph}/me/drive/items/{Uri.EscapeDataString(id)}/content")
            .ConfigureAwait(false);

        if (request is null)
        {
            return (CloudWrite.Refused, null);
        }

        request.Content = new ByteArrayContent(contents);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        if (ifVersion is { Length: > 0 })
        {
            request.Headers.TryAddWithoutValidation("if-match", ifVersion);
        }

        using var response = await Send(request, cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            return (CloudWrite.Refused, null);
        }

        if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            return (CloudWrite.Conflicted, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (CloudWrite.Refused, null);
        }

        var item = await response.Content
            .ReadFromJsonAsync(CloudJson.Default.GraphItem, cancellationToken)
            .ConfigureAwait(false);

        return (CloudWrite.Written, Stamped(item));
    }

    /// <inheritdoc />
    public async Task<CloudStamp?> StatAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var item = await ReadAsync(
            $"{Graph}/me/drive/items/{Uri.EscapeDataString(id)}?$select={Fields}",
            CloudJson.Default.GraphItem,
            cancellationToken).ConfigureAwait(false);

        return Stamped(item);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The folder is asked for rather than remembered, because the chosen file carries a readable
    /// path for the player and not a handle - and this is the one call that needs the handle. It
    /// costs a request, on a save that is already writing twice.
    /// </remarks>
    public async Task<bool> WriteBesideAsync(
        CloudFile file, string name, byte[] contents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(contents);

        var item = await ReadAsync(
            $"{Graph}/me/drive/items/{Uri.EscapeDataString(file.Id)}?$select=parentReference",
            CloudJson.Default.GraphItem,
            cancellationToken).ConfigureAwait(false);

        if (item?.Parent?.Id is not { Length: > 0 } folder)
        {
            return false;
        }

        using var request = await RequestAsync(
            HttpMethod.Put,
            $"{Graph}/me/drive/items/{Uri.EscapeDataString(folder)}:/{Uri.EscapeDataString(name)}:/content")
            .ConfigureAwait(false);

        if (request is null)
        {
            return false;
        }

        request.Content = new ByteArrayContent(contents);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        using var response = await Send(request, cancellationToken).ConfigureAwait(false);

        return response is { IsSuccessStatusCode: true };
    }

    private static CloudStamp? Stamped(GraphItem? item) =>
        item?.ETag is { Length: > 0 } version
            ? new CloudStamp(version, item.LastModified, item.Size)
            : null;

    /// <summary>The folder an item sits in, as something worth showing a person.</summary>
    /// <remarks>
    /// Graph gives a path of the form <c>/drive/root:/Documents/Games</c>; what is useful on screen
    /// is the part after the colon, and an empty one means the drive's own root.
    /// </remarks>
    private static string Folder(GraphItem item)
    {
        if (item.Parent?.Path is not { Length: > 0 } path)
        {
            return "/";
        }

        var mark = path.IndexOf(':', StringComparison.Ordinal);
        var tail = mark >= 0 ? path[(mark + 1)..] : path;

        return tail.Length == 0 ? "/" : Uri.UnescapeDataString(tail);
    }

    private async Task<T?> ReadAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        using var request = await RequestAsync(HttpMethod.Get, url).ConfigureAwait(false);

        if (request is null)
        {
            return default;
        }

        using var response = await Send(request, cancellationToken).ConfigureAwait(false);

        if (response is not { IsSuccessStatusCode: true })
        {
            return default;
        }

        return await response.Content
            .ReadFromJsonAsync(typeInfo, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>A request carrying the session, or null where there is no session to carry.</summary>
    private async Task<HttpRequestMessage?> RequestAsync(HttpMethod method, string url)
    {
        if (await _auth.TokenAsync().ConfigureAwait(false) is not { Length: > 0 } token)
        {
            return null;
        }

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    /// <summary>
    /// Sends it, treating a network that is not there as an answer rather than an exception.
    /// </summary>
    /// <remarks>
    /// A tab goes offline, sleeps, and comes back. None of that is exceptional enough to throw at a
    /// save button, and the callers above each have something sensible to say about null.
    /// </remarks>
    private async Task<HttpResponseMessage?> Send(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
