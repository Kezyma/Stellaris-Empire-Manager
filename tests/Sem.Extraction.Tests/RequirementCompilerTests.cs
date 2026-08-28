using Sem.Clausewitz;
using Sem.Extraction;
using Sem.GameData;

namespace Sem.Extraction.Tests;

/// <summary>
/// The compiler turns the game's conditions into the form the designer enforces, so a mistake here
/// shows up as an option wrongly offered or wrongly blocked.
/// </summary>
public sealed class RequirementCompilerTests
{
    private static CwBlock Block(string script) =>
        (CwBlock)CwDocument.ParseText($"x = {{{script}}}").Nodes[0].Value;

    private static RequirementCompiler WithTriggers(params (string Name, string Body)[] triggers)
    {
        var source = new InMemoryContentSource();
        var script = string.Join("\n", triggers.Select(t => $"{t.Name} = {{ {t.Body} }}"));
        source.Add("common/scripted_triggers/00_triggers.txt", script);

        var compiler = new RequirementCompiler();
        compiler.LoadScriptedTriggers(new ScriptLoader(source.AsContent()));
        return compiler;
    }

    [Fact]
    public void BareValuesInACategoryAreAllRequired()
    {
        var compiled = new RequirementCompiler()
            .CompileRequirementsList(Block("ethics = { value = ethic_pacifist value = ethic_xenophile }"));

        var all = Assert.IsType<AllRequirement>(compiled);
        Assert.Collection(
            all.Items,
            r => AssertSelection(r, SelectionCategory.Ethics, "ethic_pacifist"),
            r => AssertSelection(r, SelectionCategory.Ethics, "ethic_xenophile"));
    }

    [Fact]
    public void OrInsideACategoryRequiresOnlyOne()
    {
        var compiled = new RequirementCompiler()
            .CompileRequirementsList(Block("civics = { OR = { value = civic_a value = civic_b } }"));

        var any = Assert.IsType<AnyRequirement>(compiled);
        Assert.Equal(2, any.Items.Count);
    }

    [Fact]
    public void NorExcludesEveryListedValue()
    {
        var compiled = new RequirementCompiler().CompileRequirementsList(
            Block("ethics = { NOR = { value = ethic_egalitarian value = ethic_fanatic_egalitarian } }"));

        var not = Assert.IsType<NotRequirement>(compiled);
        Assert.Equal(2, Assert.IsType<AnyRequirement>(not.Item).Items.Count);
    }

    [Fact]
    public void NorAtTheTopLevelMeansNeitherRatherThanNotBoth()
    {
        // Getting this wrong would permit an empire holding both of the excluded civics, since
        // "not both" is satisfied by having exactly one.
        var compiled = new RequirementCompiler().CompileRequirementsList(
            Block("NOR = { civics = { value = civic_a } civics = { value = civic_b } }"));

        var not = Assert.IsType<NotRequirement>(compiled);
        Assert.Equal(2, Assert.IsType<AnyRequirement>(not.Item).Items.Count);
    }

    [Fact]
    public void OrAtTheTopLevelKeepsItsBranchesSeparate()
    {
        var compiled = new RequirementCompiler().CompileRequirementsList(
            Block("OR = { authority = { value = auth_a } civics = { value = civic_b } }"));

        var any = Assert.IsType<AnyRequirement>(compiled);
        Assert.Equal(2, any.Items.Count);
        AssertSelection(any.Items[0], SelectionCategory.Authority, "auth_a");
        AssertSelection(any.Items[1], SelectionCategory.Civics, "civic_b");
    }

    [Fact]
    public void FailureTextIsKeptSoBlockedOptionsCanExplainThemselves()
    {
        var compiled = new RequirementCompiler().CompileRequirementsList(
            Block("ethics = { NOT = { text = civic_tooltip_not_egalitarian value = ethic_egalitarian } }"));

        Assert.Equal("civic_tooltip_not_egalitarian", compiled.FailureText);
    }

    [Fact]
    public void ScalarFieldsInARequirementsListBecomeFieldChecks()
    {
        var compiled = new RequirementCompiler().CompileRequirementsList(Block("is_nomadic = no"));

        var field = Assert.IsType<FieldRequirement>(compiled);
        Assert.Equal(("is_nomadic", "no"), (field.Field, field.Value));
    }

