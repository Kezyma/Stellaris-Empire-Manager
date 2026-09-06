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
/// Read back by name, against a vocabulary of what one empire could have meant, which is only safe
/// because names resolve uniquely there. The colliding cases are real and are covered below.
/// </remarks>
public sealed class PlanTextTests
{
    [Fact]
    public void APlanComesBackAsItWentIn()
    {
        var text = English();
        var plan = new EmpirePlan(["tradition_harmony"], ["ap_mind_over_matter"], ["civic_meritocracy"]);

        var written = text.Write(plan);

        Assert.Equal(plan, text.Read(written, Vocabulary()));
    }

    /// <summary>
    /// The order is the plan, so it has to survive the round trip.
    /// </summary>
    /// <remarks>
    /// Not decoration. Twenty-five ascension perks are gated on how many come before them, which
    /// read against an ordered plan is a rule about where each one may sit - so a plan that came
    /// back in another order would be a different plan, and one the game might refuse.
    /// </remarks>
    [Fact]
    public void TheOrderComesBackToo()
    {
        var text = English();
        var plan = new EmpirePlan([], ["ap_mind_over_matter", "ap_technological_ascendancy"], []);

        var back = text.Read(text.Write(plan), Vocabulary())!;

        Assert.Equal(["ap_mind_over_matter", "ap_technological_ascendancy"], back.Perks);
    }

    /// <summary>
    /// A biography somebody actually wrote is not a plan, and must not be read as one.
    /// </summary>
    /// <remarks>
    /// The failure this guards against is silent and destructive: a biography mistaken for a plan
    /// would be replaced by generated prose the next time the empire was saved. The last two cases
    /// are the ones the marker exists for - prose that happens to carry a heading this would
    /// otherwise recognise.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("The Blorg are a friendly people who wish only to be loved.")]
    [InlineData("Notes: they are not, in fact, friendly.")]
    [InlineData("Perks: being extremely friendly, and quite round.")]
    [InlineData("Civics: we have none, and are proud of it.")]
    public void OrdinaryWritingIsNotAPlan(string? biography)
    {
        Assert.False(English().IsPlan(biography, Vocabulary()));

        // Null rather than an empty plan. An empty plan is one that has decided nothing yet;
        // this is not a plan at all, and the difference decides whether somebody's writing is
        // about to be replaced.
        Assert.Null(English().Read(biography, Vocabulary()));
    }

    /// <summary>A plan that has decided nothing is still a plan, and is still recognised.</summary>
    /// <remarks>
    /// What the marker is for. Releasing the last perk leaves a biography with one word in it, and
    /// if that did not read back as a plan the checkbox would switch itself off underneath the
    /// player in the middle of editing.
    /// </remarks>
    [Fact]
    public void AMarkerOnItsOwnIsAPlanThatHasDecidedNothing()
    {
        var written = English().Write(EmpirePlan.Empty);

        Assert.Equal(PlanText.Marker, written);
        Assert.Equal(EmpirePlan.Empty, English().Read(written, PlanVocabulary.Empty));
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
        var written = $"{PlanText.Marker}\nTraditions: Cybernetics";

        var assimilator = new PlanVocabulary(
            [("tradition_cybernetics_assimilator", "Cybernetics")], [], []);

        Assert.Equal(["tradition_cybernetics_assimilator"], text.Read(written, assimilator)!.Trees);

        var ordinary = new PlanVocabulary([("tradition_cybernetics", "Cybernetics")], [], []);

        Assert.Equal(["tradition_cybernetics"], text.Read(written, ordinary)!.Trees);
    }

    /// <summary>Something the empire cannot take is dropped, not guessed at.</summary>
    [Fact]
    public void ANameThisEmpireCannotTakeIsLeftOut()
    {
        var plan = English().Read(
            $"{PlanText.Marker}\nTraditions: Harmony, Prosperity\nPerks: Mind Over Matter",
            new PlanVocabulary([("tradition_harmony", "Harmony")], [], []))!;

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
        var whole = text.Write(new EmpirePlan(["tradition_harmony"], ["ap_mind_over_matter"], []));

        var cut = whole[..whole.IndexOf("Over", StringComparison.Ordinal)];
        var plan = text.Read(cut, Vocabulary())!;

        Assert.Equal(["tradition_harmony"], plan.Trees);
        Assert.Empty(plan.Perks);
    }

