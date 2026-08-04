namespace TiezhuToolbox.Modules.Capture;

/// <summary>基于 ADB 的模拟器会话。</summary>
public sealed class AdbGameSession : IGameSession
{
    private readonly string _serial;

    public AdbGameSession(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("ADB 设备序列号不能为空", nameof(serial));
        _serial = serial.Trim();
    }

    public string DisplayName => _serial;

    public Bitmap Capture() => AdbHelper.ScreenshotPng(_serial);

    public void Tap(int x, int y) => AdbHelper.Tap(_serial, x, y);

    public void PressBack() => AdbHelper.PressBack(_serial);
}
