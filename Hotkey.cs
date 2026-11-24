using System.Windows.Input;
using System.Windows.Interop;

namespace StreamerMode;

// "Ctrl+Alt+H" -> RegisterHotKey на HWND окна.
public static class Hotkey
{
    public static bool TryParse(string text, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= Native.MOD_CTRL; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win": mods |= Native.MOD_WIN; break;
                default:
                    try { vk = (uint)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), part, true)); }
                    catch { }
                    break;
            }
        }
        return vk != 0;
    }

    public const int MasterId = 1;
    public const int RuleIdBase = 100;   // id правила = 100 + индекс в Rules

    public static void Register(IntPtr hwnd, string text, int id)
    {
        Native.UnregisterHotKey(hwnd, id);
        if (TryParse(text, out var m, out var v))
            Native.RegisterHotKey(hwnd, id, m | Native.MOD_NOREPEAT, v);
    }

    public static void UnregisterAll(IntPtr hwnd, int ruleCount)
    {
        Native.UnregisterHotKey(hwnd, MasterId);
        for (int i = 0; i < ruleCount + 8; i++) Native.UnregisterHotKey(hwnd, RuleIdBase + i);
    }
}
