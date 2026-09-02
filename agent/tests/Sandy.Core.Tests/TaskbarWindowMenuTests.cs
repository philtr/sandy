using Sandy.Core.Launcher;

namespace Sandy.Core.Tests;

public sealed class TaskbarWindowMenuTests
{
    [Theory]
    [InlineData(TaskbarWindowState.Normal, "Minimize", "Maximize", "Close window")]
    [InlineData(TaskbarWindowState.Minimized, "Restore", "Close window")]
    [InlineData(TaskbarWindowState.Maximized, "Minimize", "Restore", "Close window")]
    public void Single_window_commands_follow_the_window_state(
        TaskbarWindowState state,
        params string[] expectedLabels)
    {
        var commands = TaskbarWindowMenu.ForSingle(state);

        Assert.Equal(expectedLabels, commands.Select(command => command.Label));
    }

    [Fact]
    public void Group_commands_apply_to_every_window()
    {
        var commands = TaskbarWindowMenu.ForGroup();

        Assert.Collection(commands,
            command => Assert.Equal(
                new TaskbarWindowCommand(TaskbarWindowCommandKind.Minimize, "Minimize all"), command),
            command => Assert.Equal(
                new TaskbarWindowCommand(TaskbarWindowCommandKind.Restore, "Restore all"), command),
            command => Assert.Equal(
                new TaskbarWindowCommand(TaskbarWindowCommandKind.Close, "Close all windows"), command));
    }
}
