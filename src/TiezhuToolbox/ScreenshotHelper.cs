using System.Drawing.Imaging;

namespace TiezhuToolbox;

/// <summary>
/// 截图辅助类：截图文件保存。
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>最多保留的原始截图数量；对应的调试图会随原图一起清理。</summary>
    private const int MaxSavedScreenshotCount = 100;

    private static readonly object RetentionLock = new();

    /// <summary>
    /// 获取截图保存目录。
    /// 注意：单文件发布时 AppDomain.CurrentDomain.BaseDirectory 指向临时解压目录，
    /// 因此使用当前进程可执行文件所在目录。
    /// </summary>
    public static string GetScreenshotDirectory()
    {
        var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(exeDir, "screenshots");
    }

    /// <summary>
    /// 保存位图到 screenshots 目录，文件名包含时间戳。
    /// </summary>
    public static string SaveBitmap(Bitmap bitmap, string baseName)
    {
        var dir = GetScreenshotDirectory();
        Directory.CreateDirectory(dir);

        var fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var fileNameSafe = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(dir, fileNameSafe);

        bitmap.Save(path, ImageFormat.Png);
        PruneOldScreenshots(dir, path, MaxSavedScreenshotCount);
        return path;
    }

    /// <summary>
    /// 只按原始截图计数，删除超出上限的最旧截图，并同步删除 OCR 生成的调试图和等级裁剪图。
    /// 清理失败不能影响本次截图及后续识别。
    /// </summary>
    private static void PruneOldScreenshots(string directory, string currentPath, int maxCount)
    {
        try
        {
            lock (RetentionLock)
            {
                var currentFullPath = Path.GetFullPath(currentPath);
                var oldScreenshots = Directory
                    .EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
                    .Where(path => !IsOcrDerivative(path))
                    .Where(path => !string.Equals(
                        Path.GetFullPath(path), currentFullPath, StringComparison.OrdinalIgnoreCase))
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .Skip(Math.Max(0, maxCount - 1))
                    .ToArray();

                foreach (var screenshot in oldScreenshots)
                {
                    var basePath = Path.Combine(
                        screenshot.DirectoryName ?? directory,
                        Path.GetFileNameWithoutExtension(screenshot.Name));
                    TryDelete(basePath + "_debug.png");
                    TryDelete(basePath + "_level.png");
                    TryDelete(screenshot.FullName);
                }
            }
        }
        catch
        {
            // 截图目录可能只读、被占用或正在被外部程序浏览；清理失败不影响识别。
        }
    }

    private static bool IsOcrDerivative(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.EndsWith("_debug", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith("_level", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 单个文件被占用时继续清理其他过期截图。
        }
    }
}
