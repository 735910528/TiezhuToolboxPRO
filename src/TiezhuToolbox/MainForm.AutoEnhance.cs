using TiezhuToolbox.Modules.Automation;
using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

public partial class MainForm
{
    private AntdUI.TabPage _autoEnhanceTab = null!;
    private AntdUI.Button _btnAutoStart = null!;
    private AntdUI.Button _btnAutoOrganize = null!;
    private AntdUI.Button _btnAutoStop = null!;
    private AntdUI.Button _btnAutoOpenSettings = null!;
    private AntdUI.Button _btnAutoClearLog = null!;
    private AntdUI.Button _btnAutoOpenLog = null!;
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
    private Label _lblAutoPreviewHint = null!;
    private DataGridView _autoResultGrid = null!;
    private PictureBox _autoResultPreview = null!;
    private string? _autoResultPreviewPath;
    private readonly List<AutoEnhancementEquipmentRecord> _autoResultRecords = new();
    private RichTextBox _autoLog = null!;
    private Form? _autoLogForm;
    private bool _autoLogFormAllowClose;
    private Form? _autoScreenshotZoomForm;
    private Form? _autoEnhanceSettingsForm;
    private bool _autoEnhanceSettingsFormAllowClose;
    private Panel _autoEnhanceSettingsPanel = null!;
    private bool _autoEnhanceSettingsScaled;
    private CancellationTokenSource? _autoEnhanceCancellation;
    private bool _isUpdatingAutoResultFilter;

    private bool IsAutoEnhancing => _autoEnhanceCancellation != null;

