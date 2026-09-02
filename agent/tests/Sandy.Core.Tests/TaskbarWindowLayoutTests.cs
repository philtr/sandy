using Sandy.Core.Launcher;

namespace Sandy.Core.Tests;

public sealed class TaskbarWindowLayoutTests
{
    [Theory]
    [InlineData(90, 1)]
    [InlineData(291, 3)]
    [InlineData(970, 10)]
    public void Capacity_expands_with_the_available_width(double availableWidth, int expected)
    {
        Assert.Equal(expected, TaskbarWindowLayout.CalculateCapacity(availableWidth, itemWidth: 97));
    }

    [Fact]
    public void Keeps_related_windows_separate_while_every_window_fits()
    {
        var windows = new[]
        {
            new Window("browser", "First"),
            new Window("browser", "Second"),
            new Window("editor", "Third")
        };

        var plan = TaskbarWindowLayout.Plan(windows, capacity: 3, window => window.Identity);

        Assert.Collection(
            plan.Groups,
            group => Assert.Equal([windows[0]], group),
            group => Assert.Equal([windows[1]], group),
            group => Assert.Equal([windows[2]], group));
        Assert.Empty(plan.Overflow);
    }

    [Fact]
    public void Groups_related_windows_when_individual_windows_do_not_fit()
    {
        var windows = new[]
        {
            new Window("browser", "First"),
            new Window("editor", "Second"),
            new Window("browser", "Third")
        };

        var plan = TaskbarWindowLayout.Plan(windows, capacity: 2, window => window.Identity);

        Assert.Collection(
            plan.Groups,
            group => Assert.Equal([windows[0], windows[2]], group),
            group => Assert.Equal([windows[1]], group));
        Assert.Empty(plan.Overflow);
    }

    [Fact]
    public void Reserves_the_last_slot_for_overflow_when_related_groups_still_do_not_fit()
    {
        var windows = new[]
        {
            new Window("browser", "First"),
            new Window("editor", "Second"),
            new Window("terminal", "Third"),
            new Window("browser", "Fourth")
        };

        var plan = TaskbarWindowLayout.Plan(windows, capacity: 2, window => window.Identity);

        var group = Assert.Single(plan.Groups);
        Assert.Equal([windows[0], windows[3]], group);
        Assert.Equal([windows[1], windows[2]], plan.Overflow);
    }

    private sealed record Window(string Identity, string Title);
}
