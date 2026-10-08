using System.Collections.ObjectModel;

namespace Winspector.Core;

public sealed class ElementNode : ObservableObject
{
    private ElementSnapshot snapshot;
    private bool isExpanded;
    private bool isSelected;
    private string change = "";
    internal ElementNode(ElementSnapshot snapshot) => this.snapshot = snapshot;
    public string Id => snapshot.Id;
    public ElementSnapshot Snapshot => snapshot;
    public ElementNode? Parent { get; internal set; }
    public ObservableCollection<ElementNode> Children { get; } = [];
    public string Label => $"{snapshot.ControlType}  {snapshot.Name}" +
        (string.IsNullOrEmpty(snapshot.AutomationId) ? "" : $"  [{snapshot.AutomationId}]") +
        (snapshot.IsOffscreen ? "  (offscreen)" : "");
    public bool IsExpanded { get => isExpanded; set => Set(ref isExpanded, value); }
    public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }
    public string Change { get => change; internal set => Set(ref change, value); }
    internal void Update(ElementSnapshot next)
    {
        if (snapshot with { Children = next.Children } != next) Change = "changed";
        snapshot = next;
        Notify(nameof(Snapshot));
        Notify(nameof(Label));
    }
}