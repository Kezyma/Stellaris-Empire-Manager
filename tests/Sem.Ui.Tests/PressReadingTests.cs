using Microsoft.AspNetCore.Components.Web;
using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Telling a finger reading an option from a hand choosing one.
/// </summary>
/// <remarks>
/// <para>
/// A pointer can hover and a finger cannot, so on a phone the only way to ask what an option does is
/// to press it - and if that press also took the option, there would be no way to read about one
/// without being given it. So the first touch on a row reads and the second chooses.
/// </para>
/// <para>
/// Untested until now, and untestable where it was used: four pickers call it and every test in this
/// project is service-level, so nothing in the suite could reach it. It is a small state machine
/// with two inputs and two questions, which is exactly the kind of thing that is easy to get subtly
/// wrong and never notice.
/// </para>
/// </remarks>
public sealed class PressReadingTests
{
    private static PointerEventArgs Finger => new() { PointerType = "touch" };

    private static PointerEventArgs Mouse => new() { PointerType = "mouse" };

    /// <summary>A mouse chooses on the first press, which is what a mouse has always done.</summary>
    [Fact]
    public void APressFromAMouseChoosesStraightAway()
    {
        var press = new PressReading();

        press.Noted(Mouse);

        Assert.False(press.ReadsOnly("civic_agrarian"));
        Assert.False(press.IsReading("civic_agrarian"));
    }

    /// <summary>A finger reads first and chooses second.</summary>
    [Fact]
    public void AFingerReadsBeforeItChooses()
    {
        var press = new PressReading();

        press.Noted(Finger);
        Assert.True(press.ReadsOnly("civic_agrarian"));
        Assert.True(press.IsReading("civic_agrarian"));

        press.Noted(Finger);
        Assert.False(press.ReadsOnly("civic_agrarian"));
    }

    /// <summary>And moving to another option starts that one over.</summary>
    /// <remarks>
    /// Otherwise the second option would be taken by the press that was only meant to ask about it.
    /// </remarks>
    [Fact]
    public void ADifferentOptionIsReadBeforeItIsChosen()
    {
        var press = new PressReading();

        press.Noted(Finger);
        Assert.True(press.ReadsOnly("civic_agrarian"));

        press.Noted(Finger);
        Assert.True(press.ReadsOnly("civic_meritocracy"));

        // And the first one is no longer the one being read about.
        Assert.False(press.IsReading("civic_agrarian"));
        Assert.True(press.IsReading("civic_meritocracy"));
    }

    /// <summary>
    /// A mouse arriving takes the reading mark with it.
    /// </summary>
    /// <remarks>
    /// A machine with both can change hands between one press and the next, and a mark drawn for a
    /// finger left on a row a mouse is nowhere near reads as a selection that did not happen.
    /// </remarks>
    [Fact]
    public void AMouseAfterAFingerClearsTheMark()
    {
        var press = new PressReading();

        press.Noted(Finger);
        press.ReadsOnly("civic_agrarian");
        Assert.True(press.IsReading("civic_agrarian"));

        press.Noted(Mouse);

        Assert.False(press.IsReading("civic_agrarian"));
        Assert.False(press.ReadsOnly("civic_agrarian"));
    }

    /// <summary>
    /// A host with touches but no pointer events is still a finger.
    /// </summary>
    /// <remarks>
    /// Without this it looks like a mouse and chooses on the first press, which is the whole failure
    /// this class exists to avoid - so the second listener is worth having.
    /// </remarks>
    [Fact]
    public void ATouchWithoutPointerEventsCountsAsAFinger()
    {
        var press = new PressReading();

        press.NotedTouch();

        Assert.True(press.ReadsOnly("civic_agrarian"));
        Assert.True(press.IsReading("civic_agrarian"));
    }

    /// <summary>Nothing is marked as being read before anything has been pressed.</summary>
    [Fact]
    public void NothingIsBeingReadToBeginWith() =>
        Assert.False(new PressReading().IsReading("civic_agrarian"));
}
