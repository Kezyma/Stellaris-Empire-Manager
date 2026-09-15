using Sem.Clausewitz;
using Sem.Extraction;
using Sem.GameData;
using Sem.Io;
using Sem.Rules;

namespace Sem.Extraction.Tests;

/// <summary>
/// What the worlds and the shipsets say about themselves beyond what a designer's picker needs.
/// </summary>
/// <remarks>
/// Both records were the thinnest in the game and for the same reason: the designer asks a planet
/// class for a picture and a habitability trait, and asks a graphical culture for a picture and
/// whether it may be chosen, so nothing else in either folder had ever been opened.
/// </remarks>
public sealed class WorldPackTests
{
    private static string? InstallRoot { get; } =
        Environment.GetEnvironmentVariable("SEM_STELLARIS_ROOT") is { Length: > 0 } configured
            ? configured
            : StellarisLocator.FindInstallRoot();

    /// <summary>The extractor, read once, since it walks thirty-five thousand files.</summary>
    private static readonly Lazy<GameDataExtractor> Read = new(() =>
    {
        var extractor = new GameDataExtractor(LayeredContent.ForInstall(InstallRoot!));
        extractor.Extract();
        return extractor;
    });

    /// <summary>
    /// A world's own numbers reach the page, which until now only the habitability trait's did.
    /// </summary>
    /// <remarks>
    /// Fifteen classes declare a modifier block and the column beside them was fed entirely by the
    /// trait, so a Gaia world's ten per cent to job output and happiness appeared nowhere at all.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AWorldCarriesItsOwnNumbers()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var worlds = Read.Value.Family.Worlds;

        Assert.Equal(69, worlds.Count);
        Assert.Equal(15, worlds.Count(w => !w.Effects.IsEmpty));

        var gaia = Assert.Single(worlds, w => w.Key == "pc_gaia");

