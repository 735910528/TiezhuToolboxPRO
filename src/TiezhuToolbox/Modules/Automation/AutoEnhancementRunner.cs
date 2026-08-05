using TiezhuToolbox.Modules.Capture;
using TiezhuToolbox.Modules.Ocr;
using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox.Modules.Automation;

public enum AutoEnhancementLogLevel
{
    Info,
    Action,
    Recognition,
    Warning,
    Error,
    Success,
}

public enum EquipmentDisposalMethod
{
    Sell,
    Extract,
}

/// <summary>自动强化页任务模式：完整强化，或仅按建议出售整理。</summary>
public enum AutoRunMode
{
    Enhance,
    OrganizeSellOnly,
}

/// <summary>单件装备在本轮自动强化中的最终处置。</summary>
public enum AutoEnhancementOutcome
{
    Sold,
    Extracted,
    Kept,
    KeptAndStopped,
    Skipped,
}

public sealed record AutoEnhancementEquipmentRecord(
    int Index,
    string SetName,
    string Part,
    string Quality,
    int Level,
    int EnhanceLevel,
    double Score,
    int Speed,
    string AdviceText,
    string AdviceDetail,
    AutoEnhancementOutcome Outcome,
    string OutcomeText,
    string ScreenshotPath,
    IReadOnlyList<string> SubStats);

public sealed record AutoEnhancementProgress(
    AutoEnhancementLogLevel Level,
    string Message,
    int Processed,
    int Enhanced,
    int Sold,
    int Extracted,
    int Kept,
    int Skipped = 0,
    AutoEnhancementEquipmentRecord? Equipment = null)
{
    /// <summary>简要模式下默认展示的级别（操作/普通信息不显示）。</summary>
    public bool VisibleInBriefMode =>
        Level is AutoEnhancementLogLevel.Success
            or AutoEnhancementLogLevel.Error
            or AutoEnhancementLogLevel.Warning
            or AutoEnhancementLogLevel.Recognition
        || Equipment != null;
}

public sealed record ReforgeEquipmentSummary(
    string SetName,
    string Part,
    IReadOnlyList<string> SubStats);

public sealed record AutoEnhancementSummary(
    int Processed,
    int Enhanced,
    int Sold,
    int Extracted,
    int Kept,
    int Skipped,
    IReadOnlyList<AutoEnhancementEquipmentRecord> Equipment,
    IReadOnlyList<ReforgeEquipmentSummary> ReforgeEquipment);

public sealed record AutoEnhancementOptions(
    int MaxEquipment,
    double LeftThreshold,
    double RightThreshold,
    double Level88Threshold,
    double MinimumDemandMatchScore,
    EquipmentDisposalMethod DisposalMethod,
    bool StopOnValuableEquipment,
    bool HeroicOnlyGambleSpeed,
    bool SpeedSetRequiresSpeed,
    bool CriticalNecklaceMainStatRule,
    IReadOnlySet<string> DisabledDemandProfiles,
    LegendarySpeedLadder LegendarySpeedLadder,
    TimeSpan UiTimeout,
    TimeSpan AnimationMinimumWait,
    AutoRunMode Mode = AutoRunMode.Enhance)
{
    public bool IsOrganizeSellOnly => Mode == AutoRunMode.OrganizeSellOnly;

    public static AutoEnhancementOptions CreateDefault(
        int maxEquipment,
        double leftThreshold,
        double rightThreshold,
        double level88Threshold,
        double minimumDemandMatchScore = EnhancementAdvisor.DefaultMinimumDemandMatchScore,
        EquipmentDisposalMethod disposalMethod = EquipmentDisposalMethod.Sell,
        bool stopOnValuableEquipment = true,
        bool heroicOnlyGambleSpeed = false,
        bool speedSetRequiresSpeed = true,
        bool criticalNecklaceMainStatRule = true,
        IReadOnlySet<string>? disabledDemandProfiles = null,
        LegendarySpeedLadder? legendarySpeedLadder = null,
        AutoRunMode mode = AutoRunMode.Enhance)
    {
        var ladder = (legendarySpeedLadder ?? LegendarySpeedLadder.CreateDefault()).Clone();
        ladder.Normalize();
        var resolvedDisposal = mode == AutoRunMode.OrganizeSellOnly
            ? EquipmentDisposalMethod.Sell
            : disposalMethod;
        var resolvedStop = mode == AutoRunMode.OrganizeSellOnly
            ? false
            : stopOnValuableEquipment;
        return new(
            Math.Clamp(maxEquipment, 1, 999),
            leftThreshold,
            rightThreshold,
            level88Threshold,
            Math.Clamp(minimumDemandMatchScore, 0, 100),
            resolvedDisposal,
            resolvedStop,
            heroicOnlyGambleSpeed,
            speedSetRequiresSpeed,
            criticalNecklaceMainStatRule,
            disabledDemandProfiles == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(disabledDemandProfiles, StringComparer.Ordinal),
            ladder,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(4),
            mode);
    }
}

