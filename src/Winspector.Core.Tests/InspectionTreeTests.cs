namespace Winspector.Core.Tests;

public sealed class InspectionTreeTests
{
    private static ElementSnapshot Element(string id, string name = "", bool offscreen = false,
        params ElementSnapshot[] children) =>
        new(id, name, id, "Button", "WindowsForms10.BUTTON", offscreen, true, new(10, 20, 100, 30), children);

    [Fact]
    public void ReconcilesAdditionsRemovalsAndOrderWithoutReplacingSurvivingNodes()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("window", children: [Element("a"), Element("b")])]);
        var root = tree.Roots[0];
        var a = root.Children[0];
        root.IsExpanded = true;
        tree.Select("a");
        tree.Apply([Element("window", children: [Element("c"), Element("a", "Updated")])]);
        Assert.Same(root, tree.Roots[0]);
        Assert.Same(a, root.Children[1]);
        Assert.True(root.IsExpanded);
        Assert.Same(a, tree.Selected);
        Assert.Equal("Updated", tree.Selected!.Snapshot.Name);
        Assert.Equal(new[] { "c", "a" }, root.Children.Select(node => node.Id));
        Assert.Equal(1, tree.Added);
        Assert.Equal(1, tree.Removed);
        Assert.Equal("added", root.Children[0].Change);
        Assert.Equal("changed", a.Change);
    }

    [Fact]
    public void TracksOffscreenChangesWithoutRemovingTheElement()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("control")]);
        tree.Select("control");
        tree.Apply([Element("control", offscreen: true)]);
        Assert.True(tree.Selected!.Snapshot.IsOffscreen);
        Assert.Contains("(offscreen)", tree.Selected.Label);
        Assert.Equal("changed", tree.Selected.Change);
        Assert.Equal(0, tree.Removed);
        tree.Apply([Element("control", offscreen: false)]);
        Assert.False(tree.Selected.Snapshot.IsOffscreen);
        Assert.DoesNotContain("(offscreen)", tree.Selected.Label);
    }

    [Fact]
    public void RemovedSelectionClearsRatherThanSelectingAnUnrelatedControl()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("a"), Element("b")]);
        tree.Select("a");
        tree.Apply([Element("b")]);
        Assert.Null(tree.Selected);
        Assert.False(tree.Roots[0].IsSelected);
    }

    [Fact]
    public void HoverSelectionRevealsAncestorsAndUnknownIdPreservesSelection()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("window", children: [Element("panel", children: [Element("button")])])]);
        Assert.True(tree.Select("button", reveal: true));
        Assert.True(tree.Roots[0].IsExpanded);
        Assert.True(tree.Roots[0].Children[0].IsExpanded);
        Assert.True(tree.Selected!.IsSelected);
        Assert.False(tree.Select("outside-target", reveal: true));
        Assert.Equal("button", tree.Selected.Id);
    }

    [Fact]
    public void ReparentingPreservesSelectionAndUpdatesAncestry()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("left", children: [Element("button")]), Element("right")]);
        tree.Select("button");
        var button = tree.Selected;
        tree.Apply([Element("left"), Element("right", children: [Element("button")])]);
        Assert.Same(button, tree.Selected);
        Assert.Equal("right", tree.Selected!.Parent!.Id);
        Assert.Empty(tree.Roots[0].Children);
        Assert.Same(button, tree.Roots[1].Children[0]);
    }

    [Fact]
    public void InvalidSnapshotDoesNotMutateTheLastCompleteTree()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("window", children: [Element("button")])]);
        tree.Select("button");
        var root = tree.Roots[0];
        Assert.Throws<ArgumentException>(() => tree.Apply([Element("duplicate"), Element("duplicate")]));
        Assert.Same(root, tree.Roots[0]);
        Assert.Equal("button", tree.Selected!.Id);
        Assert.Equal(2, tree.Count);
    }

    [Fact]
    public void EmptySnapshotRemovesClosedWindowsAndClearsSelection()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("window", children: [Element("button")])]);
        tree.Select("button");
        tree.Apply([]);
        Assert.Empty(tree.Roots);
        Assert.Null(tree.Selected);
        Assert.Equal(2, tree.Removed);
        Assert.Equal(0, tree.Count);
    }

    [Fact]
    public void UnchangedSnapshotClearsTransientChangeMarkers()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("window")]);
        tree.Apply([Element("window", children: [Element("new")])]);
        Assert.Equal("added", tree.Roots[0].Children[0].Change);
        tree.Apply([Element("window", children: [Element("new")])]);
        Assert.Equal("", tree.Roots[0].Children[0].Change);
        Assert.Equal("", tree.Roots[0].Change);
    }

    [Fact]
    public void ClearResetsTheSessionAndDoesNotReuseRuntimeIdsFromThePreviousTarget()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("same-id")]);
        var old = tree.Roots[0];
        old.IsExpanded = true;
        tree.Select("same-id");
        tree.Clear();
        Assert.Null(tree.Selected);
        Assert.Equal(0, tree.Count);
        tree.Apply([Element("same-id")]);
        Assert.NotSame(old, tree.Roots[0]);
        Assert.False(tree.Roots[0].IsExpanded);
        Assert.Equal("", tree.Roots[0].Change);
    }

    [Fact]
    public void SelectingTheSameNodeDoesNotToggleSelectionButStillRevealsItsAncestors()
    {
        var tree = new InspectionTree();
        tree.Apply([Element("window", children: [Element("button")])]);
        tree.Select("button");
        var selectionChanges = 0;
        tree.Selected!.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ElementNode.IsSelected)) selectionChanges++;
        };
        tree.Select("button", reveal: true);
        tree.Select("button", reveal: true);
        Assert.Equal(0, selectionChanges);
        Assert.True(tree.Roots[0].IsExpanded);
        Assert.True(tree.Selected.IsSelected);
    }
}