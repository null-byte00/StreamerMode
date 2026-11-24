using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace StreamerMode;

public static class Updater
{
    const string Repo = "null-byte00/StreamerMode";
    static readonly HttpClient Http = new() { DefaultRequestHeaders = { { "User-Agent", "StreamerMode" } } };

    public static string Current =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static async Task<(bool hasNew, string tag, string url)> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            string url = "";
            foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    url = a.GetProperty("browser_download_url").GetString() ?? "";
            }
            var hasNew = Version.TryParse(tag.TrimStart('v'), out var lat)
                && Version.TryParse(Current, out var cur) && lat > cur;
            return (hasNew, tag, url);
        }
        catch { return (false, "", ""); }
    }

    public static async Task DownloadAsync(string url, string path, IProgress<int>? progress = null)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(path);
        var buf = new byte[65536]; long got = 0; int n;
        while ((n = await src.ReadAsync(buf)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n));
            got += n;
            if (total > 0) progress?.Report((int)(got * 100 / total));
        }
    }

    // Подмена себя через cmd-скрипт: ждёт смерти процесса, кладёт новый exe, перезапускает.
    public static void ApplyAndRestart(string newExePath)
    {
        var exe = Environment.ProcessPath!;
        var pid = Environment.ProcessId;
        var bat = Path.Combine(Path.GetTempPath(), "sm_update.bat");
        File.WriteAllText(bat,
            "@echo off\r\n" +
            $":w\r\ntasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto w)\r\n" +
            $"move /y \"{newExePath}\" \"{exe}\" >nul\r\n" +
            $"start \"\" \"{exe}\"\r\n" +
            "del \"%~f0\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"")
        { CreateNoWindow = true, UseShellExecute = false });
        Environment.Exit(0);
    }
}
