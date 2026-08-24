using Sandy.Core.Input;

namespace Sandy.Core.Tests;

public sealed class WindowsKeySequenceTests
{
    private const int LeftWindows = 0x5B;
    private const int RightWindows = 0x5C;
    private const int R = 0x52;
    private const int Space = 0x20;

    [Fact]
    public void Windows_key_tap_invokes_home_on_release()
    {
        var sequence = new WindowsKeySequence();

        Assert.Equal(new WindowsKeyDecision(true, false), sequence.Handle(LeftWindows, true, true, false));
        Assert.Equal(new WindowsKeyDecision(true, true), sequence.Handle(LeftWindows, false, true, false));
    }

    [Fact]
    public void Windows_chord_swallows_corresponding_key_up_without_invoking_home()
    {
        var sequence = new WindowsKeySequence();

        sequence.Handle(LeftWindows, true, true, false);
        Assert.True(sequence.Handle(R, true, false, false).Suppress);
        Assert.Equal(new WindowsKeyDecision(true, false), sequence.Handle(LeftWindows, false, true, false));
        Assert.True(sequence.Handle(R, false, false, false).Suppress);
    }

    [Fact]
    public void Tracks_left_and_right_windows_keys_independently()
    {
        var sequence = new WindowsKeySequence();

        sequence.Handle(LeftWindows, true, true, false);
        sequence.Handle(RightWindows, true, true, false);
        Assert.False(sequence.Handle(LeftWindows, false, true, false).InvokeHome);
        Assert.True(sequence.Handle(RightWindows, false, true, false).InvokeHome);
    }

    [Fact]
    public void Modifier_held_before_windows_key_makes_the_sequence_a_chord()
    {
        var sequence = new WindowsKeySequence();

        sequence.Handle(LeftWindows, true, true, true);
        Assert.False(sequence.Handle(LeftWindows, false, true, true).InvokeHome);
    }

    [Fact]
    public void Windows_space_is_replayed_and_its_releases_pass_through()
    {
        var sequence = new WindowsKeySequence();

        sequence.Handle(LeftWindows, true, true, false);
        Assert.Equal(
            new WindowsKeyDecision(true, false, LeftWindows),
            sequence.Handle(Space, true, false, false, allowWindowsSpace: true));
        Assert.Equal(
            new WindowsKeyDecision(false, false),
            sequence.Handle(Space, false, false, false, allowWindowsSpace: true));
        Assert.Equal(
            new WindowsKeyDecision(false, false),
            sequence.Handle(LeftWindows, false, true, false));
    }

    [Fact]
    public void Windows_space_never_invokes_home_and_next_windows_tap_still_does()
    {
        var sequence = new WindowsKeySequence();

        sequence.Handle(RightWindows, true, true, false);
        sequence.Handle(Space, true, false, false, allowWindowsSpace: true);
        sequence.Handle(Space, false, false, false, allowWindowsSpace: true);
        Assert.False(sequence.Handle(RightWindows, false, true, false).InvokeHome);

        sequence.Handle(RightWindows, true, true, false);
        Assert.True(sequence.Handle(RightWindows, false, true, false).InvokeHome);
    }
}
