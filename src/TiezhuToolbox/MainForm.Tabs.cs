using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

public partial class MainForm
{
    private readonly AppSettings _settings = AppSettingsStore.Load();
    private readonly HashSet<string> _disabledDemandProfiles = new(StringComparer.Ordinal);
    private AntdUI.Tabs _mainTabs = null!;
    private AntdUI.TabPage _equipmentTab = null!;
    private AntdUI.TabPage _settingsTab = null!;
    private Panel _settingsScrollHost = null!;
    private DemandBrowserControl _demandBrowserControl = null!;
    private bool _isLoadingSettings;
    private Label _settingsRulesLabel = null!;
    private AntdUI.TabPage _connectionTab = null!;
    private Control _connectionContent = null!;
    private Panel _connectionCard = null!;
    private Panel _connectionPcRow = null!;
    private Label _lblConnectionMode = null!;
    private Label _lblConnectionTarget = null!;
    private Label _lblConnectionAddress = null!;
    private Label _lblConnectionResolution = null!;
    private Label _lblConnectionInput = null!;

    private bool IsEquipmentTabActive => _mainTabs.SelectedTab == _equipmentTab;

    private void InitializeTabsAndSettings()
    {
        SuspendLayout();
        Controls.Remove(topPanel);
        Controls.Remove(mainTable);
        Controls.Remove(pnlScreenshot);

        equipTable.Controls.Remove(settingsDivider);
        equipTable.Controls.Remove(thresholdPanel);
        equipTable.Controls.Remove(recognitionSettingsPanel);
        NormalizeDesignerControlForRuntime(thresholdPanel);
        NormalizeDesignerControlForRuntime(recognitionSettingsPanel);
        while (equipTable.RowStyles.Count > 8)
            equipTable.RowStyles.RemoveAt(equipTable.RowStyles.Count - 1);
        equipTable.RowCount = 8;

        _mainTabs = new AntdUI.Tabs
        {
            Dock = DockStyle.Fill,
            Type = AntdUI.TabType.Line,
            Gap = 28,
            Padding = new Padding(0),
        };
        _equipmentTab = new AntdUI.TabPage { Text = "装备", BackColor = Color.White };
        _gearScanTab = new AntdUI.TabPage { Text = "扫描", BackColor = Color.FromArgb(245, 246, 248) };
        _autoEnhanceTab = new AntdUI.TabPage { Text = "自动", BackColor = Color.FromArgb(245, 246, 248) };
        _starForgeTab = new AntdUI.TabPage { Text = "铁匠铺", BackColor = Color.FromArgb(245, 246, 248) };
        _demandTab = new AntdUI.TabPage { Text = "需求", BackColor = Color.White };
        _settingsTab = new AntdUI.TabPage { Text = "设置", BackColor = Color.FromArgb(245, 246, 248) };

        foreach (var control in new Control[]
                 {
                     comboConnectionMode, comboDevices, txtAddress, btnConnect,
                     comboWindowResolution, comboWindowInputMode, btnSetResolution, btnConnectionStep, btnRefresh,
                     btnOpenFolder, btnToggleShot, btnCaptureRecognize,
                 })
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        // Select.List 是 AntdUI 提供的不可编辑选择模式：禁止文字输入，但仍可展开下拉。
        comboConnectionMode.ReadOnly = false;
        comboConnectionMode.List = true;
        comboConnectionMode.Items.Clear();
        comboConnectionMode.Items.Add("PC");
        comboConnectionMode.Items.Add("模拟器");
        comboConnectionMode.SelectedIndexChanged += comboConnectionMode_SelectedIndexChanged;
        comboDevices.ReadOnly = false;
        comboDevices.List = true;
        comboWindowResolution.ReadOnly = false;
        comboWindowResolution.List = true;
        comboWindowResolution.Items.Clear();
        foreach (var preset in new[] { "1920x1080", "1600x900", "1280x720", "1366x768" })
            comboWindowResolution.Items.Add(preset);
        comboWindowInputMode.ReadOnly = false;
        comboWindowInputMode.List = true;
        comboWindowInputMode.Items.Clear();
        comboWindowInputMode.Items.Add("前台");
        comboWindowInputMode.Items.Add("后台");
        comboWindowInputMode.SelectedIndexChanged += comboWindowInputMode_SelectedIndexChanged;
        toolTip.SetToolTip(comboConnectionMode, "选择 PC 客户端或模拟器");
        toolTip.SetToolTip(comboWindowInputMode, "前台会抢鼠标；后台不抢键鼠，窗口可能短暂移动");
        toolTip.SetToolTip(btnConnectionStep, "打开连接页");
        toolTip.SetToolTip(comboWindowResolution, "目标游戏画面分辨率，选用窗口时会自动设置");
        toolTip.SetToolTip(btnSetResolution, "手动将当前窗口调整到目标分辨率");
        topPanel.Resize += (_, _) => LayoutTopToolbar();

        _disabledDemandProfiles.UnionWith(_settings.DisabledDemandProfiles);
        _demandBrowserControl = new DemandBrowserControl(
            profileKey => !_disabledDemandProfiles.Contains(profileKey),
            SetDemandProfileEnabled,
            OnDemandProfilesChanged);
        _demandTab.Controls.Add(_demandBrowserControl);
        _demandBrowserControl.ApplyInitialDpiScale(_layoutDpi);

        _gearScanContent = CreateGearScanContent();
        _gearScanTab.Controls.Add(_gearScanContent);
        ScaleRuntimePage(_gearScanContent);

        _autoEnhanceContent = CreateAutoEnhanceContent();
        _autoEnhanceTab.Controls.Add(_autoEnhanceContent);
        ScaleRuntimePage(_autoEnhanceContent);

        _starForgeContent = CreateStarForgeContent();
        _starForgeTab.Controls.Add(_starForgeContent);
        ScaleRuntimePage(_starForgeContent);

        var settingsContent = CreateSettingsContent();
        _settingsTab.Controls.Add(settingsContent);
        ScaleRuntimePage(settingsContent);

        _connectionTab = new AntdUI.TabPage { Text = "连接", BackColor = Color.FromArgb(245, 246, 248) };
        _connectionContent = CreateConnectionContent();
        _connectionTab.Controls.Add(_connectionContent);
        ScaleRuntimePage(_connectionContent);

        _mainTabs.Pages.Add(_equipmentTab);
        _mainTabs.Pages.Add(_gearScanTab);
        _mainTabs.Pages.Add(_autoEnhanceTab);
        _mainTabs.Pages.Add(_starForgeTab);
        _mainTabs.Pages.Add(_demandTab);
        _mainTabs.Pages.Add(_settingsTab);
        _mainTabs.Pages.Add(_connectionTab);
        _mainTabs.SelectedIndex = 0;
        _mainTabs.SelectedIndexChanged += MainTabs_SelectedIndexChanged;

        CreateHybridShell();
        RelocateEquipmentActions();
        _equipmentTab.Controls.Add(_equipmentHost);

        LoadSettingsIntoControls();
        txtAddress.Leave += (_, _) => SaveSettingsFromControls();
        ResumeLayout(performLayout: true);
        LayoutTopToolbar();
        ApplyHybridPageLayout();
    }

