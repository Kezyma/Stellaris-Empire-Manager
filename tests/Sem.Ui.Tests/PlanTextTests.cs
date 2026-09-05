using Sem.Designs;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Writing a plan into a biography, and getting the same plan back out of one.
/// </summary>
/// <remarks>
/// This is the part of the feature a test can hold still. A plan has nowhere to live in a design as
/// the game understands one - the game rewrites every design from its own model and drops what the
/// model has no slot for - so it is written as prose into a biography, which the game does keep and
/// does show the player. Everything therefore rests on the prose meaning the same thing when it is
/// read again, possibly by an app set to another language, possibly after the game has cut it short.
///
/// Read back by name, with no hidden marker, which is only safe because names resolve uniquely among
/// the choices one empire could make. The colliding cases are real and are covered below.
/// </remarks>
public sealed class PlanTextTests
{
    [Fact]
    public void APlanComesBackAsItWentIn()
    {
        var text = English();
        var plan = new EmpirePlan(PlanPath.Cybernetic, ["tradition_harmony"], ["ap_mind_over_matter"]);

        var written = text.Write(plan);

        Assert.Equal(plan, text.Read(written, Vocabulary()));
    }

    [Fact]
    public void EveryPathComesBack()
    {
        var text = English();

        foreach (var path in PlanPaths.All.Where(p => p is not PlanPath.Unset))
        {
            var written = text.Write(new EmpirePlan(path, [], []));

            Assert.Equal(path, text.Read(written, PlanVocabulary.Empty).Path);
        }
    }

    /// <summary>
    /// A biography somebody actually wrote is not a plan, and must not be read as one.
    /// </summary>
    /// <remarks>
    /// The failure this guards against is silent and destructive: a biography mistaken for a plan
    /// would be replaced by generated prose the next time the empire was saved.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("The Blorg are a friendly people who wish only to be loved.")]
    [InlineData("Notes: they are not, in fact, friendly.")]
    [InlineData("Ascension path:")]
    [InlineData("Ascension path: something the game has never heard of")]
    public void OrdinaryWritingIsNotAPlan(string? biography)
    {
        Assert.False(English().IsPlan(biography, Vocabulary()));
        Assert.Equal(EmpirePlan.Empty, English().Read(biography, Vocabulary()));
    }

    /// <summary>
    /// A name shared by two things resolves to the one this empire could have meant.
    /// </summary>
    /// <remarks>
    /// Not hypothetical. The game gives <c>tradition_cybernetics</c> and
    /// <c>tradition_cybernetics_assimilator</c> the same display name in all ten languages it ships,
    /// as it does the four Galactic Wonders perks. Every such family is mutually exclusive - by
    /// content pack or by what kind of empire it is - so only one is ever on offer, and looking the
    /// name up among what is on offer is what makes reading by name safe at all.
    /// </remarks>
    [Fact]
    public void ANameTwoThingsShareMeansTheOneOnOffer()
    {
        var text = English();
        var written = "Traditions: Cybernetics";

        var assimilator = new PlanVocabulary(
            [("tradition_cybernetics_assimilator", "Cybernetics")], []);

        Assert.Equal(["tradition_cybernetics_assimilator"], text.Read(written, assimilator).Trees);

        var ordinary = new PlanVocabulary([("tradition_cybernetics", "Cybernetics")], []);

        Assert.Equal(["tradition_cybernetics"], text.Read(written, ordinary).Trees);
    }

    /// <summary>Something the empire cannot take is dropped, not guessed at.</summary>
    [Fact]
    public void ANameThisEmpireCannotTakeIsLeftOut()
    {
        var plan = English().Read(
            "Traditions: Harmony, Prosperity\nAscension Perks: Mind Over Matter",
            new PlanVocabulary([("tradition_harmony", "Harmony")], []));

        Assert.Equal(["tradition_harmony"], plan.Trees);
        Assert.Empty(plan.Perks);
    }

