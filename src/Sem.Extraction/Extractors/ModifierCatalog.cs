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
                        Declared: true) { Settled = true };
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
                IsGood: !LooksBad(key),
                IsNeutral: false,
                Decimals: 2,
                Declared: true) { Settled = true };
        }

        return new ModifierInfo(
            IsPercentage: InferPercentage(key, observedValues),
            IsGood: !LooksBad(key),
            IsNeutral: false,
            Decimals: 2,
            Declared: false)
        {
            Settled = HasEvidence(key, observedValues),
        };
    }

    /// <summary>
    /// Whether the numbers a modifier is written with say anything, one way or the other.
    /// </summary>
    /// <remarks>
    /// An ending is evidence in itself. Otherwise the values have to agree: all fractions, or all
    /// whole numbers once the uninformative ones are set aside. Nothing left after that is not
    /// agreement but silence, and silence is what this exists to catch - six modifiers reach here
    /// written only as 1, 2 and -1, which says nothing about whether they count things or divide
    /// them.
    /// </remarks>
    private static bool HasEvidence(string key, IEnumerable<double>? observedValues)
    {
        if (key.EndsWith("_mult", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith("_add", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var values = observedValues?
            .Where(v => v != 0 && Math.Abs(v) != 1 && Math.Abs(v) != 2)
            .ToList();

        return values is { Count: > 0 } &&
            (values.All(v => v != Math.Truncate(v)) || values.All(v => v == Math.Truncate(v)));
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
    /// The rest have no statement anywhere and are settled on the game's own evidence rather than on
    /// how they look. Three things say it together. Neither <c>army_health</c> nor
    /// <c>species_leader_exp_gain</c> has a <c>_mult</c> twin anywhere in the files, so the plain
    /// form is the proportional one - where <c>army_damage</c> does have <c>army_damage_mult</c>,
    /// and it is the suffixed one a trait actually uses. An unsuffixed percentage is ordinary here:
    /// of the seventy-six modifiers the game declares <c>percentage = yes</c>, many carry no ending
    /// at all, and not one of the twenty-seven it declares flat ends in <c>_mult</c>, so the suffix
    /// rule is never contradicted by a statement. And the numbers are fractions of one across every
    /// use - thirty-six of army health, a hundred and nine of leader experience, none above 1.0,
    /// most between 0.05 and 0.4. The repeatable armour technologies grant <c>army_health = 0.05</c>,
    /// which is five per cent of an army; as a flat amount it is a twentieth of one hit point.
    /// </para>
    /// <para>
    /// The counters at the end are here for the opposite reason - not because the numbers mislead
    /// but because they say nothing at all. Every value any of them takes is 1, 2 or -1, which is
    /// exactly what <see cref="InferPercentage"/> sets aside, so they reach it with no evidence and
    /// it has to fall back on a default. Naming them is what lets that fallback be an error instead:
    /// <c>NothingIsLeftToAGuess</c> fails the build if a modifier a design can show arrives with
    /// nothing to settle it and no entry here.
    /// </para>
    /// </remarks>
    private static bool? Settled(string key) =>
        key.EndsWith("_habitability", StringComparison.OrdinalIgnoreCase) ? true
        : key.StartsWith("monthly_loyalty", StringComparison.OrdinalIgnoreCase) ? false
        : key is "army_health" or "army_morale" or "species_leader_exp_gain"
            or "intel_gain_speed" or "create_debris_chance" ? true
        : key is "leader_initial_skill" or "official_initial_skill" or "scientist_initial_skill"
            or "commander_initial_skill" or "country_leader_pool_size" or "max_rivalries" ? false
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
    /// Whether a lower number is the better one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game does not declare this for anything it defines in code, which is nearly everything a
    /// design can show - the <c>good</c> field exists, defaults to no, and appears only on the few
    /// dozen modifiers the script files invent. So it is inferred, and the first three families are
    /// the obvious ones: an empire pays a cost, pays an upkeep, and waits out a time.
    /// </para>
    /// <para>
    /// The rest were found by reading the game's own descriptions rather than by judgement. Its
    /// localisation colours values by hand with §G and §R, and across every English file there are
    /// twenty-six modifiers it writes only as red-when-positive - Psionic Theory's
    /// <c>$MOD_EMPIRE_SIZE_POPS_MULT$: §G-10%§!</c>, the Cave Dweller trait's
    /// <c>$MOD_SPECIES_EMPIRE_SIZE_MULT$: §R+10%§!</c>, and so on - and not one that it writes both
    /// ways. Sixteen of the twenty-six are modifiers this app ships. Two of those were already
    /// costs; the families below are the other fourteen, and
    /// <c>EveryModifierIsColouredTheWayTheGameColoursIt</c> is what keeps this list honest against
    /// the next patch.
    /// </para>
    /// <para>
    /// War exhaustion is the one family here the test cannot confirm, because both descriptions of
    /// it print a scripted variable rather than a number and the sign in the text is then the
    /// localiser's rather than the value's. It is kept on the same evidence read less strictly:
    /// both write it red-when-positive.
    /// </para>
    /// <para>
    /// Named as families rather than as keys because a family is what the evidence is about -
    /// amenities usage, housing usage and naval capacity usage are one idea, and a patch adding a
    /// fourth should not need this file edited. The two on the end have no family: a planet taking
    /// bombardment damage is not the same modifier as a ship dealing it, and
    /// <c>ship_orbital_bombardment_mult</c> is a bonus, so "bombardment" cannot be the rule.
    /// </para>
    /// </remarks>
    private static bool LooksBad(string key) =>
        Families.Any(family => key.Contains(family, StringComparison.OrdinalIgnoreCase)) ||
        key is "planet_orbital_bombardment_damage" or "storm_ship_hull_breaker_mult";

    private static readonly string[] Families =
    [
        "cost",
        "upkeep",
        "_time",
        "empire_size",
        "crime",
        "_usage",
        "war_exhaustion",
        "trade_fee",
    ];
}
