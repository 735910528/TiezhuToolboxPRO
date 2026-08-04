using System.Diagnostics;
using TiezhuToolbox.Modules.Automation;

namespace TiezhuToolbox;

public partial class MainForm
{
    private AntdUI.TabPage _autoEnhanceTab = null!;
    private AntdUI.Button _btnAutoStart = null!;
    private AntdUI.Button _btnAutoStop = null!;
    private AntdUI.Button _btnAutoOpenSettings = null!;
    private AntdUI.Button _btnAutoClearLog = null!;
    private AntdUI.Button _btnAutoOpenLog = null!;
    private AntdUI.Button _btnAutoOpenScreenshot = null!;
    private AntdUI.Select _comboAutoResultFilter = null!;
    private AntdUI.InputNumber _numAutoMaxEquipment = null!;
    private AntdUI.Select _comboAutoDisposalMethod = null!;
    private AntdUI.InputNumber _numHeroMatchThreshold = null!;
    private AntdUI.Checkbox _chkAutoStopOnValuableEquipment = null!;
    private AntdUI.Checkbox _chkHeroicOnlyGambleSpeed = null!;
    private AntdUI.Checkbox _chkSpeedSetRequiresSpeed = null!;
    private AntdUI.Checkbox _chkCriticalNecklaceMainStatRule = null!;
    private AntdUI.InputNumber _numLegendarySpeedPlus3 = null!;
    private AntdUI.InputNumber _numLegendarySpeedPlus6 = null!;
    private AntdUI.InputNumber _numLegendarySpeedPlus9 = null!;
    private AntdUI.InputNumber _numLegendarySpeedPlus12 = null!;
    private AntdUI.InputNumber _numLegendarySpeedPlus15 = null!;
    private AntdUI.InputNumber _numLegendarySpeedFinal = null!;
    private Label _lblAutoDevice = null!;
    private Label _lblAutoState = null!;
    private Label _lblAutoStats = null!;
    private DataGridView _autoResultGrid = null!;
    private readonly List<AutoEnhancementEquipmentRecord> _autoResultRecords = new();
    private RichTextBox _autoLog = null!;
    private Form? _autoLogForm;
    private bool _autoLogFormAllowClose;
    private CancellationTokenSource? _autoEnhanceCancellation;
    private bool _isUpdatingAutoResultFilter;

    private bool IsAutoEnhancing => _autoEnhanceCancellation != null;

