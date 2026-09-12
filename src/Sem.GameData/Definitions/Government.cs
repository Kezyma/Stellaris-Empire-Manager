namespace Sem.GameData;

/// <summary>An ethic.</summary>
public sealed record EthicDefinition(string Key, int Cost, string Category)
{
    /// <summary>
    /// Where this sits within its category. Opposing ethics share a category, so only one may be
    /// taken from each.
    /// </summary>
    public int CategoryValue { get; init; }

    /// <summary>The fanatic form of this ethic. Absent on an ethic that is already fanatic.</summary>
    public string? FanaticVariant { get; init; }

    /// <summary>The ordinary form of this ethic, present only on fanatic ethics.</summary>
    public string? RegularVariant { get; init; }

    /// <summary>True for gestalt consciousness, which cannot be combined with anything.</summary>
    public bool IsGestalt { get; init; }

    /// <summary>True when this is a fanatic ethic, which the game marks by having no fanatic form.</summary>
    public bool IsFanatic => FanaticVariant is null && !IsGestalt;

    /// <summary>What this ethic does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to the ethic's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    public string DescriptionKey => $"{Key}_desc";
}

/// <summary>A government authority.</summary>
public sealed record AuthorityDefinition(string Key)
{
    /// <summary>Whether the authority can be chosen, usually a check on owning an expansion.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>What the rest of the design must look like for this authority to be legal.</summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>Whether this authority is only ever used by the game's own empires.</summary>
    public bool AiOnly { get; init; }

    /// <summary>Traits this authority forces onto the founder species, such as <c>trait_hive_mind</c>.</summary>
    public IReadOnlyList<string> ForcedTraits { get; init; } = [];

    /// <summary>Whether the ruler has an heir.</summary>
    public bool HasHeir { get; init; }

    /// <summary>
    /// How rulers are chosen - <c>democratic</c>, <c>oligarchic</c> or <c>none</c>.
    /// </summary>
    /// <remarks>
    /// None where the authority says nothing, which is the game's own default and is written in the
    /// comment its file opens with. Left as nothing, an authority that simply has no election
    /// answered neither yes nor no to being asked - and four personalities ask.
    /// </remarks>
    public string ElectionType { get; init; } = "none";

    /// <summary>What this authority does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    /// <summary>Path to the authority's icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description.</summary>
    /// <remarks>
    /// <c>_desc</c>, as every other kind of choice uses, and as the game itself writes: the install
    /// has <c>auth_imperial_desc</c> and no <c>auth_imperial_tt</c>. This said <c>_tt</c>, which
    /// only the pruner ever asked for - so the pruner kept a key that does not exist, the real one
    /// was dropped as unused, and the designer asked at runtime for a description that had been
    /// thrown away. Seven of the eight authorities have one in the game; none of them reached the
    /// page.
    /// </remarks>
    public string DescriptionKey => $"{Key}_desc";
}

