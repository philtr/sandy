using System.Windows;
using System.Windows.Interop;

namespace Sandy.Agent.Views;

public partial class CountdownWindow : Window
{
    public nint OwnerHandle { get; }

    public CountdownWindow(System.Windows.Forms.Screen screen, nint ownerHandle)
    {
        InitializeComponent();
        OwnerHandle = ownerHandle;
        if (ownerHandle != nint.Zero)
            new WindowInteropHelper(this).Owner = ownerHandle;

        SourceInitialized += (_, _) => ForegroundNoticePosition.Apply(this, screen);
        ContentRendered += (_, _) => ForegroundNoticePosition.Apply(this, screen);
    }

    public void Update(TimeSpan remaining) => RemainingText.Text = TimeText.Format(remaining);
}
