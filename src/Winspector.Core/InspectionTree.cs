using System.Collections.ObjectModel;

namespace Winspector.Core;

/// <summary>Apply complete snapshots on the UI thread, retaining node identity and UI state.</summary>
public sealed class InspectionTree : ObservableObject
{
    private Dictionary<string, ElementNode> nodes = [];
    private ElementNode? selected;
    private bool hasSnapshot;
    public ObservableCollection<ElementNode> Roots { get; } = [];
    public ElementNode? Selected => selected;
    public bool IsUpdating { get; private set; }
    public int Count => nodes.Count;
    public int Added { get; private set; }
    public int Removed { get; private set; }

    public void Apply(IReadOnlyList<ElementSnapshot> roots)
    {
        var ids = new HashSet<string>();
        void Validate(ElementSnapshot item)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
                throw new ArgumentException("Snapshot runtime IDs must be unique and nonempty.", nameof(roots));
            foreach (var child in item.Children) Validate(child);
        }
        foreach (var root in roots) Validate(root);
        IsUpdating = true;
        try
        {
            foreach (var node in nodes.Values) node.Change = "";
            var next = new Dictionary<string, ElementNode>();
            Added = 0;
            Removed = nodes.Keys.Count(id => !ids.Contains(id));
            ElementNode Reconcile(ElementSnapshot item, ElementNode? parent)
            {
                if (!nodes.TryGetValue(item.Id, out var node))
                {
                    node = new ElementNode(item) { Change = hasSnapshot ? "added" : "" };
                    Added++;
                }
                else node.Update(item);
                node.Parent = parent;
                next.Add(item.Id, node);
                Synchronize(node.Children, item.Children.Select(child => Reconcile(child, node)).ToArray());
                return node;
            }
            Synchronize(Roots, roots.Select(root => Reconcile(root, null)).ToArray());
            nodes = next;
            hasSnapshot = true;
            // WPF may select a fallback parent when a selected container is disconnected.
            // Keep the model authoritative while reconciliation is in progress.
            if (selected is not null && nodes.ContainsKey(selected.Id))
                for (var parent = selected.Parent; parent is not null; parent = parent.Parent)
                    parent.IsExpanded = true;
            foreach (var node in nodes.Values)
                node.IsSelected = ReferenceEquals(node, selected);
            if (selected is not null && !nodes.ContainsKey(selected.Id)) SetSelection(null);
        }
        finally { IsUpdating = false; }
        Notify(nameof(Selected));
        Notify(nameof(Count));
        Notify(nameof(Added));
        Notify(nameof(Removed));
    }

    public bool Select(string? id, bool reveal = false)
    {
        if (IsUpdating || (id is not null && !nodes.ContainsKey(id))) return false;
        var nextSelection = id is null ? null : nodes[id];
        if (reveal)
            for (var parent = nextSelection?.Parent; parent is not null; parent = parent.Parent)
                parent.IsExpanded = true;
        if (ReferenceEquals(selected, nextSelection)) return true;
        SetSelection(nextSelection);
        Notify(nameof(Selected));
        return true;
    }

    private void SetSelection(ElementNode? nextSelection)
    {
        if (selected is not null) selected.IsSelected = false;
        selected = nextSelection;
        if (selected is not null) selected.IsSelected = true;
    }

    public void Clear()
    {
        IsUpdating = true;
        try
        {
            SetSelection(null);
            Roots.Clear();
            nodes.Clear();
            hasSnapshot = false;
            Added = Removed = 0;
        }
        finally { IsUpdating = false; }
        Notify(nameof(Selected));
        Notify(nameof(Count));
        Notify(nameof(Added));
        Notify(nameof(Removed));
    }

    private static void Synchronize(ObservableCollection<ElementNode> collection, ElementNode[] desired)
    {
        var wanted = desired.ToHashSet();
        for (var i = collection.Count - 1; i >= 0; i--)
            if (!wanted.Contains(collection[i])) collection.RemoveAt(i);
        for (var i = 0; i < desired.Length; i++)
        {
            var existing = collection.IndexOf(desired[i]);
            if (existing < 0) collection.Insert(i, desired[i]);
            else if (existing != i) collection.Move(existing, i);
        }
    }
}