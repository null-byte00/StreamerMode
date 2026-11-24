using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;

namespace StreamerMode;

public partial class MainWindow : Window
{
    readonly Settings _s;
    readonly HideEngine _engine;
    bool _loading;
    Rule? _binding;            // правило, для которого ловим комбо

    sealed class WinRow
    {
        public Native.Win Win = null!;
        public string Title => Win.Title;
        public string Proc => Win.Proc;
    }

    public MainWindow(Settings s, HideEngine engine)
    {
        _s = s; _engine = engine;
        InitializeComponent();
        _loading = true;
        ModeToggle.IsChecked = s.ModeOn;
        RefreshModeStatus();
        HotkeyBox.Text = s.Hotkey;
        AutostartBox.IsChecked = s.StartWithWindows;
        _loading = false;
        _engine.StateChanged += () => Dispatcher.Invoke(RefreshModeStatus);
        RefreshWindows();
        RefreshRules();
        CheckUpdate();
    }

    // ---------- апдейт ----------
    string _updateUrl = "";
    bool _updating;

    async void CheckUpdate()
    {
        UpdateStatus.Text = "проверяю обновления…";
        var (hasNew, tag, url) = await Updater.CheckAsync();
        if (hasNew)
        {
            UpdateStatus.Text = "есть " + tag;
            UpdateBtn.Content = "→ " + tag;
            UpdateBtn.Visibility = Visibility.Visible;
            _updateUrl = url;
        }
        else UpdateStatus.Text = "v" + Updater.Current;
    }

