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
        Dictionary<string, double>? values = null) =>
        new(entries ?? [], null, null, values);

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
}