    private Control CreateAutoEnhanceContent()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 246, 248),
            Padding = new Padding(12, 10, 12, 12),
            ColumnCount = 1,
            RowCount = 2,
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var controlCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 0, 8),
        };

        _lblAutoDevice = new Label
        {
            Text = "目标：跟随顶部 PC/模拟器选择",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(12, 11),
            Size = new Size(220, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        _btnAutoOpenSettings = new AntdUI.Button
        {
            Text = "强化设置",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(520, 11),
            Size = new Size(88, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnAutoOpenSettings.Click += (_, _) => ShowAutoEnhanceSettingsWindow();
        _btnAutoOrganize = new AntdUI.Button
        {
            Text = "开始整理",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(600, 11),
            Size = new Size(88, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnAutoOrganize.Click += async (_, _) => await StartAutoRunAsync(AutoRunMode.OrganizeSellOnly);
        _btnAutoStart = new AntdUI.Button
        {
            Text = "自动强化",
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(680, 11),
            Size = new Size(96, 34),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnAutoStart.Click += async (_, _) => await StartAutoRunAsync(AutoRunMode.Enhance);

        _btnAutoStop = new AntdUI.Button
        {
            Text = "停止",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(784, 11),
            Size = new Size(72, 34),
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

        toolTip.SetToolTip(_lblAutoDevice, "使用顶部工具栏选择的 PC 窗口或模拟器设备");
        toolTip.SetToolTip(_btnAutoOpenSettings, "打开自动强化规则设置");
        toolTip.SetToolTip(_btnAutoOrganize, "只出售放弃类装备，不进行强化");
        toolTip.SetToolTip(_btnAutoStart, "按强化建议自动强化并处理淘汰装备");
        toolTip.SetToolTip(_btnAutoStop, "停止当前自动强化或整理");

        controlCard.Resize += (_, _) =>
        {
            _btnAutoStop.Left = controlCard.ClientSize.Width - ScalePixel(84);
            _btnAutoStart.Left = _btnAutoStop.Left - ScalePixel(104);
            _btnAutoOrganize.Left = _btnAutoStart.Left - ScalePixel(96);
            _btnAutoOpenSettings.Left = _btnAutoOrganize.Left - ScalePixel(96);
            _lblAutoDevice.Width = Math.Max(
                ScalePixel(120),
                _btnAutoOpenSettings.Left - _lblAutoDevice.Left - ScalePixel(12));
        };
        controlCard.Controls.AddRange(new Control[]
        {
            _lblAutoDevice, _btnAutoOpenSettings,
            _btnAutoOrganize, _btnAutoStart, _btnAutoStop,
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
            Padding = new Padding(12),
            Margin = Padding.Empty,
        };
        var resultHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
        var resultTitle = new Label
        {
            Text = "本轮结果",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Dock = DockStyle.Left,
            Width = 76,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblAutoState = new Label
        {
            Text = "未开始",
            ForeColor = AdviceNoneColor,
            Dock = DockStyle.Left,
            Width = 88,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var filterLabel = new Label
        {
            Text = "筛选",
            ForeColor = Color.FromArgb(95, 99, 104),
            Dock = DockStyle.Left,
            Width = 36,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _comboAutoResultFilter = new AntdUI.Select
        {
            Dock = DockStyle.Left,
            Width = 88,
            Radius = 6,
            List = true,
            ReadOnly = false,
        };
        _comboAutoResultFilter.Items.Add("全部");
        _comboAutoResultFilter.Items.Add("保留");
        _comboAutoResultFilter.Items.Add("出售");
        _comboAutoResultFilter.Items.Add("跳过");
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
            Text = FormatAutoStats(0, 0, 0, 0, 0, 0),
            ForeColor = Color.FromArgb(95, 99, 104),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(300, 0),
            Size = new Size(480, 36),
            TextAlign = ContentAlignment.MiddleRight,
        };
        _btnAutoOpenLog = new AntdUI.Button
        {
            Text = "日志",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(860, 2),
            Size = new Size(64, 32),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnAutoOpenLog.Click += (_, _) => ShowAutoLogWindow();
        resultHeader.Resize += (_, _) =>
        {
            _btnAutoOpenLog.Left = Math.Max(0, resultHeader.ClientSize.Width - _btnAutoOpenLog.Width);
            _lblAutoStats.Left = Math.Max(
                ScalePixel(280),
                _btnAutoOpenLog.Left - _lblAutoStats.Width - ScalePixel(8));
        };
        resultHeader.Controls.AddRange(new Control[]
        {
            resultTitle, _lblAutoState, filterLabel, _comboAutoResultFilter,
            _lblAutoStats, _btnAutoOpenLog,
        });

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 0),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScalePixel(280)));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

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
            ColumnHeadersHeight = 30,
            RowTemplate = { Height = 28 },
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = Padding.Empty,
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
        _autoResultGrid.SelectionChanged += (_, _) => UpdateAutoResultScreenshotPreview();

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Padding = Padding.Empty,
        };
        _autoResultGrid.Dock = DockStyle.Fill;
        gridHost.Controls.Add(_autoResultGrid);

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 249, 250),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8),
            Margin = Padding.Empty,
        };
        var previewTitle = new Label
        {
            Text = "判定截图",
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblAutoPreviewHint = new Label
        {
            Text = "选中结果后显示，点击可放大",
            ForeColor = Color.FromArgb(95, 99, 104),
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _autoResultPreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 243, 244),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand,
            BorderStyle = BorderStyle.None,
        };
        _autoResultPreview.Click += (_, _) => ShowAutoResultScreenshotZoom();
        previewPanel.Controls.Add(_autoResultPreview);
        previewPanel.Controls.Add(_lblAutoPreviewHint);
        previewPanel.Controls.Add(previewTitle);

        body.Controls.Add(gridHost, 0, 0);
        body.Controls.Add(previewPanel, 1, 0);
        resultCard.Controls.Add(body);
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

    private void EnsureAutoEnhanceSettingsControls()
    {
        if (_autoEnhanceSettingsPanel != null && !_autoEnhanceSettingsPanel.IsDisposed)
            return;

        _autoEnhanceSettingsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(20),
        };

        var title = new Label
        {
            Text = "自动强化设置",
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(20, 16),
            AutoSize = true,
        };
        var hint = new Label
        {
            Text = "设置淘汰装备的处理方式、单次处理上限、最低需求匹配度和赌速度规则。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(22, 52),
            Size = new Size(640, 24),
            AutoEllipsis = true,
        };

        var automationPanel = new FlowLayoutPanel
        {
            Location = new Point(20, 92),
            Size = new Size(690, 34),
            AutoSize = false,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        var disposalLabel = new Label
        {
            Text = "装备处理方式",
            ForeColor = TextDarkColor,
            Size = new Size(96, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };
        _comboAutoDisposalMethod = new AntdUI.Select
        {
            List = true,
            ReadOnly = false,
            Size = new Size(86, 34),
            Radius = 6,
            Margin = new Padding(0, 0, 18, 0),
        };
        _comboAutoDisposalMethod.Items.AddRange(new object[] { "出售", "分解" });
        _comboAutoDisposalMethod.SelectedIndexChanged += (_, _) => SaveSettingsFromControls();

        var maxLabel = new Label
        {
            Text = "最多处理",
            ForeColor = TextDarkColor,
            Size = new Size(65, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };
        _numAutoMaxEquipment = new AntdUI.InputNumber
        {
            Size = new Size(76, 34),
            Minimum = 1,
            Maximum = 999,
            Value = 50,
            Radius = 6,
            Margin = Padding.Empty,
        };
        _numAutoMaxEquipment.ValueChanged += (_, _) => SaveSettingsFromControls();
        var maxUnit = new Label
        {
            Text = "件",
            ForeColor = TextDarkColor,
            Size = new Size(32, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 14, 0),
        };
        var matchLabel = new Label
        {
            Text = "最低需求匹配度",
            ForeColor = TextDarkColor,
            Size = new Size(106, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };
        _numHeroMatchThreshold = new AntdUI.InputNumber
        {
            Size = new Size(76, 34),
            Minimum = 0,
            Maximum = 100,
            Value = 70,
            Radius = 6,
            Margin = Padding.Empty,
        };
        _numHeroMatchThreshold.ValueChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };
        var matchUnit = new Label
        {
            Text = "%",
            ForeColor = TextDarkColor,
            Size = new Size(26, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 0, 0),
        };
        automationPanel.Controls.AddRange(new Control[]
        {
            disposalLabel, _comboAutoDisposalMethod, maxLabel, _numAutoMaxEquipment,
            maxUnit, matchLabel, _numHeroMatchThreshold, matchUnit,
        });

        var legendarySpeedTitle = new Label
        {
            Text = "传说装备赌速度\r\n仅红装生效；各档为该强化阶段前要求的最低速度。紫装仍用固定严格阶梯 3/6/9/12/12，终局 15。",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 33, 36),
            Location = new Point(20, 144),
            Size = new Size(650, 52),
        };
        var legendarySpeedPanel = new FlowLayoutPanel
        {
            Location = new Point(20, 204),
            Size = new Size(690, 40),
            AutoSize = false,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        _numLegendarySpeedPlus3 = CreateLegendarySpeedInput(LegendarySpeedLadder.DefaultBeforePlus3);
        _numLegendarySpeedPlus6 = CreateLegendarySpeedInput(LegendarySpeedLadder.DefaultBeforePlus6);
        _numLegendarySpeedPlus9 = CreateLegendarySpeedInput(LegendarySpeedLadder.DefaultBeforePlus9);
        _numLegendarySpeedPlus12 = CreateLegendarySpeedInput(LegendarySpeedLadder.DefaultBeforePlus12);
        _numLegendarySpeedPlus15 = CreateLegendarySpeedInput(LegendarySpeedLadder.DefaultBeforePlus15);
        _numLegendarySpeedFinal = CreateLegendarySpeedInput(LegendarySpeedLadder.DefaultFinalPlus15);
        legendarySpeedPanel.Controls.AddRange(new Control[]
        {
            CreateLegendarySpeedLabel("+3前"), _numLegendarySpeedPlus3,
            CreateLegendarySpeedLabel("+6前"), _numLegendarySpeedPlus6,
            CreateLegendarySpeedLabel("+9前"), _numLegendarySpeedPlus9,
            CreateLegendarySpeedLabel("+12前"), _numLegendarySpeedPlus12,
            CreateLegendarySpeedLabel("+15前"), _numLegendarySpeedPlus15,
            CreateLegendarySpeedLabel("+15终"), _numLegendarySpeedFinal,
        });

        _chkHeroicOnlyGambleSpeed = new AntdUI.Checkbox
        {
            Text = "紫装只赌速度（忽略分数和匹配度，速度不达标立即处理）",
            Checked = false,
            Location = new Point(20, 258),
            Size = new Size(470, 34),
        };
        _chkHeroicOnlyGambleSpeed.CheckedChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };

        _chkSpeedSetRequiresSpeed = new AntdUI.Checkbox
        {
            Text = "速度套只强化带速度的装备（鞋子看主属性，其他部位看副属性）",
            Checked = true,
            Location = new Point(20, 294),
            Size = new Size(520, 34),
        };
        _chkSpeedSetRequiresSpeed.CheckedChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };

        _chkCriticalNecklaceMainStatRule = new AntdUI.Checkbox
        {
            Text = "暴击/暴伤高权重子类的项链只强化对应主属性",
            Checked = true,
            Location = new Point(20, 330),
            Size = new Size(520, 34),
        };
        _chkCriticalNecklaceMainStatRule.CheckedChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };

        _chkAutoStopOnValuableEquipment = new AntdUI.Checkbox
        {
            Text = "遇到符合保留条件的装备后停止（关闭后将返回背包并继续下一件）",
            Checked = true,
            Location = new Point(20, 366),
            Size = new Size(520, 34),
        };
        _chkAutoStopOnValuableEquipment.CheckedChanged += (_, _) => SaveSettingsFromControls();

        _autoEnhanceSettingsPanel.Controls.AddRange(new Control[]
        {
            title, hint, automationPanel, legendarySpeedTitle, legendarySpeedPanel,
            _chkHeroicOnlyGambleSpeed, _chkSpeedSetRequiresSpeed,
            _chkCriticalNecklaceMainStatRule, _chkAutoStopOnValuableEquipment,
        });
    }

    private void ShowAutoEnhanceSettingsWindow()
    {
        EnsureAutoEnhanceSettingsControls();
        if (!_autoEnhanceSettingsScaled)
        {
            ScaleRuntimePage(_autoEnhanceSettingsPanel);
            _autoEnhanceSettingsScaled = true;
        }

        if (_autoEnhanceSettingsForm != null && !_autoEnhanceSettingsForm.IsDisposed)
        {
            if (_autoEnhanceSettingsForm.WindowState == FormWindowState.Minimized)
                _autoEnhanceSettingsForm.WindowState = FormWindowState.Normal;
            _autoEnhanceSettingsForm.Show();
            _autoEnhanceSettingsForm.BringToFront();
            _autoEnhanceSettingsForm.Activate();
            return;
        }

        if (_autoEnhanceSettingsPanel.Parent != null)
            _autoEnhanceSettingsPanel.Parent.Controls.Remove(_autoEnhanceSettingsPanel);

        _autoEnhanceSettingsForm = new Form
        {
            Text = "自动强化设置",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(760, 500),
            MinimumSize = new Size(640, 420),
            ShowInTaskbar = false,
            MinimizeBox = true,
            MaximizeBox = false,
            Owner = this,
        };
        if (Icon != null)
            _autoEnhanceSettingsForm.Icon = Icon;
        _autoEnhanceSettingsForm.Controls.Add(_autoEnhanceSettingsPanel);
        _autoEnhanceSettingsFormAllowClose = false;
        _autoEnhanceSettingsForm.FormClosing += (_, e) =>
        {
            if (!_autoEnhanceSettingsFormAllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _autoEnhanceSettingsForm.Hide();
            }
        };
        _autoEnhanceSettingsForm.Show(this);
    }

    private async Task StartAutoRunAsync(AutoRunMode mode)
    {
        if (IsAutomationRunning)
            return;

        if (!TryCreateGameSession(out var session, out var sessionError))
        {
            AppendAutoLog(AutoEnhancementLogLevel.Error, sessionError);
            return;
        }

        var organize = mode == AutoRunMode.OrganizeSellOnly;
        var disposalMethod = organize ? EquipmentDisposalMethod.Sell : GetSelectedDisposalMethod();
        var disposalName = disposalMethod == EquipmentDisposalMethod.Sell ? "出售" : "分解";
        var confirmation = MessageBox.Show(
            this,
            organize
                ? "装备整理只会按强化建议出售「放弃」类装备，不会进行任何强化。\r\n\r\n" +
                  "开始前请确认：\r\n" +
                  "1. 游戏已停在背包装备列表，并已选中准备处理的第一件装备；\r\n" +
                  "2. 已勾选“隐藏已配戴装备”；\r\n" +
                  "3. 已勾选“隐藏MAX强化装备”。\r\n\r\n" +
                  "以上设置均已完成，是否开始整理？"
                : $"自动强化会永久{disposalName}不符合当前强化建议的装备。\r\n\r\n" +
                  "开始前请确认：\r\n" +
                  "1. 游戏已停在背包装备列表，并已选中准备处理的第一件装备；\r\n" +
                  "2. 已勾选“隐藏已配戴装备”；\r\n" +
                  "3. 已勾选“隐藏MAX强化装备”。\r\n\r\n" +
                  "以上设置均已完成，是否开始？",
            organize ? "确认开始装备整理" : "确认开始自动强化",
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
        _btnAutoOrganize.Enabled = false;
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
        _lblAutoState.Text = organize ? "整理中" : "运行中";
        _lblAutoState.ForeColor = AdviceContinueColor;
        _lblAutoStats.Text = FormatAutoStats(0, 0, 0, 0, 0, 0);
        ApplyRecognitionAvailability(showHotKeySuccess: false);

        var options = AutoEnhancementOptions.CreateDefault(
            (int)_numAutoMaxEquipment.Value,
            (double)numLeftThreshold.Value,
            (double)numRightThreshold.Value,
            (double)numLevel88Threshold.Value,
            (double)_numHeroMatchThreshold.Value,
            disposalMethod,
            organize ? false : _chkAutoStopOnValuableEquipment.Checked,
            _chkHeroicOnlyGambleSpeed.Checked,
            _chkSpeedSetRequiresSpeed.Checked,
            _chkCriticalNecklaceMainStatRule.Checked,
            _disabledDemandProfiles,
            ReadLegendarySpeedLadderFromControls(),
            mode);
        var progress = new Progress<AutoEnhancementProgress>(value =>
        {
            AppendAutoLog(value.Level, value.Message);
            if (value.Equipment != null)
                AddAutoResultRow(value.Equipment);
            _lblAutoStats.Text = FormatAutoStats(
                value.Processed, value.Kept, value.Sold, value.Extracted, value.Enhanced, value.Skipped);
        });
        var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates");
        AutoEnhancementRunner? runner = null;

        try
        {
            runner = new AutoEnhancementRunner(session, templateDir, options, progress);
            var result = await runner.RunAsync(cancellationToken);
            _lblAutoStats.Text = FormatAutoStats(
                result.Processed, result.Kept, result.Sold, result.Extracted, result.Enhanced, result.Skipped);
            _lblAutoState.Text = result.StoppedForValuableEquipment ? "已安全停止" : "已完成";
            _lblAutoState.ForeColor = result.StoppedForValuableEquipment ? AdviceGambleColor : AdviceContinueColor;
            AppendAutoLog(
                AutoEnhancementLogLevel.Success,
                organize
                    ? $"本轮整理结束：处理 {result.Processed} · 出售 {result.Sold} · 跳过 {result.Skipped}"
                    : $"本轮结束：处理 {result.Processed} · 保留 {result.Kept} · 出售 {result.Sold} · 分解 {result.Extracted}");
            UpdateStatus(result.Message);
        }
        catch (OperationCanceledException)
        {
            _lblAutoState.Text = "已停止";
            _lblAutoState.ForeColor = AdviceGambleColor;
            AppendAutoLog(AutoEnhancementLogLevel.Warning,
                organize ? "装备整理已由用户停止" : "自动强化已由用户停止");
            UpdateStatus(organize ? "装备整理已停止" : "自动强化已停止");
        }
        catch (Exception ex)
        {
            _lblAutoState.Text = "发生错误，已停机";
            _lblAutoState.ForeColor = AdviceGiveUpColor;
            AppendAutoLog(AutoEnhancementLogLevel.Error, ex.Message);
            WriteDebugLog($"{(organize ? "装备整理" : "自动强化")}失败：{ex}");
            UpdateStatus($"{(organize ? "装备整理" : "自动强化")}已停止：{ex.Message}");
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
                _btnAutoOrganize.Enabled = true;
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

    private static string FormatAutoStats(
        int processed, int kept, int sold, int extracted, int enhanced, int skipped)
        => $"已处理 {processed} · 保留 {kept} · 出售 {sold} · 跳过 {skipped} · 分解 {extracted} · 强化过 {enhanced}";

    private void ClearAutoResultGrid()
    {
        _autoResultRecords.Clear();
        if (_autoResultGrid == null || _autoResultGrid.IsDisposed)
            return;
        _autoResultGrid.Rows.Clear();
        ClearAutoResultScreenshotPreview();
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
            "跳过" => record.Outcome == AutoEnhancementOutcome.Skipped,
            "分解" => record.Outcome == AutoEnhancementOutcome.Extracted,
            _ => true,
        };

    private void SyncAutoResultGrid(AutoEnhancementSummary summary)
    {
        if (_autoResultGrid == null || _autoResultGrid.IsDisposed)
            return;

        _lblAutoStats.Text = FormatAutoStats(
            summary.Processed, summary.Kept, summary.Sold, summary.Extracted, summary.Enhanced, summary.Skipped);

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

        if (_autoResultGrid.Rows.Count > 0)
        {
            _autoResultGrid.ClearSelection();
            var last = _autoResultGrid.Rows.Count - 1;
            _autoResultGrid.Rows[last].Selected = true;
            _autoResultGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, last);
            if (_autoResultGrid.Rows[last].Cells.Count > 0)
                _autoResultGrid.CurrentCell = _autoResultGrid.Rows[last].Cells[0];
        }
        else
        {
            ClearAutoResultScreenshotPreview();
        }

        UpdateAutoResultScreenshotPreview();
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
            AutoEnhancementOutcome.Skipped => AdviceGambleColor,
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

        if (_autoResultGrid?.SelectedRows.Count > 0
            && _autoResultGrid.SelectedRows[0].Tag is AutoEnhancementEquipmentRecord fromSelection)
        {
            record = fromSelection;
            return true;
        }

        return false;
    }

    private void UpdateAutoResultScreenshotPreview()
    {
        if (_autoResultPreview == null || _autoResultPreview.IsDisposed)
            return;
        if (_autoResultPreview.InvokeRequired)
        {
            _autoResultPreview.BeginInvoke(UpdateAutoResultScreenshotPreview);
            return;
        }

        if (!TryGetSelectedAutoResult(out var record))
        {
            ClearAutoResultScreenshotPreview();
            return;
        }

        if (string.IsNullOrWhiteSpace(record.ScreenshotPath) || !File.Exists(record.ScreenshotPath))
        {
            ClearAutoResultScreenshotPreview();
            if (_lblAutoPreviewHint != null && !_lblAutoPreviewHint.IsDisposed)
                _lblAutoPreviewHint.Text = "该结果没有可用截图";
            return;
        }

        if (string.Equals(_autoResultPreviewPath, record.ScreenshotPath, StringComparison.OrdinalIgnoreCase)
            && _autoResultPreview.Image != null)
        {
            if (_lblAutoPreviewHint != null && !_lblAutoPreviewHint.IsDisposed)
                _lblAutoPreviewHint.Text = "点击图片可放大查看";
            return;
        }

        try
        {
            var image = LoadImageCopy(record.ScreenshotPath);
            var old = _autoResultPreview.Image;
            _autoResultPreview.Image = image;
            _autoResultPreviewPath = record.ScreenshotPath;
            old?.Dispose();
            if (_lblAutoPreviewHint != null && !_lblAutoPreviewHint.IsDisposed)
                _lblAutoPreviewHint.Text = "点击图片可放大查看";
        }
        catch (Exception ex)
        {
            ClearAutoResultScreenshotPreview();
            if (_lblAutoPreviewHint != null && !_lblAutoPreviewHint.IsDisposed)
                _lblAutoPreviewHint.Text = $"截图加载失败：{ex.Message}";
        }
    }

    private void ClearAutoResultScreenshotPreview()
    {
        if (_autoResultPreview == null || _autoResultPreview.IsDisposed)
            return;
        var old = _autoResultPreview.Image;
        _autoResultPreview.Image = null;
        _autoResultPreviewPath = null;
        old?.Dispose();
        if (_lblAutoPreviewHint != null && !_lblAutoPreviewHint.IsDisposed)
            _lblAutoPreviewHint.Text = "选中结果后在此显示，点击图片可放大";
    }

    private static Image LoadImageCopy(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private void ShowAutoResultScreenshotZoom()
    {
        if (_autoResultPreview?.Image == null
            || string.IsNullOrWhiteSpace(_autoResultPreviewPath)
            || !File.Exists(_autoResultPreviewPath))
        {
            UpdateStatus("当前没有可放大的判定截图");
            return;
        }

        if (_autoScreenshotZoomForm != null && !_autoScreenshotZoomForm.IsDisposed)
        {
            _autoScreenshotZoomForm.Close();
            _autoScreenshotZoomForm.Dispose();
            _autoScreenshotZoomForm = null;
        }

        Image zoomImage;
        try
        {
            zoomImage = LoadImageCopy(_autoResultPreviewPath);
        }
        catch (Exception ex)
        {
            UpdateStatus($"打开放大图失败：{ex.Message}");
            return;
        }

        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 33, 36),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = zoomImage,
            Cursor = Cursors.Hand,
        };
        var form = new Form
        {
            Text = "判定截图（点击或按 Esc 关闭）",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(ScalePixel(1100), ScalePixel(720)),
            MinimumSize = new Size(ScalePixel(640), ScalePixel(420)),
            ShowInTaskbar = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(32, 33, 36),
        };
        form.Controls.Add(picture);
        form.KeyPreview = true;
        form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                form.Close();
        };
        picture.Click += (_, _) => form.Close();
        form.FormClosed += (_, _) =>
        {
            picture.Image = null;
            zoomImage.Dispose();
            if (ReferenceEquals(_autoScreenshotZoomForm, form))
                _autoScreenshotZoomForm = null;
        };
        _autoScreenshotZoomForm = form;
        form.Show(this);
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
