namespace Sem.Ui.Services;

/// <summary>
/// One file exchange standing in for whichever one the app is currently using.
/// </summary>
/// <remarks>
/// <para>
/// Both <see cref="SessionHost"/> and <see cref="DesignSync"/> take an <see cref="IFileExchange"/>
/// in their constructors and hold it for the life of the scope, which was right while a host had
/// exactly one. Connecting to a cloud provider part-way through a visit has to change where Save
/// goes without rebuilding either of them, so what they hold is this, and what this forwards to is
/// what changes.
/// </para>
/// <para>
/// Every member is forwarded explicitly, including the ones the interface gives a default. That is
/// the whole risk in this class: an implementation that leaves a member out does not fail to
/// compile, it silently answers the interface's default instead - so a router missing
/// <see cref="SavesInPlace"/> would report false over a host that saves in place, and the header
/// would offer Export where it should offer Save. A test walks the interface map to make sure none
/// is missed, because the next member added to <see cref="IFileExchange"/> will not announce itself.
/// </para>
/// </remarks>
public sealed class FileExchangeRouter : IFileExchange
{
    private IFileExchange _current;

    /// <summary>Starts with the exchange this host would have used on its own.</summary>
    public FileExchangeRouter(IFileExchange initial)
    {
        _current = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    /// <summary>
    /// Raised when the exchange behind this one has been replaced.
    /// </summary>
    /// <remarks>
    /// Anything that drew itself from the old one has to be asked again: the header's button says
    /// Save or Export depending on it, and anything watching a file is watching the wrong one.
    /// </remarks>
    public event Action? Changed;

    /// <summary>The exchange currently being forwarded to.</summary>
    public IFileExchange Current => _current;

    /// <summary>
    /// Puts a different exchange behind this one.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for what was in flight against the old one - a file being watched
    /// most of all, since the handle came from the exchange being replaced and goes on watching it.
    /// </remarks>
    public void SwitchTo(IFileExchange exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);

        if (ReferenceEquals(exchange, _current))
        {
            return;
        }

        _current = exchange;
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents) =>
        _current.SaveAsync(fileName, contents);

    /// <inheritdoc />
    public Task<SaveOutcome> SaveAsync(string fileName, byte[] contents, bool backUp) =>
        _current.SaveAsync(fileName, contents, backUp);

    /// <inheritdoc />
    public Task<SaveOutcome> ExportAsync(string fileName, byte[] contents, ExportKind kind) =>
        _current.ExportAsync(fileName, contents, kind);

    /// <inheritdoc />
    public IDisposable? Watch(Action onChanged) => _current.Watch(onChanged);

    /// <inheritdoc />
    public Task<(string Name, byte[] Contents)?> OpenAsync() => _current.OpenAsync();

    /// <inheritdoc />
    public Task<bool> CanOpenAsync() => _current.CanOpenAsync();

    /// <inheritdoc />
    public bool SavesInPlace => _current.SavesInPlace;

    /// <inheritdoc />
    public string SaveVerb => _current.SaveVerb;

    /// <inheritdoc />
    public Task WarnBeforeLeavingAsync(bool unsaved) => _current.WarnBeforeLeavingAsync(unsaved);

    /// <inheritdoc />
    public Task<(string Name, byte[] Contents)?> TryOpenExistingAsync() =>
        _current.TryOpenExistingAsync();

    /// <inheritdoc />
    public Task<bool> CopyToClipboardAsync(string text) => _current.CopyToClipboardAsync(text);

    /// <inheritdoc />
    public string? ShareBaseUri => _current.ShareBaseUri;
}
