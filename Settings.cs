using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamerMode;

public enum HideMode { Off, OnMode, Always }

public sealed class Rule
{
    public string Proc { get; set; } = "";
    public string TitleContains { get; set; } = "";   // пусто = любое окно процесса
    public string FullTitle { get; set; } = "";       // исходный заголовок — для скоупа «окно» и отображения
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HideMode Mode { get; set; } = HideMode.OnMode;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HideMode PrevMode { get; set; } = HideMode.Always; // куда вернуть хоткеем
    public string Hotkey { get; set; } = "";          // свой бинд: скрыл/вернул приложение

    public bool Matches(Native.Win w) =>
        string.Equals(w.Proc, Proc, StringComparison.OrdinalIgnoreCase)
        && (TitleContains.Length == 0 || w.Title.Contains(TitleContains, StringComparison.OrdinalIgnoreCase));

    public string Key => Proc + "|" + TitleContains;
}

public sealed class Settings
{
    public List<Rule> Rules { get; set; } = new();
    public bool ModeOn { get; set; }           // стример-режим включён
    public bool StartWithWindows { get; set; }
    public string Hotkey { get; set; } = "Ctrl+Alt+H"; // мастер-тогл режима
    public bool MinimizeToTray { get; set; } = true;

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamerMode");
    static readonly string File = Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(File)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
