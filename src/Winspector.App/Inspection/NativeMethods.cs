using System.Runtime.InteropServices;
using System.Text;

namespace Winspector.App.Inspection;

internal static class NativeMethods
{
    internal delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint window, StringBuilder title, int maximum);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out ScreenPoint point);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(ScreenPoint point);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] internal static extern int SetWindowLong(nint window, int index, int value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScreenPoint { public int X; public int Y; }
}