namespace Sandy.Core.Launcher;

public enum TaskbarWindowState
{
    Normal,
    Minimized,
    Maximized
}

public enum TaskbarWindowCommandKind
{
    Minimize,
    Maximize,
    Restore,
    Close
}

public sealed record TaskbarWindowCommand(TaskbarWindowCommandKind Kind, string Label);

public static class TaskbarWindowMenu
{
    public static IReadOnlyList<TaskbarWindowCommand> ForSingle(TaskbarWindowState state) => state switch
    {
        TaskbarWindowState.Minimized =>
        [
            new(TaskbarWindowCommandKind.Restore, "Restore"),
            new(TaskbarWindowCommandKind.Close, "Close window")
        ],
        TaskbarWindowState.Maximized =>
        [
            new(TaskbarWindowCommandKind.Minimize, "Minimize"),
            new(TaskbarWindowCommandKind.Restore, "Restore"),
            new(TaskbarWindowCommandKind.Close, "Close window")
        ],
        _ =>
        [
            new(TaskbarWindowCommandKind.Minimize, "Minimize"),
            new(TaskbarWindowCommandKind.Maximize, "Maximize"),
            new(TaskbarWindowCommandKind.Close, "Close window")
        ]
    };

    public static IReadOnlyList<TaskbarWindowCommand> ForGroup() =>
    [
        new(TaskbarWindowCommandKind.Minimize, "Minimize all"),
        new(TaskbarWindowCommandKind.Restore, "Restore all"),
        new(TaskbarWindowCommandKind.Close, "Close all windows")
    ];
}
