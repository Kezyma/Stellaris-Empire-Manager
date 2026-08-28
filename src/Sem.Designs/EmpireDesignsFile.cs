using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// The player's <c>user_empire_designs_v3.4.txt</c>: a flat list of empires, each keyed by its own
/// name.
/// </summary>
/// <remarks>
/// <para>
/// The file name is a constant in the game executable and its <c>v3.4</c> is the format version,
/// frozen since Stellaris 3.4, not the game version. It does not change with game patches.
/// </para>
/// <para>
/// Parsing is strict here. A block left unclosed in this file means it was truncated or damaged,
/// and loading half of somebody's empires would be worse than refusing to load at all.
/// </para>
/// </remarks>
public sealed class EmpireDesignsFile
{
    /// <summary>The file name the game reads and writes. Hardcoded in the executable.</summary>
    public const string FileName = "user_empire_designs_v3.4.txt";

    private readonly List<EmpireDesign> _designs;

    private EmpireDesignsFile(CwDocument document, List<EmpireDesign> designs)
    {
        Document = document;
        _designs = designs;
    }

    /// <summary>The parsed file, including anything this model does not interpret.</summary>
    public CwDocument Document { get; }

    /// <summary>The empires in the file, in order.</summary>
    public IReadOnlyList<EmpireDesign> Designs => _designs;

    /// <summary>Reads a designs file.</summary>
    public static EmpireDesignsFile Load(ReadOnlySpan<byte> bytes)
    {
        var document = CwDocument.Parse(bytes, CwParseOptions.Strict);
        return FromDocument(document);
    }

    /// <summary>Reads a designs file from already-decoded text.</summary>
    public static EmpireDesignsFile LoadText(string text)
    {
        var document = CwDocument.ParseText(text, options: CwParseOptions.Strict);
        return FromDocument(document);
    }

    /// <summary>Creates an empty designs file in the format the game writes.</summary>
    public static EmpireDesignsFile CreateEmpty() =>
        new(new CwDocument { TrailingTrivia = "\r\n" }, []);

    /// <summary>
    /// Writes the file back out. An unmodified file produces exactly the bytes it was read from.
    /// </summary>
    public byte[] Save() => Document.ToBytes();

    /// <summary>Finds an empire by its key, or returns null.</summary>
    public EmpireDesign? Find(string key) =>
        _designs.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));

    /// <summary>Adds a new, empty empire under the given key.</summary>
    public EmpireDesign Add(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        RequireUnusedKey(key);

        var block = new CwBlock();
        block.Add(CwNode.QuotedAssignment("key", key));

        var node = CwNode.Assignment(key, block, quoteKey: true);
        Document.Add(node);

        var design = new EmpireDesign(node);
        _designs.Add(design);
        return design;
    }

    /// <summary>
    /// Copies an existing empire under a new key. Used both for duplicating one of the player's
    /// own designs and for turning a built-in empire into an editable copy.
    /// </summary>
    public EmpireDesign AddCopy(EmpireDesign source, string newKey)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(newKey);
        RequireUnusedKey(newKey);

        var node = source.Node.Clone();
        Document.Add(node);

        var design = new EmpireDesign(node);
        design.Rename(newKey);
        _designs.Add(design);
        return design;
    }

    /// <summary>
    /// Adds an editable copy of one of the game's built-in empires, translating it out of the
    /// prescripted dialect. This is how a preset becomes one of the player's own designs.
    /// </summary>
    public EmpireDesign AddFromPrescripted(PrescriptedEmpire source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var design = Add(key);
        PrescriptedConverter.Populate(source, design);
        return design;
    }

    /// <summary>
    /// Adds an empire copied from a template written in this same format, which is how a new
    /// empire starts from the game's blank slate rather than from nothing.
    /// </summary>
    public EmpireDesign AddFromTemplate(string template, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var source = LoadText(template).Designs.FirstOrDefault()
            ?? throw new ArgumentException("The template holds no empire.", nameof(template));

        return AddCopy(source, key);
    }

    /// <summary>Removes an empire from the file.</summary>
    public bool Remove(EmpireDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        return _designs.Remove(design) && Document.Remove(design.Node);
    }

    private static EmpireDesignsFile FromDocument(CwDocument document)
    {
        var designs = document.Nodes
            .Where(n => n.IsAssignment && n.Block is not null)
            .Select(n => new EmpireDesign(n))
            .ToList();

        return new EmpireDesignsFile(document, designs);
    }

    private void RequireUnusedKey(string key)
    {
        if (Find(key) is not null)
        {
            throw new ArgumentException(
                $"An empire named '{key}' already exists in this file.", nameof(key));
        }
    }
}