public sealed record AutoEnhancementResult(
    AutoEnhancementSummary Summary,
    bool StoppedForValuableEquipment,
    string Message)
{
    public int Processed => Summary.Processed;
    public int Enhanced => Summary.Enhanced;
    public int Sold => Summary.Sold;
    public int Extracted => Summary.Extracted;
    public int Kept => Summary.Kept;
    public int Skipped => Summary.Skipped;
    public IReadOnlyList<AutoEnhancementEquipmentRecord> Equipment => Summary.Equipment;
    public IReadOnlyList<ReforgeEquipmentSummary> ReforgeEquipment => Summary.ReforgeEquipment;
}

/// <summary>
/// 自动强化 / 装备整理闭环：图片确认界面与按钮 → OCR 判断 → 单次点击 → 再截图确认。
/// 任一界面、按钮或 OCR 结果不确定都会抛错停机，绝不按固定坐标继续盲点。
/// 整理模式（OrganizeSellOnly）只出售放弃类建议，绝不执行强化。
/// </summary>
public sealed class AutoEnhancementRunner : IDisposable
{
    private readonly IGameSession _session;
    private readonly AutoEnhancementOptions _options;
    private readonly IProgress<AutoEnhancementProgress>? _progress;
    private readonly AutomationScreenMatcher _matcher = new();
    private readonly OcrEngine _ocrEngine;

    private int _processed;
    private int _enhanced;
    private int _sold;
    private int _extracted;
    private int _kept;
    private int _skipped;
    private readonly List<AutoEnhancementEquipmentRecord> _equipment = new();
    private readonly List<ReforgeEquipmentSummary> _reforgeEquipment = new();

    public AutoEnhancementRunner(
        IGameSession session,
        string ocrTemplateDirectory,
        AutoEnhancementOptions options,
        IProgress<AutoEnhancementProgress>? progress = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _options = options.IsOrganizeSellOnly
            ? options with
            {
                DisposalMethod = EquipmentDisposalMethod.Sell,
                StopOnValuableEquipment = false,
            }
            : options;
        _progress = progress;
        _ocrEngine = new OcrEngine(ocrTemplateDirectory);
    }

    /// <summary>整理模式下根据建议决定出售或跳过（供合成测试覆盖）。</summary>
    public static bool ShouldSellInOrganizeMode(EnhanceAdvice advice)
        => advice is EnhanceAdvice.GiveUp or EnhanceAdvice.GiveUpFixedMain;

    public static string OrganizeSkipOutcomeText(EnhanceAdvice advice) => advice switch
    {
        EnhanceAdvice.Continue => "跳过（建议继续）",
        EnhanceAdvice.GambleSpeed => "跳过（赌速度）",
        EnhanceAdvice.Keep => "跳过（保留）",
        EnhanceAdvice.Reforge => "跳过（建议重铸）",
        _ => "跳过",
    };

