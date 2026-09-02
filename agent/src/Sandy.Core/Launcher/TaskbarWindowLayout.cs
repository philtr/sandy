namespace Sandy.Core.Launcher;

public static class TaskbarWindowLayout
{
    public static int CalculateCapacity(double availableWidth, double itemWidth)
    {
        if (itemWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemWidth));
        if (availableWidth <= 0)
            return 0;

        return Math.Max(1, (int)Math.Floor(availableWidth / itemWidth));
    }

    public static TaskbarWindowPlan<T> Plan<T>(
        IReadOnlyList<T> windows,
        int capacity,
        Func<T, string> identity)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(identity);

        if (capacity <= 0)
            return new TaskbarWindowPlan<T>([], windows.ToArray());

        if (windows.Count <= capacity)
            return new TaskbarWindowPlan<T>(windows.Select(window => (IReadOnlyList<T>)[window]).ToArray(), []);

        var groupedByIdentity = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<IReadOnlyList<T>>();
        foreach (var window in windows)
        {
            var key = identity(window);
            if (!groupedByIdentity.TryGetValue(key, out var group))
            {
                group = [];
                groupedByIdentity.Add(key, group);
                groups.Add(group);
            }
            group.Add(window);
        }

        if (groups.Count <= capacity)
            return new TaskbarWindowPlan<T>(groups, []);

        var visibleGroupCount = Math.Max(0, capacity - 1);
        return new TaskbarWindowPlan<T>(
            groups.Take(visibleGroupCount).ToArray(),
            groups.Skip(visibleGroupCount).SelectMany(group => group).ToArray());
    }
}

public sealed record TaskbarWindowPlan<T>(
    IReadOnlyList<IReadOnlyList<T>> Groups,
    IReadOnlyList<T> Overflow);
