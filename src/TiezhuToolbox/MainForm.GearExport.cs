using System.Diagnostics;
using TiezhuToolbox.Modules.GearExport;

namespace TiezhuToolbox;

public partial class MainForm
{
    private AntdUI.TabPage _gearExportTab = null!;
    private AntdUI.Button _btnGearScanStart = null!;
    private AntdUI.Button _btnGearScanStop = null!;
    private AntdUI.Button _btnGearExportFile = null!;
    private AntdUI.Button _btnBailiAnalyzeFile = null!;
    private AntdUI.Button _btnBailiCopyFileId = null!;
    private AntdUI.Button _btnBailiSaveImage = null!;
    private Label _lblGearExportState = null!;
    private Label _lblBailiFileId = null!;
    private RichTextBox _txtGearExportStatus = null!;
    private PictureBox _picBailiResult = null!;
    private GearPacketScanner? _gearScanner;
    private GearTxtDocument? _gearExportDocument;
    private BailiGearStatResult? _bailiLastResult;
    private bool _gearExportBusy;
    private bool _gearExportSetupPrompted;

    private Control CreateGearExportContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 246, 248),
            Padding = new Padding(16),
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16),
        };

        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = ScalePixel(96),
            BackColor = Color.White,
        };

        var title = new Label
        {
            Text = "战力分析",
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(8, 8),
            AutoSize = true,
        };

        _lblGearExportState = new Label
        {
            Text = "状态：未开始",
            ForeColor = TextDarkColor,
            Location = new Point(140, 14),
            Size = new Size(420, 24),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var btnHelp = new AntdUI.Button
        {
            Text = "使用说明",
            Location = new Point(570, 8),
            Size = new Size(100, 32),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnHelp.Click += (_, _) => ShowGearExportSetupDialog(force: true);

        _btnGearScanStart = new AntdUI.Button
        {
            Text = "开始扫描",
            Location = new Point(8, 48),
            Size = new Size(120, 36),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnGearScanStart.Click += async (_, _) => await StartGearScanAsync();

        _btnGearScanStop = new AntdUI.Button
        {
            Text = "停止并解包",
            Location = new Point(140, 48),
            Size = new Size(120, 36),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnGearScanStop.Click += async (_, _) => await StopGearScanAndUnpackAsync();

        _btnGearExportFile = new AntdUI.Button
        {
            Text = "导出 gear.txt",
            Location = new Point(272, 48),
            Size = new Size(132, 36),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnGearExportFile.Click += (_, _) => ExportGearTxtFile();

        _btnBailiAnalyzeFile = new AntdUI.Button
        {
            Text = "选文件分析",
            Location = new Point(416, 48),
            Size = new Size(120, 36),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnBailiAnalyzeFile.Click += async (_, _) => await AnalyzeWithBailiFromFileAsync();

        topBar.Controls.AddRange(new Control[]
        {
            title, _lblGearExportState, btnHelp,
            _btnGearScanStart, _btnGearScanStop, _btnGearExportFile, _btnBailiAnalyzeFile,
        });
        topBar.Resize += (_, _) =>
        {
            btnHelp.Left = Math.Max(ScalePixel(480), topBar.ClientSize.Width - btnHelp.Width - ScalePixel(8));
            _lblGearExportState.Width = Math.Max(
                ScalePixel(160),
                btnHelp.Left - _lblGearExportState.Left - ScalePixel(12));
        };

        _txtGearExportStatus = new RichTextBox
        {
            Dock = DockStyle.Bottom,
            Height = ScalePixel(110),
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 249, 250),
            ForeColor = TextDarkColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9.25F),
            DetectUrls = false,
        };

        var resultArea = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 8),
            BackColor = Color.White,
        };
        var resultHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = ScalePixel(40),
            BackColor = Color.FromArgb(248, 249, 250),
        };
        _lblBailiFileId = new Label
        {
            Text = "战力结果：尚未分析",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Font = new Font("Microsoft YaHei UI", 9.25F),
        };
        _btnBailiCopyFileId = new AntdUI.Button
        {
            Text = "复制 fileId",
            Dock = DockStyle.Right,
            Width = 110,
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnBailiCopyFileId.Click += (_, _) => CopyBailiFileId();
        _btnBailiSaveImage = new AntdUI.Button
        {
            Text = "保存图片",
            Dock = DockStyle.Right,
            Width = 100,
            Radius = 6,
            Enabled = false,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnBailiSaveImage.Click += (_, _) => SaveBailiResultImage();
        resultHeader.Controls.Add(_lblBailiFileId);
        resultHeader.Controls.Add(_btnBailiCopyFileId);
        resultHeader.Controls.Add(_btnBailiSaveImage);

        _picBailiResult = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(32, 33, 36),
            BorderStyle = BorderStyle.FixedSingle,
        };

        resultArea.Controls.Add(_picBailiResult);
        resultArea.Controls.Add(resultHeader);

        // Dock order: Fill first in collection, then Bottom/Top (WinForms applies reverse).
        card.Controls.Add(resultArea);
        card.Controls.Add(_txtGearExportStatus);
        card.Controls.Add(topBar);
        host.Controls.Add(card);

        AppendGearExportStatus("准备就绪。停止并解包成功后，会自动上传百里并在上方显示战力分析图。");
        host.HandleCreated += (_, _) =>
        {
            BeginInvoke(() => ShowGearExportSetupDialog(force: false));
        };

        return host;
    }

    private static bool IsNpcapInstalled()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.SystemDirectory, "Npcap", "wpcap.dll"),
            Path.Combine(Environment.SystemDirectory, "wpcap.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Npcap", "wpcap.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Npcap", "wpcap.dll"),
        };
        return candidates.Any(File.Exists);
    }

    private (bool pythonOk, bool npcapOk, string summary) GetGearExportEnvironmentStatus()
    {
        var pythonOk = GearPacketScanner.FindPythonCommand() != null;
        var npcapOk = IsNpcapInstalled();
        var parts = new List<string>();
        if (!pythonOk)
            parts.Add("未检测到 Python（请安装并勾选 Add to PATH）");
        if (!npcapOk)
            parts.Add("未检测到 Npcap");
        var summary = parts.Count == 0
            ? "Python 与 Npcap 已就绪。"
            : string.Join("；", parts) + "。";
        return (pythonOk, npcapOk, summary);
    }

    private void ShowGearExportSetupDialog(bool force)
    {
        var (pythonOk, npcapOk, summary) = GetGearExportEnvironmentStatus();
        if (!force && pythonOk && npcapOk)
            return;
        if (!force && _gearExportSetupPrompted)
            return;
        _gearExportSetupPrompted = true;

        var form = new Form
        {
            Text = "战力分析 · 使用说明",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(620, 420),
            MinimumSize = new Size(520, 360),
            ShowInTaskbar = false,
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Owner = this,
        };
        if (Icon != null)
            form.Icon = Icon;

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            BackColor = Color.White,
        };

        var envLabel = new Label
        {
            Text = "环境检测：" + summary,
            ForeColor = pythonOk && npcapOk ? AccentColor : AdviceGiveUpColor,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Location = new Point(4, 4),
            Size = new Size(560, 28),
            AutoEllipsis = true,
        };
        var warning = new Label
        {
            Text = "注意：解包依赖 Fribbels 第三方云接口；战力分析依赖百里 e7bot.top。对方变更或关闭后对应功能会失效。",
            ForeColor = AdviceGiveUpColor,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            Location = new Point(4, 40),
            Size = new Size(560, 42),
        };
        var hint = new Label
        {
            Text = "流程：安装 Python3 + Npcap → 关闭游戏后开始扫描 → 进大厅 → 停止并解包 → 自动上传百里并在本页显示战力图。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(4, 90),
            Size = new Size(560, 42),
        };
        var pathTip = new Label
        {
            Text = "Python 安装时务必勾选 “Add python.exe to PATH”，装完后建议重启本程序再扫描。",
            ForeColor = AccentColor,
            Font = new Font("Microsoft YaHei UI", 9.25F, FontStyle.Bold),
            Location = new Point(4, 140),
            Size = new Size(560, 42),
        };

        var btnPython = new AntdUI.Button
        {
            Text = "下载 Python",
            Location = new Point(4, 200),
            Size = new Size(120, 34),
            Radius = 6,
            Type = pythonOk ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        btnPython.Click += (_, _) => OpenGearExportDownloadUrl(
            "https://www.python.org/downloads/windows/",
            "Python");

        var btnNpcap = new AntdUI.Button
        {
            Text = "下载 Npcap",
            Location = new Point(136, 200),
            Size = new Size(120, 34),
            Radius = 6,
            Type = npcapOk ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        btnNpcap.Click += (_, _) => OpenGearExportDownloadUrl(
            "https://npcap.com/#download",
            "Npcap");

        var btnBaili = new AntdUI.Button
        {
            Text = "打开百里官网",
            Location = new Point(268, 200),
            Size = new Size(132, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        btnBaili.Click += (_, _) => OpenGearExportDownloadUrl(
            BailiGearStatClient.SiteUrl,
            "百里战力分析");

        var btnClose = new AntdUI.Button
        {
            Text = "知道了",
            Location = new Point(440, 268),
            Size = new Size(120, 36),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        btnClose.Click += (_, _) => form.Close();

        body.Controls.AddRange(new Control[]
        {
            envLabel, warning, hint, pathTip,
            btnPython, btnNpcap, btnBaili, btnClose,
        });
        form.Controls.Add(body);
        form.AcceptButton = null;
        form.CancelButton = null;
        form.ShowDialog(this);
    }

    private void OpenGearExportDownloadUrl(string url, string name)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AppendGearExportStatus($"已打开 {name} 官网下载页。");
            if (name == "Python")
            {
                AppendGearExportStatus(
                    "安装 Python 时请勾选 “Add python.exe to PATH”，否则程序找不到 python/py 命令。");
            }
        }
        catch (Exception ex)
        {
            AppendGearExportStatus($"打开 {name} 下载页失败：{ex.Message}");
            UpdateStatus($"无法打开浏览器：{ex.Message}");
        }
    }

    private void AppendGearExportStatus(string message)
    {
        if (_txtGearExportStatus == null || _txtGearExportStatus.IsDisposed)
            return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_txtGearExportStatus.TextLength == 0)
            _txtGearExportStatus.Text = line;
        else
            _txtGearExportStatus.AppendText(Environment.NewLine + line);
        _txtGearExportStatus.SelectionStart = _txtGearExportStatus.TextLength;
        _txtGearExportStatus.ScrollToCaret();
    }

    private void SetGearExportUiState(string state, bool scanning, bool canExport)
    {
        _lblGearExportState.Text = "状态：" + state;
        _btnGearScanStart.Enabled = !scanning && !_gearExportBusy;
        _btnGearScanStop.Enabled = scanning && !_gearExportBusy;
        _btnGearExportFile.Enabled = canExport && !scanning && !_gearExportBusy;
        _btnBailiAnalyzeFile.Enabled = !scanning && !_gearExportBusy;
        var hasResult = _bailiLastResult != null;
        _btnBailiCopyFileId.Enabled = hasResult && !scanning && !_gearExportBusy;
        _btnBailiSaveImage.Enabled = hasResult && !scanning && !_gearExportBusy;
    }

    private void ClearBailiResultOnPage()
    {
        _bailiLastResult = null;
        var old = _picBailiResult.Image;
        _picBailiResult.Image = null;
        old?.Dispose();
        _lblBailiFileId.Text = "战力结果：尚未分析";
        _btnBailiCopyFileId.Enabled = false;
        _btnBailiSaveImage.Enabled = false;
    }

    private void ShowBailiResultOnPage(BailiGearStatResult result)
    {
        Image? image;
        try
        {
            using var ms = new MemoryStream(result.ImageBytes, writable: false);
            using var temp = Image.FromStream(ms);
            image = new Bitmap(temp);
        }
        catch (Exception ex)
        {
            AppendGearExportStatus("分析图解析失败：" + ex.Message);
            return;
        }

        var old = _picBailiResult.Image;
        _picBailiResult.Image = image;
        old?.Dispose();
        _bailiLastResult = result;
        _lblBailiFileId.Text = "fileId：" + result.FileId;
        _btnBailiCopyFileId.Enabled = !_gearExportBusy;
        _btnBailiSaveImage.Enabled = !_gearExportBusy;
    }

    private void CopyBailiFileId()
    {
        if (_bailiLastResult == null)
            return;
        try
        {
            Clipboard.SetText(_bailiLastResult.FileId);
            AppendGearExportStatus("已复制 fileId 到剪贴板。");
            UpdateStatus("已复制百里 fileId");
        }
        catch (Exception ex)
        {
            AppendGearExportStatus("复制失败：" + ex.Message);
        }
    }

    private void SaveBailiResultImage()
    {
        if (_bailiLastResult == null)
            return;

        using var save = new SaveFileDialog
        {
            Title = "保存战力分析图",
            FileName = "baili-gear-stat.jpg",
            Filter = "图片 (*.jpg;*.png)|*.jpg;*.jpeg;*.png|所有文件 (*.*)|*.*",
            DefaultExt = "jpg",
            AddExtension = true,
        };
        if (save.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllBytes(save.FileName, _bailiLastResult.ImageBytes);
            AppendGearExportStatus("已保存分析图：" + save.FileName);
        }
        catch (Exception ex)
        {
            AppendGearExportStatus("保存失败：" + ex.Message);
        }
    }

    private async Task StartGearScanAsync()
    {
        if (_gearExportBusy || (_gearScanner?.IsRunning ?? false))
            return;

        var (pythonOk, npcapOk, _) = GetGearExportEnvironmentStatus();
        if (!pythonOk || !npcapOk)
        {
            ShowGearExportSetupDialog(force: true);
            AppendGearExportStatus("环境未就绪，请先安装缺失依赖后再开始扫描。");
            return;
        }

        try
        {
            _gearExportDocument = null;
            ClearBailiResultOnPage();
            _gearScanner?.Dispose();
            _gearScanner = new GearPacketScanner();
            _gearScanner.Start();
            SetGearExportUiState("扫描中", scanning: true, canExport: false);
            AppendGearExportStatus("已开始扫描。请打开第七史诗并进入大厅，然后点击「停止并解包」。");
            UpdateStatus("战力分析：扫描中");
        }
        catch (Exception ex)
        {
            _gearScanner?.Dispose();
            _gearScanner = null;
            SetGearExportUiState("启动失败", scanning: false, canExport: false);
            AppendGearExportStatus("启动失败：" + ex.Message);
            UpdateStatus("战力分析启动失败：" + ex.Message);
            ShowGearExportSetupDialog(force: true);
        }

        await Task.CompletedTask;
    }

    private async Task StopGearScanAndUnpackAsync()
    {
        if (_gearScanner == null || _gearExportBusy)
            return;

        _gearExportBusy = true;
        SetGearExportUiState("停止并解包中", scanning: false, canExport: false);
        _btnGearScanStart.Enabled = false;
        _btnGearScanStop.Enabled = false;
        AppendGearExportStatus("正在停止抓包并请求第三方解包，请稍候……");
        UpdateStatus("战力分析：解包中");

        try
        {
            var chunks = await _gearScanner.StopAndCollectAsync();
            AppendGearExportStatus($"抓包完成，共 {chunks.Count} 段数据，开始调用解包服务……");

            var client = new GearUnpackClient();
            using var json = await client.UnpackAsync(chunks);
            var result = GearItemConverter.ConvertDocument(json);
            _gearExportDocument = result.Document;

            AppendGearExportStatus(
                $"解包成功：原始 {result.RawItemCount} 件，可用 {result.ExportedItemCount} 件" +
                (result.LevelZeroCount > 0 ? $"（其中等级 0：{result.LevelZeroCount}）" : "") +
                "。");

            if (result.ExportedItemCount <= 0)
            {
                ClearBailiResultOnPage();
                SetGearExportUiState("解包无装备", scanning: false, canExport: false);
                UpdateStatus("战力分析：解包结果为空");
            }
            else
            {
                try
                {
                    AppendGearExportStatus("解包完成，正在自动上传百里战力分析……");
                    SetGearExportUiState("百里分析中", scanning: false, canExport: false);
                    UpdateStatus("战力分析：百里战力分析中");

                    var baili = new BailiGearStatClient();
                    var bailiResult = await baili.AnalyzeAsync(_gearExportDocument);
                    AppendGearExportStatus(
                        $"百里分析完成，fileId={bailiResult.FileId}（可复制到 QQ 百里机器人继续使用）。");
                    ShowBailiResultOnPage(bailiResult);
                    UpdateStatus("战力分析完成");
                    SetGearExportUiState(
                        $"已分析 {_gearExportDocument.Items.Count} 件",
                        scanning: false,
                        canExport: true);
                }
                catch (Exception bailiEx)
                {
                    AppendGearExportStatus("自动百里分析失败：" + bailiEx.Message + "（可点「选文件分析」或重新解包重试）");
                    UpdateStatus("百里战力分析失败：" + bailiEx.Message);
                    SetGearExportUiState(
                        $"已解包 {_gearExportDocument.Items.Count} 件",
                        scanning: false,
                        canExport: true);
                }
            }
        }
        catch (Exception ex)
        {
            _gearExportDocument = null;
            AppendGearExportStatus("失败：" + ex.Message);
            SetGearExportUiState("失败", scanning: false, canExport: false);
            UpdateStatus("战力分析失败：" + ex.Message);
        }
        finally
        {
            _gearScanner?.Dispose();
            _gearScanner = null;
            _gearExportBusy = false;
            var canExport = _gearExportDocument?.Items.Count > 0;
            if (canExport)
            {
                SetGearExportUiState(
                    _bailiLastResult != null
                        ? $"已分析 {_gearExportDocument!.Items.Count} 件"
                        : $"已解包 {_gearExportDocument!.Items.Count} 件",
                    scanning: false,
                    canExport: true);
            }
            else if (!_lblGearExportState.Text.Contains("失败", StringComparison.Ordinal))
            {
                SetGearExportUiState("未开始", scanning: false, canExport: false);
            }
            else
            {
                _btnGearScanStart.Enabled = true;
                _btnGearScanStop.Enabled = false;
                _btnGearExportFile.Enabled = false;
                _btnBailiAnalyzeFile.Enabled = true;
                _btnBailiCopyFileId.Enabled = _bailiLastResult != null;
                _btnBailiSaveImage.Enabled = _bailiLastResult != null;
            }
        }
    }

    private void ExportGearTxtFile()
    {
        if (_gearExportDocument == null || _gearExportDocument.Items.Count == 0)
        {
            UpdateStatus("没有可导出的装备数据");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 gear.txt",
            FileName = "gear.txt",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = "txt",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            GearTxtExporter.WriteToFile(dialog.FileName, _gearExportDocument);
            AppendGearExportStatus("已导出：" + dialog.FileName);
            UpdateStatus("已导出 gear.txt");
        }
        catch (Exception ex)
        {
            AppendGearExportStatus("导出失败：" + ex.Message);
            UpdateStatus("导出 gear.txt 失败：" + ex.Message);
        }
    }

    private async Task AnalyzeWithBailiFromFileAsync()
    {
        if (_gearExportBusy || (_gearScanner?.IsRunning ?? false))
            return;

        using var open = new OpenFileDialog
        {
            Title = "选择 gear.txt",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (open.ShowDialog(this) != DialogResult.OK)
            return;
        var filePath = open.FileName;

        _gearExportBusy = true;
        var canExport = _gearExportDocument?.Items.Count > 0;
        SetGearExportUiState("百里分析中", scanning: false, canExport: false);
        AppendGearExportStatus($"正在上传到百里分析：{filePath}");
        UpdateStatus("战力分析：百里战力分析中");

        try
        {
            var client = new BailiGearStatClient();
            var result = await client.AnalyzeFileAsync(filePath);
            AppendGearExportStatus($"百里分析完成，fileId={result.FileId}（可复制到 QQ 百里机器人继续使用）。");
            ShowBailiResultOnPage(result);
            UpdateStatus("百里战力分析完成");
        }
        catch (Exception ex)
        {
            AppendGearExportStatus("百里分析失败：" + ex.Message);
            UpdateStatus("百里战力分析失败：" + ex.Message);
        }
        finally
        {
            _gearExportBusy = false;
            SetGearExportUiState(
                canExport
                    ? (_bailiLastResult != null
                        ? $"已分析 {_gearExportDocument!.Items.Count} 件"
                        : $"已解包 {_gearExportDocument!.Items.Count} 件")
                    : (_bailiLastResult != null ? "已分析" : "未开始"),
                scanning: false,
                canExport: canExport);
        }
    }
}
