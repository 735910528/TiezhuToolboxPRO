using System.Runtime.InteropServices;
using System.Text;

namespace TiezhuToolbox.Modules.Capture;

/// <summary>可见窗口信息，用于窗口模式下的目标列表。</summary>
public readonly record struct GameWindowInfo(IntPtr Handle, string Title)
{
    public override string ToString() => Title;
}

/// <summary>
/// Win32 窗口枚举与定位。参考 e7_rta_auto：按标题找窗口，并优先使用最大子窗口作为游戏渲染区。
/// </summary>
public static class GameWindowHelper
{
    private static readonly string[] FallbackTitles = ["MuMu安卓设备", "MuMu模拟器", "MuMuPlayer"];

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point pt);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>列出当前桌面可见、带标题的顶层窗口；可选按标题关键字过滤。</summary>
    public static List<GameWindowInfo> ListVisibleWindows(string? titleFilter = null)
    {
        var results = new List<GameWindowInfo>();
        var filter = titleFilter?.Trim();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var title = GetTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            if (!string.IsNullOrEmpty(filter)
                && title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                return true;

            results.Add(new GameWindowInfo(hWnd, title));
            return true;
        }, IntPtr.Zero);

        return results
            .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 按标题查找游戏窗口：先精确匹配，再包含匹配，最后尝试 MuMu 常见回退标题。
    /// </summary>
    public static GameWindowInfo? FindGameWindow(string? preferredTitle)
    {
        var title = string.IsNullOrWhiteSpace(preferredTitle) ? "第七史诗" : preferredTitle.Trim();

        var exact = FindWindow(null, title);
        if (exact != IntPtr.Zero && IsWindowVisible(exact))
            return new GameWindowInfo(exact, GetTitle(exact));

        var windows = ListVisibleWindows(title);
        if (windows.Count > 0)
            return windows[0];

        foreach (var fallback in FallbackTitles)
        {
            var hwnd = FindWindow(null, fallback);
            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
                return new GameWindowInfo(hwnd, GetTitle(hwnd));
        }

        return null;
    }

    public static bool IsAlive(IntPtr hWnd) => hWnd != IntPtr.Zero && IsWindow(hWnd);

    public static string GetTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length <= 0)
            return string.Empty;
        var sb = new StringBuilder(length + 1);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 解析游戏渲染区：优先取客户区面积最大的子窗口；没有子窗口时用主窗口客户区，
    /// 并跳过 MuMu 一类自绘顶栏（主客户区高度减去最大子窗口高度）。
    /// </summary>
    public static (IntPtr CaptureHwnd, Point ScreenOrigin, int Width, int Height) ResolveCaptureRegion(IntPtr mainHwnd)
    {
        if (!IsAlive(mainHwnd))
            throw new InvalidOperationException("游戏窗口已关闭，请重新选择窗口");

        GetClientRect(mainHwnd, out var mainClient);
        var bestChild = IntPtr.Zero;
        var bestArea = 0;
        var bestW = 0;
        var bestH = 0;
        var largestChildH = 0;

        EnumChildWindows(mainHwnd, (child, _) =>
        {
            GetClientRect(child, out var childRect);
            var area = childRect.Width * childRect.Height;
            if (childRect.Height > largestChildH)
                largestChildH = childRect.Height;
            if (area > bestArea && childRect.Width > 100 && childRect.Height > 100)
            {
                bestArea = area;
                bestChild = child;
                bestW = childRect.Width;
                bestH = childRect.Height;
            }
            return true;
        }, IntPtr.Zero);

        if (bestChild != IntPtr.Zero)
        {
            var origin = new Point(0, 0);
            ClientToScreen(bestChild, ref origin);
            return (bestChild, origin, bestW, bestH);
        }

        var drawnTitle = 0;
        if (largestChildH > 0 && largestChildH < mainClient.Height)
            drawnTitle = mainClient.Height - largestChildH;

        var mainOrigin = new Point(0, 0);
        ClientToScreen(mainHwnd, ref mainOrigin);
        return (
            mainHwnd,
            new Point(mainOrigin.X, mainOrigin.Y + drawnTitle),
            mainClient.Width,
            Math.Max(1, mainClient.Height - drawnTitle));
    }

    public static void FocusWindow(IntPtr hWnd)
    {
        if (!IsAlive(hWnd))
            throw new InvalidOperationException("游戏窗口已关闭，请重新选择窗口");

        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, 9); // SW_RESTORE
            Thread.Sleep(200);
        }

        // Alt 键规避部分系统对 SetForegroundWindow 的限制（与 e7_rta_auto 一致）。
        const byte vkMenu = 0x12;
        const uint keyeventfKeyup = 0x0002;
        keybd_event(vkMenu, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(hWnd);
        keybd_event(vkMenu, 0, keyeventfKeyup, UIntPtr.Zero);
        Thread.Sleep(80);
    }

    /// <summary>
    /// 将游戏内容区调整到目标宽高（参考 e7_rta_auto）：外框 = 目标内容 + 系统边框 + 自绘顶栏。
    /// 返回调整后 <see cref="ResolveCaptureRegion"/> 测得的实际画面尺寸。
    /// </summary>
    public static (int Width, int Height) ResizeContentArea(IntPtr mainHwnd, int targetWidth, int targetHeight)
    {
        if (!IsAlive(mainHwnd))
            throw new InvalidOperationException("游戏窗口已关闭，请重新选择窗口");
        if (targetWidth < 640 || targetHeight < 360)
            throw new ArgumentOutOfRangeException(nameof(targetWidth), "目标分辨率过小");

        if (IsIconic(mainHwnd))
        {
            ShowWindow(mainHwnd, 9);
            Thread.Sleep(200);
        }

        if (!GetWindowRect(mainHwnd, out var windowRect) || !GetClientRect(mainHwnd, out var clientRect))
            throw new InvalidOperationException("无法读取游戏窗口尺寸");

        var osDecoW = Math.Max(0, (windowRect.Right - windowRect.Left) - clientRect.Width);
        var osDecoH = Math.Max(0, (windowRect.Bottom - windowRect.Top) - clientRect.Height);
        var drawnTitle = MeasureDrawnTitleHeight(mainHwnd, clientRect.Height);

        var outerW = targetWidth + osDecoW;
        var outerH = targetHeight + osDecoH + drawnTitle;
        const uint swpNomove = 0x0002;
        const uint swpNozorder = 0x0004;
        if (!SetWindowPos(mainHwnd, IntPtr.Zero, 0, 0, outerW, outerH, swpNomove | swpNozorder))
            throw new InvalidOperationException("调整窗口大小失败（可尝试以管理员运行）");

        Thread.Sleep(180);
        var (_, _, width, height) = ResolveCaptureRegion(mainHwnd);
        return (width, height);
    }

    private static int MeasureDrawnTitleHeight(IntPtr mainHwnd, int clientHeight)
    {
        var largestChildH = 0;
        EnumChildWindows(mainHwnd, (child, _) =>
        {
            GetClientRect(child, out var childRect);
            if (childRect.Height > largestChildH)
                largestChildH = childRect.Height;
            return true;
        }, IntPtr.Zero);

        if (largestChildH > 0 && largestChildH < clientHeight)
            return clientHeight - largestChildH;
        return 0;
    }
}
