using System.Runtime.InteropServices;

namespace TiezhuToolbox.Modules.Capture;

/// <summary>
/// 基于 Win32 窗口的会话。前台模式用 CopyFromScreen + SendInput（兼容性最好，会抢键鼠）；
/// 后台模式用窗口截图 + 窗口消息点击（不抢前台，窗口可能短暂位移以对齐光标）。
/// </summary>
public sealed class WindowGameSession : IGameSession
{
    private readonly IntPtr _mainHwnd;
    private readonly string _title;
    private readonly bool _background;

    public WindowGameSession(IntPtr mainHwnd, string title, bool background = false)
    {
        if (mainHwnd == IntPtr.Zero)
            throw new ArgumentException("窗口句柄无效", nameof(mainHwnd));
        _mainHwnd = mainHwnd;
        _title = string.IsNullOrWhiteSpace(title) ? GameWindowHelper.GetTitle(mainHwnd) : title.Trim();
        _background = background;
    }

    public static WindowGameSession FromWindow(GameWindowInfo window, bool background = false)
        => new(window.Handle, window.Title, background);

    public string DisplayName => _title;

    public Bitmap Capture()
    {
        EnsureAlive();
        var (captureHwnd, origin, width, height) = GameWindowHelper.ResolveCaptureRegion(_mainHwnd);
        if (width < 16 || height < 16)
            throw new InvalidOperationException("游戏窗口客户区过小，请确认窗口未最小化且分辨率正常");

        if (_background)
            return WindowBackgroundCapture.Capture(_mainHwnd, captureHwnd, origin, width, height);

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
        var (captureHwnd, origin, width, height) = GameWindowHelper.ResolveCaptureRegion(_mainHwnd);
        var tapX = Math.Clamp(x, 0, Math.Max(0, width - 1));
        var tapY = Math.Clamp(y, 0, Math.Max(0, height - 1));
        if (_background)
        {
            SendMessageClick(captureHwnd, tapX, tapY, origin);
            return;
        }

        GameWindowHelper.FocusWindow(_mainHwnd);
        SendInputClick(origin.X + tapX, origin.Y + tapY);
    }

    public void PressBack()
    {
        EnsureAlive();
        if (_background)
        {
            SendMessageKey(_mainHwnd, 0x1B);
            return;
        }

        GameWindowHelper.FocusWindow(_mainHwnd);
        SendInputKey(0x1B); // VK_ESCAPE
    }

    private void EnsureAlive()
    {
        if (!GameWindowHelper.IsAlive(_mainHwnd))
            throw new InvalidOperationException("游戏窗口已关闭，请在顶部重新选择窗口");
    }

    /// <summary>
    /// 后台点击：光标不动，短暂把窗口目标点对齐到当前光标再发鼠标消息（同 MaaNTE SendMessageWithWindowPos）。
    /// </summary>
    private void SendMessageClick(IntPtr captureHwnd, int clientX, int clientY, GameWindowHelper.Point screenOrigin)
    {
        GameWindowHelper.EnsureShownWithoutActivate(_mainHwnd);
        var moved = false;
        GameWindowHelper.Rect original = default;
        try
        {
            if (!GameWindowHelper.IsMaximized(_mainHwnd)
                && GameWindowHelper.TryGetWindowRect(_mainHwnd, out original)
                && GetCursorPos(out var cursor))
            {
                var targetX = screenOrigin.X + clientX;
                var targetY = screenOrigin.Y + clientY;
                var nextLeft = original.Left + (cursor.X - targetX);
                var nextTop = original.Top + (cursor.Y - targetY);
                moved = GameWindowHelper.TryMoveNoActivate(_mainHwnd, nextLeft, nextTop);
            }

            var lParam = MakeLParam(clientX, clientY);
            const uint wmMousemove = 0x0200;
            const uint wmLbuttondown = 0x0201;
            const uint wmLbuttonup = 0x0202;
            const nint mkLbutton = 0x0001;
            SendMessage(captureHwnd, wmMousemove, nint.Zero, lParam);
            SendMessage(captureHwnd, wmLbuttondown, mkLbutton, lParam);
            Thread.Sleep(15);
            SendMessage(captureHwnd, wmLbuttonup, nint.Zero, lParam);
        }
        finally
        {
            if (moved)
                GameWindowHelper.TryMoveNoActivate(_mainHwnd, original.Left, original.Top);
        }
    }

    private static void SendMessageKey(IntPtr hwnd, byte virtualKey)
    {
        const uint wmKeydown = 0x0100;
        const uint wmKeyup = 0x0101;
        var scan = MapVirtualKey(virtualKey, 0);
        var downLParam = (nint)(1 | (scan << 16));
        var upLParam = (nint)(1 | (scan << 16) | (1 << 30) | (1 << 31));
        SendMessage(hwnd, wmKeydown, virtualKey, downLParam);
        Thread.Sleep(15);
        SendMessage(hwnd, wmKeyup, virtualKey, upLParam);
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

    private static void SendInputKey(byte virtualKey)
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

    private static nint MakeLParam(int low, int high) => (nint)((high << 16) | (low & 0xFFFF));

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out GameWindowHelper.Point lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(IntPtr hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

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
