namespace TiezhuToolbox.Modules.StarForge;

/// <summary>星之铁匠铺的一条目标副属性；数值表示允许停机的最低值。</summary>
public sealed record StarForgeTarget(string StatName, double MinimumValue);

/// <summary>从游戏右侧候选装备识别到的一条副属性。</summary>
public sealed record StarForgeStat(string StatName, double Value, bool IsPercent)
{
    public string DisplayValue => $"{Value:0.##}{(IsPercent ? "%" : string.Empty)}";
}

/// <summary>单帧星之铁匠铺识别结果。</summary>
public sealed record StarForgeRecognition(
    bool IsForgeScreen,
    bool CanChange,
    IReadOnlyList<StarForgeStat> Stats,
    string RawText)
{
    public bool IsReliable => IsForgeScreen && CanChange && Stats.Count == 4;
}

public sealed record StarForgeMatchResult(bool IsMatch, IReadOnlyList<string> Details);

public enum StarForgeRunStatus
{
    Matched,
    MaximumChangesReached,
}

public sealed record StarForgeRunResult(
    StarForgeRunStatus Status,
    int Changes,
    IReadOnlyList<StarForgeStat> Stats,
    string Message);

public sealed record StarForgeProgress(int Changes, string Message, bool IsRecognition = false);
