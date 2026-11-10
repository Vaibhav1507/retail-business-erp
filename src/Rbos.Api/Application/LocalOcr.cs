using System.Diagnostics;

namespace Rbos.Api.Application;

/// <summary>Optional local OCR. Uses a Tesseract executable if one is configured (Ocr:TesseractPath) or found on PATH; otherwise reports unavailable.</summary>
public sealed class LocalOcr(IConfiguration cfg, ILogger<LocalOcr> log)
{
    readonly Lazy<string?> exe = new(() => Locate(cfg["Ocr:TesseractPath"]));

    public bool Available => exe.Value is not null;

    static string? Locate(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? configured : null;
        var name = OperatingSystem.IsWindows() ? "tesseract.exe" : "tesseract";
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { var f = Path.Combine(dir.Trim(), name); if (File.Exists(f)) return f; } catch (ArgumentException) { }
        }
        if (OperatingSystem.IsWindows()) { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", name); if (File.Exists(d)) return d; }
        return null;
    }

    /// <summary>Runs OCR on an image and returns table-like rows (cells split on tabs or runs of 2+ spaces). Null when OCR is unavailable or fails.</summary>
    public async Task<List<List<string>>?> ReadImageAsync(byte[] image, CancellationToken ct)
    {
        if (exe.Value is null) return null;
        var tmp = Path.Combine(Path.GetTempPath(), "rbos-ocr-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(tmp, image, ct);
            var psi = new ProcessStartInfo(exe.Value, [tmp, "stdout", "--psm", "6"]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p is null) return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var text = await p.StandardOutput.ReadToEndAsync(timeout.Token);
            await p.WaitForExitAsync(timeout.Token);
            return p.ExitCode == 0 ? ToRows(text) : null;
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            log.LogWarning(e, "Local OCR failed");
            return null;
        }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
    }

    public static List<List<string>> ToRows(string text) =>
        text.Split('\n').Select(l => System.Text.RegularExpressions.Regex.Split(l.Trim(), @"\t|\s{2,}").Where(c => c.Length > 0).ToList()).Where(r => r.Count > 0).ToList();
}
