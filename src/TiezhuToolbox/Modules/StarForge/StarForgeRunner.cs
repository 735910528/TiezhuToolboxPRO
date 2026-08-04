using TiezhuToolbox.Modules.Capture;

namespace TiezhuToolbox.Modules.StarForge;

/// <summary>
/// 星之铁匠铺自动变更闭环。每次点击前都重新确认页面、按钮和四条属性，识别不完整时立即停机。
/// </summary>
public sealed class StarForgeRunner : IDisposable
{
    private const double ChangeButtonX = 0.383;
    private const double ChangeButtonY = 0.893;

    private readonly IGameSession _session;
    private readonly IReadOnlyList<StarForgeTarget> _targets;
    private readonly int _maximumChanges;
    private readonly IProgress<StarForgeProgress>? _progress;
    private readonly StarForgeOcrEngine _ocr = new();

    public StarForgeRunner(
        IGameSession session,
        IReadOnlyList<StarForgeTarget> targets,
        int maximumChanges,
        IProgress<StarForgeProgress>? progress = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _targets = targets;
        _maximumChanges = maximumChanges;
        _progress = progress;
    }

    public async Task<StarForgeRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var changes = 0;
        IReadOnlyList<StarForgeStat> lastStats = Array.Empty<StarForgeStat>();
        string? statsBeforeLastChange = null;
        Report(changes, $"星之铁匠铺已启动，共 {_targets.Count} 条目标，最多变更 {_maximumChanges} 次");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (recognition, screenWidth, screenHeight) = await CaptureReliableFrameAsync(cancellationToken);
            if (statsBeforeLastChange != null)
            {
                for (var retry = 0; retry < 3 && CreateStatSignature(recognition.Stats) == statsBeforeLastChange; retry++)
                {
                    await Task.Delay(350, cancellationToken);
                    (recognition, screenWidth, screenHeight) = await CaptureReliableFrameAsync(cancellationToken);
                }
                if (CreateStatSignature(recognition.Stats) == statsBeforeLastChange)
                    throw new InvalidOperationException(
                        "点击变更后四条副属性连续未发生变化，按钮可能不可用或持有点数不足，已安全停机。");
                statsBeforeLastChange = null;
            }

            lastStats = recognition.Stats;
            var currentText = string.Join("，", lastStats.Select(stat => $"{stat.StatName} {stat.DisplayValue}"));
            Report(changes, $"候选属性：{currentText}", isRecognition: true);
            var match = StarForgeRules.Match(lastStats, _targets);
            if (match.IsMatch)
            {
                return new StarForgeRunResult(
                    StarForgeRunStatus.Matched, changes, lastStats,
                    $"已找到满足全部目标的副属性组合，共变更 {changes} 次");
            }

            foreach (var detail in match.Details)
                Report(changes, detail, isRecognition: true);

            if (changes >= _maximumChanges)
            {
                return new StarForgeRunResult(
                    StarForgeRunStatus.MaximumChangesReached, changes, lastStats,
                    $"已达到最多变更次数 {_maximumChanges}，未找到满足全部目标的组合");
            }

            var tapX = Math.Clamp((int)Math.Round(screenWidth * ChangeButtonX), 0, screenWidth - 1);
            var tapY = Math.Clamp((int)Math.Round(screenHeight * ChangeButtonY), 0, screenHeight - 1);
            Report(changes, $"目标未全部命中，点击变更（{tapX}, {tapY}）");
            statsBeforeLastChange = CreateStatSignature(lastStats);
            await Task.Run(() => _session.Tap(tapX, tapY), cancellationToken);
            changes++;

            // 等待变更动画和文字稳定；下一轮仍会重新确认界面再决定是否点击。
            await Task.Delay(900, cancellationToken);
        }
    }

    private async Task<(StarForgeRecognition Recognition, int Width, int Height)> CaptureReliableFrameAsync(
        CancellationToken cancellationToken)
    {
        StarForgeRecognition? lastRecognition = null;
        var width = 0;
        var height = 0;
        for (var retry = 0; retry < 4; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var screenshot = await Task.Run(() => _session.Capture(), cancellationToken);
            width = screenshot.Width;
            height = screenshot.Height;
            lastRecognition = await _ocr.RecognizeAsync(screenshot, cancellationToken);
            if (lastRecognition.IsReliable)
                return (lastRecognition, width, height);

            if (retry < 3)
                await Task.Delay(300, cancellationToken);
        }

        throw new InvalidOperationException(
            "连续 4 次无法完整确认界面：请保持在星之铁匠铺的副能力值变更页面，且右侧完整显示四条属性。\r\n"
            + lastRecognition?.RawText);
    }

    private void Report(int changes, string message, bool isRecognition = false)
        => _progress?.Report(new StarForgeProgress(changes, message, isRecognition));

    private static string CreateStatSignature(IEnumerable<StarForgeStat> stats)
        => string.Join("|", stats.Select(stat => $"{stat.StatName}:{stat.Value:0.##}:{stat.IsPercent}"));

    public void Dispose() => _ocr.Dispose();
}