        Assert.True(gaia.Ideal);
        Assert.Equal(0.1, gaia.Effects.Modifiers["planet_jobs_produces_mult"], 3);
        Assert.Equal(0.1, gaia.Effects.Modifiers["pop_happiness"], 3);
    }

    /// <summary>
    /// How big a world turns up, and what a colony on it may build.
    /// </summary>
    /// <remarks>
    /// The artificial worlds are the ones worth checking: a ring segment states no size at all - the
    /// game fixes it - and names a district set and a starting district where a rolled world names
    /// only the set.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AWorldSaysHowBigItIsAndWhatItBuilds()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var worlds = Read.Value.Family.Worlds;

        var gaia = Assert.Single(worlds, w => w.Key == "pc_gaia");

        Assert.Equal(12, gaia.SmallestSize);
        Assert.Equal(25, gaia.LargestSize);
        Assert.Equal("standard", gaia.Districts);
        Assert.Equal(600, gaia.CarryCapacity);
        Assert.False(gaia.Artificial);

        var ring = Assert.Single(worlds, w => w.Key == "pc_ringworld_habitable");

        Assert.Null(ring.SmallestSize);
        Assert.Equal("ring_world", ring.Districts);
        Assert.Equal("district_rw_city", ring.StartingDistrict);
        Assert.True(ring.Artificial);
        Assert.True(ring.Ringworld);
    }

    /// <summary>
    /// What a world can be terraformed into, read from a folder nobody had opened.
    /// </summary>
    /// <remarks>
    /// Two hundred and thirty of the folder's two hundred and fifty-eight, from twenty worlds to
    /// fourteen, each carrying how long the work takes and what the empire needs first. Two kinds
    /// are dropped: the fourteen the game itself switches off with <c>always = no</c>, every one of
    /// them a dead second copy of a live link from a normal world to a hive world, and the ten a
    /// world makes of itself, which are the Wilderness origin regrowing a planet rather than a world
    /// turning into another one.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AWorldSaysWhatItCanBecome()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var worlds = Read.Value.Family.Worlds;
        var links = worlds.SelectMany(w => w.Becomes).ToList();

        Assert.Equal(230, links.Count);
        Assert.Equal(20, worlds.Count(w => w.Becomes.Count > 0));
        Assert.Equal(14, links.Select(t => t.World).Distinct(StringComparer.Ordinal).Count());

        // Nothing becomes itself, and the switched-off links are gone. Seventeen live links do end
        // at a hive world, which is what makes the fourteen dead ones easy to miss.
        Assert.DoesNotContain(worlds, w => w.Becomes.Any(t => t.World == w.Key));
        Assert.DoesNotContain(links, t => t.Needs?.Settled() is false);

        // And a technology the empire has not researched is unknown rather than refused, which is
        // the difference between a hundred and fifty-six links and none: compiled the way a
        // condition on a finished empire is, every one of them reads as never.
        Assert.Equal(156, links.Count(t => t.Needs is not null));

        // And every one of them lands on a world the page can draw, which is the half that would go
        // wrong quietly: a chip naming a class that is not in the database renders as nothing.
        var known = Read.Value.Extract().PlanetClasses
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(links, t => Assert.Contains(t.World, known));

        // A duration on every link, since it is what the chip wears.
        Assert.All(links, t => Assert.True(t.Days > 0));
    }

    /// <summary>
    /// The preference the game picks for itself is not the one an empire founded there starts with.
    /// </summary>
    /// <remarks>
    /// <c>auto_trait_prio</c> looks like the answer to "which preference does this world grant" and
    /// is a different mechanic. An ocean world names <c>trait_auto_wet_preference</c> - one trait
    /// covering all three wet classes with a penalty to the dry ones - which is what the game
    /// reaches for when it is choosing a preference for a species itself. An empire founded on one
    /// gets Ocean Preference. The field is carried to the page under its own heading and the rules
    /// layer keeps its own answer; this is the test that says why.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void TheGamesOwnPreferenceIsNotTheOneAnEmpireStartsWith()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var worlds = Read.Value.Family.Worlds;

        Assert.Equal(19, worlds.Count(w => w.AutoPreference.Count > 0));

        var ocean = Assert.Single(worlds, w => w.Key == "pc_ocean");
        var continental = Assert.Single(worlds, w => w.Key == "pc_continental");

        // One trait for both, which is the whole of the point: it cannot be what either starts with.
        Assert.Equal(["trait_auto_wet_preference"], ocean.AutoPreference);
        Assert.Equal(["trait_auto_wet_preference"], continental.AutoPreference);

        var rules = new EmpireRules(Read.Value.Extract());

        Assert.Equal(
            "trait_pc_ocean_preference",
            rules.HabitabilityTraitFor("pc_ocean", "BIOLOGICAL"));

        Assert.Equal(
            "trait_pc_continental_preference",
            rules.HabitabilityTraitFor("pc_continental", "BIOLOGICAL"));
    }

    /// <summary>
    /// Whether a set's hulls take the empire's colours, which is half of why a reader is here.
    /// </summary>
    /// <remarks>
    /// Twenty-five say yes and the rest keep their own livery whatever the flag says, and the page
    /// had no way to tell a reader which was which.
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AShipsetSaysWhetherItTakesTheEmpiresColours()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var sets = Read.Value.Shipsets;

        Assert.Equal(52, sets.Count);
        Assert.Equal(25, sets.Count(s => s.TakesColour));

        Assert.True(Assert.Single(sets, s => s.Key == "humanoid_01").TakesColour);
        Assert.False(Assert.Single(sets, s => s.Key == "fallen_empire_01").TakesColour);
    }

    /// <summary>
    /// A set the designer will not offer is not one the game saves for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>randomized</c> is the sibling of <c>selectable</c> - one gates the player's picker, the
    /// other the roll - and it looked like the one real gap on this page. It is not: compiled and
    /// compared, the two are written identically in all fifty-two sets, so carrying it would put a
    /// second row on every card saying what the first already says. That is why the pack does not
    /// hold it, and this is the test that would catch the game separating them.
    /// </para>
    /// <para>
    /// What it settles is the note beside the thirty unplayable sets, which used to read that the
    /// game keeps them for its own empires. It does not. No roll produces them either, so nothing
    /// but an event ever puts one in a galaxy.
    /// </para>
    /// </remarks>
    [SkippableFact]
    [Trait("Category", "RealData")]
    public void AShipsetTheGameKeepsIsNotOneItHandsOut()
    {
        Skip.If(InstallRoot is null, "Stellaris is not installed on this machine.");

        var loader = new ScriptLoader(LayeredContent.ForInstall(InstallRoot!));
        var compiler = new RequirementCompiler();
        compiler.LoadScriptedTriggers(loader);

        var compared = 0;

        foreach (var entry in loader.LoadDefinitions("common/graphical_culture"))
        {
            Requirement Gate(string field) =>
                entry.Body.Nodes.FirstOrDefault(n => n.Key == field)?.Block is { } block
                    ? compiler.CompileTrigger(block)
                    : new AlwaysRequirement(true);

            Assert.Equal(Gate("selectable"), Gate("randomized"));
            compared++;
        }

        Assert.Equal(52, compared);
    }
}
