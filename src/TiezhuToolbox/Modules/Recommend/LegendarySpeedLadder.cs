namespace TiezhuToolbox.Modules.Recommend;

/// <summary>
/// 传说（红装）赌速度阶梯。紫装仍使用 EnhancementAdvisor 内硬编码的严格阶梯。
/// 默认值与原先 3/3/6/9/12，终局 15 一致。
/// </summary>
public sealed class LegendarySpeedLadder
{
    public const int DefaultBeforePlus3 = 3;
    public const int DefaultBeforePlus6 = 3;
    public const int DefaultBeforePlus9 = 6;
    public const int DefaultBeforePlus12 = 9;
    public const int DefaultBeforePlus15 = 12;
    public const int DefaultFinalPlus15 = 15;

    /// <summary>强化 +3 前要求的最低速度。</summary>
    public int BeforePlus3 { get; set; } = DefaultBeforePlus3;

    /// <summary>强化 +6 前要求的最低速度。</summary>
    public int BeforePlus6 { get; set; } = DefaultBeforePlus6;

    /// <summary>强化 +9 前要求的最低速度。</summary>
    public int BeforePlus9 { get; set; } = DefaultBeforePlus9;

    /// <summary>强化 +12 前要求的最低速度。</summary>
    public int BeforePlus12 { get; set; } = DefaultBeforePlus12;

    /// <summary>强化 +15 前要求的最低速度。</summary>
    public int BeforePlus15 { get; set; } = DefaultBeforePlus15;

    /// <summary>+15 终局要求的最低速度（达标后 85 重铸 / 88 保留）。</summary>
    public int FinalPlus15 { get; set; } = DefaultFinalPlus15;

    public static LegendarySpeedLadder CreateDefault() => new();

    public LegendarySpeedLadder Clone() => new()
    {
        BeforePlus3 = BeforePlus3,
        BeforePlus6 = BeforePlus6,
        BeforePlus9 = BeforePlus9,
        BeforePlus12 = BeforePlus12,
        BeforePlus15 = BeforePlus15,
        FinalPlus15 = FinalPlus15,
    };

    public void Normalize()
    {
        BeforePlus3 = ClampSpeed(BeforePlus3);
        BeforePlus6 = ClampSpeed(BeforePlus6);
        BeforePlus9 = ClampSpeed(BeforePlus9);
        BeforePlus12 = ClampSpeed(BeforePlus12);
        BeforePlus15 = ClampSpeed(BeforePlus15);
        FinalPlus15 = ClampSpeed(FinalPlus15);
    }

    /// <summary>阶梯表：强化档位上限（不含）→ 最低速度。</summary>
    public (int LevelCap, int Speed)[] ToSteps() =>
    [
        (3, BeforePlus3),
        (6, BeforePlus6),
        (9, BeforePlus9),
        (12, BeforePlus12),
        (15, BeforePlus15),
    ];

    private static int ClampSpeed(int value) => Math.Clamp(value, 0, 45);
}
