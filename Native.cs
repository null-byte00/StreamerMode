using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace StreamerMode;

public static class Native
{
    public const uint WDA_NONE = 0x0;
    public const uint WDA_MONITOR = 0x1;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int idx);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    public const uint MOD_ALT = 0x1, MOD_CTRL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x312;
    public const int GW_OWNER = 4;
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WS_EX_APPWINDOW = 0x40000;

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lp);

    public sealed class Win
    {
        public IntPtr Hwnd;
        public string Title = "";
        public string Proc = "";
        public uint Affinity;
    }

    // Видимые top-level окна с заголовком (как в Alt-Tab)
    public static List<Win> EnumUserWindows()
    {
        var list = new List<Win>();
        var self = Environment.ProcessId;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetWindowTextLength(hwnd) == 0) return true;
            if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero) return true;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;

            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            if (title.Length == 0) return true;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == self) return true;
            string proc;
            try { proc = Process.GetProcessById((int)pid).ProcessName; }
            catch { proc = "?"; }
            GetWindowDisplayAffinity(hwnd, out uint aff);
            list.Add(new Win { Hwnd = hwnd, Title = title, Proc = proc, Affinity = aff });
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