    [Fact]
    public void ALimitGuardsItsSiblingsRatherThanJoiningThem()
    {
        // The MACHINE species class states one set of rules with an expansion owned and a stricter
        // set without it. Treating the guard as another requirement would apply both at once.
        var compiled = new RequirementCompiler().CompileRequirementsList(
            Block("AND = { limit = { host_has_dlc = \"The Machine Age\" } authority = { NOT = { value = auth_hive_mind } } }"));

        var any = Assert.IsType<AnyRequirement>(compiled);
        Assert.Equal(2, any.Items.Count);

        // Either the guard fails, or the guard and its requirements both hold.
        Assert.IsType<NotRequirement>(any.Items[0]);
        Assert.IsType<AllRequirement>(any.Items[1]);
    }

    [Fact]
    public void ContentPackChecksAreKeptAsConditionsRatherThanResolved()
    {
        // Which packs are owned is a fact about the person using the app, not about the files.
        var compiled = new RequirementCompiler().CompileTrigger(Block("host_has_dlc = \"Utopia\""));

        Assert.Equal("Utopia", Assert.IsType<DlcRequirement>(compiled).Name);
    }

    [Fact]
    public void ScriptedTriggersAreInlined()
    {
        var compiled = WithTriggers(("has_utopia", "host_has_dlc = \"Utopia\""))
            .CompileTrigger(Block("has_utopia = yes"));

        Assert.Equal("Utopia", Assert.IsType<DlcRequirement>(compiled).Name);
    }

    [Fact]
    public void ANegatedScriptedTriggerIsInvertedRatherThanLost()
    {
        var compiled = WithTriggers(("has_utopia", "host_has_dlc = \"Utopia\""))
            .CompileTrigger(Block("has_utopia = no"));

        var not = Assert.IsType<NotRequirement>(compiled);
        Assert.Equal("Utopia", Assert.IsType<DlcRequirement>(not.Item).Name);
    }

    [Fact]
    public void ABareTriggerNameIsResolved()
    {
        // Prescripted empires write availability as a bare trigger name with no block.
        var compiled = WithTriggers(("has_megacorp", "host_has_dlc = \"Megacorp\""))
            .CompileTriggerByName("has_megacorp");

        Assert.Equal("Megacorp", Assert.IsType<DlcRequirement>(compiled).Name);
    }

    [Fact]
    public void RecursiveTriggersTerminate()
    {
        // A cycle in the game's own triggers must not hang extraction.
        var compiled = WithTriggers(("loops", "loops = yes")).CompileTrigger(Block("loops = yes"));

        Assert.NotNull(compiled);
    }

    [Fact]
    public void ConditionsAboutTheGameStateRatherThanTheDesignAreResolvedImmediately()
    {
        var compiler = new RequirementCompiler();

        // Flags are set by events during a game; a design being created has none.
        Assert.False(Assert.IsType<AlwaysRequirement>(
            compiler.CompileTrigger(Block("has_country_flag = some_event_flag"))).Value);

        // An empire being designed is always an ordinary playable country.
        Assert.True(Assert.IsType<AlwaysRequirement>(
            compiler.CompileTrigger(Block("is_country_type = default"))).Value);

        Assert.Empty(compiler.Unrecognised);
    }

    [Fact]
    public void ConditionsAboutTheDesignBecomePredicates()
    {
        var compiled = WithTriggers(("is_gestalt_placeholder", "always = yes"))
            .CompileTrigger(Block("is_gestalt = yes"));

        Assert.Equal(DesignPredicates.IsGestalt, Assert.IsType<PredicateRequirement>(compiled).Name);
    }

    [Fact]
    public void UnknownConditionsPermitTheOptionAndAreCounted()
    {
        var compiler = new RequirementCompiler();
        var compiled = compiler.CompileTrigger(Block("some_future_trigger = yes"));

        var unknown = Assert.IsType<UnknownRequirement>(compiled);
        Assert.Equal("some_future_trigger", unknown.Name);

        // Defaulting to permitted means a patch adding script never hides an option wrongly.
        Assert.True(unknown.Assume);
        Assert.Equal(1, compiler.Unrecognised["some_future_trigger"]);
    }

    [Fact]
    public void AnAbsentConditionPermitsTheOption()
    {
        Assert.True(Assert.IsType<AlwaysRequirement>(
            new RequirementCompiler().CompileRequirementsList(null)).Value);
    }

    private static void AssertSelection(Requirement requirement, SelectionCategory category, string key)
    {
        var selection = Assert.IsType<SelectionRequirement>(requirement);
        Assert.Equal((category, key), (selection.Category, selection.Key));
    }
}
