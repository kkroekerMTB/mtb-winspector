namespace Winspector.Core;

/// <summary>Detached UI Automation data. Bounds use physical screen pixels.</summary>
public sealed record ElementSnapshot(string Id, string Name, string AutomationId,
    string ControlType, string ClassName, bool IsOffscreen, bool IsEnabled,
    ElementBounds Bounds, IReadOnlyList<ElementSnapshot> Children);

public readonly record struct ElementBounds(int X, int Y, int Width, int Height);