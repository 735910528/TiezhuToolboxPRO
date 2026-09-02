using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using TiezhuToolbox.Modules.Capture;

namespace TiezhuToolbox;

public partial class MainForm
{
    private readonly object _gameCaptureLock = new();
    private Panel _livePreviewHost = null!;
    private Label _btnLivePreview = null!;
    private System.Windows.Forms.Timer _livePreviewTimer = null!;
    private int _livePreviewBusy;

    private bool IsLivePreviewPage
        => _mainTabs.SelectedIndex is >= 0 and <= 3;

    private bool IsLivePreviewVisible
        => _livePreviewHost is { Visible: true, IsHandleCreated: true };

    private void CreateLivePreviewHost()
    {
        _livePreviewHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = ScalePixel(296),
            BackColor = Color.FromArgb(243, 238, 228),
            Padding = new Padding(ScalePixel(8)),
            Visible = false,
        };
        pnlScreenshot.Parent?.Controls.Remove(pnlScreenshot);
        pnlScreenshot.Dock = DockStyle.Top;
        pnlScreenshot.Height = ScalePixel(194);
        pnlScreenshot.Visible = true;
        lblShotTitle.Text = "游戏画面";
        btnCollapseShot.Text = "收起";
        _livePreviewHost.Controls.Add(pnlScreenshot);

        _livePreviewTimer = new System.Windows.Forms.Timer(components)
        {
            Interval = 350,
        };
        _livePreviewTimer.Tick += LivePreviewTimer_Tick;
    }

    private void CreateLivePreviewTabButton(Panel bar)
    {
        _btnLivePreview = new Label
        {
            Text = "画面",
            Dock = DockStyle.Right,
            AutoSize = false,
            Width = ScalePixel(72),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        _btnLivePreview.Click += (_, _) => ToggleScreenshotPreview();
        toolTip.SetToolTip(_btnLivePreview, "在软件内实时显示游戏画面，后台模式时可对照进度");
        bar.Controls.Add(_btnLivePreview);
    }

    private void ApplyLivePreviewLayout()
    {
        if (_livePreviewHost == null)
            return;

        _livePreviewHost.Width = ScalePixel(296);
        pnlScreenshot.Height = ScalePixel(194);
        if (_btnLivePreview != null)
            _btnLivePreview.Width = ScalePixel(72);
        ApplyLivePreviewVisibility();
    }

    private void ApplyLivePreviewVisibility()
    {
        if (_livePreviewHost == null)
            return;

        var show = _screenshotWanted && IsLivePreviewPage;
        _livePreviewHost.Visible = show;
        if (show)
            _livePreviewHost.BringToFront();

        btnToggleShot.Text = _screenshotWanted ? "收起画面" : "游戏画面";
        if (_btnLivePreview != null)
        {
            _btnLivePreview.ForeColor = _screenshotWanted
                ? Color.FromArgb(180, 83, 9)
                : Color.FromArgb(120, 113, 108);
        }

        SyncLivePreviewTimer();
        PushWebEquipment();
    }

    private void SyncLivePreviewTimer()
    {
        if (_livePreviewTimer == null)
            return;
        _livePreviewTimer.Enabled = _screenshotWanted && IsLivePreviewPage && Visible
            && WindowState != FormWindowState.Minimized;
    }

    private async void LivePreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _livePreviewBusy, 1) == 1)
            return;

        try
        {
            if (!IsLivePreviewVisible || _isRecognizing)
                return;

            if (!TryCreateGameSession(out var session, out _))
            {
                SetLivePreviewTitle("游戏画面（未连接）");
                return;
            }

            Bitmap? frame = null;
            try
            {
                frame = await Task.Run(session.Capture).ConfigureAwait(true);
                if (IsDisposed || !IsLivePreviewVisible)
                {
                    frame?.Dispose();
                    return;
                }

                ApplyLivePreviewFrame(frame);
                frame = null;
                SetLivePreviewTitle("游戏画面 · 实时");
            }
            catch (Exception ex)
            {
                frame?.Dispose();
                SetLivePreviewTitle("游戏画面（截取失败）");
                WriteDebugLog($"实时画面失败：{ex.Message}");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _livePreviewBusy, 0);
        }
    }

    private void ApplyLivePreviewFrame(Bitmap source)
    {
        var maxWidth = Math.Max(160, pictureBox.ClientSize.Width);
        Bitmap preview;
        try
        {
            preview = CreatePreviewBitmap(source, maxWidth);
        }
        finally
        {
            source.Dispose();
        }

        var previous = pictureBox.Image;
        pictureBox.Image = preview;
        previous?.Dispose();
    }

    private static Bitmap CreatePreviewBitmap(Bitmap source, int maxWidth)
    {
        if (source.Width <= maxWidth)
            return new Bitmap(source);

        var height = Math.Max(1, (int)Math.Round(source.Height * (maxWidth / (double)source.Width)));
        var dest = new Bitmap(maxWidth, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(dest);
        graphics.InterpolationMode = InterpolationMode.Low;
        graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.DrawImage(source, 0, 0, maxWidth, height);
        return dest;
    }

    private void SetLivePreviewTitle(string text)
    {
        if (lblShotTitle.Text != text)
            lblShotTitle.Text = text;
    }
}
