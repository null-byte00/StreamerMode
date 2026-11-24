using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreamerMode;

public partial class MainWindow : Window
{
    readonly Settings _s;
    readonly HideEngine _engine;
    bool _loading;

    sealed class Row : INotifyPropertyChanged
    {
        public Native.Win Win = null!;
        public string Title => Win.Title;
        public string Proc => Win.Proc;
        public bool WholeProc { get; set; }
        int _modeIndex;
        public int ModeIndex { get => _modeIndex; set { _modeIndex = value; } }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }

    public MainWindow(Settings s, HideEngine engine)
    {
        _s = s; _engine = engine;
        InitializeComponent();
        _loading = true;
        ModeToggle.IsChecked = s.ModeOn;
        ModeStatus.Text = s.ModeOn ? "ВКЛ — окна 'по режиму' скрыты в записи" : "выкл — всё видно в записи";
        HotkeyBox.Text = s.Hotkey;
        AutostartBox.IsChecked = s.StartWithWindows;
        _loading = false;
        _engine.StateChanged += () => Dispatcher.Invoke(UpdateStatus);
        RefreshWindows();
        RefreshRules();
    }

    void UpdateStatus() =>
        ModeStatus.Text = _s.ModeOn ? "ВКЛ — окна 'по режиму' скрыты в записи" : "выкл — всё видно в записи";

    public void ToggleMode() => ModeToggle.IsChecked = !_s.ModeOn;

    void ModeToggle_Checked(object s, RoutedEventArgs e) { if (!_loading) { _engine.ModeOn = true; UpdateStatus(); } }
    void ModeToggle_Unchecked(object s, RoutedEventArgs e) { if (!_loading) { _engine.ModeOn = false; UpdateStatus(); } }

    void Refresh_Click(object s, RoutedEventArgs e) => RefreshWindows();

    void RefreshWindows()
    {
        var rows = new List<Row>();
        foreach (var w in Native.EnumUserWindows().OrderBy(w => w.Proc).ThenBy(w => w.Title))
        {
            var rule = _s.Rules.FirstOrDefault(r => r.Matches(w));
            rows.Add(new Row
            {
                Win = w,
                WholeProc = rule != null && rule.TitleContains == "",
                ModeIndex = rule?.Mode switch { HideMode.Always => 2, HideMode.OnMode => 1, _ => 0 }
            });
        }
        WinList.ItemsSource = rows;
    }

    void RefreshRules()
    {
        RulesList.ItemsSource = _s.Rules.Select(r =>
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var del = new Button { Content = "✕", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0) };
            del.Click += (_, _) => { _s.Rules.Remove(r); _s.Save(); RefreshRules(); RefreshWindows(); };
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            var label = r.TitleContains == "" ? r.Proc + " — любое окно" : r.Proc + " — «" + r.TitleContains + "»";
            row.Children.Add(new TextBlock
            {
                Text = label + "   [" + (r.Mode == HideMode.Always ? "всегда" : "по режиму") + "]",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.LightGray
            });
            return row;
        }).ToList();
    }

    void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || sender is not ComboBox cb || cb.Tag is not Row row) return;
        var mode = cb.SelectedIndex switch { 1 => HideMode.OnMode, 2 => HideMode.Always, _ => HideMode.Off };
        _engine.SetRule(row.Win, mode, row.WholeProc);
        RefreshRules();
    }

    void WholeProc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.DataContext is not Row row) return;
        var mode = row.ModeIndex switch { 1 => HideMode.OnMode, 2 => HideMode.Always, _ => HideMode.Off };
        _engine.SetRule(row.Win, mode, row.WholeProc);
        RefreshRules();
    }

    void HotkeyBox_LostFocus(object s, RoutedEventArgs e)
    {
        _s.Hotkey = HotkeyBox.Text;
        _s.Save();
        ((App)Application.Current).RegisterHotkey();
    }

    void Autostart_Changed(object s, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.StartWithWindows = AutostartBox.IsChecked == true;
        _s.Save();
        App.SetAutostart(_s.StartWithWindows);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_s.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    // Хоткей-бокс: зажал комбо — записало текстом
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        HotkeyBox.PreviewKeyDown += HotkeyBox_KeyDown;
    }

    void HotkeyBox_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin) return;
        var parts = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        HotkeyBox.Text = string.Join("+", parts);
    }
}
