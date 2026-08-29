using Sem.Designs;
using Sem.GameData;

namespace Sem.Core.Tests.Rules;

/// <summary>
/// A small hand-built game database, so the rules can be tested without a Stellaris installation
/// and each test can state exactly the situation it is about.
/// </summary>
internal static class RulesTestData
{
    public static GameDatabase Database { get; } = Build();

    /// <summary>Builds a valid empire that individual tests then break in one specific way.</summary>
    public static EmpireDesign ValidEmpire()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("Test Empire");

        design.Species.Class = "MAM";
        design.Species.Portrait = "mam1";
        design.Species.SetTraits(["trait_organic", "trait_intelligent", "trait_deviants"]);
        design.Authority = "auth_democratic";
        design.Origin = "origin_default";
        design.PlanetClass = "pc_continental";
        design.SetEthics(["ethic_fanatic_militarist", "ethic_xenophile"]);
        design.SetCivics(["civic_beacon_of_liberty", "civic_functional_architecture"]);

        return design;
    }

    private static GameDatabase Build() => new()
    {
        SchemaVersion = 1,
        GameVersion = "test",
        ExtractorVersion = "test",
        Defines = new GameDefines { EthicsPoints = 3, CivicPoints = 2 },

        Dlc =
        [
            new DlcDefinition("dlc014_utopia", "Utopia", null, "expansion", Installed: true),
            new DlcDefinition("dlc026_aquatics", "Aquatics Species Pack", null, "species_pack", Installed: false),
        ],

        Archetypes =
        [
            new ArchetypeDefinition("BIOLOGICAL", TraitPoints: 2, MaxTraits: 5, IsRobotic: false),
            new ArchetypeDefinition("MACHINE", TraitPoints: 1, MaxTraits: 5, IsRobotic: true),
        ],

        SpeciesClasses =
        [
            new SpeciesClassDefinition("MAM", "BIOLOGICAL") { ForcedTrait = "trait_organic" },
            new SpeciesClassDefinition("MACHINE", "MACHINE")
            {
                ForcedTrait = "trait_machine_unit",

                // Machines cannot be a hive mind, which is the shape the real game uses too.
                Possible = new SelectionRequirement(SelectionCategory.Authority, "auth_hive_mind")
                {
                    FailureText = "SPECIES_CLASS_MUST_NOT_USE_HIVE_MIND",
                }.Negated(),
            },
            new SpeciesClassDefinition("PSIONIC", null),
        ],

        Ethics =
        [
            Ethic("ethic_militarist", 1, "mil", fanatic: "ethic_fanatic_militarist"),
            Ethic("ethic_fanatic_militarist", 2, "mil", regular: "ethic_militarist"),
            Ethic("ethic_pacifist", 1, "mil", fanatic: "ethic_fanatic_pacifist"),
            Ethic("ethic_fanatic_pacifist", 2, "mil", regular: "ethic_pacifist"),
            Ethic("ethic_xenophile", 1, "xen", fanatic: "ethic_fanatic_xenophile"),
            Ethic("ethic_fanatic_xenophile", 2, "xen", regular: "ethic_fanatic_xenophile"),
            new EthicDefinition("ethic_gestalt_consciousness", 3, "hive") { IsGestalt = true },
        ],

        Authorities =
        [
            new AuthorityDefinition("auth_democratic"),
            new AuthorityDefinition("auth_hive_mind")
            {
                ForcedTraits = ["trait_hive_mind"],
                Possible = new SelectionRequirement(SelectionCategory.Ethics, "ethic_gestalt_consciousness")
                {
                    FailureText = "AUTHORITY_REQUIRES_GESTALT",
                },
            },
            new AuthorityDefinition("auth_corporate")
            {
                Playable = new DlcRequirement("Megacorp"),
            },
        ],

        Civics =
        [
            new CivicDefinition("civic_beacon_of_liberty", IsOrigin: false),
            new CivicDefinition("civic_functional_architecture", IsOrigin: false),
            new CivicDefinition("civic_needs_utopia", IsOrigin: false)
            {
                Playable = new DlcRequirement("Utopia"),
            },
            new CivicDefinition("civic_gestalt_only", IsOrigin: false)
            {
                Potential = new SelectionRequirement(SelectionCategory.Ethics, "ethic_gestalt_consciousness"),
            },
            new CivicDefinition("civic_not_with_beacon", IsOrigin: false)
            {
                Possible = new SelectionRequirement(SelectionCategory.Civics, "civic_beacon_of_liberty")
                {
                    FailureText = "civic_tooltip_not_beacon",
                }.Negated(),
            },
            new CivicDefinition("origin_default", IsOrigin: true),
            new CivicDefinition("origin_void_dwellers", IsOrigin: true)
            {
                StartingColony = "pc_habitat",
                Initializers = ["void_dweller_system"],
                ForcedTraits = ["trait_void_dweller_1"],
            },
            new CivicDefinition("origin_syncretic_evolution", IsOrigin: true)
            {
                RequiresSecondarySpecies = true,
                SecondarySpeciesTraits = ["trait_syncretic_proles"],
            },
            new CivicDefinition("civic_natural_design", IsOrigin: false)
            {
                TraitBudgetModifiers = new Dictionary<string, double>
                {
                    ["BIOLOGICAL_species_trait_points_add"] = 2,
                    ["BIOLOGICAL_species_trait_picks_add"] = 2,
                },
            },
        ],

        GovernmentTypes =
        [
            new GovernmentTypeDefinition("gov_fallback", Weight: 1, FileOrder: 0),
            new GovernmentTypeDefinition("gov_democratic", Weight: 10, FileOrder: 1)
            {
                Possible = new SelectionRequirement(SelectionCategory.Authority, "auth_democratic"),
            },
            new GovernmentTypeDefinition("gov_beacon", Weight: 1000, FileOrder: 2)
            {
                Possible = new SelectionRequirement(SelectionCategory.Civics, "civic_beacon_of_liberty"),
            },
            new GovernmentTypeDefinition("gov_beacon_tie", Weight: 1000, FileOrder: 3)
            {
                Possible = new SelectionRequirement(SelectionCategory.Civics, "civic_beacon_of_liberty"),
            },
        ],

        PlanetClasses =
        [
            new PlanetClassDefinition("pc_continental") { IsStartingWorld = true, Climate = "wet" },
            new PlanetClassDefinition("pc_ocean") { IsStartingWorld = true, Climate = "wet" },
            new PlanetClassDefinition("pc_volcanic")
            {
                IsStartingWorld = true,
                Climate = "dry",
                Potential = new DlcRequirement("Infernals Species Pack"),
            },
            new PlanetClassDefinition("pc_habitat"),
        ],

        Initializers =
        [
            new InitializerDefinition("custom_starting_init_01", InitializerUsage.CustomEmpire),
            new InitializerDefinition("void_dweller_system", InitializerUsage.Origin),
        ],

        Traits =
        [
            new TraitDefinition("trait_organic", TraitKind.Species) { Cost = 0, AllowedArchetypes = ["BIOLOGICAL"] },
            new TraitDefinition("trait_machine_unit", TraitKind.Species) { Cost = 0, AllowedArchetypes = ["MACHINE"] },
            new TraitDefinition("trait_intelligent", TraitKind.Species)
            {
                Cost = 2,
                AllowedArchetypes = ["BIOLOGICAL"],
                Opposites = ["trait_nerve_stapled"],
            },
            new TraitDefinition("trait_nerve_stapled", TraitKind.Species)
            {
                Cost = -2,
                AllowedArchetypes = ["BIOLOGICAL"],
                Opposites = ["trait_intelligent"],
            },
            new TraitDefinition("trait_deviants", TraitKind.Species) { Cost = -1, AllowedArchetypes = ["BIOLOGICAL"] },

            // Dear, and nothing else stands in its way, so the budget is the only thing that can.
            new TraitDefinition("trait_expensive", TraitKind.Species) { Cost = 3, AllowedArchetypes = ["BIOLOGICAL"] },
            new TraitDefinition("trait_aquatic", TraitKind.Species)
            {
                Cost = 2,
                AllowedArchetypes = ["BIOLOGICAL"],
                AllowedPlanetClasses = ["pc_ocean"],
                RequiredDlc = "Aquatics Species Pack",
            },
            new TraitDefinition("trait_psionic_only", TraitKind.Species)
            {
                Cost = 2,
                AllowedArchetypes = ["BIOLOGICAL"],
                AllowedSpeciesClasses = ["PSIONIC"],
                PortraitOverride = ["mam_rat"],
            },
            new TraitDefinition("trait_overtuned_only", TraitKind.Species)
            {
                Cost = 1,
                AllowedArchetypes = ["BIOLOGICAL"],
                AllowedOrigins = ["origin_overtuned"],
            },
            new TraitDefinition("trait_not_gestalt", TraitKind.Species)
            {
                Cost = 1,
                AllowedArchetypes = ["BIOLOGICAL"],
                ForbiddenEthics = ["ethic_gestalt_consciousness"],
            },
            new TraitDefinition("trait_hidden", TraitKind.Species) { Cost = 1, Hidden = true },
            new TraitDefinition("trait_not_initial", TraitKind.Species) { Cost = 1, Initial = false },
        ],
    };

    private static EthicDefinition Ethic(string key, int cost, string category, string? fanatic = null, string? regular = null) =>
        new(key, cost, category) { FanaticVariant = fanatic, RegularVariant = regular };
}

/// <summary>Small helpers for writing conditions in tests.</summary>
internal static class RequirementExtensions
{
    /// <summary>Wraps a condition in a negation, keeping the explanation on the outside.</summary>
    public static Requirement Negated(this Requirement requirement) =>
        new NotRequirement(requirement with { FailureText = null })
        {
            FailureText = requirement.FailureText,
        };
}
