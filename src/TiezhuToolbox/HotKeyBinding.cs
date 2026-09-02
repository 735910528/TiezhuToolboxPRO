namespace TiezhuToolbox;

/// <summary>全局识别快捷键：仅单键，支持 F1–F12、空格和回车。</summary>
internal readonly record struct HotKeyBinding(Keys Key)
{
    public const uint ModNoRepeat = 0x4000;

    public static HotKeyBinding Default { get; } = new(Keys.F2);

    public bool IsEmpty => Key == Keys.None;

    public uint RegisterModifiers => ModNoRepeat;

    public bool IsPlainFunctionKey
        => Key is >= Keys.F1 and <= Keys.F12;

    public static bool TryParse(string? text, out HotKeyBinding binding)
    {
        binding = Default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // 旧设置里可能残留 Ctrl+F2 这类组合，一律视为无效并回退默认。
        if (text.Contains('+'))
            return false;

        if (!TryParseKey(text.Trim(), out var key) || !IsBindableKey(key))
            return false;

        binding = new HotKeyBinding(key);
        return true;
    }

    public static bool TryFromKeyEvent(Keys keyCode, Keys modifierKeys, out HotKeyBinding binding)
    {
        binding = default;
        if (modifierKeys.HasFlag(Keys.Control)
            || modifierKeys.HasFlag(Keys.Alt)
            || modifierKeys.HasFlag(Keys.Shift))
        {
            return false;
        }

        var key = keyCode & Keys.KeyCode;
        if (!IsBindableKey(key))
            return false;

        binding = new HotKeyBinding(key);
        return true;
    }

    public string ToDisplayString()
        => FormatKey(Key);

    private static bool TryParseKey(string text, out Keys key)
    {
        if (text.Equals("空格", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            key = Keys.Space;
            return true;
        }

        if (text.Equals("回车", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Enter", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Return", StringComparison.OrdinalIgnoreCase))
        {
            key = Keys.Enter;
            return true;
        }

        return Enum.TryParse(text, ignoreCase: true, out key);
    }

    private static bool IsBindableKey(Keys key)
        => key is >= Keys.F1 and <= Keys.F12
            or Keys.Space
            or Keys.Enter
            or Keys.Return;

    private static string FormatKey(Keys key)
        => key switch
        {
            Keys.Space => "空格",
            Keys.Enter or Keys.Return => "回车",
            _ => key.ToString(),
        };
}
