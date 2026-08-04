using System.Diagnostics;
using TiezhuToolbox.Modules.GearExport;

namespace TiezhuToolbox;

public partial class MainForm
{
    private AntdUI.TabPage _gearExportTab = null!;
    private AntdUI.Button _btnGearScanStart = null!;
    private AntdUI.Button _btnGearScanStop = null!;
    private AntdUI.Button _btnGearExportFile = null!;
    private AntdUI.Button _btnBailiAnalyze = null!;
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

    private Control CreateGearExportContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 246, 248),
            Padding = new Padding(24),
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(22),
        };

        var title = new Label
        {
            Text = "战力分析",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(22, 16),
            AutoSize = true,
        };
        var warning = new Label
        {
            Text = "注意：解包依赖 Fribbels 第三方云接口；战力分析依赖百里 e7bot.top。对方变更或关闭后对应功能会失效。",
            ForeColor = AdviceGiveUpColor,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Location = new Point(24, 58),
            Size = new Size(900, 28),
            AutoEllipsis = true,
        };
        var hint = new Label
        {
            Text = "流程：安装 Python3 + Npcap → 关闭游戏后开始扫描 → 进大厅 → 停止并解包 → 自动上传百里并在本页显示战力图。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(24, 90),
            Size = new Size(900, 28),
        };
        var pathTip = new Label
        {
            Text = "Python 安装提示：务必勾选 “Add python.exe to PATH”（将 Python 添加到环境变量），装完后建议重启本程序再扫描。",
            ForeColor = AccentColor,
            Font = new Font("Microsoft YaHei UI", 9.25F, FontStyle.Bold),
            Location = new Point(24, 120),
            Size = new Size(900, 28),
            AutoEllipsis = true,
        };

        var btnOpenPythonDownload = new AntdUI.Button
        {
            Text = "下载 Python",
            Location = new Point(24, 156),
            Size = new Size(120, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        btnOpenPythonDownload.Click += (_, _) => OpenGearExportDownloadUrl(
            "https://www.python.org/downloads/windows/",
            "Python");

        var btnOpenNpcapDownload = new AntdUI.Button
        {
            Text = "下载 Npcap",
            Location = new Point(156, 156),
            Size = new Size(120, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        btnOpenNpcapDownload.Click += (_, _) => OpenGearExportDownloadUrl(
            "https://npcap.com/#download",
            "Npcap");

        var btnOpenBailiSite = new AntdUI.Button
        {
            Text = "打开百里官网",
            Location = new Point(288, 156),
            Size = new Size(132, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        btnOpenBailiSite.Click += (_, _) => OpenGearExportDownloadUrl(
            BailiGearStatClient.SiteUrl,
            "百里战力分析");

        _lblGearExportState = new Label
        {
            Text = "状态：未开始",
            ForeColor = TextDarkColor,
            Location = new Point(24, 200),
            Size = new Size(720, 32),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _btnGearScanStart = new AntdUI.Button
        {
            Text = "开始扫描",
            Location = new Point(24, 240),
            Size = new Size(120, 36),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnGearScanStart.Click += async (_, _) => await StartGearScanAsync();

        _btnGearScanStop = new AntdUI.Button
        {
            Text = "停止并解包",
            Location = new Point(156, 240),
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
            Location = new Point(288, 240),
            Size = new Size(132, 36),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnGearExportFile.Click += (_, _) => ExportGearTxtFile();

        _btnBailiAnalyze = new AntdUI.Button
        {
            Text = "重新分析",
            Location = new Point(432, 240),
            Size = new Size(120, 36),
            Radius = 6,
            Enabled = false,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnBailiAnalyze.Click += async (_, _) => await AnalyzeWithBailiAsync(fromFile: false);

        _btnBailiAnalyzeFile = new AntdUI.Button
        {
            Text = "选文件分析",
            Location = new Point(564, 240),
            Size = new Size(120, 36),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnBailiAnalyzeFile.Click += async (_, _) => await AnalyzeWithBailiAsync(fromFile: true);

        _txtGearExportStatus = new RichTextBox
        {
            Location = new Point(24, 292),
            Size = new Size(360, 360),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 249, 250),
            ForeColor = TextDarkColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            DetectUrls = false,
        };
        AppendGearExportStatus(
            "准备就绪。停止并解包成功后，会自动上传百里并在右侧显示战力分析图。");

        var resultHeader = new Panel
        {
            Location = new Point(400, 292),
            Size = new Size(520, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
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
            Location = new Point(400, 338),
            Size = new Size(520, 314),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(32, 33, 36),
            BorderStyle = BorderStyle.FixedSingle,
        };

        card.Resize += (_, _) => LayoutGearExportContent(card, warning, hint, pathTip, resultHeader);

        card.Controls.AddRange(new Control[]
        {
            title, warning, hint, pathTip,
            btnOpenPythonDownload, btnOpenNpcapDownload, btnOpenBailiSite,
            _lblGearExportState,
            _btnGearScanStart, _btnGearScanStop, _btnGearExportFile,
            _btnBailiAnalyze, _btnBailiAnalyzeFile,
            _txtGearExportStatus, resultHeader, _picBailiResult,
        });
        host.Controls.Add(card);
        return host;
    }

    private void LayoutGearExportContent(
        Panel card,
        Label warning,
        Label hint,
        Label pathTip,
        Panel resultHeader)
    {
        var contentWidth = Math.Max(ScalePixel(300), card.ClientSize.Width - ScalePixel(48));
        warning.Width = contentWidth;
        hint.Width = contentWidth;
        pathTip.Width = contentWidth;
        _lblGearExportState.Width = contentWidth;

        var gap = ScalePixel(16);
        var leftWidth = Math.Max(ScalePixel(260), contentWidth * 38 / 100);
        var rightWidth = Math.Max(ScalePixel(220), contentWidth - leftWidth - gap);
        var bottomHeight = Math.Max(
            ScalePixel(180),
            card.ClientSize.Height - _txtGearExportStatus.Top - ScalePixel(24));

        _txtGearExportStatus.Width = leftWidth;
        _txtGearExportStatus.Height = bottomHeight;

        resultHeader.Location = new Point(_txtGearExportStatus.Left + leftWidth + gap, _txtGearExportStatus.Top);
        resultHeader.Width = rightWidth;
        resultHeader.Height = ScalePixel(40);

        _picBailiResult.Location = new Point(resultHeader.Left, resultHeader.Bottom + ScalePixel(6));
        _picBailiResult.Width = rightWidth;
        _picBailiResult.Height = Math.Max(ScalePixel(120), bottomHeight - resultHeader.Height - ScalePixel(6));
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
        if (_txtGearExportStatus.IsDisposed)
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
        _btnBailiAnalyze.Enabled = canExport && !scanning && !_gearExportBusy;
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
            // Clone so the stream can be disposed safely.
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
                    AppendGearExportStatus("自动百里分析失败：" + bailiEx.Message + "（可点「重新分析」重试）");
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
                _btnBailiAnalyze.Enabled = false;
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

    private async Task AnalyzeWithBailiAsync(bool fromFile)
    {
        if (_gearExportBusy || (_gearScanner?.IsRunning ?? false))
            return;

        string? filePath = null;
        if (fromFile)
        {
            using var open = new OpenFileDialog
            {
                Title = "选择 gear.txt",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                CheckFileExists = true,
            };
            if (open.ShowDialog(this) != DialogResult.OK)
                return;
            filePath = open.FileName;
        }
        else if (_gearExportDocument == null || _gearExportDocument.Items.Count == 0)
        {
            UpdateStatus("没有可分析的装备数据，请先解包或选择文件");
            return;
        }

        _gearExportBusy = true;
        var canExport = _gearExportDocument?.Items.Count > 0;
        SetGearExportUiState("百里分析中", scanning: false, canExport: false);
        AppendGearExportStatus(
            fromFile
                ? $"正在上传到百里分析：{filePath}"
                : "正在重新上传当前解包结果到百里战力分析……");
        UpdateStatus("战力分析：百里战力分析中");

        try
        {
            var client = new BailiGearStatClient();
            BailiGearStatResult result;
            if (fromFile)
                result = await client.AnalyzeFileAsync(filePath!);
            else
                result = await client.AnalyzeAsync(_gearExportDocument!);

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
                    : "未开始",
                scanning: false,
                canExport: canExport);
        }
    }
}
