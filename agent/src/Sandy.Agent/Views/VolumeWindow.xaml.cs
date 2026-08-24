using System.Runtime.InteropServices;
using System.Windows;
using Sandy.Agent.Shell;

namespace Sandy.Agent.Views;

public partial class VolumeWindow : Window
{
    private readonly SystemVolumeController _volume;
    private bool _loaded;

    public VolumeWindow()
    {
        InitializeComponent();
        _volume = new SystemVolumeController();
        VolumeSlider.Value = _volume.Volume * 100;
        MuteCheck.IsChecked = _volume.Muted;
        _loaded = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _volume.Dispose();
        base.OnClosed(e);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded)
            return;
        try { _volume.Volume = (float)(e.NewValue / 100); }
        catch (COMException) { Close(); }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        try { _volume.Muted = MuteCheck.IsChecked == true; }
        catch (COMException) { Close(); }
    }
}
