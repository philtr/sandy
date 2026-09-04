using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Launcher;

public sealed class LauncherIconCache(string directory)
{
    public LauncherPin Cache(LauncherPin pin)
    {
        if (pin.Kind is LauncherPinKind.Uri or LauncherPinKind.PackagedApp)
            return pin;
        if (pin.Kind != LauncherPinKind.Shortcut
            && !string.IsNullOrWhiteSpace(pin.IconPath)
            && Path.GetDirectoryName(pin.IconPath)?.Equals(directory, StringComparison.OrdinalIgnoreCase) == true
            && File.Exists(pin.IconPath))
            return pin;

        var source = ShortcutIconSource.Resolve(pin) ?? pin.IconPath ?? pin.Target;
        if (!File.Exists(source))
            return pin;
        try
        {
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, $"{pin.Id:N}.png");
            using var icon = Icon.ExtractAssociatedIcon(source);
            if (icon is null)
                return pin;
            using var bitmap = icon.ToBitmap();
            bitmap.Save(destination, ImageFormat.Png);
            return pin with { IconPath = destination };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                               or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            return pin;
        }
    }
}
