using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace StreamerMode;

public partial class App : System.Windows.Application
{
    Settings _s = null!;
    HideEngine _engine = null!;
    MainWindow _win = null!;
    System.Windows.Forms.NotifyIcon? _tray;
    IntPtr _hwnd;
    static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "StreamerMode_single", out bool fresh);
        if (!fresh) { Shutdown(); return; }

        _s = Settings.Load();
        _engine = new HideEngine(_s);
        _engine.RulesChanged += () => Dispatcher.Invoke(RegisterHotkey);
        _win = new MainWindow(_s, _engine);
        _win.Show();
        _win.SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(_win).Handle;
            HwndSource.FromHwnd(_hwnd).AddHook(WndProc);
            RegisterHotkey();
        };
        SetupTray();
        base.OnStartup(e);
    }

    // Мастер-бинд + бинд каждого правила
    public void RegisterHotkey()
    {
        if (_hwnd == IntPtr.Zero) return;
        Hotkey.UnregisterAll(_hwnd, _s.Rules.Count);
        Hotkey.Register(_hwnd, _s.Hotkey, Hotkey.MasterId);
        for (int i = 0; i < _s.Rules.Count; i++)
            if (_s.Rules[i].Hotkey.Length > 0)
                Hotkey.Register(_hwnd, _s.Rules[i].Hotkey, Hotkey.RuleIdBase + i);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            int id = w.ToInt32();
            if (id == Hotkey.MasterId)
            {
                _engine.ModeOn = !_engine.ModeOn;
                Toast(_engine.ModeOn ? "Стример-режим ВКЛ — приватные окна скрыты" : "Стример-режим выкл");
                handled = true;
            }
            else if (id >= Hotkey.RuleIdBase)
            {
                int i = id - Hotkey.RuleIdBase;
                if (i < _s.Rules.Count)
                {
                    var r = _s.Rules[i];
                    _engine.ToggleRule(r);
                    Toast(r.Proc + (r.Mode == HideMode.Off ? " — видно в записи" : " — скрыт"));
                    handled = true;
                }
            }
        }
        return IntPtr.Zero;
    }

    void Toast(string text) => _tray?.ShowBalloonTip(1400, "StreamerMode", text, System.Windows.Forms.ToolTipIcon.Info);

    void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "StreamerMode",
            Visible = true,
            ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip()
        };
        var menu = _tray.ContextMenuStrip;
        var toggle = new System.Windows.Forms.ToolStripMenuItem("Стример-режим");
        toggle.Click += (_, _) => _engine.ModeOn = !_engine.ModeOn;
        menu.Items.Add(toggle);
        menu.Items.Add("Открыть", null, (_, _) => ShowMain());
        menu.Items.Add("Выход", null, (_, _) => RealExit());
        _tray.DoubleClick += (_, _) => ShowMain();
        _engine.StateChanged += () => Dispatcher.Invoke(() =>
        {
            toggle.Checked = _engine.ModeOn;
            _tray.Text = "StreamerMode" + (_engine.ModeOn ? " — режим ВКЛ" : "");
        });
    }

    void ShowMain() => Dispatcher.Invoke(() => { _win.Show(); _win.Activate(); });

    void RealExit()
    {
        _s.ModeOn = false;
        foreach (var w in Native.EnumUserWindows())
            if (w.Affinity != Native.WDA_NONE) Native.SetWindowDisplayAffinity(w.Hwnd, Native.WDA_NONE);
        _tray!.Visible = false;
        Shutdown();
    }

    public static void SetAutostart(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (on) key?.SetValue("StreamerMode", '"' + Environment.ProcessPath + '"');
        else key?.DeleteValue("StreamerMode", false);
    }
}
