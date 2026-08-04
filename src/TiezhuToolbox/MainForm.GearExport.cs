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
    private Label _lblGearExportState = null!;
    private RichTextBox _txtGearExportStatus = null!;
    private GearPacketScanner? _gearScanner;
    private GearTxtDocument? _gearExportDocument;
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
            Text = "装备导出",
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
            Text = "流程：安装 Python3 + Npcap → 关闭游戏后开始扫描 → 进大厅 → 停止并解包 → 导出 gear.txt / 百里战力分析。",
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
            Size = new Size(520, 32),
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
            Text = "百里战力分析",
            Location = new Point(432, 240),
            Size = new Size(140, 36),
            Radius = 6,
            Enabled = false,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnBailiAnalyze.Click += async (_, _) => await AnalyzeWithBailiAsync(fromFile: false);

        _btnBailiAnalyzeFile = new AntdUI.Button
        {
            Text = "选文件分析",
            Location = new Point(584, 240),
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
            Size = new Size(900, 320),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 249, 250),
            ForeColor = TextDarkColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            DetectUrls = false,
        };
        AppendGearExportStatus(
            "准备就绪。需本机 Python 与 Npcap（Scapy 已随程序附带）。解包后可导出或直接上传百里分析战力。");

        card.Resize += (_, _) =>
        {
            warning.Width = Math.Max(ScalePixel(300), card.ClientSize.Width - ScalePixel(48));
            hint.Width = warning.Width;
            pathTip.Width = warning.Width;
            _txtGearExportStatus.Width = Math.Max(ScalePixel(300), card.ClientSize.Width - ScalePixel(48));
            _txtGearExportStatus.Height = Math.Max(
                ScalePixel(160),
                card.ClientSize.Height - _txtGearExportStatus.Top - ScalePixel(24));
        };

        card.Controls.AddRange(new Control[]
        {
            title, warning, hint, pathTip,
            btnOpenPythonDownload, btnOpenNpcapDownload, btnOpenBailiSite,
            _lblGearExportState,
            _btnGearScanStart, _btnGearScanStop, _btnGearExportFile,
            _btnBailiAnalyze, _btnBailiAnalyzeFile, _txtGearExportStatus,
        });
        host.Controls.Add(card);
        return host;
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
    }

    private async Task StartGearScanAsync()
    {
        if (_gearExportBusy || (_gearScanner?.IsRunning ?? false))
            return;

        try
        {
            _gearExportDocument = null;
            _gearScanner?.Dispose();
            _gearScanner = new GearPacketScanner();
            _gearScanner.Start();
            SetGearExportUiState("扫描中", scanning: true, canExport: false);
            AppendGearExportStatus("已开始扫描。请打开第七史诗并进入大厅，然后点击「停止并解包」。");
            UpdateStatus("装备导出：扫描中");
        }
        catch (Exception ex)
        {
            _gearScanner?.Dispose();
            _gearScanner = null;
            SetGearExportUiState("启动失败", scanning: false, canExport: false);
            AppendGearExportStatus("启动失败：" + ex.Message);
            UpdateStatus("装备导出启动失败：" + ex.Message);
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
        UpdateStatus("装备导出：解包中");

        try
        {
            var chunks = await _gearScanner.StopAndCollectAsync();
            AppendGearExportStatus($"抓包完成，共 {chunks.Count} 段数据，开始调用解包服务……");

            var client = new GearUnpackClient();
            using var json = await client.UnpackAsync(chunks);
            var result = GearItemConverter.ConvertDocument(json);
            _gearExportDocument = result.Document;

            AppendGearExportStatus(
                $"解包成功：原始 {result.RawItemCount} 件，导出 {result.ExportedItemCount} 件" +
                (result.LevelZeroCount > 0 ? $"（其中等级 0：{result.LevelZeroCount}）" : "") +
                "。可点击「导出 gear.txt」。");
            SetGearExportUiState(
                $"已解包 {result.ExportedItemCount} 件",
                scanning: false,
                canExport: result.ExportedItemCount > 0);
            UpdateStatus($"装备导出完成：{result.ExportedItemCount} 件");
        }
        catch (Exception ex)
        {
            _gearExportDocument = null;
            AppendGearExportStatus("失败：" + ex.Message);
            SetGearExportUiState("失败", scanning: false, canExport: false);
            UpdateStatus("装备导出失败：" + ex.Message);
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
                    $"已解包 {_gearExportDocument!.Items.Count} 件",
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
                : "正在上传当前解包结果到百里战力分析……");
        UpdateStatus("装备导出：百里战力分析中");

        try
        {
            var client = new BailiGearStatClient();
            BailiGearStatResult result;
            if (fromFile)
                result = await client.AnalyzeFileAsync(filePath!);
            else
                result = await client.AnalyzeAsync(_gearExportDocument!);

            AppendGearExportStatus($"百里分析完成，fileId={result.FileId}（可复制到 QQ 百里机器人继续使用）。");
            UpdateStatus("百里战力分析完成");
            ShowBailiResultDialog(result);
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
                canExport ? $"已解包 {_gearExportDocument!.Items.Count} 件" : "未开始",
                scanning: false,
                canExport: canExport);
        }
    }

    private void ShowBailiResultDialog(BailiGearStatResult result)
    {
        Image? image = null;
        try
        {
            using var ms = new MemoryStream(result.ImageBytes, writable: false);
            image = Image.FromStream(ms);
        }
        catch (Exception ex)
        {
            AppendGearExportStatus("分析图解析失败：" + ex.Message);
            return;
        }

        var form = new Form
        {
            Text = "百里战力分析",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(960, 720),
            MinimumSize = new Size(640, 480),
            ShowInTaskbar = false,
            MinimizeBox = true,
            MaximizeBox = true,
            Owner = this,
        };
        if (Icon != null)
            form.Icon = Icon;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.FromArgb(248, 249, 250),
        };
        var lblId = new Label
        {
            Text = "fileId：" + result.FileId,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 9.5F),
        };
        var btnCopy = new AntdUI.Button
        {
            Text = "复制 fileId",
            Dock = DockStyle.Right,
            Width = 110,
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
            Margin = new Padding(8, 0, 0, 0),
        };
        btnCopy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(result.FileId);
                AppendGearExportStatus("已复制 fileId 到剪贴板。");
                UpdateStatus("已复制百里 fileId");
            }
            catch (Exception ex)
            {
                AppendGearExportStatus("复制失败：" + ex.Message);
            }
        };
        var btnSave = new AntdUI.Button
        {
            Text = "保存图片",
            Dock = DockStyle.Right,
            Width = 100,
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
            Margin = new Padding(8, 0, 0, 0),
        };
        btnSave.Click += (_, _) =>
        {
            using var save = new SaveFileDialog
            {
                Title = "保存战力分析图",
                FileName = "baili-gear-stat.jpg",
                Filter = "图片 (*.jpg;*.png)|*.jpg;*.jpeg;*.png|所有文件 (*.*)|*.*",
                DefaultExt = "jpg",
                AddExtension = true,
            };
            if (save.ShowDialog(form) != DialogResult.OK)
                return;
            try
            {
                File.WriteAllBytes(save.FileName, result.ImageBytes);
                AppendGearExportStatus("已保存分析图：" + save.FileName);
            }
            catch (Exception ex)
            {
                AppendGearExportStatus("保存失败：" + ex.Message);
            }
        };

        header.Controls.Add(lblId);
        header.Controls.Add(btnCopy);
        header.Controls.Add(btnSave);

        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(32, 33, 36),
            Image = image,
        };

        form.FormClosed += (_, _) =>
        {
            picture.Image = null;
            image.Dispose();
            form.Dispose();
        };

        form.Controls.Add(picture);
        form.Controls.Add(header);
        form.Show(this);
    }
}