    async void Update_Click(object s, RoutedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "StreamerMode-new.exe");
            var prog = new Progress<int>(p => UpdateStatus.Text = "качаю… " + p + "%");
            UpdateBtn.IsEnabled = false;
            await Updater.DownloadAsync(_updateUrl, tmp, prog);
            UpdateStatus.Text = "ставлю…";
            Updater.ApplyAndRestart(tmp);
        }
        catch (Exception ex)
        {
            UpdateStatus.Text = "ошибка: " + ex.Message;
            _updating = false;
            UpdateBtn.IsEnabled = true;
        }
    }

    // ---------- режим ----------
    void RefreshModeStatus() =>
        ModeStatus.Text = _s.ModeOn ? "ВКЛ — окна «по режиму» скрыты" : "выкл — всё видно в записи";

    void ModeToggle_Checked(object s, RoutedEventArgs e) { if (!_loading) { _engine.ModeOn = true; RefreshModeStatus(); } }
    void ModeToggle_Unchecked(object s, RoutedEventArgs e) { if (!_loading) { _engine.ModeOn = false; RefreshModeStatus(); } }

    void Refresh_Click(object s, RoutedEventArgs e) { RefreshWindows(); RefreshRules(); }

    // ---------- список окон (справа) ----------
    void RefreshWindows()
    {
        WinList.ItemsSource = Native.EnumUserWindows()
            .OrderBy(w => w.Proc).ThenBy(w => w.Title)
            .Select(w => new WinRow { Win = w }).ToList();
    }

    void Hide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not WinRow row) return;
        _engine.SetRule(row.Win, HideMode.Always, false);
        RefreshRules();
    }

    // ---------- список приложений (слева) ----------
    static readonly Brush Txt3 = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
    static readonly Brush Txt = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
    static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
    static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0xA4));

    Button Pill(string text, Brush fg) => new()
    {
        Style = (Style)FindResource("FlatBtn"),
        Content = text,
        Foreground = fg,
        Padding = new Thickness(8, 3, 8, 3),
        Margin = new Thickness(4, 0, 0, 0)
    };

    void RefreshRules()
    {
        RulesList.ItemsSource = _s.Rules.Select(r =>
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = r.TitleContains == "" ? r.Proc + " — всё" : r.Proc;
            var label = new TextBlock
            {
                Text = r.TitleContains == "" ? r.Proc + " — все окна" : r.Proc + " — «" + r.TitleContains + "»",
                Foreground = Txt, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = r.FullTitle
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // бинд хоткея
            var bind = Pill(r.Hotkey.Length > 0 ? r.Hotkey : "+ bind", Txt3);
            bind.MinWidth = 62;
            bind.ToolTip = "Клик — задать хоткей этого приложения";
            bind.Tag = r;
            bind.Click += Bind_Click;
            Grid.SetColumn(bind, 1);
            grid.Children.Add(bind);

            // скоуп окно/проц
            var scope = Pill(r.TitleContains == "" ? "проц" : "окно", Txt3);
            scope.ToolTip = "Одно окно или весь процесс";
            scope.Tag = r;
            scope.Click += Scope_Click;
            Grid.SetColumn(scope, 2);
            grid.Children.Add(scope);

            // режим
            var (mt, mc) = r.Mode switch
            {
                HideMode.Always => ("всегда", Red),
                HideMode.OnMode => ("режим", Accent),
                _ => ("выкл", Txt3)
            };
            var mode = Pill(mt, mc);
            mode.MinWidth = 62;
            mode.ToolTip = "Клик — сменить режим";
            mode.Tag = r;
            mode.Click += Mode_Click;
            Grid.SetColumn(mode, 3);
            grid.Children.Add(mode);

            var del = new Button
            {
                Style = (Style)FindResource("IconBtn"),
                Content = "✕",
                Foreground = Txt3,
                Margin = new Thickness(4, 0, 0, 0)
            };
            del.Tag = r;
            del.Click += Del_Click;
            Grid.SetColumn(del, 4);
            grid.Children.Add(del);

            return grid;
        }).ToList();
    }

    void Del_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: Rule r }) return;
        _s.Rules.Remove(r);
        _s.Save();
        _engine.NotifyRulesChanged();
        _engine.TickPublic();
        RefreshRules();
        RefreshWindows();
    }

    void Scope_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: Rule r }) return;
        if (r.TitleContains == "")
            r.TitleContains = r.FullTitle.Length > 0 ? r.FullTitle : r.Proc;
        else
            r.TitleContains = "";
        _s.Save();
        _engine.TickPublic();
        RefreshRules();
    }

    void Mode_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button { Tag: Rule r }) return;
        r.Mode = r.Mode switch { HideMode.OnMode => HideMode.Always, HideMode.Always => HideMode.Off, _ => HideMode.OnMode };
        _s.Save();
        _engine.TickPublic();
        RefreshRules();
    }

    // --- бинд на правило ---
    void Bind_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button b || b.Tag is not Rule r) return;
        _binding = r;
        b.Content = "жми…";
        b.Focus();
        b.PreviewKeyDown -= RuleKeyDown;
        b.PreviewKeyDown += RuleKeyDown;
    }

    void RuleKeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        if (_binding == null) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var parts = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        _binding.Hotkey = string.Join("+", parts);
        _binding = null;
        _s.Save();
        _engine.NotifyRulesChanged();
        Keyboard.ClearFocus();
        RefreshRules();
    }

    // --- мастер-хоткей ---
    void Hotkey_GotFocus(object s, RoutedEventArgs e) => HotkeyBox.Text = "зажми комбо…";

    void HotkeyBox_LostFocus(object s, RoutedEventArgs e)
    {
        if (HotkeyBox.Text == "зажми комбо…") HotkeyBox.Text = _s.Hotkey;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        HotkeyBox.PreviewKeyDown -= HotkeyBox_KeyDown;
        HotkeyBox.PreviewKeyDown += HotkeyBox_KeyDown;
    }

    void HotkeyBox_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var parts = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        var text = string.Join("+", parts);
        HotkeyBox.Text = text;
        _s.Hotkey = text;
        _s.Save();
        ((App)Application.Current).RegisterHotkey();
        Keyboard.ClearFocus();
    }

    // ---------- прочее ----------
    void Autostart_Changed(object s, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.StartWithWindows = AutostartBox.IsChecked == true;
        _s.Save();
        App.SetAutostart(_s.StartWithWindows);
    }

    void TitleBar_Drag(object s, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    void Min_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object s, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_s.MinimizeToTray) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
}
