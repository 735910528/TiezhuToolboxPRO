using System.Globalization;
using System.Text.RegularExpressions;

namespace TiezhuToolbox.Modules.StarForge;

/// <summary>星之铁匠铺属性名称、默认阈值和匹配规则。</summary>
public static partial class StarForgeRules
{
    public static readonly string[] StatNames =
    [
        "速度", "暴击率", "暴击伤害", "攻击力%", "防御力%", "生命值%",
        "效果命中", "效果抗性", "攻击力", "防御力", "生命值",
    ];

    public static double GetDefaultMinimum(string statName) => statName switch
    {
        "速度" => 5,
        "暴击率" => 5,
        "暴击伤害" => 7,
        "生命值" => 200,
        "防御力" => 30,
        "攻击力" => 40,
        _ => 8,
    };

    public static bool IsPercentStat(string statName)
        => statName is "暴击率" or "暴击伤害" or "效果命中" or "效果抗性"
            || statName.EndsWith('%');

    public static StarForgeMatchResult Match(
        IReadOnlyList<StarForgeStat> stats,
        IReadOnlyList<StarForgeTarget> targets)
    {
        var details = new List<string>(targets.Count);
        var allMatched = targets.Count > 0;
        foreach (var target in targets)
        {
            var candidate = stats.FirstOrDefault(stat => stat.StatName == target.StatName);
            var matched = candidate != null && candidate.Value >= target.MinimumValue;
            allMatched &= matched;
            details.Add(candidate == null
                ? $"{target.StatName}：未出现（需要 ≥ {FormatTarget(target)}）"
                : $"{target.StatName}：{candidate.DisplayValue} / 需要 ≥ {FormatTarget(target)}{(matched ? "，已命中" : "，未达标")}");
        }
        return new StarForgeMatchResult(allMatched, details);
    }

    public static bool TryParseStatLine(string rawText, out StarForgeStat stat)
    {
        stat = null!;
        var text = NormalizeOcrText(rawText);
        var valueMatch = ValueAtEndRegex().Match(text);
        if (!valueMatch.Success
            || !double.TryParse(valueMatch.Groups[1].Value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        var baseName = FindBaseStatName(text);
        if (baseName == null)
            return false;

        var isPercent = valueMatch.Groups[2].Value.Length > 0 || IsAlwaysPercentBaseName(baseName);
        var canonicalName = baseName is "攻击力" or "防御力" or "生命值" && isPercent
            ? baseName + "%"
            : baseName;
        stat = new StarForgeStat(canonicalName, value, isPercent);
        return true;
    }

    public static string FormatTarget(StarForgeTarget target)
        => $"{target.MinimumValue:0.##}{(IsPercentStat(target.StatName) ? "%" : string.Empty)}";

    private static string? FindBaseStatName(string text)
    {
        // 长名称必须先匹配，避免“暴击伤害”被不完整的“暴击”截断。
        if (ContainsAny(text, "暴击伤害", "暴击损害", "暴伤")) return "暴击伤害";
        if (ContainsAny(text, "效果命中", "命中效果")) return "效果命中";
        if (ContainsAny(text, "效果抗性", "抗性效果")) return "效果抗性";
        if (ContainsAny(text, "攻击力", "攻击カ")) return "攻击力";
        if (ContainsAny(text, "防御力", "防御カ")) return "防御力";
        if (ContainsAny(text, "生命值", "生命直")) return "生命值";
        if (ContainsAny(text, "暴击率", "暴击几率", "暴率")) return "暴击率";
        if (text.Contains("速度", StringComparison.Ordinal)) return "速度";
        return null;
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.Ordinal));

    private static bool IsAlwaysPercentBaseName(string statName)
        => statName is "暴击率" or "暴击伤害" or "效果命中" or "效果抗性";

    private static string NormalizeOcrText(string text)
        => text.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("％", "%", StringComparison.Ordinal)
            .Replace("O", "0", StringComparison.OrdinalIgnoreCase)
            .Replace("。", ".", StringComparison.Ordinal);

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(%?)\D*$", RegexOptions.Compiled)]
    private static partial Regex ValueAtEndRegex();
}
