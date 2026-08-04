namespace TiezhuToolbox.Modules.Capture;

/// <summary>
/// 游戏画面采集与输入会话。ADB 模拟器与 Windows 窗口共用同一套坐标语义：
/// Tap/PressBack 使用与 Capture() 返回位图相同的像素坐标系。
/// </summary>
public interface IGameSession
{
    /// <summary>界面展示用名称（设备序列号或窗口标题）。</summary>
    string DisplayName { get; }

    /// <summary>截取当前游戏画面，调用方负责 Dispose。</summary>
    Bitmap Capture();

    /// <summary>点击画面坐标（相对 Capture 位图左上角）。</summary>
    void Tap(int x, int y);

    /// <summary>返回上一层：ADB 发 Android Back；窗口模式发 Esc。</summary>
    void PressBack();
}
