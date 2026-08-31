using System.Text;
using Sem.Clausewitz;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Builds a trait's icon the way the game builds it, by stacking the layers its script describes.
/// </summary>
/// <remarks>
/// <para>
/// Galactic Paragons stopped giving a leader trait a picture and started giving it a recipe. A trait
/// names an <c>inline_script</c> and fills in its blanks — which glyph, how rare, which council
/// seat, what tier — and the script stacks a coloured background, the glyph over it, and whatever
/// markers those answers call for. Taking only the glyph, as this did before, left every trait
/// wearing the same near-black mark on nothing, because the glyph is authored almost black and it
/// is the background beneath that carries the colour.
/// </para>
/// <para>
/// The scripts are followed rather than transcribed. An inline script is a text file with
/// <c>$NAME$</c> holes in it, and the game fills the holes before reading it — which is the only
/// thing that makes <c>rarity_$RARITY$</c> name a file. So the text is substituted, then parsed,
/// then walked; a nested script inherits its parent's answers and may override them. That costs no
/// more than hand-listing the three scripts named in the empire designer and picks up the dozen
/// others — psionic, society, engineering — without knowing they were there.
/// </para>
/// </remarks>
internal static class TraitIconComposer
{
    /// <summary>Where the game keeps the scripts.</summary>
    private const string ScriptRoot = "common/inline_scripts";

    /// <summary>How deep a script may call another before this gives up.</summary>
    /// <remarks>
    /// The real chain is three — a trait's icon, its council element, that element's per-class
    /// file — and a bad substitution could otherwise name a file that names itself.
    /// </remarks>
    private const int MaxDepth = 8;

    /// <summary>
    /// Registers the composed icon a trait's script describes, or nothing when it has no script.
    /// </summary>
    public static string? Compose(
        CwBlock body,
        string key,
        ScriptLoader loader,
        AssetCatalog assets,
        IReadOnlyDictionary<string, (byte R, byte G, byte B, byte A)> colors)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(colors);

        // A trait names at most one icon script; the rest of its inline scripts do other work, and
        // the one carrying an ICON is the one that draws.
        var call = body.Nodes.FirstOrDefault(n =>
            n.Key == "inline_script" && n.Block?.GetString("ICON") is { Length: > 0 });

        if (call?.Block is not { } arguments || arguments.GetString("script") is not { Length: > 0 } script)
        {
            return null;
        }

        var layers = new List<(string? Sprite, (byte R, byte G, byte B, byte A)? Tint)>();
        Walk(script, Variables(arguments), 0);

        return layers.Count > 0
            ? assets.RegisterLayers(layers, $"icons/traits/{key}.png")
            : null;

        void Walk(string path, IReadOnlyDictionary<string, string> variables, int depth)
        {
            if (depth >= MaxDepth || Read(loader, path, variables) is not { } document)
            {
                return;
            }

            // The layers live inside the script's icon block, except in the elements it calls, which
            // are fragments and carry theirs at the top. Both are walked the same way.
            var nodes = document.Nodes.FirstOrDefault(n => n.Key == "icon")?.Block?.Nodes
                ?? document.Nodes;

            foreach (var node in nodes)
            {
                switch (node.Key)
                {
                    case "layer" when node.Block is { } layer:
                        Add(layer);
                        break;

                    // Written either as a bare path, inheriting everything, or as a block that adds
                    // to or overrides what it inherited.
                    case "inline_script" when node.Block is { } nested:
                        if (nested.GetString("script") is { Length: > 0 } target)
                        {
                            Walk(target, Merge(variables, Variables(nested)), depth + 1);
                        }

                        break;

                    case "inline_script" when node.ScalarValue is { Length: > 0 } bare:
                        Walk(bare, variables, depth + 1);
                        break;
                }
            }
        }