/// <summary>A civic, or an origin, which the game defines in the same files.</summary>
public sealed record CivicDefinition(string Key, bool IsOrigin)
{
    /// <summary>Whether this is offered at all, usually a check on owning content.</summary>
    public Requirement Playable { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// Whether this appears in the list for an empire of this shape. A civic that fails here is
    /// hidden rather than shown as blocked.
    /// </summary>
    public Requirement Potential { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// Whether this is legal given the rest of the design. A civic that fails here is shown but
    /// blocked, with the game's own explanation.
    /// </summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// Whether a government reform could add this to an empire that did not start with it.
    /// </summary>
    /// <remarks>
    /// The game's own field, which its comment describes as "set to no to prevent adding or
    /// removing this after creation of the empire". Ninety-six civics say no outright and
    /// thirty-three make it conditional; the rest, which is most of them, say nothing and mean yes.
    /// A civic that cannot be added is not offered in a plan, because a plan is about what an
    /// empire becomes.
    /// </remarks>
    public Requirement CanAddLater { get; init; } = new AlwaysRequirement(true);

    /// <summary>Whether a government reform could take this away again.</summary>
    /// <remarks>
    /// The other half of the same field. One that cannot be removed is shown in a plan but cannot
    /// be chosen or given up - it is already spending a slot, and saying so is the difference
    /// between a plan the game would accept and one it would not.
    /// </remarks>
    public Requirement CanRemoveLater { get; init; } = new AlwaysRequirement(true);

    /// <summary>Traits this forces onto the founder species.</summary>
    public IReadOnlyList<string> ForcedTraits { get; init; } = [];

    /// <summary>Traits an origin grants that the player may remove again.</summary>
    public IReadOnlyList<string> SoftTraits { get; init; } = [];

    /// <summary>What this does, and how the game describes it.</summary>
    public EffectSet Effects { get; init; } = EffectSet.None;

    // No separate list of trait-budget modifiers. There was one, copied out of the always-on
    // modifiers at extraction, and it lost every bonus the game states inside a swap. The budget is
    // worked out from Effects above, which holds the conditional ones too.

    /// <summary>The homeworld type an origin forces, such as a habitat for Void Dwellers.</summary>
    public string? StartingColony { get; init; }

    /// <summary>
    /// The climate an origin gives the founder species regardless of what was picked, such as
    /// ocean for Ocean Paradise. Traits tied to a homeworld type are judged against this.
    /// </summary>
    public string? HabitabilityPreference { get; init; }

    /// <summary>Starting systems an origin restricts the empire to.</summary>
    public IReadOnlyList<string> Initializers { get; init; } = [];

    /// <summary>Homeworld types this adds to the picker.</summary>
    public IReadOnlyList<string> AddedPlanetClasses { get; init; } = [];

    /// <summary>Homeworld types this removes from the picker.</summary>
    public IReadOnlyList<string> RemovedPlanetClasses { get; init; } = [];

    /// <summary>Traits the second species gets, when this origin adds one.</summary>
    public IReadOnlyList<string> SecondarySpeciesTraits { get; init; } = [];

    /// <summary>Whether this origin requires the player to design a second species.</summary>
    public bool RequiresSecondarySpecies { get; init; }

    /// <summary>Path to the icon within the extracted assets.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Path to the larger picture an origin carries, beside its icon.
    /// </summary>
    /// <remarks>
    /// Every origin has one, and in the game it is most of how an origin is presented: a scene of
    /// the world the empire wakes up on. Civics have none.
    /// </remarks>
    public string? Picture { get; init; }

    /// <summary>What this is called and said to be for particular kinds of empire.</summary>
    public IReadOnlyList<OptionVariant> Variants { get; init; } = [];

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;

    /// <summary>Localisation key for the description of what it does.</summary>
    public string? EffectsKey { get; init; }

    /// <summary>Localisation key for the description of its drawbacks.</summary>
    public string? PenaltiesKey { get; init; }
}

/// <summary>
/// A government type. The game picks the highest-weighted one whose conditions the design meets,
/// which is how an empire ends up called a Divine Empire rather than a Despotic Hegemony.
/// </summary>
public sealed record GovernmentTypeDefinition(string Key, double Weight, int FileOrder)
{
    /// <summary>What the design must look like for this government to apply.</summary>
    public Requirement Possible { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// What multiplies its weight, and when.
    /// </summary>
    /// <remarks>
    /// Thirteen governments raise their own weight against a civic - a militarist empire with
    /// Distinguished Admiralty is twice as likely to be called a Star Empire, and a Cybernetic Creed
    /// one with Exalted Priesthood twice as likely to be a Holy Tribunal. Reading only the base left
    /// those thirteen competing at half their real weight, and the government decides the empire's
    /// title and every name the game would generate for it.
    /// </remarks>
    public IReadOnlyList<WeightFactor> Factors { get; init; } = [];

    /// <summary>Localisation key for the ruler's title.</summary>
    public string? RulerTitleKey { get; init; }

    /// <summary>Localisation key for the female form of the ruler's title.</summary>
    public string? RulerTitleFemaleKey { get; init; }

    /// <summary>
    /// What this government calls the ruler's heir, where power passes to one.
    /// </summary>
    /// <remarks>
    /// Thirty-one governments name one and twenty-seven name a female form, and none of them was
    /// being read - so the designer offered ruler titles in the heir's box and showed no default
    /// behind it, while the game had an answer for both.
    /// </remarks>
    public string? HeirTitleKey { get; init; }

    /// <summary>Localisation key for the female form of the heir's title.</summary>
    public string? HeirTitleFemaleKey { get; init; }

    /// <summary>
    /// The ruler's title for a ruler of this gender, as the game would write it.
    /// </summary>
    /// <remarks>
    /// A hundred and twenty-eight of the hundred and seventy governments name a female form -
    /// Empress for Emperor, Matriarch for Patriarch - and it was extracted, kept through pruning and
    /// then never read, so every female ruler who had not been given a title by hand was shown the
    /// male one. Only <c>female</c> takes the second form: the game has four genders and writes two
    /// titles, so the unset and indeterminable cases are the plain one.
    /// </remarks>
    /// <param name="gender">The ruler's gender, as the design stores it.</param>
    public string? RulerTitleFor(string? gender) =>
        string.Equals(gender, "female", StringComparison.Ordinal)
            ? RulerTitleFemaleKey ?? RulerTitleKey
            : RulerTitleKey;

    /// <summary>Localisation key for the display name.</summary>
    public string NameKey => Key;
}

/// <summary>
/// One of the personalities the game hands to an AI empire.
/// </summary>
/// <remarks>
/// <para>
/// Not a thing anybody picks, and not a thing an empire is - it is what an empire will be played as
/// when it turns up in a galaxy as somebody else's neighbour. Which makes it the same kind of fact
/// as the government: read off the ethics, the civics and the rest rather than stored anywhere.
/// </para>
/// <para>
/// And unlike the government it is not settled. The game keeps every personality the empire allows
/// and draws one, weighted - so an empire has a set of them with odds, not a name.
/// </para>
/// </remarks>
/// <param name="Key">What the game calls it.</param>
/// <param name="Weight">Its base weight in that draw.</param>
/// <param name="FileOrder">Where it was defined, which is the only stable order these have.</param>
public sealed record PersonalityDefinition(string Key, double Weight, int FileOrder)
{
    /// <summary>What the empire has to be for this to be one of the ones drawn from.</summary>
    public Requirement Allow { get; init; } = new AlwaysRequirement(true);

    /// <summary>
    /// What is added to the weight, and when.
    /// </summary>
    /// <remarks>
    /// Added, not multiplied. The game says so itself, at the top of its own file - "NOTE: Weight
    /// is additive!" - and it is the one place these differ from the governments, whose factors
    /// multiply. <see cref="WeightFactor"/> is shared with them, so read its number as an addition
    /// here.
    /// </remarks>
    public IReadOnlyList<WeightFactor> Additions { get; init; } = [];

    /// <summary>
    /// What it is called.
    /// </summary>
    /// <remarks>
    /// Under a prefix rather than under its own key, which is the one thing about these that is not
    /// the usual convention. Fifty of the fifty-one are named this way, with a description beside
    /// them; the odd one out belongs to a fallen empire and no design reaches it.
    /// </remarks>
    public string NameKey => $"personality_{Key}";

    /// <summary>And where its description is written.</summary>
    public string DescriptionKey => $"{NameKey}_desc";
}

/// <summary>Something that multiplies a weight when its condition holds.</summary>
/// <param name="When">The condition.</param>
/// <param name="Factor">What the weight is multiplied by while it does.</param>
public sealed record WeightFactor(Requirement When, double Factor);

/// <summary>
/// What an option is called and said to be for one particular kind of empire.
/// </summary>
/// <remarks>
/// <para>
/// The game's swaps do two things at once. The numbers they change were read already, as conditional
/// effects; the words they change were not, and there are more of those: a hundred and eighty-two
/// traditions, thirty-two civics and origins and six ascension perks are shown under a different
/// name to the empire that triggers them, and forty-one civics under a different description.
/// </para>
/// <para>
/// The conditions are mostly things a design settles outright - hive, machine, wilderness, nomadic,
/// an origin - so this is not guesswork. A hive mind reading its own tradition trees was being shown
/// the wording written for somebody else throughout, and Natural Neural Network read as the hive
/// version to a wilderness empire whose game calls it something else.
/// </para>
/// </remarks>
/// <param name="When">Which empires are shown this wording.</param>
/// <param name="NameKey">What to call it, or null to keep the option's own name.</param>
/// <param name="DescriptionKey">What to say about it, or null to keep the option's own.</param>
public sealed record OptionVariant(Requirement When, string? NameKey, string? DescriptionKey)
{
    /// <summary>
    /// The drawbacks this form of the option carries, where they are not the option's own.
    /// </summary>
    /// <remarks>
    /// Two origins say. Arc Welders names one set of drawbacks and another for a nomad; Life-Seeded
    /// names another for a machine. A third swap declares the same key its option already has, so
    /// it changes nothing and reads the same either way.
    /// </remarks>
    public string? PenaltyKey { get; init; }
}
