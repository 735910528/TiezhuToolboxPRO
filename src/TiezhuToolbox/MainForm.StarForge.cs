using TiezhuToolbox.Modules.StarForge;

namespace TiezhuToolbox;

public partial class MainForm
{
    private sealed class StarForgeTargetRow
    {
        public required AntdUI.Checkbox Enabled { get; init; }
        public required AntdUI.Select Stat { get; init; }
        public required AntdUI.InputNumber Minimum { get; init; }
        public required Label Unit { get; init; }
    }

    private AntdUI.TabPage _starForgeTab = null!;
    private readonly List<StarForgeTargetRow> _starForgeRows = new();
    private AntdUI.InputNumber _numStarForgeMaximumChanges = null!;
    private AntdUI.Button _btnStarForgeStart = null!;
    private AntdUI.Button _btnStarForgeStop = null!;
    private Label _lblStarForgeDevice = null!;
    private Label _lblStarForgeState = null!;
    private Label _lblStarForgeStats = null!;
    private RichTextBox _starForgeLog = null!;
    private CancellationTokenSource? _starForgeCancellation;

    private bool IsStarForging => _starForgeCancellation != null;
    private bool IsAutomationRunning => IsAutoEnhancing || IsStarForging;

    private Control CreateStarForgeContent()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 246, 248),
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 2,
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 292));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var controlCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(22),
            Margin = new Padding(0, 0, 0, 14),
        };
        var title = new Label
        {
            Text = "星之铁匠铺",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(22, 16),
            AutoSize = true,
        };
        var hint = new Label
        {
            Text = "请在游戏中打开星之铁匠铺并进入副能力值变更页面。所有启用的目标必须同时出现且达到最低值才会停止。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(24, 54),
            Size = new Size(850, 24),
            AutoEllipsis = true,
        };
        var warning = new Label
        {
            Text = "注意：每次变更会消耗游戏内持有点数；程序无法完整确认页面、按钮或四条副属性时会立即停机。",
            ForeColor = AdviceGiveUpColor,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Location = new Point(24, 78),
            Size = new Size(850, 24),
            AutoEllipsis = true,
        };
        var targetHeading = new Label
        {
            Text = "目标条件",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(24, 108),
            Size = new Size(100, 28),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        for (var i = 0; i < 4; i++)
        {
            var top = 138 + i * 37;
            var enabled = new AntdUI.Checkbox
            {
                Text = $"目标 {i + 1}",
                Location = new Point(24, top),
                Size = new Size(82, 32),
            };
            var stat = new AntdUI.Select
            {
                List = true,
                ReadOnly = false,
                Location = new Point(112, top),
                Size = new Size(142, 32),
                Radius = 6,
            };
            stat.Items.AddRange(StarForgeRules.StatNames.Cast<object>().ToArray());
            var minimumLabel = new Label
            {
                Text = "最低值",
                ForeColor = TextDarkColor,
                Location = new Point(270, top),
                Size = new Size(56, 32),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var minimum = new AntdUI.InputNumber
            {
                Location = new Point(326, top),
                Size = new Size(92, 32),
                Minimum = 0,
                Maximum = 99999,
                Value = 5,
                Radius = 6,
            };
            var unit = new Label
            {
                Text = string.Empty,
                ForeColor = Color.FromArgb(95, 99, 104),
                Location = new Point(424, top),
                Size = new Size(28, 32),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var row = new StarForgeTargetRow { Enabled = enabled, Stat = stat, Minimum = minimum, Unit = unit };
            _starForgeRows.Add(row);

            enabled.CheckedChanged += (_, _) => SaveSettingsFromControls();
            minimum.ValueChanged += (_, _) => SaveSettingsFromControls();
            stat.SelectedIndexChanged += (_, _) =>
            {
                var selected = GetSelectedStarForgeStat(row);
                UpdateStarForgeRowUnit(row, selected);
                if (!_isLoadingSettings)
                    row.Minimum.Value = (decimal)StarForgeRules.GetDefaultMinimum(selected);
                SaveSettingsFromControls();
            };
            controlCard.Controls.AddRange(new Control[] { enabled, stat, minimumLabel, minimum, unit });
        }

        var maxLabel = new Label
        {
            Text = "最多变更",
            ForeColor = TextDarkColor,
            Location = new Point(494, 143),
            Size = new Size(76, 32),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _numStarForgeMaximumChanges = new AntdUI.InputNumber
        {
            Location = new Point(570, 143),
            Size = new Size(94, 32),
            Minimum = 1,
            Maximum = 9999,
            Value = 100,
            Radius = 6,
        };
        _numStarForgeMaximumChanges.ValueChanged += (_, _) => SaveSettingsFromControls();
        var timesLabel = new Label
        {
            Text = "次",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(670, 143),
            Size = new Size(28, 32),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblStarForgeDevice = new Label
        {
            Text = "目标：跟随顶部 ADB/窗口选择",
            ForeColor = TextDarkColor,
            Location = new Point(494, 185),
            Size = new Size(360, 32),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        _btnStarForgeStart = new AntdUI.Button
        {
            Text = "开始自动变更",
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Location = new Point(708, 232),
            Size = new Size(132, 34),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnStarForgeStart.Click += btnStarForgeStart_Click;
        _btnStarForgeStop = new AntdUI.Button
        {
            Text = "停止",
            Location = new Point(848, 232),
            Size = new Size(88, 34),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = AdviceGiveUpColor,
            ForeColor = AdviceGiveUpColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnStarForgeStop.Click += (_, _) =>
        {
            if (_starForgeCancellation == null)
                return;
            AppendStarForgeLog("用户请求停止，正在结束当前操作……", AdviceGambleColor);
            _starForgeCancellation.Cancel();
            _btnStarForgeStop.Enabled = false;
        };
        controlCard.Resize += (_, _) =>
        {
            hint.Width = Math.Max(ScalePixel(300), controlCard.ClientSize.Width - ScalePixel(48));
            warning.Width = Math.Max(ScalePixel(300), controlCard.ClientSize.Width - ScalePixel(48));
            _btnStarForgeStop.Left = controlCard.ClientSize.Width - ScalePixel(110);
            _btnStarForgeStart.Left = _btnStarForgeStop.Left - ScalePixel(140);
        };
        controlCard.Controls.AddRange(new Control[]
        {
            title, hint, warning, targetHeading, maxLabel, _numStarForgeMaximumChanges, timesLabel,
            _lblStarForgeDevice, _btnStarForgeStart, _btnStarForgeStop,
        });

        var logCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
            Margin = Padding.Empty,
        };
        var logHeader = new Panel { Dock = DockStyle.Top, Height = 42 };
        var logTitle = new Label
        {
            Text = "识别与变更日志",
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Dock = DockStyle.Left,
            Width = 150,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblStarForgeState = new Label
        {
            Text = "未开始",
            ForeColor = AdviceNoneColor,
            Dock = DockStyle.Left,
            Width = 140,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblStarForgeStats = new Label
        {
            Text = "已变更 0 次",
            ForeColor = Color.FromArgb(95, 99, 104),
            Dock = DockStyle.Right,
            Width = 180,
            TextAlign = ContentAlignment.MiddleRight,
        };
        var clear = new AntdUI.Button
        {
            Text = "清空日志",
            Dock = DockStyle.Right,
            Width = 88,
            Height = 34,
            Radius = 6,
            Margin = new Padding(8, 4, 0, 4),
        };
        clear.Click += (_, _) => _starForgeLog.Clear();
        logHeader.Controls.AddRange(new Control[] { logTitle, _lblStarForgeState, _lblStarForgeStats, clear });
        _starForgeLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 249, 250),
            ForeColor = TextDarkColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            DetectUrls = false,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };
        logCard.Controls.Add(_starForgeLog);
        logCard.Controls.Add(logHeader);
        host.Controls.Add(controlCard, 0, 0);
        host.Controls.Add(logCard, 0, 1);
        return host;
    }

    private async void btnStarForgeStart_Click(object? sender, EventArgs e)
    {
        if (IsAutomationRunning)
            return;

        if (!TryCreateGameSession(out var session, out var sessionError))
        {
            UpdateStatus(sessionError);
            return;
        }

        var targets = _starForgeRows
            .Where(row => row.Enabled.Checked)
            .Select(row => new StarForgeTarget(GetSelectedStarForgeStat(row), (double)row.Minimum.Value))
            .ToList();
        if (targets.Count == 0)
        {
            UpdateStatus("星之铁匠铺至少需要启用一条目标条件");
            return;
        }
        var duplicate = targets.GroupBy(target => target.StatName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            UpdateStatus($"目标属性不能重复：{duplicate.Key}");
            return;
        }

        var targetDescription = string.Join("、", targets.Select(target =>
            $"{target.StatName} ≥ {StarForgeRules.FormatTarget(target)}"));
        var confirmation = MessageBox.Show(
            this,
            $"程序会消耗游戏内持有点数，反复点击“变更副能力值”。\r\n\r\n" +
            $"目标：{targetDescription}\r\n" +
            $"最多变更：{_numStarForgeMaximumChanges.Value:0} 次\r\n\r\n" +
            "确认游戏已经停在星之铁匠铺的副能力值变更页面，是否开始？",
            "确认开始星之铁匠铺自动变更",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
            return;

        SaveSettingsFromControls();
        _starForgeCancellation = new CancellationTokenSource();
        var cancellationToken = _starForgeCancellation.Token;
        SetStarForgeControlsEnabled(false);
        _btnStarForgeStop.Enabled = true;
        _btnAutoStart.Enabled = false;
        _lblStarForgeDevice.Text = $"目标：{session.DisplayName}";
        _lblStarForgeState.Text = "运行中";
        _lblStarForgeState.ForeColor = AdviceContinueColor;
        _starForgeLog.Clear();
        ApplyRecognitionAvailability(showHotKeySuccess: false);

        var progress = new Progress<StarForgeProgress>(value =>
        {
            _lblStarForgeStats.Text = $"已变更 {value.Changes} 次";
            AppendStarForgeLog(value.Message, value.IsRecognition ? AdviceReforgeColor : TextDarkColor);
        });

        try
        {
            using var runner = new StarForgeRunner(
                session, targets, (int)_numStarForgeMaximumChanges.Value, progress);
            var result = await runner.RunAsync(cancellationToken);
            _lblStarForgeStats.Text = $"已变更 {result.Changes} 次";
            _lblStarForgeState.Text = result.Status == StarForgeRunStatus.Matched ? "已命中并停止" : "已达到上限";
            _lblStarForgeState.ForeColor = result.Status == StarForgeRunStatus.Matched
                ? AdviceContinueColor
                : AdviceGambleColor;
            AppendStarForgeLog(result.Message,
                result.Status == StarForgeRunStatus.Matched ? AdviceContinueColor : AdviceGambleColor);
            UpdateStatus(result.Message);
        }
        catch (OperationCanceledException)
        {
            _lblStarForgeState.Text = "已停止";
            _lblStarForgeState.ForeColor = AdviceGambleColor;
            AppendStarForgeLog("星之铁匠铺自动变更已由用户停止", AdviceGambleColor);
            UpdateStatus("星之铁匠铺自动变更已停止");
        }
        catch (Exception ex)
        {
            _lblStarForgeState.Text = "发生错误，已停机";
            _lblStarForgeState.ForeColor = AdviceGiveUpColor;
            AppendStarForgeLog(ex.Message, AdviceGiveUpColor);
            WriteDebugLog($"星之铁匠铺自动变更失败：{ex}");
            UpdateStatus($"星之铁匠铺已停止：{ex.Message}");
        }
        finally
        {
            _starForgeCancellation?.Dispose();
            _starForgeCancellation = null;
            if (!IsDisposed)
            {
                SetStarForgeControlsEnabled(true);
                _btnStarForgeStop.Enabled = false;
                _btnAutoStart.Enabled = true;
                ApplyRecognitionAvailability(showHotKeySuccess: false);
            }
        }
    }

    private void SetStarForgeControlsEnabled(bool enabled)
    {
        _btnStarForgeStart.Enabled = enabled;
        _numStarForgeMaximumChanges.Enabled = enabled;
        foreach (var row in _starForgeRows)
        {
            row.Enabled.Enabled = enabled;
            row.Stat.Enabled = enabled;
            row.Minimum.Enabled = enabled;
        }
    }

    private static string GetSelectedStarForgeStat(StarForgeTargetRow row)
        => row.Stat.SelectedValue as string ?? row.Stat.Text;

    private static void UpdateStarForgeRowUnit(StarForgeTargetRow row, string statName)
        => row.Unit.Text = StarForgeRules.IsPercentStat(statName) ? "%" : string.Empty;

    private void AppendStarForgeLog(string message, Color color)
    {
        if (_starForgeLog.IsDisposed)
            return;
        _starForgeLog.SelectionStart = _starForgeLog.TextLength;
        _starForgeLog.SelectionLength = 0;
        _starForgeLog.SelectionColor = color;
        _starForgeLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        _starForgeLog.SelectionColor = _starForgeLog.ForeColor;
        _starForgeLog.ScrollToCaret();
    }
}