    private bool _isLayingOutToolbar;

    private void LayoutTopToolbar()
    {
        if (_isLayingOutToolbar)
            return;
        _isLayingOutToolbar = true;
        try
        {
            LayoutTopToolbarCore();
        }
        finally
        {
            _isLayingOutToolbar = false;
        }
    }

    private void LayoutTopToolbarCore()
    {
        topPanel.Visible = false;
        topPanel.Height = 0;
        btnOpenFolder.Visible = false;
        btnConnectionStep.Visible = false;

        var showNativeActions = IsEquipmentTabActive && !CanUseWebPage;
        btnCaptureRecognize.Visible = showNativeActions;
        btnToggleShot.Visible = showNativeActions;
        if (!showNativeActions)
            return;

        btnCaptureRecognize.Size = new Size(ScalePixel(112), ScalePixel(34));
        btnToggleShot.Size = new Size(ScalePixel(92), ScalePixel(34));
        btnToggleShot.Text = _screenshotWanted ? "收起画面" : "游戏画面";
    }

    private void RelocateEquipmentActions()
    {
        topPanel.Controls.Remove(btnCaptureRecognize);
        topPanel.Controls.Remove(btnToggleShot);
        btnCaptureRecognize.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        btnToggleShot.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        btnCaptureRecognize.Margin = new Padding(ScalePixel(16), 0, ScalePixel(8), 0);
        btnToggleShot.Margin = Padding.Empty;
        heroesHeader.WrapContents = false;
        heroesHeader.Controls.Add(btnCaptureRecognize);
        heroesHeader.Controls.Add(btnToggleShot);
    }