    private Control CreateAutoEnhanceContent()
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
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
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
            Text = "自动强化",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(22, 18),
            AutoSize = true,
        };
        var hint = new Label
        {
            Text = "请先在游戏中打开背包装备列表并选中第一件装备。程序会用标题和按钮图片确认位置，无法确认时立即停止。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(24, 57),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(720, 24),
            AutoEllipsis = true,
        };
        var warning = new Label
        {
            Text = "注意：淘汰装备会按设置出售或分解；符合保留条件时按设置停止，或返回背包继续下一件。",
            ForeColor = AdviceGiveUpColor,
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Location = new Point(24, 83),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(720, 24),
            AutoEllipsis = true,
        };

        _lblAutoDevice = new Label
        {
            Text = "目标：跟随顶部 ADB/窗口选择",
            ForeColor = TextDarkColor,
            Location = new Point(24, 123),
            Size = new Size(240, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        _btnAutoOpenSettings = new AntdUI.Button
        {
            Text = "强化设置",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(600, 123),
            Size = new Size(96, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnAutoOpenSettings.Click += (_, _) => OpenAutoEnhanceSettings();
        _btnAutoStart = new AntdUI.Button
        {
            Text = "开始自动强化",
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(708, 123),
            Size = new Size(132, 34),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnAutoStart.Click += btnAutoStart_Click;

        _btnAutoStop = new AntdUI.Button
        {
            Text = "停止",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(848, 123),
            Size = new Size(88, 34),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = AdviceGiveUpColor,
            ForeColor = AdviceGiveUpColor,
        };
        _btnAutoStop.Click += (_, _) =>
        {
            if (_autoEnhanceCancellation == null)
                return;
            AppendAutoLog(AutoEnhancementLogLevel.Warning, "用户请求停止，正在结束当前操作……");
            _autoEnhanceCancellation.Cancel();
            _btnAutoStop.Enabled = false;
        };

        controlCard.Resize += (_, _) =>
        {
            hint.Width = Math.Max(ScalePixel(300), controlCard.ClientSize.Width - ScalePixel(48));
            warning.Width = Math.Max(ScalePixel(300), controlCard.ClientSize.Width - ScalePixel(48));
            _btnAutoStop.Left = controlCard.ClientSize.Width - ScalePixel(110);
            _btnAutoStart.Left = _btnAutoStop.Left - ScalePixel(140);
            _btnAutoOpenSettings.Left = _btnAutoStart.Left - ScalePixel(104);
            _lblAutoDevice.Width = Math.Max(
                ScalePixel(120),
                _btnAutoOpenSettings.Left - _lblAutoDevice.Left - ScalePixel(12));
        };
        controlCard.Controls.AddRange(new Control[]
        {
            title, hint, warning, _lblAutoDevice, _btnAutoOpenSettings, _btnAutoStart, _btnAutoStop,
        });

        EnsureAutoLogControl();
        host.Controls.Add(controlCard, 0, 0);
        host.Controls.Add(CreateAutoResultCard(), 0, 1);
        return host;
    }

    private Control CreateAutoResultCard()
    {
        var resultCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
            Margin = Padding.Empty,
        };
        var resultHeader = new Panel { Dock = DockStyle.Top, Height = 42 };
        var resultTitle = new Label
        {
            Text = "本轮结果",
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Dock = DockStyle.Left,
            Width = 88,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblAutoState = new Label
        {
            Text = "未开始",
            ForeColor = AdviceNoneColor,
            Dock = DockStyle.Left,
            Width = 100,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var filterLabel = new Label
        {
            Text = "结果",
            ForeColor = Color.FromArgb(95, 99, 104),
            Dock = DockStyle.Left,
            Width = 36,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _comboAutoResultFilter = new AntdUI.Select
        {
            Dock = DockStyle.Left,
            Width = 96,
            Radius = 6,
            List = true,
            ReadOnly = false,
        };
        _comboAutoResultFilter.Items.Add("全部");
        _comboAutoResultFilter.Items.Add("保留");
        _comboAutoResultFilter.Items.Add("出售");
        _comboAutoResultFilter.Items.Add("分解");
        _comboAutoResultFilter.SelectedIndex = 0;
        _comboAutoResultFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_isUpdatingAutoResultFilter)
                return;
            RefreshAutoResultGrid();
        };
        _lblAutoStats = new Label
        {
            Text = FormatAutoStats(0, 0, 0, 0, 0),
            ForeColor = Color.FromArgb(95, 99, 104),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(300, 0),
            Size = new Size(400, 42),
            TextAlign = ContentAlignment.MiddleRight,
        };
        _btnAutoOpenLog = new AntdUI.Button
        {
            Text = "过程日志",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(760, 4),
            Size = new Size(88, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnAutoOpenLog.Click += (_, _) => ShowAutoLogWindow();
        _btnAutoOpenScreenshot = new AntdUI.Button
        {
            Text = "打开截图",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(858, 4),
            Size = new Size(88, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
            Enabled = false,
        };
        _btnAutoOpenScreenshot.Click += (_, _) => OpenSelectedAutoResultScreenshot();
        resultHeader.Resize += (_, _) =>
        {
            _btnAutoOpenScreenshot.Left = Math.Max(0, resultHeader.ClientSize.Width - _btnAutoOpenScreenshot.Width);
            _btnAutoOpenLog.Left = Math.Max(0, _btnAutoOpenScreenshot.Left - _btnAutoOpenLog.Width - ScalePixel(8));
            _lblAutoStats.Left = Math.Max(
                ScalePixel(320),
                _btnAutoOpenLog.Left - _lblAutoStats.Width - ScalePixel(8));
        };
        resultHeader.Controls.AddRange(new Control[]
        {
            resultTitle, _lblAutoState, filterLabel, _comboAutoResultFilter,
            _lblAutoStats, _btnAutoOpenLog, _btnAutoOpenScreenshot,
        });

        _autoResultGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(248, 249, 250),
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Color.FromArgb(232, 234, 237),
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 34,
            RowTemplate = { Height = 30 },
            Font = new Font("Microsoft YaHei UI", 9F),
        };
        _autoResultGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 243, 244);
        _autoResultGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDarkColor;
        _autoResultGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        _autoResultGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 240, 254);
        _autoResultGrid.DefaultCellStyle.SelectionForeColor = TextDarkColor;
        _autoResultGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", FillWeight = 28 },
            new DataGridViewTextBoxColumn { Name = "SetName", HeaderText = "套装", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "Part", HeaderText = "部位", FillWeight = 42 },
            new DataGridViewTextBoxColumn { Name = "Level", HeaderText = "等级", FillWeight = 36 },
            new DataGridViewTextBoxColumn { Name = "Enhance", HeaderText = "强化", FillWeight = 36 },
            new DataGridViewTextBoxColumn { Name = "Score", HeaderText = "分数", FillWeight = 42 },
            new DataGridViewTextBoxColumn { Name = "Speed", HeaderText = "速度", FillWeight = 36 },
            new DataGridViewTextBoxColumn { Name = "Advice", HeaderText = "建议", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "Outcome", HeaderText = "结果", FillWeight = 70 },
            new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = "备注", FillWeight = 140 });
        _autoResultGrid.SelectionChanged += (_, _) =>
            _btnAutoOpenScreenshot.Enabled = TryGetSelectedAutoResult(out _);
        _autoResultGrid.CellDoubleClick += (_, _) => OpenSelectedAutoResultScreenshot();

        resultCard.Controls.Add(_autoResultGrid);
        resultCard.Controls.Add(resultHeader);
        return resultCard;
    }

    private void EnsureAutoLogControl()
    {
        if (_autoLog != null && !_autoLog.IsDisposed)
            return;

        _autoLog = new RichTextBox
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
    }

    private void ShowAutoLogWindow()
    {
        EnsureAutoLogControl();
        if (_autoLogForm != null && !_autoLogForm.IsDisposed)
        {
            if (_autoLogForm.WindowState == FormWindowState.Minimized)
                _autoLogForm.WindowState = FormWindowState.Normal;
            _autoLogForm.Show();
            _autoLogForm.BringToFront();
            _autoLogForm.Activate();
            return;
        }

        _btnAutoClearLog = new AntdUI.Button
        {
            Text = "清空",
            Size = new Size(76, 32),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnAutoClearLog.Click += (_, _) =>
        {
            if (_autoLog != null && !_autoLog.IsDisposed)
                _autoLog.Clear();
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8),
        };
        var title = new Label
        {
            Text = "自动强化 · 过程日志",
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            AutoSize = true,
            Location = new Point(12, 12),
        };
        header.Controls.Add(title);
        header.Controls.Add(_btnAutoClearLog);
        header.Resize += (_, _) =>
            _btnAutoClearLog.Location = new Point(
                Math.Max(12, header.ClientSize.Width - _btnAutoClearLog.Width - 12),
                8);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 12, 12),
        };
        if (_autoLog.Parent != null)
            _autoLog.Parent.Controls.Remove(_autoLog);
        body.Controls.Add(_autoLog);

        _autoLogForm = new Form
        {
            Text = "过程日志",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(780, 520),
            MinimumSize = new Size(480, 320),
            ShowInTaskbar = false,
            MinimizeBox = true,
            MaximizeBox = true,
            Owner = this,
        };
        if (Icon != null)
            _autoLogForm.Icon = Icon;
        _autoLogForm.Controls.Add(body);
        _autoLogForm.Controls.Add(header);
        _autoLogFormAllowClose = false;
        _autoLogForm.FormClosing += (_, e) =>
        {
            if (!_autoLogFormAllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _autoLogForm.Hide();
            }
        };
        _autoLogForm.Show(this);
    }

    private async void btnAutoStart_Click(object? sender, EventArgs e)
    {
        if (IsAutomationRunning)
            return;

        if (!TryCreateGameSession(out var session, out var sessionError))
        {
            AppendAutoLog(AutoEnhancementLogLevel.Error, sessionError);
            return;
        }

        var disposalMethod = GetSelectedDisposalMethod();
        var disposalName = disposalMethod == EquipmentDisposalMethod.Sell ? "出售" : "分解";
        var confirmation = MessageBox.Show(
            this,
            $"自动强化会永久{disposalName}不符合当前强化建议的装备。\r\n\r\n" +
            "开始前请确认：\r\n" +
            "1. 游戏已停在背包装备列表，并已选中准备处理的第一件装备；\r\n" +
            "2. 已勾选“隐藏已配戴装备”；\r\n" +
            "3. 已勾选“隐藏MAX强化装备”。\r\n\r\n" +
            "以上设置均已完成，是否开始？",
            "确认开始自动强化",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
            return;

        ClearAutoResultGrid();
        ResetAutoResultFilter();
        if (_autoLog != null && !_autoLog.IsDisposed)
            _autoLog.Clear();
        _autoEnhanceCancellation = new CancellationTokenSource();
        var cancellationToken = _autoEnhanceCancellation.Token;
        _btnAutoStart.Enabled = false;
        _btnStarForgeStart.Enabled = false;
        _btnAutoStop.Enabled = true;
        _numAutoMaxEquipment.Enabled = false;
        _comboAutoDisposalMethod.Enabled = false;
        _numHeroMatchThreshold.Enabled = false;
        _chkAutoStopOnValuableEquipment.Enabled = false;
        _chkHeroicOnlyGambleSpeed.Enabled = false;
        _chkSpeedSetRequiresSpeed.Enabled = false;
        _chkCriticalNecklaceMainStatRule.Enabled = false;
        SetLegendarySpeedInputsEnabled(false);
        _lblAutoDevice.Text = $"目标：{session.DisplayName}";
        _lblAutoState.Text = "运行中";
        _lblAutoState.ForeColor = AdviceContinueColor;
        _lblAutoStats.Text = FormatAutoStats(0, 0, 0, 0, 0);
        ApplyRecognitionAvailability(showHotKeySuccess: false);

        var options = AutoEnhancementOptions.CreateDefault(
            (int)_numAutoMaxEquipment.Value,
            (double)numLeftThreshold.Value,
            (double)numRightThreshold.Value,
            (double)numLevel88Threshold.Value,
            (double)_numHeroMatchThreshold.Value,
            disposalMethod,
            _chkAutoStopOnValuableEquipment.Checked,
            _chkHeroicOnlyGambleSpeed.Checked,
            _chkSpeedSetRequiresSpeed.Checked,
            _chkCriticalNecklaceMainStatRule.Checked,
            _disabledDemandProfiles,
            ReadLegendarySpeedLadderFromControls());
        var progress = new Progress<AutoEnhancementProgress>(value =>
        {
            AppendAutoLog(value.Level, value.Message);
            if (value.Equipment != null)
                AddAutoResultRow(value.Equipment);
            _lblAutoStats.Text = FormatAutoStats(
                value.Processed, value.Kept, value.Sold, value.Extracted, value.Enhanced);
        });
        var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates");
        AutoEnhancementRunner? runner = null;

        try
        {
            runner = new AutoEnhancementRunner(session, templateDir, options, progress);
            var result = await runner.RunAsync(cancellationToken);
            _lblAutoStats.Text = FormatAutoStats(
                result.Processed, result.Kept, result.Sold, result.Extracted, result.Enhanced);
            _lblAutoState.Text = result.StoppedForValuableEquipment ? "已安全停止" : "已完成";
            _lblAutoState.ForeColor = result.StoppedForValuableEquipment ? AdviceGambleColor : AdviceContinueColor;
            AppendAutoLog(
                AutoEnhancementLogLevel.Success,
                $"本轮结束：处理 {result.Processed} · 保留 {result.Kept} · 出售 {result.Sold} · 分解 {result.Extracted}");
            UpdateStatus(result.Message);
        }
        catch (OperationCanceledException)
        {
            _lblAutoState.Text = "已停止";
            _lblAutoState.ForeColor = AdviceGambleColor;
            AppendAutoLog(AutoEnhancementLogLevel.Warning, "自动强化已由用户停止");
            UpdateStatus("自动强化已停止");
        }
        catch (Exception ex)
        {
            _lblAutoState.Text = "发生错误，已停机";
            _lblAutoState.ForeColor = AdviceGiveUpColor;
            AppendAutoLog(AutoEnhancementLogLevel.Error, ex.Message);
            WriteDebugLog($"自动强化失败：{ex}");
            UpdateStatus($"自动强化已停止：{ex.Message}");
        }
        finally
        {
            if (runner != null)
            {
                SyncAutoResultGrid(runner.GetSummary());
                runner.Dispose();
            }
            _autoEnhanceCancellation?.Dispose();
            _autoEnhanceCancellation = null;
            if (!IsDisposed)
            {
                _btnAutoStart.Enabled = true;
                _btnStarForgeStart.Enabled = true;
                _btnAutoStop.Enabled = false;
                _numAutoMaxEquipment.Enabled = true;
                _comboAutoDisposalMethod.Enabled = true;
                _numHeroMatchThreshold.Enabled = true;
                _chkAutoStopOnValuableEquipment.Enabled = true;
                _chkHeroicOnlyGambleSpeed.Enabled = true;
                _chkSpeedSetRequiresSpeed.Enabled = true;
                _chkCriticalNecklaceMainStatRule.Enabled = true;
                SetLegendarySpeedInputsEnabled(true);
                ApplyRecognitionAvailability(showHotKeySuccess: false);
            }
        }
    }

    private void SetLegendarySpeedInputsEnabled(bool enabled)
    {
        _numLegendarySpeedPlus3.Enabled = enabled;
        _numLegendarySpeedPlus6.Enabled = enabled;
        _numLegendarySpeedPlus9.Enabled = enabled;
        _numLegendarySpeedPlus12.Enabled = enabled;
        _numLegendarySpeedPlus15.Enabled = enabled;
        _numLegendarySpeedFinal.Enabled = enabled;
    }

    private EquipmentDisposalMethod GetSelectedDisposalMethod()
        => (_comboAutoDisposalMethod.SelectedValue as string ?? _comboAutoDisposalMethod.Text) == "分解"
            ? EquipmentDisposalMethod.Extract
            : EquipmentDisposalMethod.Sell;

    private static string FormatAutoStats(int processed, int kept, int sold, int extracted, int enhanced)
        => $"已处理 {processed} · 保留 {kept} · 出售 {sold} · 分解 {extracted} · 强化过 {enhanced}";

    private void ClearAutoResultGrid()
    {
        _autoResultRecords.Clear();
        if (_autoResultGrid == null || _autoResultGrid.IsDisposed)
            return;
        _autoResultGrid.Rows.Clear();
        _btnAutoOpenScreenshot.Enabled = false;
    }

    private void ResetAutoResultFilter()
    {
        if (_comboAutoResultFilter == null || _comboAutoResultFilter.IsDisposed)
            return;
        _isUpdatingAutoResultFilter = true;
        try
        {
            _comboAutoResultFilter.SelectedIndex = 0;
        }
        finally
        {
            _isUpdatingAutoResultFilter = false;
        }
    }

    private string GetSelectedAutoResultFilter()
        => _comboAutoResultFilter.SelectedValue as string
           ?? _comboAutoResultFilter.Text
           ?? "全部";

    private static bool MatchesAutoResultFilter(AutoEnhancementEquipmentRecord record, string filter)
        => filter switch
        {
            "保留" => record.Outcome is AutoEnhancementOutcome.Kept
                or AutoEnhancementOutcome.KeptAndStopped,
            "出售" => record.Outcome == AutoEnhancementOutcome.Sold,
            "分解" => record.Outcome == AutoEnhancementOutcome.Extracted,
            _ => true,
        };

    private void SyncAutoResultGrid(AutoEnhancementSummary summary)
    {
        if (_autoResultGrid == null || _autoResultGrid.IsDisposed)
            return;

        _lblAutoStats.Text = FormatAutoStats(
            summary.Processed, summary.Kept, summary.Sold, summary.Extracted, summary.Enhanced);

        // 进度回调可能因线程时序漏行；结束时按完整清单对齐一次。
        if (_autoResultRecords.Count == summary.Equipment.Count
            && _autoResultRecords.Select(item => item.Index)
                .SequenceEqual(summary.Equipment.Select(item => item.Index)))
        {
            RefreshAutoResultGrid();
            return;
        }

        _autoResultRecords.Clear();
        _autoResultRecords.AddRange(summary.Equipment);
        RefreshAutoResultGrid();
    }

    private void AddAutoResultRow(AutoEnhancementEquipmentRecord record)
    {
        if (_autoResultGrid == null || _autoResultGrid.IsDisposed)
            return;
        if (_autoResultGrid.InvokeRequired)
        {
            _autoResultGrid.BeginInvoke(() => AddAutoResultRow(record));
            return;
        }

        // 同序号只保留最后一次判定（强化过程中多阶段截图不应重复占行）。
        for (var i = _autoResultRecords.Count - 1; i >= 0; i--)
        {
            if (_autoResultRecords[i].Index == record.Index)
                _autoResultRecords.RemoveAt(i);
        }

        _autoResultRecords.Add(record);
        RefreshAutoResultGrid();
    }

    private void RefreshAutoResultGrid()
    {
        if (_autoResultGrid == null || _autoResultGrid.IsDisposed)
            return;
        if (_autoResultGrid.InvokeRequired)
        {
            _autoResultGrid.BeginInvoke(RefreshAutoResultGrid);
            return;
        }

        var filter = GetSelectedAutoResultFilter();
        var visible = _autoResultRecords
            .Where(record => MatchesAutoResultFilter(record, filter))
            .OrderBy(record => record.Index)
            .ToList();

        _autoResultGrid.Rows.Clear();
        foreach (var record in visible)
            AppendAutoResultGridRow(record);

        _btnAutoOpenScreenshot.Enabled = TryGetSelectedAutoResult(out _);
        if (_autoResultGrid.Rows.Count > 0)
        {
            _autoResultGrid.ClearSelection();
            var last = _autoResultGrid.Rows.Count - 1;
            _autoResultGrid.Rows[last].Selected = true;
            _autoResultGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, last);
            _btnAutoOpenScreenshot.Enabled = TryGetSelectedAutoResult(out _);
        }
    }

    private void AppendAutoResultGridRow(AutoEnhancementEquipmentRecord record)
    {
        var rowIndex = _autoResultGrid.Rows.Add(
            record.Index,
            record.SetName,
            record.Part,
            record.Level,
            $"+{record.EnhanceLevel}",
            record.Score.ToString("0.##"),
            record.Speed,
            record.AdviceText,
            record.OutcomeText,
            record.AdviceDetail);
        var row = _autoResultGrid.Rows[rowIndex];
        row.Tag = record;
        row.DefaultCellStyle.ForeColor = record.Outcome switch
        {
            AutoEnhancementOutcome.Kept or AutoEnhancementOutcome.KeptAndStopped => AdviceContinueColor,
            AutoEnhancementOutcome.Sold or AutoEnhancementOutcome.Extracted => AdviceGiveUpColor,
            _ => TextDarkColor,
        };
    }

    private bool TryGetSelectedAutoResult(out AutoEnhancementEquipmentRecord record)
    {
        record = null!;
        if (_autoResultGrid?.CurrentRow?.Tag is AutoEnhancementEquipmentRecord selected)
        {
            record = selected;
            return true;
        }
        return false;
    }

    private void OpenSelectedAutoResultScreenshot()
    {
        if (!TryGetSelectedAutoResult(out var record))
            return;
        if (string.IsNullOrWhiteSpace(record.ScreenshotPath) || !File.Exists(record.ScreenshotPath))
        {
            UpdateStatus("该结果没有可用的判定截图");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(record.ScreenshotPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UpdateStatus($"打开截图失败：{ex.Message}");
        }
    }

    private void AppendAutoLog(AutoEnhancementLogLevel level, string message)
    {
        EnsureAutoLogControl();
        if (_autoLog.InvokeRequired)
        {
            _autoLog.BeginInvoke(() => AppendAutoLog(level, message));
            return;
        }

        var label = level switch
        {
            AutoEnhancementLogLevel.Action => "操作",
            AutoEnhancementLogLevel.Recognition => "识别",
            AutoEnhancementLogLevel.Warning => "警告",
            AutoEnhancementLogLevel.Error => "错误",
            AutoEnhancementLogLevel.Success => "完成",
            _ => "信息",
        };
        var color = level switch
        {
            AutoEnhancementLogLevel.Action => AccentColor,
            AutoEnhancementLogLevel.Recognition => AdviceReforgeColor,
            AutoEnhancementLogLevel.Warning => AdviceGambleColor,
            AutoEnhancementLogLevel.Error => AdviceGiveUpColor,
            AutoEnhancementLogLevel.Success => AdviceContinueColor,
            _ => TextDarkColor,
        };

        _autoLog.SelectionStart = _autoLog.TextLength;
        _autoLog.SelectionLength = 0;
        _autoLog.SelectionColor = color;
        _autoLog.AppendText($"[{DateTime.Now:HH:mm:ss}] [{label}] {message}{Environment.NewLine}");
        _autoLog.SelectionColor = _autoLog.ForeColor;
        _autoLog.ScrollToCaret();

        if (_autoLog.TextLength > 250_000)
        {
            _autoLog.Select(0, Math.Min(50_000, _autoLog.TextLength));
            _autoLog.SelectedText = string.Empty;
        }
    }
}
