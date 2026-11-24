namespace StreamerMode;

// Фоново следит за окнами и ставит/снимает WDA_EXCLUDEFROMCAPTURE по правилам.
public sealed class HideEngine
{
    readonly Settings _s;
    readonly System.Threading.Timer _timer;
    public event Action? StateChanged;   // после тика — обновить статус в UI

    public HideEngine(Settings s)
    {
        _s = s;
        _timer = new System.Threading.Timer(Tick, null, 300, 1500);
    }

    public bool ModeOn
    {
        get => _s.ModeOn;
        set { _s.ModeOn = value; _s.Save(); Tick(null); }
    }

    void Tick(object? _)
    {
        foreach (var w in Native.EnumUserWindows())
        {
            uint want = Native.WDA_NONE;
            foreach (var r in _s.Rules)
            {
                if (!r.Matches(w)) continue;
                if (r.Mode == HideMode.Always || (r.Mode == HideMode.OnMode && _s.ModeOn))
                    want = Native.WDA_EXCLUDEFROMCAPTURE;
            }
            if (w.Affinity != want)
                Native.SetWindowDisplayAffinity(w.Hwnd, want);
        }
        StateChanged?.Invoke();
    }

    public Rule RuleFor(Native.Win w) =>
        _s.Rules.FirstOrDefault(r => r.Matches(w)) ?? new Rule
        { Proc = w.Proc, TitleContains = w.Title, Mode = HideMode.Off };

    public void SetRule(Native.Win w, HideMode mode, bool wholeProc)
    {
        var pattern = wholeProc ? "" : w.Title;
        _s.Rules.RemoveAll(r => r.Key == w.Proc + "|" + pattern || (r.Proc == w.Proc && wholeProc && r.TitleContains == ""));
        var ex = _s.Rules.FirstOrDefault(r => r.Proc.Equals(w.Proc, StringComparison.OrdinalIgnoreCase) && r.TitleContains == pattern);
        if (mode == HideMode.Off) _s.Rules.Remove(ex!);
        else if (ex != null) ex.Mode = mode;
        else _s.Rules.Add(new Rule { Proc = w.Proc, TitleContains = pattern, Mode = mode });
        _s.Save();
        Tick(null);
    }
}