    /// <summary>
    /// A biography the game cut short still reads, and never reads as the wrong thing.
    /// </summary>
    /// <remarks>
    /// The game truncates a biography without saying so - the one real measurement is a species
    /// biography that came back at 476 characters, cut mid-phrase. What matters is that a half-written
    /// name is dropped rather than matched to something else.
    /// </remarks>
    [Fact]
    public void ABiographyCutShortKeepsWhatSurvivedTheCut()
    {
        var text = English();
        var whole = text.Write(new EmpirePlan(
            PlanPath.Cybernetic, ["tradition_harmony"], ["ap_mind_over_matter"]));

        var cut = whole[..whole.IndexOf("Over", StringComparison.Ordinal)];
        var plan = text.Read(cut, Vocabulary());

        Assert.Equal(PlanPath.Cybernetic, plan.Path);
        Assert.Equal(["tradition_harmony"], plan.Trees);
        Assert.Empty(plan.Perks);
    }

    /// <summary>
    /// A plan too long for a biography loses whole items rather than half a name.
    /// </summary>
    /// <remarks>
    /// Dropping the perks line is deliberate: what is left still says which path and which trees,
    /// which is the shape of the decision. Letting it run over instead would hand the game something
    /// to cut wherever it liked.
    /// </remarks>
    [Fact]
    public void APlanTooLongLosesTheLastLineRatherThanRunningOver()
    {
        var long_ = string.Join(", ", Enumerable.Range(0, 40).Select(i => $"a_very_long_perk_name_{i}"));
        var text = English();

        var written = text.Write(new EmpirePlan(
            PlanPath.Cybernetic,
            ["tradition_harmony"],
            [.. long_.Split(", ")]));

        Assert.True(written.Length <= PlanText.Budget, $"wrote {written.Length} characters");
        Assert.Contains("Cybernetics", written, StringComparison.Ordinal);
        Assert.DoesNotContain("a_very_long_perk_name", written, StringComparison.Ordinal);
    }

    /// <summary>A reader who typed a name in their own case still meant the name.</summary>
    [Fact]
    public void CaseDoesNotMatter()
    {
        var plan = English().Read("ascension path: cybernetics", PlanVocabulary.Empty);

        Assert.Equal(PlanPath.Cybernetic, plan.Path);
    }

    /// <summary>
    /// A plan written by a player in one language reads in an app set to the same one.
    /// </summary>
    /// <remarks>
    /// The names in a plan are the game's own words, so they are whatever the writer's game was set
    /// to. This holds the round trip still for a second language; reading a plan written in a
    /// language the reader does not have is a separate problem and a separate answer.
    /// </remarks>
    [Fact]
    public void APlanRoundTripsInAnotherLanguage()
    {
        var german = new PlanText(new Localizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tradition_cybernetics"] = "Kybernetik",
            ["tradition_harmony"] = "Harmonie",
            ["TRADITIONS"] = "Traditionen",
            ["ASCENSION_PERKS"] = "Erweckungsprivilegien",
        }));

        var plan = new EmpirePlan(PlanPath.Cybernetic, ["tradition_harmony"], []);
        var written = german.Write(plan);

        Assert.Contains("Kybernetik", written, StringComparison.Ordinal);
        Assert.Contains("Traditionen: Harmonie", written, StringComparison.Ordinal);

        // Read with the vocabulary that same game would offer, which is the German one.
        Assert.Equal(plan, german.Read(written, new PlanVocabulary([("tradition_harmony", "Harmonie")], [])));
    }

    private static PlanText English() => new(new Localizer(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tradition_cybernetics"] = "Cybernetics",
            ["tradition_genetics"] = "Genetics",
            ["tradition_purity"] = "Purity",
            ["tradition_cloning"] = "Cloning",
            ["tradition_mutation"] = "Mutation",
            ["tradition_psionics"] = "Psionics",
            ["tradition_synthetics"] = "Synthetics",
            ["tradition_harmony"] = "Harmony",
            ["ap_mind_over_matter"] = "Mind Over Matter",
            ["TRADITIONS"] = "Traditions",
            ["ASCENSION_PERKS"] = "Ascension Perks",
        }));

    private static PlanVocabulary Vocabulary() => new(
        [("tradition_harmony", "Harmony")],
        [("ap_mind_over_matter", "Mind Over Matter")]);
}
