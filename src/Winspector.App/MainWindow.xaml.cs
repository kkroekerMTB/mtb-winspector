using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Winspector.App.Inspection;
using Winspector.Core;

namespace Winspector.App;

public partial class MainWindow : Window
{
    private readonly InspectorViewModel viewModel;
    private readonly HighlightWindow overlay = new();
    private HwndSource? source;
    private bool hotkeyRegistered;
    public MainWindow()
    {
        InitializeComponent();
        viewModel = new InspectorViewModel(Dispatcher);
        DataContext = viewModel;
        viewModel.SelectionChanged += overlay.Highlight;
        Loaded += async (_, _) => await viewModel.ReloadTargetsAsync();
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WindowMessage);
            hotkeyRegistered = NativeMethods.RegisterHotKey(source.Handle, 1, 0x2 | 0x4 | 0x4000, 0x46);
            if (!hotkeyRegistered) viewModel.ReportStatus("Ctrl+Shift+F is already in use. Use the Freeze selection checkbox.");
        };
        Closed += (_, _) =>
        {
            if (source is not null)
            {
                if (hotkeyRegistered) NativeMethods.UnregisterHotKey(source.Handle, 1);
                source.RemoveHook(WindowMessage);
            }
            viewModel.SelectionChanged -= overlay.Highlight;
            viewModel.Dispose();
            overlay.Close();
        };
    }
    private nint WindowMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x312 && wParam == 1)
        {
            viewModel.Frozen = !viewModel.Frozen;
            handled = true;
        }
        return 0;
    }
    private async void ReloadApplications_Click(object sender, RoutedEventArgs e) => await viewModel.ReloadTargetsAsync();
    private void RefreshTree_Click(object sender, RoutedEventArgs e) => viewModel.Refresh();
    private void TreeItem_Selected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item)
            Dispatcher.BeginInvoke(() => item.BringIntoView());
        e.Handled = true;
    }
    private void ElementTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ElementNode node) viewModel.Select(node);

    }
}