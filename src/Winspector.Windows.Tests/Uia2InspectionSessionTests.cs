using System.IO;
using System.Diagnostics;
using System.Threading.Channels;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;
using Winspector.App.Inspection;
using Winspector.Core;

namespace Winspector.Windows.Tests;

public sealed class Uia2InspectionSessionTests
{
    [Fact]
    [Trait("Category", "WindowsDesktop")]
    public async Task InspectsNet48WinFormsAndTracksDynamicControlsDialogsPauseAndExit()
    {
        using var process = Process.Start(new ProcessStartInfo(
            Path.Combine(AppContext.BaseDirectory, "Fixture", "Winspector.WinFormsFixture.exe"))
        { UseShellExecute = false })!;
        try
        {
            Assert.True(process.WaitForInputIdle(10000));
            using var automation = new UIA2Automation();
            using var session = new Uia2InspectionSession { HoverEnabled = false };
            var frames = Channel.CreateUnbounded<InspectionFrame>();
            session.FrameReady += frame => frames.Writer.TryWrite(frame);
            var generation = session.Attach(new(process.Id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName, "Fixture"));
            var initial = await NextSnapshot(frames.Reader, roots => Contains(roots, "AddControl"));
            Assert.Equal(generation, initial.Generation);
            Assert.False(Contains(initial.Roots!, "DynamicButton"));

            AutomationElement Button(string id)
            {
                var window = automation.FromHandle(process.MainWindowHandle);
                return window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(id))
                    ?? throw new InvalidOperationException($"Fixture button {id} is missing.");
            }
            void Invoke(string id) => Button(id).Patterns.Invoke.Pattern.Invoke();

            Invoke("AddControl");
            await NextSnapshot(frames.Reader, roots => Contains(roots, "DynamicButton"));
            Invoke("ToggleVisibility");
            await NextSnapshot(frames.Reader, roots => !Contains(roots, "DynamicButton") ||
                Flatten(roots).First(item => item.AutomationId == "DynamicButton").IsOffscreen);
            Invoke("ToggleVisibility");
            await NextSnapshot(frames.Reader, roots => Flatten(roots).Any(item => item.AutomationId == "DynamicButton" && !item.IsOffscreen));
            Invoke("RemoveControl");
            await NextSnapshot(frames.Reader, roots => !Contains(roots, "DynamicButton"));

            Invoke("OpenDialog");
            await NextSnapshot(frames.Reader, roots => Contains(roots, "ExtraWindow"));

            session.Paused = true;
            await Task.Delay(700); // Let any snapshot already in flight complete.
            while (frames.Reader.TryRead(out _)) { }
            Invoke("AddControl");
            await Task.Delay(2000);
            while (frames.Reader.TryRead(out var pausedFrame))
                Assert.Null(pausedFrame.Roots);
            session.Refresh();
            await NextSnapshot(frames.Reader, roots => Contains(roots, "DynamicButton"));

            process.Kill();
            var exited = await NextFrame(frames.Reader, frame => frame.TargetExited);
            Assert.Empty(exited.Roots!);
        }
        finally
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
        }
    }

    private static IEnumerable<ElementSnapshot> Flatten(IReadOnlyList<ElementSnapshot> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Flatten(root.Children)) yield return child;
        }
    }
    private static bool Contains(IReadOnlyList<ElementSnapshot> roots, string automationId) =>
        Flatten(roots).Any(item => item.AutomationId == automationId);
    private static Task<InspectionFrame> NextSnapshot(ChannelReader<InspectionFrame> reader,
        Func<IReadOnlyList<ElementSnapshot>, bool> predicate) =>
        NextFrame(reader, frame => frame.Roots is not null && predicate(frame.Roots));

    private static async Task<InspectionFrame> NextFrame(ChannelReader<InspectionFrame> reader, Func<InspectionFrame, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        string? lastError = null;
        string lastTree = "";
        try
        {
            while (true)
            {
                var frame = await reader.ReadAsync(timeout.Token);
                lastError = frame.Error ?? lastError;
                if (frame.Roots is not null)
                    lastTree = string.Join("; ", Flatten(frame.Roots).Select(item =>
                        item.ControlType + ":" + item.Name + ":" + item.AutomationId));
                if (predicate(frame)) return frame;
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Expected inspection update did not arrive. Last provider error: " + lastError + " Tree: " + lastTree);
        }
    }
}