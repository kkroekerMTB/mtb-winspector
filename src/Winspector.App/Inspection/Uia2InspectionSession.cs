using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;
using Winspector.Core;

namespace Winspector.App.Inspection;

public sealed record InspectionFrame(long Generation, IReadOnlyList<ElementSnapshot>? Roots,
    string? HoverId, string? Error, bool TargetExited = false);

/// <summary>Owns UIA references and subscriptions on one dedicated MTA thread.</summary>
public sealed class Uia2InspectionSession : IDisposable
{
    private readonly Thread worker;
    private readonly ManualResetEventSlim stop = new();
    private readonly object gate = new();
    private TargetWindow? target;
    private long generation;
    private int dirty = 1;
    private int manualRefresh;
    private volatile bool paused;
    private volatile bool hoverEnabled = true;
    private int disposed;
    public event Action<InspectionFrame>? FrameReady;

    public Uia2InspectionSession()
    {
        worker = new Thread(Run) { IsBackground = true, Name = "Winspector UIA2" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
    }
    public long Attach(TargetWindow? next)
    {
        lock (gate)
        {
            target = next;
            generation++;
            Interlocked.Exchange(ref dirty, 1);
            return generation;
        }
    }
    public bool Paused { get => paused; set => paused = value; }
    public bool HoverEnabled { get => hoverEnabled; set => hoverEnabled = value; }
    public void Refresh() => Interlocked.Exchange(ref manualRefresh, 1);

    private void Run()
    {
        using var automation = new UIA2Automation();
        long activeGeneration = -1;
        var lastRefresh = DateTime.MinValue;
        string[] subscribedRoots = [];
        string? lastHover = null;
        var hasDesktopSubscription = false;
        while (!stop.Wait(100))
        {
            TargetWindow? current;
            long version;
            lock (gate) { current = target; version = generation; }
            if (version != activeGeneration)
            {
                automation.UnregisterAllEvents();
                subscribedRoots = [];
                hasDesktopSubscription = false;
                activeGeneration = version;
                lastRefresh = DateTime.MinValue;
                lastHover = null;
            }
            if (current is null) continue;
            try
            {
                if (!IsAlive(current))
                {
                    FrameReady?.Invoke(new(version, [], null, "The target process exited.", true));
                    lock (gate) { if (generation == version) target = null; }
                    automation.UnregisterAllEvents();
                    continue;
                }
                var now = DateTime.UtcNow;
                var forced = Interlocked.Exchange(ref manualRefresh, 0) != 0;
                var elapsed = now - lastRefresh;
                var needsRefresh = forced || (!paused &&
                    (elapsed >= TimeSpan.FromSeconds(1.5) ||
                     (Volatile.Read(ref dirty) != 0 && elapsed >= TimeSpan.FromMilliseconds(350))));
                IReadOnlyList<ElementSnapshot>? snapshot = null;
                if (needsRefresh)
                {
                    Interlocked.Exchange(ref dirty, 0);
                    var desktop = automation.GetDesktop();
                    var windows = desktop.FindAllChildren(automation.ConditionFactory.ByProcessId(current.ProcessId));
                    var windowIds = windows.Select(RuntimeId).ToArray();
                    if (!hasDesktopSubscription || !windowIds.SequenceEqual(subscribedRoots))
                    {
                        automation.UnregisterAllEvents();
                        // New dialogs are not descendants of the original target window.
                        desktop.RegisterStructureChangedEvent(TreeScope.Children, (_, _, _) => Interlocked.Exchange(ref dirty, 1));
                        foreach (var window in windows)
                            window.RegisterStructureChangedEvent(TreeScope.Subtree, (_, _, _) => Interlocked.Exchange(ref dirty, 1));
                        subscribedRoots = windowIds;
                        hasDesktopSubscription = true;
                    }
                    var walker = automation.TreeWalkerFactory.GetRawViewWalker();
                    var count = 0;
                    ElementSnapshot Read(AutomationElement element, int depth)
                    {
                        if (stop.IsSet) throw new OperationCanceledException();
                        if (++count > 10000 || depth > 100)
                            throw new InvalidOperationException("Tree exceeds the inspection limit (10,000 nodes / 100 levels).");
                        var children = new List<ElementSnapshot>();
                        var id = RuntimeId(element);
                        var rectangle = element.BoundingRectangle;
                        var name = element.Properties.Name.ValueOrDefault ?? "";
                        var automationId = element.Properties.AutomationId.ValueOrDefault ?? "";
                        var type = element.Properties.ControlType.ValueOrDefault.ToString();
                        var className = element.Properties.ClassName.ValueOrDefault ?? "";
                        var offscreen = element.Properties.IsOffscreen.ValueOrDefault;
                        var enabled = element.Properties.IsEnabled.ValueOrDefault;
                        for (var child = walker.GetFirstChild(element); child is not null; child = walker.GetNextSibling(child))
                            children.Add(Read(child, depth + 1));
                        return new(id, name, automationId, type, className, offscreen, enabled,
                            new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height), children.ToArray());
                    }
                    snapshot = windows.Select(window => Read(window, 0)).ToArray();
                    lastRefresh = now;
                }
                string? hoverId = null;
                if (hoverEnabled && NativeMethods.GetCursorPos(out var point))
                {
                    var window = NativeMethods.WindowFromPoint(point);
                    NativeMethods.GetWindowThreadProcessId(window, out var processId);
                    if (processId == current.ProcessId)
                    {
                        var hovered = automation.FromPoint(new System.Drawing.Point(point.X, point.Y));
                        if (hovered.Properties.ProcessId.ValueOrDefault == current.ProcessId)
                            hoverId = RuntimeId(hovered);
                    }
                }
                if (snapshot is not null || (hoverId is not null && hoverId != lastHover))
                    FrameReady?.Invoke(new(version, snapshot, hoverId, null));
                lastHover = hoverId;
            }
            catch (OperationCanceledException) when (stop.IsSet) { break; }
            catch (Exception ex)
            {
                // A provider can disappear mid-walk. Keep the previous complete tree and retry.
                lastRefresh = DateTime.UtcNow;
                FrameReady?.Invoke(new(version, null, null, $"Inspection failed: {ex.Message}"));
                if (stop.Wait(500)) break;
            }
        }
    }
    private static string RuntimeId(AutomationElement element)
    {
        var id = element.FrameworkAutomationElement.RuntimeId.Value;
        if (id is null || id.Length == 0) throw new InvalidOperationException("The provider did not expose a runtime ID.");
        return string.Join(".", id);
    }
    private static bool IsAlive(TargetWindow target)
    {
        try
        {
            using var process = Process.GetProcessById(target.ProcessId);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == target.StartTimeTicks;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return false; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Set();
        // A stalled external provider must not prevent window shutdown.
        if (worker.Join(TimeSpan.FromMilliseconds(500))) stop.Dispose();
    }
}