    public async Task<AutoEnhancementResult> RunAsync(CancellationToken cancellationToken)
    {
        if (_options.IsOrganizeSellOnly)
        {
            Report(AutoEnhancementLogLevel.Info,
                $"装备整理已启动（只卖不强化），目标 {_session.DisplayName}，本次最多处理 {_options.MaxEquipment} 件，" +
                $"紫装规则：{(_options.HeroicOnlyGambleSpeed ? "只赌速度" : "按常规评分")}，" +
                $"速度套速度规则：{(_options.SpeedSetRequiresSpeed ? "开启" : "关闭")}，暴击项链规则：{(_options.CriticalNecklaceMainStatRule ? "开启" : "关闭")}");
        }
        else
        {
            Report(AutoEnhancementLogLevel.Info,
                $"自动强化已启动，目标 {_session.DisplayName}，本次最多处理 {_options.MaxEquipment} 件装备，" +
                $"淘汰装备处理方式：{DisposalDisplayName}，紫装规则：{(_options.HeroicOnlyGambleSpeed ? "只赌速度" : "按常规评分")}，" +
                $"速度套速度规则：{(_options.SpeedSetRequiresSpeed ? "开启" : "关闭")}，暴击项链规则：{(_options.CriticalNecklaceMainStatRule ? "开启" : "关闭")}，" +
                $"符合保留条件后：{(_options.StopOnValuableEquipment ? "停止" : "返回背包继续")}");
        }

        while (_processed < _options.MaxEquipment)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(AutoEnhancementLogLevel.Info, $"准备处理第 {_processed + 1} 件装备");
            await EnterEnhancementScreenAsync(cancellationToken);

            int? expectedEnhanceLevel = null;
            var currentEquipmentEnhanced = false;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var screenshot = await CaptureAsync(cancellationToken);
                var screen = _matcher.DetectScreen(screenshot, out var screenConfidence);
                if (screen != AutomationGameScreen.EnhanceEquipment)
                    throw new InvalidOperationException(
                        $"OCR 前界面确认失败：期望“强化装备”，实际 {DescribeScreen(screen)}（{screenConfidence:P1}）");

                var path = ScreenshotHelper.SaveBitmap(
                    screenshot,
                    $"auto_{_processed + 1:000}_stage_{DateTime.Now:HHmmssfff}");
                Report(AutoEnhancementLogLevel.Info, $"已保存判定截图：{path}");

                var info = await _ocrEngine.RecognizeAsync(path);
                cancellationToken.ThrowIfCancellationRequested();
                EnsureValidEquipmentInfo(info);
                if (expectedEnhanceLevel is int expected && info.EnhanceLevel < expected)
                {
                    throw new InvalidOperationException(
                        $"强化结果校验失败：期望至少 +{expected}，OCR 识别为 +{info.EnhanceLevel}");
                }
                expectedEnhanceLevel = null;

                Report(AutoEnhancementLogLevel.Recognition, DescribeEquipment(info));
                var advice = EnhancementAdvisor.Analyze(
                    info,
                    _options.LeftThreshold,
                    _options.RightThreshold,
                    _options.Level88Threshold,
                    _options.MinimumDemandMatchScore,
                    _options.HeroicOnlyGambleSpeed,
                    _options.SpeedSetRequiresSpeed,
                    _options.CriticalNecklaceMainStatRule,
                    _options.DisabledDemandProfiles,
                    _options.LegendarySpeedLadder);
                Report(AutoEnhancementLogLevel.Recognition,
                    $"强化判断：{advice.Text}；{advice.Detail}");

                if (_options.IsOrganizeSellOnly)
                {
                    await HandleOrganizeAdviceAsync(screenshot, info, advice, path, cancellationToken);
                    break;
                }

                if (advice.Advice is EnhanceAdvice.Continue or EnhanceAdvice.GambleSpeed)
                {
                    var targetLevel = AutomationScreenMatcher.NextTargetLevel(info.EnhanceLevel);
                    if (targetLevel == null)
                    {
                        return FinishKeptEquipment(
                            info, advice, path,
                            AutoEnhancementOutcome.KeptAndStopped,
                            "保留并停止",
                            $"装备已达到 +{info.EnhanceLevel}，没有更高的自动强化档位");
                    }

                    await EnhanceToTargetAsync(screenshot, targetLevel.Value, cancellationToken);
                    expectedEnhanceLevel = targetLevel.Value;
                    if (!currentEquipmentEnhanced)
                    {
                        currentEquipmentEnhanced = true;
                        _enhanced++;
                    }
                    Report(AutoEnhancementLogLevel.Success,
                        $"+{targetLevel} 强化动画结束，开始重新识别并判断");
                    continue;
                }

                if (advice.Advice is EnhanceAdvice.GiveUp or EnhanceAdvice.GiveUpFixedMain)
                {
                    await DisposeRejectedEquipmentAsync(screenshot, cancellationToken);
                    var outcome = _options.DisposalMethod == EquipmentDisposalMethod.Sell
                        ? AutoEnhancementOutcome.Sold
                        : AutoEnhancementOutcome.Extracted;
                    _processed++;
                    if (outcome == AutoEnhancementOutcome.Sold)
                        _sold++;
                    else
                        _extracted++;
                    RecordEquipment(info, advice, path, outcome, DisposalDisplayName);
                    Report(AutoEnhancementLogLevel.Success,
                        $"第 {_processed} 件装备已{DisposalDisplayName}，游戏已返回背包并选择下一件装备");
                    break;
                }

                if (advice.Advice is EnhanceAdvice.Keep or EnhanceAdvice.Reforge)
                {
                    if (_options.StopOnValuableEquipment)
                    {
                        return FinishKeptEquipment(
                            info, advice, path,
                            AutoEnhancementOutcome.KeptAndStopped,
                            advice.Advice == EnhanceAdvice.Reforge ? "保留（建议重铸）并停止" : "保留并停止",
                            $"检测到值得保留的 +{info.EnhanceLevel} 装备：{advice.Text}。已安全停止，未执行{DisposalDisplayName}");
                    }

                    await ReturnToBackpackAndSelectFirstEquipmentAsync(cancellationToken);
                    _processed++;
                    _kept++;
                    RecordEquipment(
                        info, advice, path,
                        AutoEnhancementOutcome.Kept,
                        advice.Advice == EnhanceAdvice.Reforge ? "保留（建议重铸）" : "保留");
                    Report(AutoEnhancementLogLevel.Success,
                        $"第 {_processed} 件装备符合保留条件，已保留并选中背包左上角第一件装备，继续流程");
                    break;
                }

                throw new InvalidOperationException(
                    $"强化建议为“{advice.Text}”，无法安全决定强化或{DisposalDisplayName}：{advice.Detail}");
            }
        }

        var message = _options.IsOrganizeSellOnly
            ? $"已达到本次上限 {_options.MaxEquipment} 件，装备整理结束"
            : $"已达到本次上限 {_options.MaxEquipment} 件，自动强化结束";
        Report(AutoEnhancementLogLevel.Success, message);
        return new AutoEnhancementResult(GetSummary(), false, message);
    }

    private async Task HandleOrganizeAdviceAsync(
        Bitmap screenshot,
        EquipmentInfo info,
        EnhanceAdviceResult advice,
        string path,
        CancellationToken cancellationToken)
    {
        if (ShouldSellInOrganizeMode(advice.Advice))
        {
            if (_options.DisposalMethod != EquipmentDisposalMethod.Sell)
                throw new InvalidOperationException("整理模式必须使用出售处理方式");

            await DisposeRejectedEquipmentAsync(screenshot, cancellationToken);
            _processed++;
            _sold++;
            RecordEquipment(info, advice, path, AutoEnhancementOutcome.Sold, "出售");
            Report(AutoEnhancementLogLevel.Success,
                $"第 {_processed} 件装备已出售，游戏已返回背包并选择下一件装备");
            return;
        }

        if (advice.Advice is not (
            EnhanceAdvice.Continue or EnhanceAdvice.GambleSpeed
            or EnhanceAdvice.Keep or EnhanceAdvice.Reforge))
        {
            throw new InvalidOperationException(
                $"整理模式无法处理建议“{advice.Text}”：{advice.Detail}");
        }

        await ReturnToBackpackAndSelectFirstEquipmentAsync(cancellationToken);
        _processed++;
        _skipped++;
        var outcomeText = OrganizeSkipOutcomeText(advice.Advice);
        RecordEquipment(info, advice, path, AutoEnhancementOutcome.Skipped, outcomeText);
        Report(AutoEnhancementLogLevel.Success,
            $"第 {_processed} 件装备{outcomeText}，已返回背包并选中下一件");
    }

    private async Task EnterEnhancementScreenAsync(CancellationToken cancellationToken)
    {
        using var screenshot = await CaptureAsync(cancellationToken);
        var screen = _matcher.DetectScreen(screenshot, out var confidence);
        switch (screen)
        {
            case AutomationGameScreen.EnhanceEquipment:
                Report(AutoEnhancementLogLevel.Info,
                    $"当前已在强化装备界面（图片置信度 {confidence:P1}）");
                return;

            case AutomationGameScreen.Backpack:
                Report(AutoEnhancementLogLevel.Info,
                    $"已确认背包界面（图片置信度 {confidence:P1}）");
                await ClickTemplateAsync(screenshot, AutomationTemplate.BackpackEnhance,
                    "背包右下角“强化”", cancellationToken);
                using (await WaitForScreenAsync(
                           AutomationGameScreen.EnhanceEquipment, _options.UiTimeout, cancellationToken))
                {
                    Report(AutoEnhancementLogLevel.Success, "已进入强化装备界面");
                }
                return;

            case AutomationGameScreen.AutoRegisterPopup:
                throw new InvalidOperationException("检测到自动登记弹窗，请先手动关闭弹窗后再开始");

            default:
                throw new InvalidOperationException(
                    $"无法确认当前游戏界面（最佳图片置信度 {confidence:P1}），请回到背包装备列表后重试");
        }
    }

    private async Task EnhanceToTargetAsync(
        Bitmap enhancementScreenshot,
        int targetLevel,
        CancellationToken cancellationToken)
    {
        if (_options.IsOrganizeSellOnly)
            throw new InvalidOperationException("整理模式禁止执行强化操作");

        await ClickTemplateAsync(enhancementScreenshot, AutomationTemplate.AutoRegister,
            "右下角“自动登记”", cancellationToken);

        using var popup = await WaitForScreenAsync(
            AutomationGameScreen.AutoRegisterPopup, _options.UiTimeout, cancellationToken);
        Report(AutoEnhancementLogLevel.Success, "已确认强化等级选择弹窗");

        var targetTemplate = AutomationScreenMatcher.TargetTemplateForLevel(targetLevel);
        await ClickTemplateAsync(popup, targetTemplate, $"+{targetLevel} 阶段", cancellationToken);
        using var registered = await WaitForRegisteredMaterialsAsync(_options.UiTimeout, cancellationToken);
        Report(AutoEnhancementLogLevel.Success, $"游戏已自动放置 +{targetLevel} 所需强化材料");

        await ClickTemplateAsync(registered, AutomationTemplate.ReadyEnhance,
            "绿色“强化”", cancellationToken);
        Report(AutoEnhancementLogLevel.Action,
            $"已点击强化，至少等待 {_options.AnimationMinimumWait.TotalSeconds:0.#} 秒动画");
        await Task.Delay(_options.AnimationMinimumWait, cancellationToken);

        using var completed = await WaitForAnimationCompletionAsync(_options.UiTimeout, cancellationToken);
    }

    private async Task DisposeRejectedEquipmentAsync(Bitmap screenshot, CancellationToken cancellationToken)
    {
        var isSell = _options.DisposalMethod == EquipmentDisposalMethod.Sell;
        var actionTemplate = isSell ? AutomationTemplate.Sell : AutomationTemplate.Extract;
        var confirmationScreen = isSell
            ? AutomationGameScreen.SellConfirmation
            : AutomationGameScreen.ExtractConfirmation;
        var confirmationButton = isSell
            ? AutomationTemplate.SellConfirmButton
            : AutomationTemplate.ExtractConfirmButton;
        var iconName = isSell ? "左下角垃圾桶（出售）" : "左下角方块图标（分解/萃取）";

        Report(AutoEnhancementLogLevel.Warning,
            $"当前装备不值得继续，准备{DisposalDisplayName}并自动完成二次确认");
        await ClickTemplateAsync(screenshot, actionTemplate, iconName, cancellationToken);

        using var confirmation = await WaitForScreenAsync(
            confirmationScreen, _options.UiTimeout, cancellationToken);
        Report(AutoEnhancementLogLevel.Success,
            $"已确认{DisposalDisplayName}弹窗，准备点击右侧确认按钮");
        await ClickTemplateAsync(confirmation, confirmationButton,
            isSell ? "出售弹窗右侧“确认”" : "分解弹窗右侧“萃取”", cancellationToken);

        using var backpack = await WaitForScreenAsync(
            AutomationGameScreen.Backpack, _options.UiTimeout, cancellationToken);
    }

    private async Task ReturnToBackpackAndSelectFirstEquipmentAsync(CancellationToken cancellationToken)
    {
        Report(AutoEnhancementLogLevel.Action,
            _options.IsOrganizeSellOnly
                ? "整理模式跳过当前装备：发送返回（ADB Back / 窗口 Esc）"
                : "当前装备符合保留条件，设置为继续运行：发送返回（ADB Back / 窗口 Esc）");
        await Task.Run(() => _session.PressBack(), cancellationToken);

        using var backpack = await WaitForScreenAsync(
            AutomationGameScreen.Backpack, _options.UiTimeout, cancellationToken);
        var x = (int)Math.Round(backpack.Width * 115D / AutomationScreenMatcher.ReferenceWidth);
        var y = (int)Math.Round(backpack.Height * 130D / AutomationScreenMatcher.ReferenceHeight);
        Report(AutoEnhancementLogLevel.Action,
            $"已确认返回背包，点击左上角第一件装备 ({x}, {y})");
        await Task.Run(() => _session.Tap(x, y), cancellationToken);
        await Task.Delay(350, cancellationToken);

        using var selected = await CaptureAsync(cancellationToken);
        var screen = _matcher.DetectScreen(selected, out var confidence);
        if (screen != AutomationGameScreen.Backpack)
        {
            throw new InvalidOperationException(
                $"点击背包左上角第一件装备后界面校验失败：实际 {DescribeScreen(screen)}（{confidence:P1}）");
        }
    }

    private async Task ClickTemplateAsync(
        Bitmap screenshot,
        AutomationTemplate template,
        string displayName,
        CancellationToken cancellationToken)
    {
        var match = _matcher.Find(screenshot, template);
        if (!match.IsMatch())
        {
            throw new InvalidOperationException(
                $"未找到{displayName}按钮（图片置信度 {match.Confidence:P1}，要求 {AutomationScreenMatcher.DefaultConfidenceThreshold:P0}）");
        }

        Report(AutoEnhancementLogLevel.Action,
            $"图片确认 {displayName}（{match.Confidence:P1}），点击 ({match.Center.X}, {match.Center.Y})");
        await Task.Run(() => _session.Tap(match.Center.X, match.Center.Y), cancellationToken);
    }

    private async Task<Bitmap> WaitForScreenAsync(
        AutomationGameScreen expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var lastScreen = AutomationGameScreen.Unknown;
        var lastConfidence = 0.0;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screenshot = await CaptureAsync(cancellationToken);
            lastScreen = _matcher.DetectScreen(screenshot, out lastConfidence);
            if (lastScreen == expected)
                return screenshot;
            screenshot.Dispose();
            await Task.Delay(350, cancellationToken);
        }

        throw new TimeoutException(
            $"等待{DescribeScreen(expected)}超时；最后检测到 {DescribeScreen(lastScreen)}（{lastConfidence:P1}）");
    }

    private async Task<Bitmap> WaitForRegisteredMaterialsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screenshot = await CaptureAsync(cancellationToken);
            var screen = _matcher.DetectScreen(screenshot, out _);
            if (screen == AutomationGameScreen.EnhanceEquipment
                && _matcher.HasRegisteredMaterials(screenshot))
            {
                return screenshot;
            }
            screenshot.Dispose();
            await Task.Delay(350, cancellationToken);
        }

        throw new TimeoutException("选择强化等级后未检测到已登记的强化材料，可能是材料不足");
    }

    private async Task<Bitmap> WaitForAnimationCompletionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screenshot = await CaptureAsync(cancellationToken);
            var screen = _matcher.DetectScreen(screenshot, out _);
            if (screen == AutomationGameScreen.EnhancementRewardPopup)
            {
                Report(AutoEnhancementLogLevel.Warning,
                    "检测到强化暴击后的经验溢出奖励弹窗，准备点击关闭");
                try
                {
                    await ClickTemplateAsync(screenshot, AutomationTemplate.RewardClose,
                        "奖励弹窗“点击关闭”", cancellationToken);
                }
                finally
                {
                    screenshot.Dispose();
                }

                Report(AutoEnhancementLogLevel.Success,
                    "已关闭经验溢出奖励弹窗，继续等待强化界面恢复");
                await Task.Delay(350, cancellationToken);
                continue;
            }

            var register = _matcher.Find(screenshot, AutomationTemplate.AutoRegister);
            if (screen == AutomationGameScreen.EnhanceEquipment
                && register.IsMatch()
                && !_matcher.HasRegisteredMaterials(screenshot))
            {
                return screenshot;
            }
            screenshot.Dispose();
            await Task.Delay(450, cancellationToken);
        }

        throw new TimeoutException("等待强化动画结束超时，未重新检测到可操作的强化界面");
    }

    private async Task<Bitmap> CaptureAsync(CancellationToken cancellationToken)
        => await Task.Run(() => _session.Capture(), cancellationToken);

    private AutoEnhancementResult FinishKeptEquipment(
        EquipmentInfo info,
        EnhanceAdviceResult advice,
        string screenshotPath,
        AutoEnhancementOutcome outcome,
        string outcomeText,
        string message)
    {
        _processed++;
        _kept++;
        RecordEquipment(info, advice, screenshotPath, outcome, outcomeText);
        Report(AutoEnhancementLogLevel.Success, message);
        return new AutoEnhancementResult(GetSummary(), true, message);
    }

    public AutoEnhancementSummary GetSummary()
        => new(
            _processed,
            _enhanced,
            _sold,
            _extracted,
            _kept,
            _skipped,
            _equipment.ToArray(),
            _reforgeEquipment.ToArray());

    private void RecordEquipment(
        EquipmentInfo info,
        EnhanceAdviceResult advice,
        string screenshotPath,
        AutoEnhancementOutcome outcome,
        string outcomeText)
    {
        var part = DescribePart(info.Quality);
        var subStats = info.SubStats
            .Select(stat => $"{stat.Name}{stat.Value}")
            .ToArray();
        var record = new AutoEnhancementEquipmentRecord(
            _processed,
            string.IsNullOrWhiteSpace(info.SetName) ? "未知套装" : info.SetName,
            part,
            info.Quality,
            info.Level,
            info.EnhanceLevel,
            info.Score,
            GetSpeed(info),
            advice.Text,
            advice.Detail,
            outcome,
            outcomeText,
            screenshotPath,
            subStats);
        _equipment.Add(record);

        if (advice.Advice == EnhanceAdvice.Reforge)
            _reforgeEquipment.Add(new ReforgeEquipmentSummary(record.SetName, record.Part, subStats));

        Report(AutoEnhancementLogLevel.Success,
            $"结果表：#{record.Index} {record.SetName} {record.Part} +{record.EnhanceLevel} → {record.OutcomeText}",
            record);
    }

    private static string DescribePart(string quality)
        => EquipmentRules.DetectPart(quality) switch
        {
            EquipmentPart.Weapon => "武器",
            EquipmentPart.Helm => "头盔",
            EquipmentPart.Armor => "铠甲",
            EquipmentPart.Necklace => "项链",
            EquipmentPart.Ring => "戒指",
            EquipmentPart.Boots => "鞋子",
            _ => "未知部位",
        };

    private static int GetSpeed(EquipmentInfo info)
    {
        var speedSub = info.SubStats.FirstOrDefault(s => s.Name == "速度");
        if (speedSub == null)
            return 0;
        var digits = new string(speedSub.Value.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var speed) ? speed : 0;
    }

    private static void EnsureValidEquipmentInfo(EquipmentInfo info)
    {
        if (info.Level is <= 0 or > 100
            || (info.EnhanceLevel != 0 && info.EnhanceLevel is not (3 or 6 or 9 or 12 or 15))
            || string.IsNullOrWhiteSpace(info.Quality)
            || string.IsNullOrWhiteSpace(info.MainStatName)
            || string.IsNullOrWhiteSpace(info.MainStatValue)
            || string.IsNullOrWhiteSpace(info.SetName)
            || info.SubStats.Count is < 1 or > 4
            || !double.IsFinite(info.Score)
            || info.Score <= 0
            || info.RawText.Contains("[OCR 失败:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"装备 OCR 结果不完整，已停止：等级 {info.Level}，+{info.EnhanceLevel}，品质“{info.Quality}”，主属性“{info.MainStatName} {info.MainStatValue}”，副属性 {info.SubStats.Count} 条，套装“{info.SetName}”");
        }
    }

    private static string DescribeEquipment(EquipmentInfo info)
    {
        var subStats = string.Join("，", info.SubStats.Select(stat => $"{stat.Name}{stat.Value}"));
        return $"OCR：{info.Level}级 +{info.EnhanceLevel} {info.Quality}，{info.SetName}，主属性 {info.MainStatName}{info.MainStatValue}，副属性 [{subStats}]，民间分 {info.Score:0.##}";
    }

    private static string DescribeScreen(AutomationGameScreen screen) => screen switch
    {
        AutomationGameScreen.Backpack => "背包界面",
        AutomationGameScreen.EnhanceEquipment => "强化装备界面",
        AutomationGameScreen.AutoRegisterPopup => "强化等级选择弹窗",
        AutomationGameScreen.SellConfirmation => "出售确认弹窗",
        AutomationGameScreen.ExtractConfirmation => "分解确认弹窗",
        AutomationGameScreen.EnhancementRewardPopup => "强化经验溢出奖励弹窗",
        _ => "未知界面",
    };

    private void Report(
        AutoEnhancementLogLevel level,
        string message,
        AutoEnhancementEquipmentRecord? equipment = null)
        => _progress?.Report(new AutoEnhancementProgress(
            level, message, _processed, _enhanced, _sold, _extracted, _kept, _skipped, equipment));

    private string DisposalDisplayName
        => _options.DisposalMethod == EquipmentDisposalMethod.Sell ? "出售" : "分解";

    public void Dispose()
    {
        _ocrEngine.Dispose();
        _matcher.Dispose();
    }
}
