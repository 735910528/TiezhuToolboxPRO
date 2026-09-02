using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

public partial class MainForm
{
    private const string WebHostName = "app.tiezhu";
    private static readonly JsonSerializerOptions WebJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] SettingsRuleLines =
    [
        "红装赌速度：各档最低速度可在自动强化设置中自定义，默认 3/3/6/9/12，终局 15。",
        "紫装只赌速度：鞋子除外；开启后忽略分数与匹配度，按严格速度阶梯处理。",
        "速度套速度规则：鞋子必须为速度主属性，其他部位必须含速度副属性。",
        "暴击项链规则：暴击率或暴伤达到高权重时，项链只接受对应的主属性。",
        "套装子类：匹配当前套装下已启用的内置及手动属性组合，不使用旧角色算法回退。",
        "右三主属性：85级按90级满值预估，88/90使用同一满值档参与用途匹配。",
        "强化分数：始终只统计副属性；主属性不会加入分数阶梯或重铸分数。",
        "固定主属性：右三固定攻击、生命、防御不匹配任何需求子类。",
    ];

    private Panel _legacyHost = null!;
    private Panel _tabBar = null!;
    private readonly List<Label> _tabButtons = new();
    private Panel _contentHost = null!;
    private Panel _nativeHost = null!;
    private Panel _equipmentHost = null!;
    private Control _autoEnhanceContent = null!;
    private Control _gearScanContent = null!;
    private Control _starForgeContent = null!;
    private AntdUI.TabPage _demandTab = null!;
    private WebView2? _webView;
    private Label _webFallback = null!;
    private bool _screenshotWanted;
    private bool _webUiReady;
    private bool _webUiFailed;
    private bool _webUiSkipped;

    private static readonly string[] PageIds = ["equipment", "scan", "auto", "forge", "demand", "settings", "connect"];
    private static readonly string[] PageTitles =
        ["装备", "扫描", "自动", "铁匠铺", "需求", "设置", "连接"];

    private bool CanUseWebPage
        => _webUiReady && !_webUiFailed && !_webUiSkipped && _webView?.CoreWebView2 != null;

    private bool IsWebContentPage(int index) => index is 0 or 5;

    private void CreateHybridShell()
    {
        _equipmentHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(243, 238, 228) };
        _equipmentHost.Controls.Add(mainTable);
        mainTable.Dock = DockStyle.Fill;

        _tabBar = CreateTabBar();
        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(243, 238, 228),
        };
        _nativeHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(243, 238, 228),
        };
        _webFallback = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(120, 113, 108),
            Font = new Font("Microsoft YaHei UI", 11F),
            Text = "正在加载页面…",
            Visible = false,
        };
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(243, 238, 228),
            Visible = false,
            TabStop = false,
        };

        _screenshotWanted = false;
        CreateLivePreviewHost();

        _contentHost.Controls.Add(_nativeHost);
        _contentHost.Controls.Add(_webFallback);
        _contentHost.Controls.Add(_webView);
        _contentHost.Controls.Add(_livePreviewHost);

        _legacyHost = new Panel
        {
            Dock = DockStyle.Fill,
            TabStop = false,
        };
        _mainTabs.Dock = DockStyle.Fill;
        _mainTabs.TabStop = false;
        _legacyHost.Controls.Add(_mainTabs);

        BackColor = Color.FromArgb(243, 238, 228);
        topPanel.Visible = false;
        topPanel.Height = 0;
        _tabBar.Dock = DockStyle.Top;

        Controls.Add(_legacyHost);
        Controls.Add(_contentHost);
        Controls.Add(_tabBar);
        Controls.Add(topPanel);
        Controls.SetChildIndex(_contentHost, 0);
        Controls.SetChildIndex(_legacyHost, 1);
        Controls.SetChildIndex(_tabBar, 2);
        Controls.SetChildIndex(topPanel, 3);
        Controls.SetChildIndex(statusStrip, 4);
        _contentHost.BringToFront();
    }

    private Panel CreateTabBar()
    {
        var bar = new Panel
        {
            Height = ScalePixel(44),
            BackColor = Color.White,
            Padding = new Padding(ScalePixel(10), 0, ScalePixel(10), 0),
        };
        var border = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Color.FromArgb(232, 223, 210),
        };
        bar.Controls.Add(border);
        CreateLivePreviewTabButton(bar);
        var x = ScalePixel(8);
        for (var i = 0; i < PageTitles.Length; i++)
        {
            var index = i;
            var title = PageTitles[i];
            var button = new Label
            {
                Text = title,
                AutoSize = false,
                Location = new Point(x, 0),
                Size = new Size(ScalePixel(title.Length <= 2 ? 56 : 72), ScalePixel(43)),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = index,
            };
            button.Click += (_, _) =>
            {
                if (_mainTabs.SelectedIndex != index)
                    _mainTabs.SelectedIndex = index;
            };
            _tabButtons.Add(button);
            bar.Controls.Add(button);
            x += button.Width + ScalePixel(6);
        }

        bar.Paint += (_, e) =>
        {
            var selected = _tabButtons.ElementAtOrDefault(_mainTabs.SelectedIndex);
            if (selected == null)
                return;
            using var pen = new Pen(Color.FromArgb(180, 83, 9), 3);
            var y = bar.Height - 3;
            e.Graphics.DrawLine(pen, selected.Left + 12, y, selected.Right - 12, y);
        };
        return bar;
    }

    private void RefreshTabBar()
    {
        for (var i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].ForeColor = i == _mainTabs.SelectedIndex
                ? Color.FromArgb(28, 25, 23)
                : Color.FromArgb(120, 113, 108);
        }

        _tabBar.Invalidate();
    }

    private async Task InitializeWebUiAsync()
    {
        if (_webView == null || _webUiSkipped || IsDisposed)
            return;

        try
        {
            var assetsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            var userData = Path.Combine(AppPaths.UserRoot, "webview2");
            Directory.CreateDirectory(userData);
            var environment = await CoreWebView2Environment.CreateAsync(null, userData);
            if (IsDisposed || _webView.IsDisposed)
                return;
            await _webView.EnsureCoreWebView2Async(environment);
            if (IsDisposed || _webView.CoreWebView2 == null)
                return;

            var settings = _webView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                WebHostName,
                assetsRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                    return;
                _webUiReady = true;
                _webUiFailed = false;
                ApplyHybridPageLayout();
                PushWebInit();
            };
            _webView.CoreWebView2.Navigate($"https://{WebHostName}/WebUI/index.html");
        }
        catch (Exception ex)
        {
            _webUiFailed = true;
            _webUiReady = false;
            WriteDebugLog($"WebView2 初始化失败：{ex}");
            if (!IsDisposed)
            {
                UpdateStatus("页面引擎不可用，已回退 WinForms 界面。可安装 Microsoft Edge WebView2 运行时后重启。");
                ApplyHybridPageLayout();
            }
        }
    }

    private void ApplyHybridPageLayout()
    {
        if (_contentHost == null)
            return;

        var index = _mainTabs.SelectedIndex;
        var useWeb = CanUseWebPage && IsWebContentPage(index);
        RefreshTabBar();
        LayoutTopToolbar();

        if (useWeb)
        {
            _nativeHost.Visible = false;
            _webFallback.Visible = false;
            _webView!.Visible = true;
            _webView.BringToFront();
            ApplyLivePreviewVisibility();
            PostWebMessage(new { type = "page", page = PageIds[index] });
            if (index == 0)
                PushWebEquipment();
            else
                PushWebSettings();
            return;
        }

        if (_webView != null)
            _webView.Visible = false;

        var waitingForWeb = !_webUiSkipped && !_webUiFailed && !_webUiReady && IsWebContentPage(index);
        if (waitingForWeb)
        {
            _nativeHost.Visible = false;
            _webFallback.Text = "正在加载页面…";
            _webFallback.Visible = true;
            _webFallback.BringToFront();
            ApplyLivePreviewVisibility();
            return;
        }

        _webFallback.Visible = false;
        _nativeHost.Visible = true;
        _nativeHost.BringToFront();
        ApplyLivePreviewVisibility();
        ShowNativePage(index);
    }

    private void ShowNativePage(int index)
    {
        var content = NativeContentFor(index);
        foreach (Control child in _nativeHost.Controls.Cast<Control>().ToArray())
        {
            if (child != content)
                ReturnNativeToTab(child);
        }

        if (content.Parent != _nativeHost)
        {
            _nativeHost.Controls.Add(content);
            content.Dock = DockStyle.Fill;
        }

        content.Visible = true;
        content.BringToFront();
    }

    private Control NativeContentFor(int index) => index switch
    {
        0 => _equipmentHost,
        1 => _gearScanContent,
        2 => _autoEnhanceContent,
        3 => _starForgeContent,
        4 => _demandBrowserControl,
        5 => _settingsScrollHost,
        6 => _connectionContent,
        _ => _equipmentHost,
    };

    private void ReturnNativeToTab(Control content)
    {
        Control tab = content switch
        {
            _ when content == _equipmentHost => _equipmentTab,
            _ when content == _gearScanContent => _gearScanTab,
            _ when content == _autoEnhanceContent => _autoEnhanceTab,
            _ when content == _starForgeContent => _starForgeTab,
            _ when content == _demandBrowserControl => _demandTab,
            _ when content == _settingsScrollHost => _settingsTab,
            _ when content == _connectionContent => _connectionTab,
            _ => _equipmentTab,
        };
        if (content.Parent == tab)
            return;
        tab.Controls.Add(content);
        content.Dock = DockStyle.Fill;
    }

    private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;
            switch (type)
            {
                case "ready":
                    _webUiReady = true;
                    PushWebInit();
                    ApplyHybridPageLayout();
                    break;
                case "selectDemandSet":
                    break;
                case "toggleProfile":
                    HandleWebToggleProfile(root);
                    break;
                case "settings":
                    HandleWebSettings(root);
                    break;
                case "resetSettings":
                    ResetSettings();
                    break;
                case "bindHotKey":
                    ToggleBindRecognitionHotKey();
                    break;
                case "toggleScreenshot":
                    ToggleScreenshotPreview();
                    break;
                case "captureRecognize":
                    _ = CaptureAndRecognizeAsync();
                    break;
                case "openFolder":
                    btnOpenFolder_Click(this, EventArgs.Empty);
                    break;
                case "openAutoSettings":
                    ShowAutoEnhanceSettingsWindow();
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteDebugLog($"WebUI 消息处理失败：{ex}");
        }
    }

    private void HandleWebToggleProfile(JsonElement root)
    {
        var key = root.TryGetProperty("key", out var keyElement) ? keyElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(key))
            return;
        var enabled = root.TryGetProperty("enabled", out var enabledElement) && enabledElement.GetBoolean();
        SetDemandProfileEnabled(key, enabled);
        _demandBrowserControl.RefreshProfiles();
    }

    private void HandleWebSettings(JsonElement root)
    {
        decimal ReadDecimal(string name, decimal fallback)
            => root.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number)
                ? number
                : fallback;

        _isLoadingSettings = true;
        try
        {
            numLeftThreshold.Value = ReadDecimal("leftThreshold", numLeftThreshold.Value);
            numRightThreshold.Value = ReadDecimal("rightThreshold", numRightThreshold.Value);
            numLevel88Threshold.Value = ReadDecimal("level88Threshold", numLevel88Threshold.Value);
            if (root.TryGetProperty("continuousRecognition", out var continuous))
                chkContinuousRecognition.Checked = continuous.GetBoolean();
            var interval = ReadDecimal("recognitionIntervalSeconds", numRecognitionInterval.Value);
            numRecognitionInterval.Value = Math.Clamp(interval, 0.1M, 60M);
            continuousRecognitionTimer.Interval = Math.Max(100, (int)(numRecognitionInterval.Value * 1000));
        }
        finally
        {
            _isLoadingSettings = false;
        }

        SaveSettingsFromControls();
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        UpdateAdvice();
        PushWebEquipment();
    }

    private void PushWebInit()
    {
        var catalog = BuildDemandCatalog();
        PostWebMessage(new
        {
            type = "init",
            page = IsWebContentPage(_mainTabs.SelectedIndex) ? PageIds[_mainTabs.SelectedIndex] : "equipment",
            settings = BuildWebSettings(),
            catalog,
            selectedSet = catalog.Sets.FirstOrDefault()?.Code,
            equipment = BuildWebEquipment(),
        });
    }

    private void PushWebSettings() => PostWebMessage(BuildWebSettings());

    private void PushWebDemand()
        => PostWebMessage(new
        {
            type = "demand",
            catalog = BuildDemandCatalog(),
        });

    private void PushWebProfileEnabled(string key, bool enabled)
        => PostWebMessage(new { type = "profileEnabled", key, enabled });

    private void PushWebEquipment()
    {
        if (!CanUseWebPage)
            return;
        PostWebMessage(BuildWebEquipment());
    }

    private WebSettingsDto BuildWebSettings()
        => new(
            "settings",
            numLeftThreshold.Value,
            numRightThreshold.Value,
            numLevel88Threshold.Value,
            _recognitionHotKeyText,
            chkContinuousRecognition.Checked,
            numRecognitionInterval.Value,
            Enumerable.Range(1, 12).Select(i => $"F{i}").ToArray(),
            SettingsRuleLines,
            _isBindingHotKey);

    private DemandCatalogDto BuildDemandCatalog()
    {
        var database = DemandDatabase.Instance;
        return new DemandCatalogDto(
            database.IsLoaded,
            database.ErrorMessage,
            database.UpdatedAt,
            database.Sets.Select(set => new DemandSetDto(
                set.Code,
                set.Name,
                $"https://{WebHostName}/HeroData/sets/{set.Code}.png",
                set.Profiles
                    .OrderByDescending(profile => profile.DemandWeight)
                    .ThenBy(profile => profile.Name, StringComparer.CurrentCulture)
                    .Select(profile =>
                    {
                        var key = SetProfileMatcher.CreateProfileKey(set.Code, profile.Id);
                        return new DemandProfileDto(
                            profile.Id,
                            key,
                            profile.Name,
                            profile.Stats,
                            profile.Weights,
                            profile.DemandWeight,
                            !_disabledDemandProfiles.Contains(key),
                            profile.Heroes
                                .OrderByDescending(hero => hero.DemandContribution)
                                .ThenBy(hero => hero.Name, StringComparer.CurrentCulture)
                                .Select(hero => new DemandHeroDto(
                                    hero.Code,
                                    hero.Name,
                                    hero.ComboName,
                                    hero.SampleShare,
                                    hero.DemandContribution,
                                    $"https://{WebHostName}/HeroData/heroes/{hero.Code}.png"))
                                .ToList());
                    })
                    .ToList()))
                .ToList());
    }

    private WebEquipmentDto BuildWebEquipment()
    {
        if (_lastInfo == null)
        {
            return new WebEquipmentDto(
                "equipment",
                false,
                "—",
                "点击「截图识别」，或使用识别快捷键",
                "None",
                "等待识别",
                "识别后会在这里给出继续强化、赌速度、重铸或放弃建议。",
                "主属性：—",
                "套装：—",
                [],
                "套装需求",
                "识别装备后显示适用子类",
                [],
                _screenshotWanted);
        }

        var info = _lastInfo;
        var meta = $"等级 {info.Level} · 强化 +{info.EnhanceLevel}";
        if (!string.IsNullOrEmpty(info.Quality))
            meta += $" · {info.Quality}";
        var advice = EnhancementAdvisor.Analyze(
            info, (double)numLeftThreshold.Value, (double)numRightThreshold.Value,
            (double)numLevel88Threshold.Value, (double)_numHeroMatchThreshold.Value,
            _chkHeroicOnlyGambleSpeed.Checked,
            _chkSpeedSetRequiresSpeed.Checked,
            _chkCriticalNecklaceMainStatRule.Checked,
            _disabledDemandProfiles,
            ReadLegendarySpeedLadderFromControls());
        var database = DemandDatabase.Instance;
        var set = database.FindSet(info.SetName);
        var recommendations = set == null
            ? []
            : SetProfileMatcher.Match(info, disabledProfileKeys: _disabledDemandProfiles);
        var recsTitle = !database.IsLoaded
            ? $"套装需求（数据未加载：{database.ErrorMessage}）"
            : set == null
                ? "套装需求（套装未识别）"
                : set.Profiles.Count == 0
                    ? $"{set.Name}需求（暂无内置数据）"
                    : set.Profiles.All(profile => _disabledDemandProfiles.Contains(
                        SetProfileMatcher.CreateProfileKey(set.Code, profile.Id)))
                        ? $"{set.Name}需求（全部子类已停用）"
                        : recommendations.Count > 0
                            ? $"{set.Name}适用子类"
                            : $"{set.Name}需求（装备属性无匹配）";
        return new WebEquipmentDto(
            "equipment",
            true,
            info.Score.ToString("0.##"),
            meta,
            advice.Advice.ToString(),
            advice.Text,
            advice.Detail,
            $"主属性：{info.MainStatName} {info.MainStatValue}",
            $"套装：{info.SetName}",
            info.SubStats.Select(sub =>
            {
                var name = sub.RollCount > 0 ? $"{sub.Name}({sub.RollCount})" : sub.Name;
                var value = string.IsNullOrEmpty(sub.EnhanceValue) ? sub.Value : $"{sub.Value} ({sub.EnhanceValue})";
                return new WebSubStatDto(name, value);
            }).ToList(),
            recsTitle,
            recommendations.Count == 0 ? recsTitle : "",
            recommendations.Select(rec => new WebRecommendationDto(
                rec.ProfileName,
                rec.Score,
                rec.DemandWeight,
                rec.MatchedStats,
                rec.MainStatContribution,
                rec.Heroes.Select(hero => new WebHeroDto(
                    hero.Name,
                    hero.ComboName,
                    hero.Score,
                    hero.SampleShare,
                    hero.DemandContribution,
                    hero.MatchedStats,
                    string.IsNullOrWhiteSpace(hero.Code)
                        ? null
                        : $"https://{WebHostName}/HeroData/heroes/{hero.Code}.png")).ToList()))
                .ToList(),
            _screenshotWanted);
    }

    private void PostWebMessage(object payload)
    {
        if (_webView?.CoreWebView2 == null || _webUiFailed)
            return;
        try
        {
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, WebJson));
        }
        catch (Exception ex)
        {
            WriteDebugLog($"WebUI 推送失败：{ex}");
        }
    }

    private sealed record WebSettingsDto(
        string Type,
        decimal LeftThreshold,
        decimal RightThreshold,
        decimal Level88Threshold,
        string RecognitionHotKey,
        bool ContinuousRecognition,
        decimal RecognitionIntervalSeconds,
        string[] HotKeys,
        string[] Rules,
        bool HotKeyListening);

    private sealed record DemandCatalogDto(
        bool Loaded,
        string Error,
        string UpdatedAt,
        List<DemandSetDto> Sets);

    private sealed record DemandSetDto(
        string Code,
        string Name,
        string Icon,
        List<DemandProfileDto> Profiles);

    private sealed record DemandProfileDto(
        string Id,
        string Key,
        string Name,
        List<string> Stats,
        Dictionary<string, double> Weights,
        double DemandWeight,
        bool Enabled,
        List<DemandHeroDto> Heroes);

    private sealed record DemandHeroDto(
        string Code,
        string Name,
        string ComboName,
        double SampleShare,
        double DemandContribution,
        string Avatar);

    private sealed record WebEquipmentDto(
        string Type,
        bool HasResult,
        string Score,
        string Meta,
        string Advice,
        string AdviceText,
        string AdviceDetail,
        string MainStat,
        string SetName,
        List<WebSubStatDto> SubStats,
        string RecsTitle,
        string RecsEmpty,
        List<WebRecommendationDto> Recommendations,
        bool ScreenshotWanted);

    private sealed record WebSubStatDto(string Name, string Value);

    private sealed record WebRecommendationDto(
        string ProfileName,
        double Score,
        double DemandWeight,
        List<string> MatchedStats,
        string MainStatContribution,
        List<WebHeroDto> Heroes);

    private sealed record WebHeroDto(
        string Name,
        string ComboName,
        double Score,
        double SampleShare,
        double DemandContribution,
        List<string> MatchedStats,
        string? Avatar);
}