    /// <summary>
    /// A plan too long for a biography loses items from the end, not lines and never half a name.
    /// </summary>
    /// <remarks>
    /// It used to drop whole lines, and in a language with long names one character over budget cost
    /// every perk in the plan. Dropping the last item of the longest line instead keeps all three
    /// parts represented and gives up the things furthest in the future, which are the least certain
    /// anyway.
    /// </remarks>
    [Fact]
    public void APlanTooLongLosesItemsFromTheEnd()
    {
        var text = English();

        var written = text.Write(new EmpirePlan(
            ["tradition_harmony"],
            [.. Enumerable.Range(0, 40).Select(i => $"a_very_long_perk_name_{i}")],
            ["civic_meritocracy"]));

        Assert.True(written.Length <= PlanText.Budget, $"wrote {written.Length} characters");

        // The shorter lines survive: what went is the length, taken from where the length was.
        Assert.Contains("Traditions: Harmony", written, StringComparison.Ordinal);
        Assert.Contains("Civics: Meritocracy", written, StringComparison.Ordinal);

        // And whatever perks are left are whole ones, in order, from the front. The names read
        // prettified because nothing in this little dictionary names them, which is the localizer
        // doing what it does to a key it has never seen.
        Assert.Contains("Perks: A Very Long Perk Name 0,", written, StringComparison.Ordinal);
        Assert.DoesNotContain("Name 39", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The longest plan the game can produce fits in a biography, in every language it ships.
    /// </summary>
    /// <remarks>
    /// The one that a future content pack breaks. Names in French and Polish run half as long again
    /// as the English ones, and the worst case measured against the real data - the longest seven
    /// trees, eight perks and three civics at once - overruns the budget in six of the ten languages
    /// before anything shortens it. What is asserted is that something does, and that what survives
    /// is made of whole names.
    /// </remarks>
    [Fact]
    public void TheLongestPossiblePlanStillFits()
    {
        var names = Enumerable.Range(0, 20)
            .ToDictionary(i => $"k{i}", i => new string('W', 40) + i, StringComparer.Ordinal);

        var text = new PlanText(new Localizer(names));

        var written = text.Write(new EmpirePlan(
            [.. names.Keys.Take(7)],
            [.. names.Keys.Skip(7).Take(8)],
            [.. names.Keys.Skip(15).Take(3)]));

        Assert.True(written.Length <= PlanText.Budget, $"wrote {written.Length} characters");

        // And every name that survived is a whole one. This is the property that matters more than
        // the length: a name cut in half can read back as a different name.
        foreach (var line in written.Split('\n').Skip(1))
        {
            var items = line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..]
                .Split(", ", StringSplitOptions.TrimEntries);

            Assert.All(items, item => Assert.Contains(item, names.Values));
        }
    }

    /// <summary>A reader who typed a name in their own case still meant the name.</summary>
    [Fact]
    public void CaseDoesNotMatter()
    {
        var plan = English().Read(
            "plan\ntraditions: harmony",
            new PlanVocabulary([("tradition_harmony", "Harmony")], [], []))!;

        Assert.Equal(["tradition_harmony"], plan.Trees);
    }

    /// <summary>
    /// A plan written by a player in one language reads in an app set to the same one.
    /// </summary>
    /// <remarks>
    /// The names in a plan are the game's own words, so they are whatever the writer's game was set
    /// to. The headings are not - they are fixed English, which is what lets a German plan at least
    /// be recognised as a plan by an English app even when none of its names resolve.
    /// </remarks>
    [Fact]
    public void APlanRoundTripsInAnotherLanguage()
    {
        var german = new PlanText(new Localizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tradition_harmony"] = "Harmonie",
        }));

        var plan = new EmpirePlan(["tradition_harmony"], [], []);
        var written = german.Write(plan);

        Assert.Contains("Traditions: Harmonie", written, StringComparison.Ordinal);

        // Read with the vocabulary that same game would offer, which is the German one.
        Assert.Equal(
            plan,
            german.Read(written, new PlanVocabulary([("tradition_harmony", "Harmonie")], [], [])));

        // And an app set to English still knows it is looking at a plan, even though the one name
        // in it means nothing here.
        Assert.True(English().IsPlan(written, Vocabulary()));
    }

    private static PlanText English() => new(new Localizer(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tradition_harmony"] = "Harmony",
            ["ap_mind_over_matter"] = "Mind Over Matter",
            ["ap_technological_ascendancy"] = "Technological Ascendancy",
            ["civic_meritocracy"] = "Meritocracy",
        }));

    private static PlanVocabulary Vocabulary() => new(
        [("tradition_harmony", "Harmony")],
        [("ap_mind_over_matter", "Mind Over Matter"), ("ap_technological_ascendancy", "Technological Ascendancy")],
        [("civic_meritocracy", "Meritocracy")]);
}
