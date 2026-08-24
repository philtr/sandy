using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Sandy.Agent.Launcher;
using Sandy.Core.Launcher;
using MessageBox = System.Windows.MessageBox;
using Mouse = System.Windows.Input.Mouse;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Sandy.Agent.Views;

public partial class EditAppsWindow : Window
{
    private static string? _lastBrowseDirectory;
    private readonly ILauncherPinStore _store;
    private readonly Func<bool> _isAuthorized;
    private readonly Func<LauncherPin, LauncherPin> _cacheIcon;
    private readonly Action<IReadOnlyList<LauncherPin>> _pinsChanged;
    private readonly ObservableCollection<LauncherPin> _pins;
    private readonly DispatcherTimer _authorizationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private System.Windows.Point _dragStart;

    public EditAppsWindow(
        IReadOnlyList<LauncherPin> pins,
        ILauncherPinStore store,
        Func<bool> isAuthorized,
        Func<LauncherPin, LauncherPin> cacheIcon,
        Action<IReadOnlyList<LauncherPin>> pinsChanged)
    {
        InitializeComponent();
        _store = store;
        _isAuthorized = isAuthorized;
        _cacheIcon = cacheIcon;
        _pinsChanged = pinsChanged;
        _pins = new ObservableCollection<LauncherPin>(pins);
        PinsList.ItemsSource = _pins;
        _authorizationTimer.Tick += AuthorizationTimer_Tick;
        _authorizationTimer.Start();
        DragEnter += EditAppsWindow_DragEnter;
        Drop += EditAppsWindow_Drop;
        UpdateAuthorization();
        UpdateCount();
    }

    protected override void OnClosed(EventArgs e)
    {
        _authorizationTimer.Stop();
        _authorizationTimer.Tick -= AuthorizationTimer_Tick;
        DragEnter -= EditAppsWindow_DragEnter;
        Drop -= EditAppsWindow_Drop;
        base.OnClosed(e);
    }

    private void ChooseStart_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAuthorized())
            return;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            var picker = new StartApplicationPickerWindow(ManualApplicationSource.EnumerateStartApplications())
            {
                Owner = this
            };
            if (picker.ShowDialog() == true && picker.SelectedApplication is not null)
            {
                var choice = picker.SelectedApplication;
                AddPin(new LauncherPin(
                    Guid.NewGuid(), choice.Name, choice.Kind, choice.Target, choice.IconPath, choice.MatchIdentity));
            }
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAuthorized())
            return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose an application or shortcut",
            Filter = "Applications and shortcuts (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(_lastBrowseDirectory)
                ? _lastBrowseDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };
        if (dialog.ShowDialog(this) == true)
        {
            _lastBrowseDirectory = Path.GetDirectoryName(dialog.FileName);
            AddPin(ManualApplicationSource.FromFile(dialog.FileName));
        }
    }

    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAuthorized())
            return;
        var dialog = new AddLinkWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Pin is not null)
            AddPin(dialog.Pin);
    }

    private async void MoveUp_Click(object sender, RoutedEventArgs e) => await MoveSelectedAsync(-1);
    private async void MoveDown_Click(object sender, RoutedEventArgs e) => await MoveSelectedAsync(1);

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureAuthorized() || PinsList.SelectedItem is not LauncherPin selected)
            return;
        _pins.Remove(selected);
        await PersistAsync();
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void PinsList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(PinsList);

    private void PinsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isAuthorized() || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed
            || PinsList.SelectedItem is not LauncherPin selected)
            return;
        var current = e.GetPosition(PinsList);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        System.Windows.DragDrop.DoDragDrop(PinsList, selected, System.Windows.DragDropEffects.Move);
    }

    private void PinsList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(LauncherPin)))
        {
            e.Effects = _isAuthorized() ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }
    }

    private async void PinsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!_isAuthorized() || e.Data.GetData(typeof(LauncherPin)) is not LauncherPin dragged)
            return;
        var target = FindItem(e.OriginalSource as DependencyObject)?.DataContext as LauncherPin;
        var from = _pins.IndexOf(dragged);
        var to = target is null ? _pins.Count - 1 : _pins.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to)
        {
            _pins.Move(from, to);
            PinsList.SelectedItem = dragged;
            await PersistAsync();
        }
        e.Handled = true;
    }

    private void EditAppsWindow_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = _isAuthorized() && GetDroppedFile(e.Data) is not null
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void EditAppsWindow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!EnsureAuthorized())
            return;
        var path = GetDroppedFile(e.Data);
        if (path is not null)
            AddPin(ManualApplicationSource.FromFile(path));
    }

    private async void AddPin(LauncherPin pin)
    {
        try
        {
            pin = _cacheIcon(pin.Validate());
            if (_pins.Count >= LauncherPinStore.MaximumPins)
                throw new InvalidDataException($"Sandy supports up to {LauncherPinStore.MaximumPins} pinned apps.");
            if (_pins.Any(existing => existing.Kind == pin.Kind
                                      && string.Equals(existing.Target, pin.Target, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("That application is already pinned.");
            _pins.Add(pin);
            await PersistAsync();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            MessageBox.Show(exception.Message, "Could not pin app", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task MoveSelectedAsync(int offset)
    {
        if (!EnsureAuthorized() || PinsList.SelectedItem is not LauncherPin selected)
            return;
        var current = _pins.IndexOf(selected);
        var next = current + offset;
        if (next < 0 || next >= _pins.Count)
            return;
        _pins.Move(current, next);
        PinsList.SelectedItem = selected;
        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync(_pins.ToArray());
        _pinsChanged(_pins.ToArray());
        UpdateCount();
    }

    private bool EnsureAuthorized()
    {
        UpdateAuthorization();
        if (_isAuthorized())
            return true;
        MessageBox.Show("Ask a parent to unlock app editing in Sandy. The PC must also be online and have screen time remaining.",
            "App editing locked", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void AuthorizationTimer_Tick(object? sender, EventArgs e) => UpdateAuthorization();

    private void UpdateAuthorization()
    {
        var allowed = _isAuthorized();
        EditActions.IsEnabled = allowed;
        PinsList.IsEnabled = allowed;
        MoveUpButton.IsEnabled = allowed;
        MoveDownButton.IsEnabled = allowed;
        RemoveButton.IsEnabled = allowed;
        AuthorizationText.Text = allowed
            ? "App editing unlocked. Changes are saved immediately."
            : "Ask a parent to unlock app editing. Sandy must be online and have time remaining.";
        AuthorizationText.Foreground = (System.Windows.Media.Brush)FindResource(allowed ? "AccentBrush" : "MutedBrush");
    }

    private void UpdateCount() => CountText.Text = $"{_pins.Count}/{LauncherPinStore.MaximumPins} pinned";

    private static string? GetDroppedFile(System.Windows.IDataObject data)
    {
        if (!data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            || data.GetData(System.Windows.DataFormats.FileDrop) is not string[] { Length: 1 } files)
            return null;
        return LauncherPin.IsSupportedFile(files[0]) ? files[0] : null;
    }

    private static ListBoxItem? FindItem(DependencyObject? current)
    {
        while (current is not null && current is not ListBoxItem)
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        return current as ListBoxItem;
    }
}
