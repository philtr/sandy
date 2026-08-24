using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sandy.Core.Launcher;

namespace Sandy.Agent.Launcher;

public static class IconLoader
{
    public static ImageSource? Load(LauncherPin pin)
    {
        if (pin.Kind == LauncherPinKind.Uri || pin.Kind == LauncherPinKind.PackagedApp)
            return null;
        var path = pin.IconPath ?? pin.Target;
        if (!File.Exists(path))
            return null;
        if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        using var icon = Icon.ExtractAssociatedIcon(path);
        if (icon is null)
            return null;
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(96, 96));
        source.Freeze();
        return source;
    }
}
