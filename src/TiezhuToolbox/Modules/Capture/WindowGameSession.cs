using System.Runtime.InteropServices;

namespace TiezhuToolbox.Modules.Capture;

/// <summary>
/// 基于 Win32 窗口的会话：截取游戏渲染区，并用 SendInput 点击。
/// 同时适用于 PC 客户端窗口与 MuMu 等模拟器窗口（窗口标题含「第七史诗」时）。
/// </summary>
public sealed class WindowGameSession : IGameSession
{
    private readonly IntPtr _mainHwnd;
    private readonly string _title;

    public WindowGameSession(IntPtr mainHwnd, string title)
    {
        if (mainHwnd == IntPtr.Zero)
            throw new ArgumentException("窗口句柄无效", nameof(mainHwnd));
        _mainHwnd = mainHwnd;
        _title = string.IsNullOrWhiteSpace(title) ? GameWindowHelper.GetTitle(mainHwnd) : title.Trim();
    }

    public static WindowGameSession FromWindow(GameWindowInfo window)
        => new(window.Handle, window.Title);

    public string DisplayName => _title;

    public Bitmap Capture()
    {
        EnsureAlive();
        var (_, origin, width, height) = GameWindowHelper.ResolveCaptureRegion(_mainHwnd);
        if (width < 16 || height < 16)
            throw new InvalidOperationException("游戏窗口客户区过小，请确认窗口未最小化且分辨率正常");

        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public void Tap(int x, int y)
    {
        if (x < 0 || y < 0)
            throw new ArgumentOutOfRangeException(nameof(x), "点击坐标不能为负数");

        EnsureAlive();
        GameWindowHelper.FocusWindow(_mainHwnd);
        var (_, origin, width, height) = GameWindowHelper.ResolveCaptureRegion(_mainHwnd);
        var tapX = Math.Clamp(x, 0, Math.Max(0, width - 1));
        var tapY = Math.Clamp(y, 0, Math.Max(0, height - 1));
        SendInputClick(origin.X + tapX, origin.Y + tapY);
    }

    public void PressBack()
    {
        EnsureAlive();
        GameWindowHelper.FocusWindow(_mainHwnd);
        SendKey(0x1B); // VK_ESCAPE，PC/多数窗口模式下对应返回
    }

    private void EnsureAlive()
    {
        if (!GameWindowHelper.IsAlive(_mainHwnd))
            throw new InvalidOperationException("游戏窗口已关闭，请在顶部重新选择窗口");
    }

    private static void SendInputClick(int screenX, int screenY)
    {
        var virtualWidth = Math.Max(1, GetSystemMetrics(78));  // SM_CXVIRTUALSCREEN
        var virtualHeight = Math.Max(1, GetSystemMetrics(79)); // SM_CYVIRTUALSCREEN
        var virtualLeft = GetSystemMetrics(76);                // SM_XVIRTUALSCREEN
        var virtualTop = GetSystemMetrics(77);                 // SM_YVIRTUALSCREEN

        var normalizedX = (int)((screenX - virtualLeft) * 65535L / virtualWidth);
        var normalizedY = (int)((screenY - virtualTop) * 65535L / virtualHeight);

        const uint mouseeventfMove = 0x0001;
        const uint mouseeventfLeftdown = 0x0002;
        const uint mouseeventfLeftup = 0x0004;
        const uint mouseeventfAbsolute = 0x8000;
        const uint mouseeventfVirtualdesk = 0x4000;
        const uint moveFlags = mouseeventfMove | mouseeventfAbsolute | mouseeventfVirtualdesk;
        const uint downFlags = mouseeventfLeftdown | mouseeventfAbsolute | mouseeventfVirtualdesk;
        const uint upFlags = mouseeventfLeftup | mouseeventfAbsolute | mouseeventfVirtualdesk;

        var inputs = new Input[3];
        inputs[0] = CreateMouseInput(normalizedX, normalizedY, moveFlags);
        inputs[1] = CreateMouseInput(normalizedX, normalizedY, downFlags);
        inputs[2] = CreateMouseInput(normalizedX, normalizedY, upFlags);

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
            throw new InvalidOperationException("SendInput 鼠标点击失败");
    }

    private static void SendKey(byte virtualKey)
    {
        const uint keyeventfKeyup = 0x0002;
        var inputs = new Input[2];
        inputs[0] = CreateKeyboardInput(virtualKey, 0);
        inputs[1] = CreateKeyboardInput(virtualKey, keyeventfKeyup);

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
            throw new InvalidOperationException("SendInput 按键失败");
    }

    private static Input CreateMouseInput(int x, int y, uint flags) => new()
    {
        Type = 0, // INPUT_MOUSE
        Union = new InputUnion
        {
            Mouse = new MouseInput
            {
                Dx = x,
                Dy = y,
                MouseData = 0,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        },
    };

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags) => new()
    {
        Type = 1, // INPUT_KEYBOARD
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                Vk = virtualKey,
                Scan = 0,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        },
    };

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