        void Add(CwBlock layer)
        {
            // A layer the game only sometimes draws asks a question about a game in progress — is
            // this leader on the council, does this player own Paragon — and an empire being
            // designed is not a game in progress. The house rule is that a design-time answer is the
            // default answer, and the default is that none of these are showing: an unowned pack's
            // frame, or the cross over a trait a leader cannot use. So a layer with a condition is
            // left out.
            if (layer.GetBlock("visible") is not null || layer.GetString("icon") is not { Length: > 0 } icon)
            {
                return;
            }

            layers.Add((icon, layer.GetString("color") is { Length: > 0 } named
                ? colors.TryGetValue(named, out var color) ? color : null
                : null));
        }
    }

    /// <summary>
    /// Loads a script with its blanks filled in.
    /// </summary>
    /// <remarks>
    /// Substituted as text before parsing because that is what the game does, and because a hole can
    /// sit inside a file name — <c>trait/icon_element/tier_$TIER$</c> — where nothing that had
    /// already been parsed could reach it. A hole with no answer is left as it stands, which turns
    /// into a file that does not exist and so into no layers, rather than into a wrong picture.
    /// </remarks>
    private static CwDocument? Read(
        ScriptLoader loader,
        string script,
        IReadOnlyDictionary<string, string> variables)
    {
        var path = $"{ScriptRoot}/{Substitute(script, variables)}.txt";

        if (!loader.Content.Contains(path))
        {
            return null;
        }

        var text = Substitute(Encoding.UTF8.GetString(loader.Content.Read(path)), variables);

        return CwDocument.Parse(Encoding.UTF8.GetBytes(text), CwParseOptions.Lenient);
    }

    /// <summary>Fills in every <c>$NAME$</c> an answer was given for.</summary>
    private static string Substitute(string text, IReadOnlyDictionary<string, string> variables)
    {
        if (!text.Contains('$', StringComparison.Ordinal))
        {
            return text;
        }

        var result = new StringBuilder(text);

        foreach (var (name, value) in variables)
        {
            result.Replace($"${name}$", value);
        }

        return result.ToString();
    }

    /// <summary>The answers an inline script call supplies, which is everything but its own name.</summary>
    private static Dictionary<string, string> Variables(CwBlock arguments) => arguments.Nodes
        .Where(n => n.Key is { Length: > 0 } and not "script" && n.ScalarValue is not null)
        .GroupBy(n => n.Key!, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.First().ScalarValue!, StringComparer.Ordinal);

    /// <summary>What a nested call knows: what it was told, over what it inherited.</summary>
    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> inherited,
        Dictionary<string, string> own)
    {
        var merged = new Dictionary<string, string>(inherited, StringComparer.Ordinal);

        foreach (var (name, value) in own)
        {
            merged[name] = value;
        }

        return merged;
    }

    /// <summary>
    /// The colours the game names, as the icon scripts refer to them.
    /// </summary>
    /// <remarks>
    /// <c>common/named_colors</c> holds <c>name = { color = { r g b a } }</c>, which is a different
    /// shape from the flag colours and so needs its own reading. The mint a trait's background wears
    /// is <c>trait_bg_default</c> at 48, 223, 185; a drawback's red is <c>trait_bg_negative</c>.
    /// </remarks>
    public static Dictionary<string, (byte R, byte G, byte B, byte A)> ReadNamedColors(ScriptLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        var colors = new Dictionary<string, (byte, byte, byte, byte)>(StringComparer.Ordinal);

        foreach (var (_, document) in loader.LoadDirectory("common/named_colors"))
        {
            foreach (var node in document.Nodes)
            {
                if (node.Key is { Length: > 0 } name && node.Block?.GetList("color") is { Count: >= 3 } values &&
                    Channel(values, 0) is { } r && Channel(values, 1) is { } g && Channel(values, 2) is { } b)
                {
                    // Alpha is written for most and meant to be opaque when it is not.
                    colors[name] = (r, g, b, values.Count > 3 ? Channel(values, 3) ?? (byte)255 : (byte)255);
                }
            }
        }

        return colors;
    }

    private static byte? Channel(IReadOnlyList<string> values, int index) =>
        byte.TryParse(values[index], System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
}
