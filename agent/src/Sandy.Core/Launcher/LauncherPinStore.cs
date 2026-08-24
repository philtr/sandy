using System.Text.Json;
using Sandy.Core.Protocol;

namespace Sandy.Core.Launcher;

public interface ILauncherPinStore
{
    Task<IReadOnlyList<LauncherPin>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<LauncherPin> pins, CancellationToken cancellationToken = default);
}

public sealed class LauncherPinStore(string path) : ILauncherPinStore
{
    public const int MaximumPins = 15;

    public async Task<IReadOnlyList<LauncherPin>> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<LauncherPinDocument>(
                stream, JsonDefaults.Options, cancellationToken);
            if (document is null || document.SchemaVersion != 1 || document.Pins.Count > MaximumPins)
                return [];
            return document.Pins.Select(pin => pin.Validate()).ToArray();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException
                                              or IOException or JsonException or InvalidDataException)
        {
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyList<LauncherPin> pins, CancellationToken cancellationToken = default)
    {
        if (pins.Count > MaximumPins)
            throw new InvalidDataException($"Sandy supports at most {MaximumPins} pinned applications.");
        var validated = pins.Select(pin => pin.Validate()).ToArray();
        if (validated.Select(pin => pin.Id).Distinct().Count() != validated.Length)
            throw new InvalidDataException("Launcher pin IDs must be unique.");

        var directory = Path.GetDirectoryName(path)
                        ?? throw new InvalidOperationException("Launcher pin path must include a directory.");
        Directory.CreateDirectory(directory);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new LauncherPinDocument(1, validated),
                    JsonDefaults.Options,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
