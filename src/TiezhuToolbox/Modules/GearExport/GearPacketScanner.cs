using System.Diagnostics;
using System.Text;

namespace TiezhuToolbox.Modules.GearExport;

/// <summary>启动本机 Python + Scapy 抓取游戏 TCP 包，收集 hex 片段。</summary>
public sealed class GearPacketScanner : IDisposable
{
    private Process? _process;
    private readonly StringBuilder _stdout = new();
    private readonly object _sync = new();
    private bool _disposed;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _process is { HasExited: false };
        }
    }

    public static string? FindPythonCommand()
    {
        foreach (var command in new[] { "py", "python", "python3" })
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(start);
                if (process == null)
                    continue;
                if (!process.WaitForExit(4000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    continue;
                }
                if (process.ExitCode == 0)
                    return command;
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    public static string ResolveScannerScriptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", "GearExport", "scanner.py"),
            Path.Combine(baseDir, "GearExport", "scanner.py"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            "找不到抓包脚本 Assets/GearExport/scanner.py，请重新安装或编译程序。");
    }

    public void Start()
    {
        if (IsRunning)
            throw new InvalidOperationException("扫描已在进行中");

        var python = FindPythonCommand()
            ?? throw new InvalidOperationException(
                "未找到 Python。请安装 Python 3，并勾选「Add Python to PATH」。抓包还需安装 Npcap；Scapy 已随程序附带。");

        var script = ResolveScannerScriptPath();
        lock (_sync)
            _stdout.Clear();

        var start = new ProcessStartInfo
        {
            FileName = python,
            Arguments = $"\"{script}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(script) ?? baseDirSafe(),
        };

        var process = Process.Start(start)
            ?? throw new InvalidOperationException("无法启动 Python 抓包进程");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;
            lock (_sync)
            {
                _stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            // Scapy 噪声较多，仅保留可能有用的错误行供调试。
            Debug.WriteLine("[gear-scanner] " + e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        lock (_sync)
            _process = process;
    }

    public async Task<IReadOnlyList<string>> StopAndCollectAsync(CancellationToken cancellationToken = default)
    {
        Process process;
        lock (_sync)
        {
            process = _process ?? throw new InvalidOperationException("尚未开始扫描");
        }

        try
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteAsync("END\n");
                await process.StandardInput.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("向抓包进程发送停止指令失败：" + ex.Message, ex);
        }

        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (!process.HasExited && DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
            string snapshot;
            lock (_sync)
                snapshot = _stdout.ToString();
            if (snapshot.Contains("DONE", StringComparison.Ordinal))
                break;
        }

        if (!process.HasExited)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        string raw;
        lock (_sync)
            raw = _stdout.ToString();

        lock (_sync)
            _process = null;

        return ParseHexChunks(raw);
    }

    internal static IReadOnlyList<string> ParseHexChunks(string raw)
    {
        return raw
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(chunk => new string(chunk.Where(c => !char.IsWhiteSpace(c)).ToArray()))
            .Where(chunk => chunk.Length > 0 && !chunk.Contains("DONE", StringComparison.Ordinal))
            .Where(chunk => chunk.All(c => Uri.IsHexDigit(c)))
            .ToList();
    }

    private static string baseDirSafe() => AppContext.BaseDirectory;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            lock (_sync)
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
                _process?.Dispose();
                _process = null;
            }
        }
        catch
        {
            // ignore
        }
    }
}
