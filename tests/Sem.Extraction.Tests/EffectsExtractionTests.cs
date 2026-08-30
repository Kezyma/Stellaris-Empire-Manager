using Sem.Clausewitz;
using Sem.Extraction.Extractors;
using Sem.GameData;
using Sem.Io;

namespace Sem.Extraction.Tests;

/// <summary>
/// An empire designer that cannot say what an option does is not doing its job, so these check that
/// the numbers come out, that the conditional ones are kept apart from the rest, and that an option
/// which describes itself in its own words does not also have its numbers listed underneath.
/// </summary>
public sealed class EffectsExtractionTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    private static EffectSet Read(string script, string? tagsKey = null)
    {
        var document = CwDocument.Parse(System.Text.Encoding.UTF8.GetBytes(script), CwParseOptions.Lenient);
        var body = document.Nodes[0].Block!;

        return EffectsReader.Read(
            body,
            new ScriptLoader(LayeredContent.ForInstall(InstallRoot ?? Environment.CurrentDirectory)),
            new RequirementCompiler(),
            tagsKey);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ReadsAnEthicsModifiersConditionalModifiersAndCapabilities()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var effects = Read(
            """
            ethic_militarist = {
                cost = 1
                country_modifier = {
                    ship_fire_rate_mult = 0.10
                }
                triggered_country_modifier = {
                    potential = { is_nomadic = no }
                    country_claim_influence_cost_mult = -0.1
                }
                triggered_country_modifier = {
                    potential = { is_nomadic = yes }
                    scavenge_debris_mult = 0.05
                }
                tags = { ETHIC_ALLOW_NO_RETREAT }
            }
            """,
            tagsKey: "tags");

        Assert.Equal(0.10, effects.Modifiers["ship_fire_rate_mult"], tolerance: 0.0001);

        // The cost is the ethic's price, not something it grants.
        Assert.DoesNotContain("cost", effects.Modifiers.Keys);

        // Two separate conditions, not one merged list: they contradict each other, so an empire
        // gets one or the other and never both.
        Assert.Equal(2, effects.Conditional.Count);
        Assert.Contains(effects.Conditional, c => c.Modifiers.ContainsKey("country_claim_influence_cost_mult"));
        Assert.Contains(effects.Conditional, c => c.Modifiers.ContainsKey("scavenge_debris_mult"));

        Assert.Equal(["ETHIC_ALLOW_NO_RETREAT"], effects.TagKeys);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnOptionThatRestatesItsNumbersInItsOwnWordsDoesNotAlsoListThem()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // This is auth_democratic. Its tooltip spells out both modifiers by hand, because declaring
        // one inside the block suppresses the automatic list. Listing both would show +10% twice.
        var effects = Read(
            """
            auth_democratic = {
                country_modifier = {
                    faction_approval = 0.10
                    country_leader_pool_size = 1
                    custom_tooltip = auth_democratic_tt
                }
            }
            """);

        Assert.Equal("auth_democratic_tt", effects.TooltipKey);
        Assert.True(effects.TooltipReplacesModifiers);
        Assert.Empty(effects.VisibleModifiers);

        // The values are still known, just not listed, so a combined total can still count them.
        Assert.Equal(2, effects.Modifiers.Count);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AnOptionMayAskForBothItsWordsAndItsNumbers()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var optedIn = Read(
            """
            auth_cyber = {
                country_modifier = {
                    custom_tooltip = auth_cyber_tt
                    show_only_custom_tooltip = no
                    pop_cyborg_happiness = 0.2
                }
            }
            """);

        Assert.False(optedIn.TooltipReplacesModifiers);
        Assert.Single(optedIn.VisibleModifiers);

        // The other field appends by definition and never replaces.
        var appended = Read(
            """
            trait_void_dweller_1 = {
                custom_tooltip_with_modifiers = void_dweller_trait_tooltip
                modifier = { pop_happiness = 0.1 }
            }
            """);

        Assert.Equal("void_dweller_trait_tooltip", appended.TooltipKey);
        Assert.False(appended.TooltipReplacesModifiers);
        Assert.Single(appended.VisibleModifiers);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ModifiersHiddenByTheGameStayHidden()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var effects = Read(
            """
            civic_quiet = {
                hide_modifiers = yes
                modifier = { pop_happiness = 0.1 }
                description = civic_quiet_effects
                negative_description = civic_quiet_penalties
            }
            """);

        Assert.True(effects.HideModifiers);
        Assert.Empty(effects.VisibleModifiers);
        Assert.Equal("civic_quiet_effects", effects.DescriptionKey);
        Assert.Equal("civic_quiet_penalties", effects.PenaltyKey);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void ConditionalBlocksTheGameDoesNotDisplayAreLeftOut()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        // The game's own documentation says this block does not appear in a tooltip, and expects the
        // trait to describe it in words instead. Listing it would put numbers on screen that the
        // game never claims.
        var effects = Read(
            """
            trait_void_dweller_1 = {
                triggered_pop_group_modifier = {
                    potential = { exists = planet }
                    pop_happiness = -0.3
                }
            }
            """);

        Assert.Empty(effects.Conditional);
        Assert.True(effects.IsEmpty);
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void EveryEthicInTheGameHasSomethingToShow()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var database = GameDataExtractor.ExtractFrom(InstallRoot!);
        var silent = database.Ethics.Where(e => e.Effects.IsEmpty).Select(e => e.Key).ToList();

        Assert.True(
            silent.Count == 0,
            "These ethics would show the player nothing about what they do: " + string.Join(", ", silent));
    }
}
