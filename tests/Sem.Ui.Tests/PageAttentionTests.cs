using Sem.Ui.Services;

namespace Sem.Ui.Tests;

/// <summary>
/// Whether the app is in front of the player, and when it comes back.
/// </summary>
/// <remarks>
/// Two distinctions carry the whole of it, and both are easy to collapse by accident: visibility is
/// not the same as attention returning, and a host with no page at all must behave as though the
/// page is always there rather than as though it has gone.
/// </remarks>
public sealed class PageAttentionTests
{
    /// <summary>
    /// With nothing to listen with, the app is attended and nothing is ever announced.
    /// </summary>
    /// <remarks>
    /// What the desktop gets. Its document lives inside an embedded browser and is visible for as
    /// long as the app is running, so the safe default is the true one - and the unsafe default
    /// would stop every watch in the app dead.
    /// </remarks>
    [Fact]
    public void WithNoPageItIsAttendedAndSilent()
    {
        var attention = new PageAttention();
        var returned = 0;

        attention.Returned += () => returned++;

        Assert.True(attention.Attended);
        Assert.Equal(0, returned);
    }

    /// <summary>Going away and coming back is one return.</summary>
    [Fact]
    public void ComingBackIntoViewAnnouncesItOnce()
    {
        var attention = new PageAttention();
        var returned = 0;

        attention.Returned += () => returned++;

        attention.Attend(false);

        Assert.False(attention.Attended);
        Assert.Equal(0, returned);

        attention.Attend(true);

        Assert.True(attention.Attended);
        Assert.Equal(1, returned);
    }

    /// <summary>Being told it is visible when it already was announces nothing.</summary>
    /// <remarks>
    /// visibilitychange can fire without the state having moved, and every spurious announcement is
    /// a request to a provider that answers what the last one answered.
    /// </remarks>
    [Fact]
    public void StayingInViewAnnouncesNothing()
    {
        var attention = new PageAttention();
        var returned = 0;

        attention.Returned += () => returned++;

        attention.Attend(true);
        attention.Attend(true);

        Assert.Equal(0, returned);
    }

    /// <summary>
    /// Focus, network and a restored page announce a return without touching visibility.
    /// </summary>
    /// <remarks>
    /// The distinction the desktop depends on. Inside an embedded browser the document never stops
    /// being visible, so a handler that only watched visibility would never fire there - and the
    /// window being activated is still a moment when the file underneath may have moved.
    /// </remarks>
    [Fact]
    public void ReconnectingAnnouncesAReturnWithoutChangingWhatIsSeen()
    {
        var attention = new PageAttention();
        var returned = 0;

        attention.Returned += () => returned++;

        attention.Reconnected();

        Assert.True(attention.Attended);
        Assert.Equal(1, returned);

        // Even while away, which is what an "online" event during a hidden tab looks like.
        attention.Attend(false);
        attention.Reconnected();

        Assert.False(attention.Attended);
        Assert.Equal(2, returned);
    }
}
