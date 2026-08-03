using System.Drawing.Imaging;
using OpenCvSharp;
using TiezhuToolbox.Modules.Ocr;

namespace TiezhuToolbox.Modules.StarForge;

/// <summary>只识别星之铁匠铺右侧四条候选副属性，并用页面标题和变更按钮双重确认界面。</summary>
public sealed class StarForgeOcrEngine : IDisposable
{
    private readonly PaddleOcrEngine _paddle;

    public StarForgeOcrEngine()
    {
        var modelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PaddleOCR");
        _paddle = new PaddleOcrEngine(modelDirectory);
    }

    public Task<StarForgeRecognition> RecognizeAsync(Bitmap screenshot, CancellationToken cancellationToken)
        => Task.Run(() => Recognize(screenshot, cancellationToken), cancellationToken);

    internal StarForgeRecognition Recognize(Bitmap screenshot, CancellationToken cancellationToken = default)
    {
        using var full = BitmapToMat(screenshot);
        cancellationToken.ThrowIfCancellationRequested();

        // 坐标均来自游戏画面的相对位置，可适配相同比例的不同模拟器分辨率。
        using var title = CropNormalized(full, 0.035, 0.008, 0.190, 0.090);
        using var action = CropNormalized(full, 0.300, 0.830, 0.535, 0.950);
        using var statArea = CropNormalized(full, 0.395, 0.235, 0.665, 0.535);

        var titleText = _paddle.RecognizeLine(title);
        var actionWords = _paddle.Run(action);
        var actionText = string.Concat(actionWords.OrderBy(word => word.Box.X).Select(word => word.Text));
        var words = _paddle.Run(statArea);
        cancellationToken.ThrowIfCancellationRequested();

        var lines = words
            .GroupBy(word => (int)Math.Round((word.Box.Y + word.Box.Height / 2D) / Math.Max(10D, statArea.Height * 0.075)))
            .Select(group => new
            {
                Y = group.Min(word => word.Box.Y),
                Text = string.Concat(group.OrderBy(word => word.Box.X).Select(word => word.Text)),
            })
            .OrderBy(line => line.Y)
            .ToList();

        var stats = new List<StarForgeStat>(4);
        foreach (var line in lines)
        {
            if (StarForgeRules.TryParseStatLine(line.Text, out var stat))
                stats.Add(stat);
        }

        // 某些帧会把同一行拆进相邻分组；失败时按固定的四条横带做单行识别兜底。
        if (stats.Count != 4)
        {
            stats.Clear();
            foreach (var (top, bottom) in new[] { (0.00, 0.22), (0.22, 0.47), (0.47, 0.73), (0.73, 1.00) })
            {
                using var row = CropNormalized(statArea, 0.05, top, 0.98, bottom);
                var rowText = _paddle.RecognizeLine(row);
                if (StarForgeRules.TryParseStatLine(rowText, out var stat))
                    stats.Add(stat);
            }
        }

        var isForgeScreen = titleText.Contains("铁", StringComparison.Ordinal)
                            && titleText.Contains("工", StringComparison.Ordinal);
        var canChange = actionText.Contains("变更", StringComparison.Ordinal)
                        && (actionText.Contains("副", StringComparison.Ordinal)
                            || actionText.Contains("能力", StringComparison.Ordinal));
        var rawText = $"标题：{titleText}{Environment.NewLine}按钮：{actionText}{Environment.NewLine}"
                      + string.Join(Environment.NewLine, lines.Select(line => line.Text));
        return new StarForgeRecognition(isForgeScreen, canChange, stats, rawText);
    }

    private static Mat BitmapToMat(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return Cv2.ImDecode(stream.ToArray(), ImreadModes.Color);
    }

    private static Mat CropNormalized(Mat source, double left, double top, double right, double bottom)
    {
        var x = Math.Clamp((int)Math.Round(source.Width * left), 0, source.Width - 1);
        var y = Math.Clamp((int)Math.Round(source.Height * top), 0, source.Height - 1);
        var x2 = Math.Clamp((int)Math.Round(source.Width * right), x + 1, source.Width);
        var y2 = Math.Clamp((int)Math.Round(source.Height * bottom), y + 1, source.Height);
        return new Mat(source, new Rect(x, y, x2 - x, y2 - y)).Clone();
    }

    public void Dispose() => _paddle.Dispose();
}
