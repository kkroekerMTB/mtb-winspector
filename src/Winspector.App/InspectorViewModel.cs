using System.Collections.ObjectModel;
using System.Windows.Threading;
using Winspector.App.Inspection;
using Winspector.Core;

namespace Winspector.App;

public sealed class InspectorViewModel : ObservableObject, IDisposable
{
    private readonly Uia2InspectionSession session = new();
    private readonly Dispatcher dispatcher;
    private TargetWindow? selectedTarget;
    private bool hoverEnabled = true;
    private bool frozen;
    private bool paused;
    private bool disposed;
    private long generation;
    private string status = "Select an application to begin.";
    public InspectionTree Tree { get; } = new();
    public ObservableCollection<TargetWindow> Targets { get; } = [];
    public ObservableCollection<KeyValuePair<string, string>> Details { get; } = [];
    public event Action<ElementSnapshot?>? SelectionChanged;

    public InspectorViewModel(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        session.FrameReady += Receive;
        Tree.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Tree.Selected)) UpdateDetails();
        };
    }
    public TargetWindow? SelectedTarget
    {
        get => selectedTarget;
        set
        {
            if (!Set(ref selectedTarget, value)) return;
            generation = session.Attach(value);
            Tree.Clear();
            Paused = false;
            Frozen = false;
            Status = value is null ? "Select an application to begin." : $"Reading {value.Display}…";
        }
    }
    public bool HoverEnabled { get => hoverEnabled; set { if (Set(ref hoverEnabled, value)) UpdateHover(); } }
    public bool Frozen { get => frozen; set { if (Set(ref frozen, value)) UpdateHover(); } }
    public bool Paused { get => paused; set { if (Set(ref paused, value)) session.Paused = value; } }
    public string Status { get => status; private set => Set(ref status, value); }
    public void ReportStatus(string message) => Status = message;
    public void Refresh() => session.Refresh();
    public void Select(ElementNode? node) => Tree.Select(node?.Id);
    private void UpdateHover() => session.HoverEnabled = HoverEnabled && !Frozen;

    public async Task ReloadTargetsAsync()
    {
        try
        {
            var targets = await Task.Run(TargetCatalog.List);
            if (disposed) return;
            // Keep the current target object in the list so reloading does not detach it.
            var current = SelectedTarget;
            if (current is not null && !targets.Any(item => item.ProcessId == current.ProcessId && item.StartTimeTicks == current.StartTimeTicks))
                targets = targets.Append(current).ToArray();
            foreach (var obsolete in Targets.Where(item => !targets.Any(next => next.ProcessId == item.ProcessId && next.StartTimeTicks == item.StartTimeTicks)).ToArray())
                Targets.Remove(obsolete);
            foreach (var item in targets)
                if (!Targets.Any(existing => existing.ProcessId == item.ProcessId && existing.StartTimeTicks == item.StartTimeTicks))
                    Targets.Add(item);
        }
        catch (Exception ex) { Status = $"Could not list applications: {ex.Message}"; }
    }
    private void Receive(InspectionFrame frame)
    {
        if (dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(() =>
        {
            if (disposed || frame.Generation != generation) return;
            if (frame.TargetExited)
            {
                Tree.Clear();
                Status = frame.Error!;
                return;
            }
            if (frame.Error is not null)
            {
                Status = frame.Error + " The last complete tree is retained.";
                SelectionChanged?.Invoke(null);
                return;
            }
            if (frame.Roots is not null)
            {
                try { Tree.Apply(frame.Roots); }
                catch (ArgumentException ex)
                {
                    Status = "Provider returned an invalid tree: " + ex.Message + " The last complete tree is retained.";
                    SelectionChanged?.Invoke(null);
                    return;
                }
                Status = $"{Tree.Count} elements · +{Tree.Added} / −{Tree.Removed} · updated {DateTime.Now:T}";
            }
            if (HoverEnabled && !Frozen && frame.HoverId is not null)
                Tree.Select(frame.HoverId, reveal: true);
        });
    }
    private void UpdateDetails()
    {
        Details.Clear();
        var node = Tree.Selected;
        if (node is not null)
        {
            var item = node.Snapshot;
            void Add(string key, object value) => Details.Add(new(key, value.ToString() ?? ""));
            Add("AutomationId", item.AutomationId);
            Add("Name", item.Name);
            Add("ControlType", item.ControlType);
            Add("ClassName", item.ClassName);
            Add("IsOffscreen", item.IsOffscreen);
            Add("IsEnabled", item.IsEnabled);
            Add("Bounds (pixels)", $"X={item.Bounds.X}, Y={item.Bounds.Y}, W={item.Bounds.Width}, H={item.Bounds.Height}");
            Add("RuntimeId", item.Id);
            var parents = new List<string>();
            for (var parent = node.Parent; parent is not null; parent = parent.Parent) parents.Add(parent.Label);
            parents.Reverse();
            Add("Ancestors", string.Join(" → ", parents));
        }
        SelectionChanged?.Invoke(node?.Snapshot);
    }
    public void Dispose()
    {
        disposed = true;
        session.FrameReady -= Receive;
        session.Dispose();
    }
}