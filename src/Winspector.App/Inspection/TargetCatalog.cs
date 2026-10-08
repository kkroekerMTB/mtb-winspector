using System.Diagnostics;
using System.Text;

namespace Winspector.App.Inspection;

public static class TargetCatalog
{
    public static IReadOnlyList<TargetWindow> List()
    {
        var targets = new Dictionary<int, TargetWindow>();
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window)) return true;
            var title = new StringBuilder(512);
            NativeMethods.GetWindowText(window, title, title.Capacity);
            if (title.Length == 0) return true;
            NativeMethods.GetWindowThreadProcessId(window, out var id);
            if (id == Environment.ProcessId || targets.ContainsKey((int)id)) return true;
            try
            {
                using var process = Process.GetProcessById((int)id);
                targets[(int)id] = new((int)id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName, title.ToString());
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // A window can close while the catalog is being read.
            }
            return true;
        }, 0);
        return targets.Values.OrderBy(item => item.ProcessName).ThenBy(item => item.ProcessId).ToArray();
    }
}