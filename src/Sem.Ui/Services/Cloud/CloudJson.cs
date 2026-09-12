using System.Text.Json.Serialization;

namespace Sem.Ui.Services.Cloud;

/// <summary>One item as Graph describes it. Only the fields this app reads.</summary>
public sealed class GraphItem
{
    /// <summary>The handle everything else is asked by.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The file's name, extension and all.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// What Graph changes when the item changes, and compares against on a conditional write.
    /// </summary>
    /// <remarks>
    /// The eTag rather than the cTag. The cTag changes only when the contents do, which would make
    /// a better change signal - but If-Match on an upload is specified against the eTag, and using
    /// one for noticing and the other for refusing would mean the conflict check compared a value
    /// the poll had never seen. The cost is that a rename or a move reads the file again once.
    /// </remarks>
    [JsonPropertyName("eTag")]
    public string? ETag { get; set; }

    /// <summary>How large it is.</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>When it last changed.</summary>
    [JsonPropertyName("lastModifiedDateTime")]
    public DateTimeOffset LastModified { get; set; }

    /// <summary>The folder it sits in.</summary>
    [JsonPropertyName("parentReference")]
    public GraphParent? Parent { get; set; }

    /// <summary>Present only on a folder, which is how one is told from a file.</summary>
    [JsonPropertyName("folder")]
    public GraphFolder? Folder { get; set; }

    /// <summary>
    /// A link to the bytes that carries its own short-lived permission.
    /// </summary>
    /// <remarks>
    /// Asked for instead of following the redirect from the content endpoint, so that the session's
    /// own token is never sent to the content host - which is a different host, on a name that
    /// varies per account. The link is good for a few minutes and for this file only.
    /// </remarks>
    [JsonPropertyName("@microsoft.graph.downloadUrl")]
    public string? DownloadUrl { get; set; }
}

/// <summary>The mark of a folder. Its presence is the whole message.</summary>
public sealed class GraphFolder
{
    /// <summary>How much is inside, which is worth showing beside the name.</summary>
    [JsonPropertyName("childCount")]
    public int ChildCount { get; set; }
}

/// <summary>Where an item sits, as much of it as is useful.</summary>
public sealed class GraphParent
{
    /// <summary>The folder's own handle, which is what a sibling is written beside.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>A readable path, for telling two files of the same name apart on screen.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

/// <summary>A page of items, which is what a search answers with.</summary>
public sealed class GraphItems
{
    /// <summary>The items themselves.</summary>
    [JsonPropertyName("value")]
    public List<GraphItem>? Value { get; set; }
}

/// <summary>
/// The shapes that cross the wire to a provider, serialised without reflection.
/// </summary>
/// <remarks>
/// Source-generated for the reason the game data's own context is: this assembly is published into
/// a trimmed WebAssembly bundle, where a reflecting serialiser is both a warning and a way to find
/// out at runtime that a property was trimmed away.
/// </remarks>
[JsonSerializable(typeof(TokenGrant))]
[JsonSerializable(typeof(GraphItem))]
[JsonSerializable(typeof(GraphItems))]
internal sealed partial class CloudJson : JsonSerializerContext;
