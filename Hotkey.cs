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
                    var key = (Key)Enum.Parse(typeof(Key), part, true);
                    vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return vk != 0;
    }

    public const int Id = 1;

    public static void Register(IntPtr hwnd, string text)
    {
        Native.UnregisterHotKey(hwnd, Id);
        if (TryParse(text, out var m, out var v))
            Native.RegisterHotKey(hwnd, Id, m | Native.MOD_NOREPEAT, v);
    }
}
