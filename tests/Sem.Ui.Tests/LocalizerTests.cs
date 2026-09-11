using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Reading the game's text the way the game reads it.
/// </summary>
/// <remarks>
/// The markup is the game's own: variables in dollar signs standing for other entries or for numbers
/// the script declared, colour runs bounded by section signs, icons between pound signs. A token
/// that goes unresolved is not a small fault — it appears in the middle of a sentence a player is
/// reading, which is how <c>$@living_standard_energy_normal|*0$</c> came to be shown as the Machine
/// trait's upkeep.
/// </remarks>
public sealed class LocalizerTests
{
    private static Localizer With(
        Dictionary<string, string>? entries = null,
        Dictionary<string, double>? values = null,
        Dictionary<string, string>? scripted = null) =>
        new(entries ?? [], null, null, values, scripted);

    [Fact]
    public void ANumberTheScriptNamedIsWrittenOutWhereTheTextRefersToIt()
    {
        var localizer = With(
            new() { ["TRAIT_ORGANIC_EFFECT"] = "Upkeep: +$@living_standard_energy_normal|*0$" },
            new() { ["living_standard_energy_normal"] = 1.0 });

        Assert.Equal("Upkeep: +1", localizer.Text("TRAIT_ORGANIC_EFFECT"));
    }

    [Theory]

    // The formats the game's own text actually asks for.
    [InlineData(1.0, "*0", "1")]
    [InlineData(0.25, "0%", "25%")]
    [InlineData(0.25, "%0", "25%")]
    [InlineData(0.2, "+0%", "+20%")]
    [InlineData(-0.2, "0%+", "-20%")]
    [InlineData(1.5, "1", "1.5")]
    [InlineData(3.0, "", "3.00")]
    public void TheFormatAfterTheBarSaysHowToWriteIt(double value, string flags, string expected)
    {
        var token = flags.Length == 0 ? "$@v$" : $"$@v|{flags}$";
        var localizer = With(new() { ["k"] = token }, new() { ["v"] = value });

        Assert.Equal(expected, localizer.Text("k"));
    }

    [Fact]
    public void AVariableTheScriptNeverDeclaredIsLeftAloneRatherThanBlanked()
    {
        // Better a visible token than a sentence with a hole in it: the first can be chased, and the
        // second reads as though the game said nothing.
        var localizer = With(new() { ["k"] = "before $@missing|0$ after" });

        Assert.Equal("before $@missing|0$ after", localizer.Text("k"));
    }

    [Fact]
    public void OrdinaryVariablesStillStandForOtherEntries()
    {
        var localizer = With(new()
        {
            ["k"] = "a $inner$ c",
            ["inner"] = "b",
        });

        Assert.Equal("a b c", localizer.Text("k"));
    }

    [Theory]

    // The game words some counters as a sentence with the figure in it and others as a bare noun,
    // and a bar wants both as one line. Only the colon and the slash are ours.
    [InlineData("Points: $POINTS|H$", "Points: 2/5")]
    [InlineData("Traits", "Traits: 2/5")]
    [InlineData("Civics:", "Civics: 2/5")]
    public void ACounterReadsAsASpendAgainstItsAllowance(string entry, string expected)
    {
        var localizer = With(new() { ["k"] = entry });

        Assert.Equal(expected, localizer.Gauge("k", "fallback", "2/5"));
    }

    [Fact]
    public void APhraseTheTextCallsScriptForShowsWhatTheGameWouldShow()
    {
        // The attunement modifiers read "Add Attunement with [This.GetCradleColor]", and every
        // branch of that call asks about a game in progress. The default is the branch that applies
        // to an empire being designed, and deleting the call left the sentence hanging.
        var localizer = With(
            new()
            {
                ["mod_add_attunement"] = "Add Attunement with [This.GetCradleColor]",
                ["UNDISCOVERED_PATRON_ARTICLE"] = "an Unknown Entity",
            },
            scripted: new() { ["GetCradleColor"] = "UNDISCOVERED_PATRON_ARTICLE" });

        Assert.Equal("Add Attunement with an Unknown Entity", localizer.Text("mod_add_attunement"));
    }

    [Fact]
    public void APhraseWithNoDefaultIsStillRemovedRatherThanShownAsACall()
    {
        // The gap it leaves is not worth chasing: every one of these is read as HTML, which folds
        // runs of whitespace down to one.
        var localizer = With(new() { ["k"] = "before [This.GetSomething] after" });

        Assert.Equal("before  after", localizer.Text("k"));
    }

