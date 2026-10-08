namespace Winspector.App.Inspection;

public sealed record TargetWindow(int ProcessId, long StartTimeTicks, string ProcessName, string Title)
{
    public string Display => $"{ProcessName} ({ProcessId}) — {Title}";
}