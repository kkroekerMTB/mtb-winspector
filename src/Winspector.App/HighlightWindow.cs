using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using Winspector.App.Inspection;
using Winspector.Core;

namespace Winspector.App;

internal sealed class HighlightWindow : Window
{
    private nint handle;
    public HighlightWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = new Border { BorderBrush = Brushes.DeepSkyBlue, BorderThickness = new Thickness(3) };
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            const int extendedStyle = -20;
            var style = NativeMethods.GetWindowLong(handle, extendedStyle);
            NativeMethods.SetWindowLong(handle, extendedStyle, style | 0x20 | 0x80 | 0x8000000);
        };
    }
    public void Highlight(ElementSnapshot? element)
    {
        if (element is null || element.IsOffscreen || element.Bounds.Width <= 0 || element.Bounds.Height <= 0)
        {
            Hide();
            return;
        }
        if (!IsVisible) Show();
        var bounds = element.Bounds;
        // Physical pixels under PerMonitorV2; no WPF DIP conversions.
        NativeMethods.SetWindowPos(handle, new nint(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10 | 0x40);
    }
}