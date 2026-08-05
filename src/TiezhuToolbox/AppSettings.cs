using System.Text.Json;
using System.Text.Json.Serialization;
using TiezhuToolbox.Modules.Recommend;
using TiezhuToolbox.Modules.StarForge;

namespace TiezhuToolbox;

/// <summary>软件设置。新增字段必须提供兼容旧文件的默认值。</summary>
public class AppSettings
{
    public const int CurrentVersion = 12;

    public int Version { get; set; } = CurrentVersion;
    public decimal LeftThreshold { get; set; } = 24;
    public decimal RightThreshold { get; set; } = 24;
    public decimal Level88Threshold { get; set; } = 28;
    public string RecognitionHotKey { get; set; } = "F2";
    public bool ContinuousRecognition { get; set; }
    public decimal RecognitionIntervalSeconds { get; set; } = 0.1M;
    /// <summary>连接方式：ADB（模拟器调试桥）或 窗口（PC/模拟器窗口截图）。</summary>
    public string ConnectionMode { get; set; } = "ADB";
    public string AdbAddress { get; set; } = "127.0.0.1:16384";
    /// <summary>窗口模式的标题关键字，默认匹配「第七史诗」。</summary>
    public string WindowTitle { get; set; } = "第七史诗";
    /// <summary>窗口模式目标游戏画面分辨率，如 1920x1080。</summary>
    public string WindowContentResolution { get; set; } = "1920x1080";
    public int AutoEnhanceMaxEquipment { get; set; } = 50;
    public string AutoEnhanceDisposalMethod { get; set; } = "出售";
    // 保留旧 JSON 字段名，兼容已经保存的 settings.json。
    [JsonPropertyName("MinimumHeroMatchScore")]
    public decimal MinimumDemandMatchScore { get; set; } = 70;
    public bool AutoEnhanceStopOnValuableEquipment { get; set; } = true;
    public bool HeroicOnlyGambleSpeed { get; set; }
    public bool SpeedSetRequiresSpeed { get; set; } = true;
    // 保留第一版特殊规则使用的 JSON 字段名，兼容已经保存的 settings.json。
    [JsonPropertyName("DoubleCritNecklaceOnly")]
    public bool CriticalNecklaceMainStatRule { get; set; } = true;
    /// <summary>传说装备赌速度各档最低速度（紫装仍用固定严格阶梯）。</summary>
    public LegendarySpeedLadder LegendarySpeedLadder { get; set; } = new();
    /// <summary>不参与装备用途匹配的需求子类，键格式为“套装代码/子类代码”。</summary>
    public List<string> DisabledDemandProfiles { get; set; } = new();
    public int StarForgeMaximumChanges { get; set; } = 100;
    public List<StarForgeTargetSetting> StarForgeTargets { get; set; } = CreateDefaultStarForgeTargets();

    public static AppSettings CreateDefault() => new();

    internal void Normalize()
    {
        Version = CurrentVersion;
        LeftThreshold = Math.Clamp(LeftThreshold, 0, 200);
        RightThreshold = Math.Clamp(RightThreshold, 0, 200);
        Level88Threshold = Math.Clamp(Level88Threshold, 0, 200);
        RecognitionIntervalSeconds = Math.Clamp(RecognitionIntervalSeconds, 0.1M, 60M);
        AutoEnhanceMaxEquipment = Math.Clamp(AutoEnhanceMaxEquipment, 1, 999);
        MinimumDemandMatchScore = Math.Clamp(MinimumDemandMatchScore, 0, 100);
        StarForgeMaximumChanges = Math.Clamp(StarForgeMaximumChanges, 1, 9999);
        if (AutoEnhanceDisposalMethod is not ("出售" or "分解"))
            AutoEnhanceDisposalMethod = "出售";
        if (!Enum.TryParse<Keys>(RecognitionHotKey, out var key) || key is < Keys.F1 or > Keys.F12)
            RecognitionHotKey = "F2";
        if (ConnectionMode is not ("ADB" or "窗口"))
            ConnectionMode = "ADB";
        if (string.IsNullOrWhiteSpace(AdbAddress))
            AdbAddress = "127.0.0.1:16384";
        if (string.IsNullOrWhiteSpace(WindowTitle))
            WindowTitle = "第七史诗";
        if (!TryParseWindowContentResolution(WindowContentResolution, out _, out _))
            WindowContentResolution = "1920x1080";
        LegendarySpeedLadder ??= new();
        LegendarySpeedLadder.Normalize();
        DisabledDemandProfiles ??= new List<string>();
        DisabledDemandProfiles = DisabledDemandProfiles
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        StarForgeTargets ??= new List<StarForgeTargetSetting>();
        var defaults = CreateDefaultStarForgeTargets();
        StarForgeTargets = StarForgeTargets.Take(4).Select((target, index) =>
        {
            target ??= defaults[index];
            if (!StarForgeRules.StatNames.Contains(target.StatName, StringComparer.Ordinal))
                target.StatName = defaults[index].StatName;
            target.MinimumValue = Math.Clamp(target.MinimumValue, 0, 99999);
            return target;
        }).ToList();
        while (StarForgeTargets.Count < 4)
            StarForgeTargets.Add(defaults[StarForgeTargets.Count]);
    }

    public static bool TryParseWindowContentResolution(string? text, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Trim().ToLowerInvariant().Split('x', '×', '*');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0].Trim(), out width) || !int.TryParse(parts[1].Trim(), out height))
            return false;
        return width >= 640 && height >= 360;
    }

    private static List<StarForgeTargetSetting> CreateDefaultStarForgeTargets() =>
    [
        new() { Enabled = true, StatName = "速度", MinimumValue = 5 },
        new() { StatName = "暴击率", MinimumValue = 5 },
        new() { StatName = "暴击伤害", MinimumValue = 7 },
        new() { StatName = "攻击力%", MinimumValue = 8 },
    ];
}

public sealed class StarForgeTargetSetting
{
    public bool Enabled { get; set; }
    public string StatName { get; set; } = "速度";
    public double MinimumValue { get; set; } = 5;
}

internal static class AppSettingsStore
{
    public static AppSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsPath))
            return AppSettings.CreateDefault();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(AppPaths.SettingsPath), AppPaths.JsonOptions) ?? AppSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            AppPaths.PreserveBrokenFile(AppPaths.SettingsPath);
            return AppSettings.CreateDefault();
        }
    }

    public static void Save(AppSettings settings)
    {
        settings.Normalize();
        AppPaths.WriteJsonAtomic(AppPaths.SettingsPath, settings);
    }
}
