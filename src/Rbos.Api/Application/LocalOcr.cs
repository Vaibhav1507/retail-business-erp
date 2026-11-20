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
            var timeoutSeconds = Math.Clamp(cfg.GetValue("Ocr:TimeoutSeconds", 12), 3, 60);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            // Drain both redirected streams concurrently; otherwise a verbose Tesseract
            // stderr stream can block the child process while the parent waits for stdout.
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await stdout;
            _ = await stderr;
            return process.ExitCode == 0 ? ToRows(text) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process)
            log.LogWarning("Local OCR exceeded its bounded {TimeoutSeconds}s time budget", Math.Clamp(cfg.GetValue("Ocr:TimeoutSeconds", 12), 3, 60));
            return null;
        }
        catch (OperationCanceledException)
        {
            TryKill(process)
            throw;
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            log.LogWarning(e, "Local OCR failed");
            return null;
        }
        finally
        {
            process?.Dispose();
            try { File.Delete(tmp); } catch (IOException) { }
        }
    }

    static void TryKill(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    public static List<List<string>> ToRows(string text) =>
        text.Split('\n').Select(l => System.Text.RegularExpressions.Regex.Split(l.Trim(), @"\t|\s{2,}").Where(c => c.Length > 0).ToList()).Where(r => r.Count > 0).ToList();
}
