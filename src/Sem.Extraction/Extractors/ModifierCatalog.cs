using Sem.Clausewitz;
using Sem.GameData;

namespace Sem.Extraction.Extractors;

/// <summary>
/// Reads what the game knows about how a modifier should be displayed.
/// </summary>
/// <remarks>
/// <para>
/// Whether a modifier is a proportion or a flat amount is not something its name or its label
/// reveals. The clearest proof is naval capacity, where the flat and proportional forms share a
/// single label string — one renders "+4" and the other "+4%" from identical text — so the
/// distinction cannot be coming from localisation.
/// </para>
/// <para>
/// Three sources, in descending order of authority. A few dozen modifiers state their own settings
/// in <c>common/scripted_modifiers</c> and those are taken as given. A short table below settles the
/// families the game states nowhere and a name cannot decide, each entry with its evidence. The rest
/// are inferred, and the number of guesses is reported so the inaccuracy is visible rather than
/// silent.
/// </para>
/// <para>
/// The debug log used to be a fourth. It is not one: the table the game writes with
/// <c>-debug_mode</c> is a list of names and categories - "army_health, Category: Armies" - and says
/// nothing about how a value is displayed, which is the only question here. It was also being looked
/// for in the wrong place, since a Documents folder redirected into OneDrive is not where the
/// locator looks. Both of those were true at once, so the path had never run.
/// </para>
/// </remarks>
public sealed class ModifierCatalog
{
    private readonly Dictionary<string, ModifierInfo> _known;

    private ModifierCatalog(Dictionary<string, ModifierInfo> known)
    {
        _known = known;
    }

    /// <summary>An empty catalogue, for callers with no installation to read.</summary>
    public static ModifierCatalog Empty { get; } =
        new(new Dictionary<string, ModifierInfo>(StringComparer.OrdinalIgnoreCase));

    /// <summary>How many modifiers stated their own display settings.</summary>
    public int Count => _known.Count;

    /// <summary>Reads the settings an installation declares.</summary>
    public static ModifierCatalog Read(LayeredContent content, string? installRoot = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var known = new Dictionary<string, ModifierInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in content.EnumerateFiles("common/scripted_modifiers"))
        {
            CwDocument document;

            try
            {
                document = CwDocument.Parse(content.Read(path), CwParseOptions.Lenient);
            }
            catch (Exception ex) when (ex is CwSyntaxException or IOException)
            {
                continue;
            }

            foreach (var node in document.Nodes)
            {
                if (node.Key is { Length: > 0 } key && node.Block is { } body)
                {
                    known[key] = new ModifierInfo(
                        IsPercentage: body.GetBool("percentage"),
                        IsGood: body.GetBool("good"),
                        IsNeutral: body.GetBool("neutral"),
                        Decimals: int.TryParse(body.GetString("max_decimals"), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 2,
                        Declared: true);
                }
            }
        }

        return new ModifierCatalog(known);
    }

    /// <summary>
    /// How a modifier should be displayed, inferring it when the game does not say.
    /// </summary>
    /// <param name="key">The modifier's name.</param>
    /// <param name="observedValues">
    /// The values the game's own script gives this modifier. These settle most of the cases a name
    /// alone cannot, because the two kinds are written differently: a proportion is a fraction of
    /// one, while a flat amount is a whole number of whatever it counts.
    /// </param>
    public ModifierInfo Describe(string key, IEnumerable<double>? observedValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_known.TryGetValue(key, out var known))
        {
            return known;
        }

        if (Settled(key) is { } decided)
        {
            return new ModifierInfo(
                IsPercentage: decided,
                IsGood: !LooksLikeACost(key),
                IsNeutral: false,
                Decimals: 2,
                Declared: true);
        }

        return new ModifierInfo(
            IsPercentage: InferPercentage(key, observedValues),
            IsGood: !LooksLikeACost(key),
            IsNeutral: false,
            Decimals: 2,
            Declared: false);
    }

    /// <summary>
    /// The families the game states nowhere and a name cannot decide, settled by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short on purpose. Everything with a <c>_mult</c> or <c>_add</c> ending decides itself, which
    /// is two thirds of them, and most of the rest are settled by the numbers the game gives them.
    /// These are the ones where the numbers mislead, and each is here with a reason that can be
    /// checked rather than because it looked wrong.
    /// </para>
    /// <para>
    /// Habitability is stated outright: the game's own
    /// <c>POP_MAX_GROWTH_HABITABILITY: "Habitability: $VALUE|0=-%$"</c> carries the percent code. It
    /// is here because eight of the twenty world classes name a value of 1 or -2 somewhere - a
    /// perfectly ordinary +100% or -200% - and that was enough to have Gaia and Machine worlds
    /// showing "+1" while Ocean and Tundra showed "+20%", the same number in the same list drawn two
    /// ways.
    /// </para>
    /// <para>
    /// Loyalty is stated as plainly and the other way round:
    /// <c>$MOD_MONTHLY_LOYALTY_GAIN$: §G+1§!</c> in the game's own description of the Tree of Life
    /// holding, a whole number and no percent sign. Both loyalty modifiers take fractional values,
    /// which is what had one of them showing as a proportion.
    /// </para>
    /// <para>
    /// The rest are families whose other members are already settled, and which no flat amount could
    /// be: an army at -0.06 of its health, a leader at -0.13 of their experience, a wreck with -0.5
    /// of a chance of leaving debris.
    /// </para>
    /// </remarks>
    private static bool? Settled(string key) =>
        key.EndsWith("_habitability", StringComparison.OrdinalIgnoreCase) ? true
        : key.StartsWith("monthly_loyalty", StringComparison.OrdinalIgnoreCase) ? false
        : key is "army_health" or "army_morale" or "species_leader_exp_gain"
            or "intel_gain_speed" or "create_debris_chance" ? true
        : null;

    /// <summary>
    /// Works out whether a modifier is a proportion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The suffixes are decisive where they appear, and about two thirds of modifiers carry one.
    /// For the rest the values decide: <c>faction_approval = 0.10</c> is ten percent while
    /// <c>country_leader_pool_size = 1</c> is one extra leader, and no rule based on the name can
    /// tell those apart — which is why the suffix alone gets both of them wrong.
    /// </para>
    /// <para>
    /// One, two and their negatives say nothing and are set aside before the question is asked. A
    /// proportion writes a doubling as 1, and a count writes one more of something the same way, so
    /// those numbers are the one place the two kinds look alike. Requiring every value to be a
    /// fraction meant a single <c>army_health = 1</c> among thirty fractions decided the whole
    /// modifier was a flat amount, and every army bonus in the app then read "+0.2".
    /// </para>
    /// <para>
    /// Fractions are evidence only among the endings that have none. Sixty-two of the hundred and
    /// fifty-two <c>_add</c> modifiers the game ships take fractional values - half a unit of food
    /// per job is still half a unit - so this rule would be wrong about two flat modifiers in five
    /// if the suffixes did not answer first.
    /// </para>
    /// </remarks>
    private static bool InferPercentage(string key, IEnumerable<double>? observedValues)
    {
        if (key.EndsWith("_mult", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (key.EndsWith("_add", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = observedValues?
            .Where(v => v != 0 && Math.Abs(v) != 1 && Math.Abs(v) != 2)
            .ToList();

        return values is { Count: > 0 } && values.All(v => v != Math.Truncate(v));
    }

    /// <summary>
    /// Whether a lower number is the better one, which is true of anything an empire pays.
    /// </summary>
    private static bool LooksLikeACost(string key) =>
        key.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("upkeep", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("_time", StringComparison.OrdinalIgnoreCase);
}