    private Control CreateConnectionContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24),
            BackColor = Color.FromArgb(245, 246, 248),
        };
        _connectionCard = new Panel
        {
            BackColor = Color.White,
            Location = new Point(24, 24),
            Size = new Size(720, 400),
        };
        host.Resize += (_, _) =>
        {
            _connectionCard.Width = Math.Min(
                ScalePixel(760),
                Math.Max(ScalePixel(560), host.ClientSize.Width - ScalePixel(48)));
            LayoutConnectionPage();
        };

        var title = new Label
        {
            Text = "连接",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            Location = new Point(24, 18),
            Size = new Size(650, 40),
        };
        var hint = new Label
        {
            Text = "选择 PC 或模拟器后刷新并选用目标。后台模式不抢键鼠，适合把游戏放在一边。",
            ForeColor = Color.FromArgb(120, 113, 108),
            Location = new Point(24, 62),
            Size = new Size(650, 36),
        };

        foreach (var control in new Control[]
                 {
                     comboConnectionMode, comboDevices, txtAddress, btnConnect,
                     comboWindowResolution, comboWindowInputMode, btnSetResolution, btnRefresh,
                 })
        {
            topPanel.Controls.Remove(control);
            _connectionCard.Controls.Add(control);
        }

        _lblConnectionMode = CreateConnectionLabel("方式", 24, 118);
        _lblConnectionTarget = CreateConnectionLabel("目标", 24, 168);
        _lblConnectionAddress = CreateConnectionLabel("窗口标题", 24, 218);

        _connectionPcRow = new Panel
        {
            Location = new Point(24, 268),
            Size = new Size(670, 34),
        };
        _lblConnectionResolution = CreateConnectionLabel("分辨率", 0, 0);
        _lblConnectionInput = CreateConnectionLabel("输入", 320, 0);
        _connectionPcRow.Controls.Add(_lblConnectionResolution);
        _connectionPcRow.Controls.Add(_lblConnectionInput);
        _connectionCard.Controls.Remove(comboWindowResolution);
        _connectionCard.Controls.Remove(comboWindowInputMode);
        _connectionPcRow.Controls.Add(comboWindowResolution);
        _connectionPcRow.Controls.Add(comboWindowInputMode);

        _connectionCard.Controls.Add(title);
        _connectionCard.Controls.Add(hint);
        _connectionCard.Controls.Add(_lblConnectionMode);
        _connectionCard.Controls.Add(_lblConnectionTarget);
        _connectionCard.Controls.Add(_lblConnectionAddress);
        _connectionCard.Controls.Add(_connectionPcRow);
        host.Controls.Add(_connectionCard);
        LayoutConnectionPage();
        return host;
    }

    private static Label CreateConnectionLabel(string text, int x, int y)
        => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(88, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(32, 33, 36),
        };

    private void LayoutConnectionPage()
    {
        if (_connectionCard == null)
            return;

        var pad = ScalePixel(24);
        var gap = ScalePixel(8);
        var labelWidth = ScalePixel(88);
        var controlX = pad + labelWidth + ScalePixel(12);
        var rowRight = _connectionCard.Width - pad;
        if (rowRight < controlX + ScalePixel(180))
            rowRight = controlX + ScalePixel(400);

        PlaceConnectionLabel(_lblConnectionMode, pad, ScalePixel(118), labelWidth);
        comboConnectionMode.Location = new Point(controlX, ScalePixel(118));
        comboConnectionMode.Size = new Size(ScalePixel(120), ScalePixel(34));

        PlaceConnectionLabel(_lblConnectionTarget, pad, ScalePixel(168), labelWidth);
        PlaceFromRight(btnRefresh, ScalePixel(76), ScalePixel(168), ref rowRight, gap);
        PlaceFromRight(btnConnect, ScalePixel(76), ScalePixel(168), ref rowRight, gap);
        comboDevices.Location = new Point(controlX, ScalePixel(168));
        comboDevices.Size = new Size(Math.Max(ScalePixel(180), rowRight - controlX), ScalePixel(34));

        PlaceConnectionLabel(_lblConnectionAddress, pad, ScalePixel(218), labelWidth);
        txtAddress.Location = new Point(controlX, ScalePixel(218));
        txtAddress.Size = new Size(Math.Max(ScalePixel(180), _connectionCard.Width - controlX - pad), ScalePixel(34));

        _connectionPcRow.Location = new Point(pad, ScalePixel(268));
        _connectionPcRow.Width = Math.Max(ScalePixel(400), _connectionCard.Width - pad * 2);
        PlaceConnectionLabel(_lblConnectionResolution, 0, 0, labelWidth);
        comboWindowResolution.Location = new Point(labelWidth + ScalePixel(12), 0);
        comboWindowResolution.Size = new Size(ScalePixel(118), ScalePixel(34));
        var inputX = comboWindowResolution.Right + ScalePixel(24);
        PlaceConnectionLabel(_lblConnectionInput, inputX, 0, labelWidth);
        comboWindowInputMode.Location = new Point(inputX + labelWidth + ScalePixel(12), 0);
        comboWindowInputMode.Size = new Size(ScalePixel(76), ScalePixel(34));
        btnSetResolution.Visible = false;
        _connectionPcRow.Visible = IsWindowConnectionMode;
        _connectionCard.Height = IsWindowConnectionMode ? ScalePixel(330) : ScalePixel(280);
    }

    private void PlaceConnectionLabel(Label label, int x, int y, int width)
    {
        if (label == null)
            return;
        label.Location = new Point(x, y);
        label.Size = new Size(width, ScalePixel(34));
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private void PlaceFromRight(Control control, int width, int y, ref int right, int gap)
    {
        right -= width;
        control.Location = new Point(right, y);
        control.Size = new Size(width, ScalePixel(34));
        right -= gap;
    }

    private Control CreateSettingsContent()
    {
        _settingsScrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
        var card = new Panel
        {
            BackColor = Color.White,
            Location = new Point(24, 24),
            Size = new Size(720, 1080),
            Padding = new Padding(24),
        };
        _settingsScrollHost.Resize += (_, _) => card.Width = Math.Min(
            ScalePixel(760),
            Math.Max(ScalePixel(560), _settingsScrollHost.ClientSize.Width - ScalePixel(48)));

        var title = new Label
        {
            Text = "软件设置",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            Location = new Point(24, 18),
            Size = new Size(650, 48),
        };
        var scoreTitle = CreateSettingsHeading("强化分数", "85级按左/右三件阈值每跳 +6；88级使用独立阈值，每跳 +7。", 70);
        thresholdPanel.Dock = DockStyle.None;
        thresholdPanel.Location = new Point(24, 132);
        // 高 DPI 下 FlowLayoutPanel 会按缩放后的 Margin 排列子控件，预留足够宽度避免最右侧 88 级输入框被裁剪。
        thresholdPanel.Size = new Size(700, 34);
        thresholdPanel.AutoSize = false;
        thresholdPanel.Margin = Padding.Empty;
        numLeftThreshold.Size = new Size(82, 34);
        numRightThreshold.Size = new Size(82, 34);
        numLevel88Threshold.Size = new Size(82, 34);
        foreach (var label in new[] { lblThresholdGroup, lblThLeft, lblThRight, lblTh88 })
            ConfigureSettingsRowLabel(label);

        var recognitionTitle = CreateSettingsHeading("识别控制", "全局快捷键和持续识别只在“装备”页生效。", 188);
        recognitionSettingsPanel.Dock = DockStyle.None;
        recognitionSettingsPanel.Location = new Point(24, 258);
        recognitionSettingsPanel.Size = new Size(620, 34);
        recognitionSettingsPanel.AutoSize = false;
        recognitionSettingsPanel.Margin = Padding.Empty;
        comboRecognitionHotKey.Size = new Size(76, 34);
        chkContinuousRecognition.Size = new Size(108, 34);
        numRecognitionInterval.Size = new Size(88, 34);
        foreach (var label in new[]
                 { lblRecognitionGroup, lblRecognitionHotKey, lblRecognitionInterval, lblIntervalUnit })
            ConfigureSettingsRowLabel(label);

        EnsureAutoEnhanceSettingsControls();
        var automationTitle = CreateSettingsHeading(
            "自动强化",
            "装备处理方式、最多处理、需求匹配度与赌速度规则在独立窗口中配置。",
            314);
        var openAutoSettings = new AntdUI.Button
        {
            Text = "打开自动强化设置",
            Location = new Point(24, 384),
            Size = new Size(160, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        openAutoSettings.Click += (_, _) => ShowAutoEnhanceSettingsWindow();
        var openFolder = new AntdUI.Button
        {
            Text = "打开截图目录",
            Location = new Point(196, 384),
            Size = new Size(140, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        openFolder.Click += (_, _) => btnOpenFolder_Click(this, EventArgs.Empty);

        var rulesTitle = CreateSettingsHeading(
            "自动规则说明",
            "推荐匹配与套装需求数据会自动应用以下规则。",
            440);
        var rulesPanel = new Panel
        {
            BackColor = Color.FromArgb(247, 249, 252),
            Location = new Point(24, 500),
            Size = new Size(690, 194),
            Padding = new Padding(12, 9, 12, 9),
        };
        _settingsRulesLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9.2F),
            ForeColor = Color.FromArgb(66, 70, 77),
            Text = string.Join("\r\n", SettingsRuleLines.Select(line => "• " + line)),
        };
        rulesPanel.Controls.Add(_settingsRulesLabel);

        var reset = new AntdUI.Button
        {
            Text = "恢复默认设置",
            Location = new Point(24, 720),
            Size = new Size(120, 34),
            Radius = 6,
        };
        reset.Click += (_, _) => ResetSettings();

        card.Size = new Size(720, 800);
        card.Controls.Add(reset);
        card.Controls.Add(rulesPanel);
        card.Controls.Add(rulesTitle);
        card.Controls.Add(openFolder);
        card.Controls.Add(openAutoSettings);
        card.Controls.Add(automationTitle);
        card.Controls.Add(recognitionSettingsPanel);
        card.Controls.Add(recognitionTitle);
        card.Controls.Add(thresholdPanel);
        card.Controls.Add(scoreTitle);
        card.Controls.Add(title);
        _settingsScrollHost.Controls.Add(card);
        return _settingsScrollHost;
    }

    private static Label CreateLegendarySpeedLabel(string text)
        => new()
        {
            Text = text,
            ForeColor = Color.FromArgb(32, 33, 36),
            Size = new Size(48, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };

    private AntdUI.InputNumber CreateLegendarySpeedInput(int defaultValue)
    {
        var input = new AntdUI.InputNumber
        {
            Size = new Size(54, 34),
            Minimum = 0,
            Maximum = 45,
            Value = defaultValue,
            Radius = 6,
            Margin = new Padding(0, 0, 8, 0),
        };
        input.ValueChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };
        return input;
    }

    /// <summary>
    /// 设置行来自设计器页面，高 DPI 下字体可能先于运行时页面完成缩放。
    /// 标签宽度交给首选尺寸计算，避免固定像素宽度只显示部分文字。
    /// </summary>
    private static void ConfigureSettingsRowLabel(Label label)
    {
        label.AutoSize = true;
        label.MinimumSize = new Size(0, 34);
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static Label CreateSettingsHeading(string title, string description, int top)
        => new()
        {
            Text = $"{title}\r\n{description}",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 33, 36),
            Location = new Point(24, top),
            Size = new Size(650, 52),
        };

    private void LoadSettingsIntoControls()
    {
        _isLoadingSettings = true;
        try
        {
            numLeftThreshold.Value = _settings.LeftThreshold;
            numRightThreshold.Value = _settings.RightThreshold;
            numLevel88Threshold.Value = _settings.Level88Threshold;
            comboRecognitionHotKey.SelectedValue = _settings.RecognitionHotKey;
            _recognitionHotKeyText = _settings.RecognitionHotKey;
            if (HotKeyBinding.TryParse(_recognitionHotKeyText, out var loadedHotKey) && loadedHotKey.IsPlainFunctionKey)
                comboRecognitionHotKey.SelectedValue = loadedHotKey.Key.ToString();
            RefreshBindHotKeyButton();
            chkContinuousRecognition.Checked = _settings.ContinuousRecognition;
            numRecognitionInterval.Value = _settings.RecognitionIntervalSeconds;
            continuousRecognitionTimer.Interval = Math.Max(100, (int)(_settings.RecognitionIntervalSeconds * 1000));
            comboConnectionMode.SelectedValue = _settings.ConnectionMode is "ADB" or "模拟器"
                ? "模拟器"
                : "PC";
            if (comboConnectionMode.SelectedIndex < 0)
                comboConnectionMode.SelectedIndex = 0;
            comboWindowResolution.Text = string.IsNullOrWhiteSpace(_settings.WindowContentResolution)
                ? "1920x1080"
                : _settings.WindowContentResolution;
            comboWindowInputMode.SelectedValue = _settings.WindowInputMode == "后台" ? "后台" : "前台";
            if (comboWindowInputMode.SelectedIndex < 0)
                comboWindowInputMode.SelectedIndex = 0;
            _screenshotWanted = _settings.LiveGamePreview;

            ApplyConnectionModeUi();
            _comboGearScanMinimumEnhance.SelectedValue = $"+{_settings.GearScanMinimumEnhance}";
            _comboGearScanHeroFilter.SelectedValue = GetGearScanHeroFilterText(_settings.GearScanHeroFilterMode);
            _numAutoMaxEquipment.Value = _settings.AutoEnhanceMaxEquipment;
            _comboAutoDisposalMethod.SelectedValue = _settings.AutoEnhanceDisposalMethod;
            _numHeroMatchThreshold.Value = _settings.MinimumDemandMatchScore;
            _chkAutoStopOnValuableEquipment.Checked = _settings.AutoEnhanceStopOnValuableEquipment;
            _chkHeroicOnlyGambleSpeed.Checked = _settings.HeroicOnlyGambleSpeed;
            _chkSpeedSetRequiresSpeed.Checked = _settings.SpeedSetRequiresSpeed;
            _chkCriticalNecklaceMainStatRule.Checked = _settings.CriticalNecklaceMainStatRule;
            var ladder = _settings.LegendarySpeedLadder ?? new LegendarySpeedLadder();
            _numLegendarySpeedPlus3.Value = ladder.BeforePlus3;
            _numLegendarySpeedPlus6.Value = ladder.BeforePlus6;
            _numLegendarySpeedPlus9.Value = ladder.BeforePlus9;
            _numLegendarySpeedPlus12.Value = ladder.BeforePlus12;
            _numLegendarySpeedPlus15.Value = ladder.BeforePlus15;
            _numLegendarySpeedFinal.Value = ladder.FinalPlus15;
            _numStarForgeMaximumChanges.Value = _settings.StarForgeMaximumChanges;
            for (var i = 0; i < _starForgeRows.Count; i++)
            {
                var setting = _settings.StarForgeTargets[i];
                var row = _starForgeRows[i];
                row.Enabled.Checked = setting.Enabled;
                row.Stat.SelectedValue = setting.StatName;
                row.Minimum.Value = (decimal)setting.MinimumValue;
                UpdateStarForgeRowUnit(row, setting.StatName);
            }
        }
        finally
        {
            _isLoadingSettings = false;
        }

        PushWebSettings();
        ApplyLivePreviewVisibility();
    }

    private void SaveSettingsFromControls()
    {
        if (_isLoadingSettings)
            return;
        _settings.LeftThreshold = numLeftThreshold.Value;
        _settings.RightThreshold = numRightThreshold.Value;
        _settings.Level88Threshold = numLevel88Threshold.Value;
        _settings.RecognitionHotKey = _recognitionHotKeyText;
        _settings.ContinuousRecognition = chkContinuousRecognition.Checked;
        _settings.RecognitionIntervalSeconds = numRecognitionInterval.Value;
        _settings.ConnectionMode = IsWindowConnectionMode ? "窗口" : "ADB";
        if (IsWindowConnectionMode)
            _settings.WindowTitle = string.IsNullOrWhiteSpace(txtAddress.Text)
                ? "第七史诗"
                : txtAddress.Text.Trim();
        else
            _settings.AdbAddress = string.IsNullOrWhiteSpace(txtAddress.Text)
                ? "127.0.0.1:16384"
                : txtAddress.Text.Trim();
        var resolutionText = comboWindowResolution.SelectedValue as string ?? comboWindowResolution.Text;
        if (AppSettings.TryParseWindowContentResolution(resolutionText, out var resW, out var resH))
            _settings.WindowContentResolution = $"{resW}x{resH}";
        else
            _settings.WindowContentResolution = "1920x1080";
        _settings.WindowInputMode = IsWindowBackgroundMode ? "后台" : "前台";
        _settings.LiveGamePreview = _screenshotWanted;
        _settings.GearScanMinimumEnhance = GetGearScanMinimumEnhance();
        _settings.GearScanHeroFilterMode = GetGearScanHeroFilter();
        _settings.AutoEnhanceMaxEquipment = (int)_numAutoMaxEquipment.Value;
        _settings.AutoEnhanceDisposalMethod = _comboAutoDisposalMethod.SelectedValue as string
            ?? _comboAutoDisposalMethod.Text;
        _settings.MinimumDemandMatchScore = _numHeroMatchThreshold.Value;
        _settings.AutoEnhanceStopOnValuableEquipment = _chkAutoStopOnValuableEquipment.Checked;
        _settings.HeroicOnlyGambleSpeed = _chkHeroicOnlyGambleSpeed.Checked;
        _settings.SpeedSetRequiresSpeed = _chkSpeedSetRequiresSpeed.Checked;
        _settings.CriticalNecklaceMainStatRule = _chkCriticalNecklaceMainStatRule.Checked;
        _settings.LegendarySpeedLadder = new LegendarySpeedLadder
        {
            BeforePlus3 = (int)_numLegendarySpeedPlus3.Value,
            BeforePlus6 = (int)_numLegendarySpeedPlus6.Value,
            BeforePlus9 = (int)_numLegendarySpeedPlus9.Value,
            BeforePlus12 = (int)_numLegendarySpeedPlus12.Value,
            BeforePlus15 = (int)_numLegendarySpeedPlus15.Value,
            FinalPlus15 = (int)_numLegendarySpeedFinal.Value,
        };
        _settings.StarForgeMaximumChanges = (int)_numStarForgeMaximumChanges.Value;
        _settings.StarForgeTargets = _starForgeRows.Select(row => new StarForgeTargetSetting
        {
            Enabled = row.Enabled.Checked,
            StatName = GetSelectedStarForgeStat(row),
            MinimumValue = (double)row.Minimum.Value,
        }).ToList();
        _settings.DisabledDemandProfiles = _disabledDemandProfiles
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        try
        {
            AppSettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            UpdateStatus($"保存设置失败：{ex.Message}");
            WriteDebugLog($"保存设置失败：{ex}");
        }
    }

    private void ResetSettings()
    {
        UnregisterRecognitionHotKey();
        var defaults = AppSettings.CreateDefault();
        _settings.LeftThreshold = defaults.LeftThreshold;
        _settings.RightThreshold = defaults.RightThreshold;
        _settings.Level88Threshold = defaults.Level88Threshold;
        _settings.RecognitionHotKey = defaults.RecognitionHotKey;
        _recognitionHotKeyText = defaults.RecognitionHotKey;
        _settings.ContinuousRecognition = defaults.ContinuousRecognition;
        _settings.RecognitionIntervalSeconds = defaults.RecognitionIntervalSeconds;
        _settings.AdbAddress = defaults.AdbAddress;
        _settings.WindowInputMode = defaults.WindowInputMode;
        _settings.LiveGamePreview = defaults.LiveGamePreview;
        _settings.GearScanMinimumEnhance = defaults.GearScanMinimumEnhance;
        _settings.GearScanHeroFilterMode = defaults.GearScanHeroFilterMode;
        _settings.AutoEnhanceMaxEquipment = defaults.AutoEnhanceMaxEquipment;
        _settings.AutoEnhanceDisposalMethod = defaults.AutoEnhanceDisposalMethod;
        _settings.MinimumDemandMatchScore = defaults.MinimumDemandMatchScore;
        _settings.AutoEnhanceStopOnValuableEquipment = defaults.AutoEnhanceStopOnValuableEquipment;
        _settings.HeroicOnlyGambleSpeed = defaults.HeroicOnlyGambleSpeed;
        _settings.SpeedSetRequiresSpeed = defaults.SpeedSetRequiresSpeed;
        _settings.CriticalNecklaceMainStatRule = defaults.CriticalNecklaceMainStatRule;
        _settings.LegendarySpeedLadder = new LegendarySpeedLadder();
        _settings.StarForgeMaximumChanges = defaults.StarForgeMaximumChanges;
        _settings.StarForgeTargets = defaults.StarForgeTargets;
        _disabledDemandProfiles.Clear();
        _settings.DisabledDemandProfiles.Clear();
        LoadSettingsIntoControls();
        _demandBrowserControl.RefreshProfiles();
        SaveSettingsFromControls();
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        UpdateAdvice();
        PushWebDemand();
        PushWebEquipment();
        UpdateStatus("软件设置已恢复默认");
    }

    private void SetDemandProfileEnabled(string profileKey, bool enabled)
    {
        if (enabled)
            _disabledDemandProfiles.Remove(profileKey);
        else
            _disabledDemandProfiles.Add(profileKey);

        SaveSettingsFromControls();
        if (_lastInfo != null)
        {
            ShowDemandRecommendations(_lastInfo);
            UpdateAdvice();
        }

        PushWebProfileEnabled(profileKey, enabled);
        PushWebEquipment();
    }

    private void OnDemandProfilesChanged()
    {
        if (_lastInfo != null)
        {
            ShowDemandRecommendations(_lastInfo);
            UpdateAdvice();
        }

        PushWebDemand();
        PushWebEquipment();
    }

    private void MainTabs_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
    {
        if (!IsEquipmentTabActive)
            StopBindRecognitionHotKey(cancelled: true);
        ApplyHybridPageLayout();
        LayoutTopToolbar();
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        if (IsEquipmentTabActive && _lastInfo != null)
        {
            ShowDemandRecommendations(_lastInfo);
            UpdateAdvice();
        }
    }

    private void ApplyRecognitionAvailability(bool showHotKeySuccess)
    {
        continuousRecognitionTimer.Enabled = IsEquipmentTabActive
                                             && !IsAutomationRunning
                                             && chkContinuousRecognition.Checked;
        if (!IsEquipmentTabActive || IsAutomationRunning)
        {
            UnregisterRecognitionHotKey();
            return;
        }
        RegisterSelectedRecognitionHotKey(showHotKeySuccess);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        RequestGearScanShutdown();
        _autoEnhanceCancellation?.Cancel();
        _starForgeCancellation?.Cancel();
        _autoEnhanceSettingsFormAllowClose = true;
        _autoLogFormAllowClose = true;
        if (_autoEnhanceSettingsForm != null && !_autoEnhanceSettingsForm.IsDisposed)
            _autoEnhanceSettingsForm.Close();
        if (_autoLogForm != null && !_autoLogForm.IsDisposed)
            _autoLogForm.Close();
        base.OnFormClosing(e);
    }
}
