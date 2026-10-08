using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlaUI.Core.Input;
using FlaUI.UIA2;
using Winspector.App;
using Winspector.App.Inspection;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Winspector.Windows.Tests;

public sealed class InspectorWindowTests
{
    [Fact]
    [Trait("Category", "WindowsDesktop")]
    public async Task RealWindowSynchronizesHoverAndFreezeWithoutSelectionRecursion()
    {
        using var process = Process.Start(new ProcessStartInfo(
            Path.Combine(AppContext.BaseDirectory, "Fixture", "Winspector.WinFormsFixture.exe"))
        { UseShellExecute = false })!;
        var originalCursor = Mouse.Position;
        try
        {
            Assert.True(process.WaitForInputIdle(10000));
            await WaitUntil(() => { process.Refresh(); return process.MainWindowHandle != 0; });
            using var automation = new UIA2Automation();
            var fixture = automation.FromHandle(process.MainWindowHandle);
            var add = fixture.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("AddControl"));
            var remove = fixture.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("RemoveControl"));
            Assert.NotNull(add);
            Assert.NotNull(remove);
            var addBounds = add.BoundingRectangle;
            var removeBounds = remove.BoundingRectangle;
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                dispatcher.BeginInvoke(async () =>
                {
                    MainWindow? window = null;
                    try
                    {
                        window = new MainWindow { Left = 700, Top = 50, Width = 1000, Height = 650 };
                        window.Show();
                        var model = (InspectorViewModel)window.DataContext;
                        var target = new TargetWindow(process.Id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName, "Fixture");
                        model.Targets.Add(target);
                        model.SelectedTarget = target;
                        await WaitUntil(() => model.Tree.Count > 0);
                        Mouse.MoveTo(addBounds.X + addBounds.Width / 2, addBounds.Y + addBounds.Height / 2);
                        await WaitUntil(() => model.Tree.Selected?.Snapshot.AutomationId == "AddControl");
                        Assert.Contains(model.Details, item => item.Key == "AutomationId" && item.Value == "AddControl");
                        model.Frozen = true;
                        Mouse.MoveTo(removeBounds.X + removeBounds.Width / 2, removeBounds.Y + removeBounds.Height / 2);
                        await Task.Delay(500);
                        Assert.Equal("AddControl", model.Tree.Selected!.Snapshot.AutomationId);
                        model.Frozen = false;
                        await WaitUntil(() => model.Tree.Selected?.Snapshot.AutomationId == "RemoveControl");
                        model.Frozen = true;
                        model.Paused = true;

                        // Exercise scrolling a deeply nested selection beyond the visible viewport.
                        var rows = Enumerable.Range(0, 80).Select(index =>
                            new Winspector.Core.ElementSnapshot("row-" + index, "Control " + index, "id-" + index,
                                "Button", "Fixture", false, true, new(0, 0, 0, 0), [])).ToArray();
                        model.Tree.Apply([new("root", "Long form", "", "Window", "Fixture", false, true, new(), rows)]);
                        model.Tree.Select("row-79", reveal: true);
                        await Task.Delay(300);
                        var tree = (System.Windows.Controls.TreeView)window.FindName("ElementTree");
                        var root = (System.Windows.Controls.TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(model.Tree.Roots[0]);
                        var child = (System.Windows.Controls.TreeViewItem)root.ItemContainerGenerator.ContainerFromItem(model.Tree.Selected);
                        Assert.NotNull(child);
                        var point = child.TransformToAncestor(tree).Transform(new Point(0, 0));
                        Assert.InRange(point.Y, 0, tree.ActualHeight - 1);

                        window.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        var screenshot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/inspector.png"));
                        Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                        using var output = File.Create(screenshot);
                        encoder.Save(output);
                        var selectedNode = model.Tree.Selected;
                        model.Tree.Apply([
                            new("root", "Long form", "", "Window", "Fixture", false, true, new(), rows.Take(79).ToArray()),
                            new("other", "Moved controls", "", "Pane", "Fixture", false, true, new(), [rows[79]])
                        ]);
                        await Task.Delay(300);
                        Assert.Same(selectedNode, model.Tree.Selected);
                        Assert.Same(selectedNode, tree.SelectedItem);
                        Assert.Equal("other", model.Tree.Selected!.Parent!.Id);
                        model.Tree.Apply([new("root", "Long form", "", "Window", "Fixture", false, true, new(), rows.Take(79).ToArray())]);
                        await Task.Delay(100);
                        Assert.Null(model.Tree.Selected);
                        Assert.Null(tree.SelectedItem);
                        Assert.Empty(model.Details);

                        finished.TrySetResult();
                    }
                    catch (Exception ex) { finished.TrySetException(ex); }
                    finally
                    {
                        window?.Close();
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }
                });
                Dispatcher.Run();
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(40));
            Assert.True(thread.Join(2000));
        }
        finally
        {
            Mouse.MoveTo(originalCursor);
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
        }
    }

    private static async Task WaitUntil(Func<bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Inspector did not reach the expected state.");
            await Task.Delay(100);
        }
    }
}