    [Fact]
    public void APhraseThatResolvesToItselfDoesNotLoopForever()
    {
        var localizer = With(
            new() { ["k"] = "[This.GetLoop]", ["loop"] = "round [This.GetLoop] again" },
            scripted: new() { ["GetLoop"] = "loop" });

        // Whatever it settles on, it settles: the depth limit is what stops a cycle in the game's
        // own text from hanging the page.
        Assert.StartsWith("round", localizer.Text("k"), StringComparison.Ordinal);
    }

    [Fact]
    public void ACallOnAJobAnswersWithThatJobsName()
    {
        // "£physics£ produced per 100 [bureaucrat.GetNamePlural]" is how the game labels Dimensional
        // Worship's research bonus. Only the word after the last dot used to be looked at, so the
        // call answered nothing and the label read "produced per 100" with no subject at all.
        var localizer = With(
            new()
            {
                ["mod_planet_bureaucrats_physics_research_produces_add"] =
                    "Physics Research per 100 $bureaucrat_type_plural_with_icon$",
                ["bureaucrat_type_plural_with_icon"] = "[GetBureaucratSwapPluralWithIcon]",
                ["job_bureaucrat_swap_plural_with_icon"] = "[bureaucrat.GetNamePlural]",
                ["job_bureaucrat_plural"] = "Bureaucrats",
            },
            scripted: new() { ["GetBureaucratSwapPluralWithIcon"] = "job_bureaucrat_swap_plural_with_icon" });

        Assert.Equal(
            "Physics Research per 100 Bureaucrats",
            localizer.Text("mod_planet_bureaucrats_physics_research_produces_add"));
    }

    [Fact]
    public void ACallOnSomethingThatIsNotAJobStillDisappears()
    {
        // Only a running game can say what Root is called, and printing the call would be worse
        // than printing nothing.
        var localizer = With(new() { ["X"] = "Owned by [Root.GetName]." });

        Assert.Equal("Owned by .", localizer.Text("X"));
    }

    /*
       The three below guard one property: nothing that reaches the page as raw HTML can carry
       markup of its own.

       Twenty places render a string with (MarkupString), which tells Blazor to stop escaping. Every
       one of them is fed from here, and every one is game text - but "game text" is a statement
       about where the string came from, and the answer to that is about to change. A designs file
       can already arrive from a share link, and cloud import will let one arrive from a provider,
       which means from somebody else. An empire carries a species biography the player types, and
       a name they type, and those travel in the file.

       So these are not tests of a feature. They are the tests that have to keep passing for the
       feature to be safe to add, and they belong here rather than beside the cloud code because
       this is where the escaping actually happens.
    */

    /// <summary>A key nobody has heard of is shown as itself, escaped.</summary>
    /// <remarks>
    /// Which is the case that matters: a string the player typed is, by definition, not a key the
    /// game defines, so it takes this path. The fallback is what gets drawn.
    /// </remarks>
    [Fact]
    public void TextFromOutsideTheGameIsEscapedRatherThanDrawn()
    {
        var localizer = With([]);

        Assert.Equal(
            "&lt;script&gt;alert(1)&lt;/script&gt;",
            localizer.Html("SOME_KEY", "<script>alert(1)</script>"));
    }

    /// <summary>And so is one with no key at all.</summary>
    [Fact]
    public void AFallbackWithNoKeyIsEscapedToo()
    {
        var localizer = With([]);

        Assert.Equal("&lt;img src=x onerror=alert(1)&gt;", localizer.Html(null, "<img src=x onerror=alert(1)>"));
    }

    /// <summary>
    /// Text already in hand is escaped as it is turned into HTML, markup and all.
    /// </summary>
    /// <remarks>
    /// The colour runs survive because they are the game's own notation rather than characters in
    /// the text - a section sign and a letter become a span, and everything else goes through
    /// HtmlEncode on the way past. So a sentence may be coloured and may not be an element.
    /// </remarks>
    [Fact]
    public void MarkupSurvivesAndAngleBracketsDoNot()
    {
        var localizer = With([]);

        var html = localizer.HtmlOf("§Y<b>bold</b>§!");

        // The span is the game's colour run, and which colour it is is not what this is about.
        Assert.StartsWith("<span style=\"color:", html, StringComparison.Ordinal);
        Assert.EndsWith("</span>", html, StringComparison.Ordinal);

        Assert.Contains("&lt;b&gt;bold&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
    }
}
