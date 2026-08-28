using System.Text.Json.Serialization;

namespace Sem.GameData;

/// <summary>
/// A condition an empire design must satisfy, compiled from the game's script at extraction time
/// and evaluated against the player's current selections at runtime.
/// </summary>
/// <remarks>
/// <para>
/// The game expresses conditions in three different grammars: the requirements list used by
/// <c>potential</c> and <c>possible</c> on authorities, civics and origins; ordinary triggers used
/// by <c>playable</c> and <c>selectable</c>; and scripted values used by weights. All three are
/// compiled into this one shape so the designer has a single thing to evaluate.
/// </para>
/// <para>
/// DLC checks are deliberately left as conditions rather than resolved during extraction. Which
/// packs are owned is a fact about the person using the app, not about the game files, and the web
/// build has to let them say so.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(AlwaysRequirement), "always")]
[JsonDerivedType(typeof(AllRequirement), "all")]
[JsonDerivedType(typeof(AnyRequirement), "any")]
[JsonDerivedType(typeof(NotRequirement), "not")]
[JsonDerivedType(typeof(SelectionRequirement), "selection")]
[JsonDerivedType(typeof(DlcRequirement), "dlc")]
[JsonDerivedType(typeof(FieldRequirement), "field")]
[JsonDerivedType(typeof(PredicateRequirement), "predicate")]
[JsonDerivedType(typeof(UnknownRequirement), "unknown")]
public abstract record Requirement
{
    /// <summary>
    /// Localisation key explaining why this condition failed, taken from the <c>text</c> the game
    /// script supplies. This is the message the game itself shows, so the designer can explain a
    /// blocked option in the player's own language rather than inventing wording.
    /// </summary>
    public string? FailureText { get; init; }
}

/// <summary>A condition that is always true or always false.</summary>
public sealed record AlwaysRequirement(bool Value) : Requirement;

/// <summary>Every child must hold.</summary>
public sealed record AllRequirement(IReadOnlyList<Requirement> Items) : Requirement;

/// <summary>At least one child must hold.</summary>
public sealed record AnyRequirement(IReadOnlyList<Requirement> Items) : Requirement;

/// <summary>The child must not hold. The game writes this as <c>NOT</c> or <c>NOR</c>.</summary>
public sealed record NotRequirement(Requirement Item) : Requirement;

/// <summary>Something the player has selected must match a given key.</summary>
public sealed record SelectionRequirement(SelectionCategory Category, string Key) : Requirement;

/// <summary>A downloadable content pack must be owned, named exactly as the game names it.</summary>
public sealed record DlcRequirement(string Name) : Requirement;

/// <summary>
/// A plain field on the design must have a given value, such as <c>is_nomadic = no</c>.
/// </summary>
public sealed record FieldRequirement(string Field, string Value) : Requirement;

/// <summary>
/// A named condition about the design as a whole, such as <c>is_gestalt</c>, which the rules
/// engine works out from the current selections.
/// </summary>
public sealed record PredicateRequirement(string Name) : Requirement;

/// <summary>
/// A condition the extractor did not recognise, kept so a game patch that introduces new script
/// degrades into a visible warning rather than a wrong answer or a crash.
/// </summary>
/// <param name="Name">The trigger or key that was not understood.</param>
/// <param name="Assume">
/// What to treat it as while evaluating. True by default, so unrecognised script never hides an
/// option the player should be able to pick.
/// </param>
public sealed record UnknownRequirement(string Name, bool Assume = true) : Requirement;

/// <summary>What part of an empire design a <see cref="SelectionRequirement"/> refers to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SelectionCategory>))]
public enum SelectionCategory
{
    /// <summary>One of the empire's ethics.</summary>
    Ethics,

    /// <summary>The empire's authority.</summary>
    Authority,

    /// <summary>One of the empire's civics.</summary>
    Civics,

    /// <summary>The empire's origin.</summary>
    Origin,

    /// <summary>The founder species' archetype, such as <c>BIOLOGICAL</c> or <c>MACHINE</c>.</summary>
    SpeciesArchetype,

    /// <summary>The founder species' class, such as <c>MAM</c> or <c>LITHOID</c>.</summary>
    SpeciesClass,

    /// <summary>One of the founder species' traits.</summary>
    Traits,

    /// <summary>The homeworld's planet class.</summary>
    PreferredPlanetClass,

    /// <summary>The empire's ship appearance set.</summary>
    GraphicalCulture,

    /// <summary>
    /// The kind of country. Only meaningful in game; a design in the editor is always
    /// <c>default</c>.
    /// </summary>
    CountryType,
}
