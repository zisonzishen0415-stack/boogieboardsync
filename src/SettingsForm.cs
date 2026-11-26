using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class SettingsForm : Form
    {
        private readonly TrayContext owner;
        private bool loading;
        private Label statusLabel;
        private Label detailLabel;
        private Label footerLabel;
        private ComboBox connectionCombo;
        private RadioButton penRadio;
        private RadioButton mouseRadio;
        private CheckBox pausedCheck;
        private ComboBox rotationCombo;
        private ComboBox screenCombo;
        private ComboBox mapFitCombo;
        private NumericUpDown mapXNumeric;
        private NumericUpDown mapYNumeric;
        private NumericUpDown mapWNumeric;
        private NumericUpDown mapHNumeric;
        private NumericUpDown rawMinXNumeric;
        private NumericUpDown rawMaxXNumeric;
        private NumericUpDown rawMinYNumeric;
        private NumericUpDown rawMaxYNumeric;
        private ComboBox curveCombo;
        private NumericUpDown minNumeric;
        private NumericUpDown maxNumeric;
        private NumericUpDown gammaNumeric;
        private NumericUpDown deadzoneNumeric;
        private NumericUpDown smoothingNumeric;
        private CheckBox autoStartCheck;
        private PressureMonitor pressureMonitor;
        private PressureCurve pressureCurve;
        private MappingPreview mappingPreview;
        private Timer refreshTimer;
        private TabControl tabs;

        public SettingsForm(TrayContext owner)
        {
            this.owner = owner;
            Text = AppInfo.DisplayName + " 设置";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(740, 700);
            MinimumSize = new Size(700, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Icon = SystemIcons.Application;

            BuildContent();
            LoadFromRuntime();

            refreshTimer = new Timer();
            refreshTimer.Interval = 100;
            refreshTimer.Tick += delegate(object sender, EventArgs e)
            {
                UpdateStatus();
                if (pressureMonitor != null)
                {
                    pressureMonitor.UpdateFromRuntime(owner.Runtime);
                }
                if (mappingPreview != null)
                {
                    mappingPreview.UpdateFromRuntime();
                }
                if (pressureCurve != null)
                {
                    pressureCurve.UpdateFromRuntime();
                }
            };
            refreshTimer.Start();
            Activated += delegate(object sender, EventArgs e) { LoadFromRuntime(); };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        internal void SelectTab(int index)
        {
            if (tabs == null || index < 0 || index >= tabs.TabPages.Count)
            {
                return;
            }
            tabs.SelectedIndex = index;
        }

        private void BuildContent()
        {
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 6);

            FlowLayoutPanel inputPage = MakePage(tabs, "输入");
            FlowLayoutPanel mappingPage = MakePage(tabs, "映射");
            FlowLayoutPanel pressurePage = MakePage(tabs, "力度");
            FlowLayoutPanel diagnosticsPage = MakePage(tabs, "诊断");

            BuildInputTab(inputPage);
            BuildMappingTab(mappingPage);
            BuildPressureTab(pressurePage);
            BuildDiagnosticsTab(diagnosticsPage);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 46;
            footer.BackColor = Color.FromArgb(242, 244, 247);

            footerLabel = new Label();
            footerLabel.Dock = DockStyle.Left;
            footerLabel.Width = 470;
            footerLabel.TextAlign = ContentAlignment.MiddleLeft;
            footerLabel.Padding = new Padding(18, 0, 0, 0);
            footerLabel.ForeColor = Color.DimGray;
            footer.Controls.Add(footerLabel);

            FlowLayoutPanel footerButtons = new FlowLayoutPanel();
            footerButtons.Dock = DockStyle.Right;
            footerButtons.AutoSize = true;
            footerButtons.FlowDirection = FlowDirection.RightToLeft;
            footerButtons.Padding = new Padding(0, 8, 14, 0);
            Button closeButton = MakeButton("关闭设置窗口");
            closeButton.Click += delegate(object sender, EventArgs e) { Hide(); };
            footerButtons.Controls.Add(closeButton);
            footer.Controls.Add(footerButtons);

            Controls.Add(tabs);
            Controls.Add(footer);
        }

        private static FlowLayoutPanel MakePage(TabControl tabs, string title)
        {
            TabPage page = new TabPage(title);
            page.BackColor = Color.FromArgb(247, 248, 250);
            page.Padding = new Padding(0);
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.TopDown;
            panel.WrapContents = false;
            panel.AutoScroll = true;
            panel.Padding = new Padding(16, 14, 16, 14);
            panel.BackColor = Color.FromArgb(247, 248, 250);
            page.Controls.Add(panel);
            tabs.TabPages.Add(page);
            return panel;
        }

        private void BuildInputTab(FlowLayoutPanel root)
        {
            SectionPanel inputGroup = MakeSection("输入状态");
            TableLayoutPanel inputTable = MakeTable();

            connectionCombo = new ComboBox();
            connectionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            connectionCombo.Items.Add("自动（USB 优先）");
            connectionCombo.Items.Add("仅 USB");
            connectionCombo.Items.Add("仅蓝牙");
            connectionCombo.SelectedIndex = 0;
            connectionCombo.SelectedIndexChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            AddRow(inputTable, "连接方式", connectionCombo);

            FlowLayoutPanel modeFlow = new FlowLayoutPanel();
            modeFlow.AutoSize = true;
            modeFlow.WrapContents = false;
            modeFlow.Margin = new Padding(0);
            penRadio = new RadioButton();
            penRadio.Text = "数位板（Windows Ink 压感）";
            penRadio.AutoSize = true;
            penRadio.Margin = new Padding(0, 2, 18, 2);
            mouseRadio = new RadioButton();
            mouseRadio.Text = "鼠标（侧键右键）";
            mouseRadio.AutoSize = true;
            mouseRadio.Margin = new Padding(0, 2, 0, 2);
            penRadio.CheckedChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            mouseRadio.CheckedChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            modeFlow.Controls.Add(penRadio);
            modeFlow.Controls.Add(mouseRadio);
            AddRow(inputTable, "输入方式", modeFlow);

            pausedCheck = new CheckBox();
            pausedCheck.Text = "暂停输入（驱动继续运行，但不注入笔迹）";
            pausedCheck.AutoSize = true;
            pausedCheck.CheckedChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            AddWideRow(inputTable, pausedCheck);

            statusLabel = new Label();
            statusLabel.AutoSize = true;
            statusLabel.Font = new Font(Font, FontStyle.Bold);
            AddRow(inputTable, "当前状态", statusLabel);

            detailLabel = new Label();
            detailLabel.AutoSize = true;
            detailLabel.ForeColor = Color.DimGray;
            AddWideRow(inputTable, detailLabel);

            Label liveHint = new Label();
            liveHint.Text = "所有修改会立即生效，不需要保存按钮。设置自动写入 settings.ini。";
            liveHint.AutoSize = true;
            liveHint.ForeColor = Color.DimGray;
            AddWideRow(inputTable, liveHint);

            Label bluetoothHint = new Label();
            bluetoothHint.Text = "蓝牙需要先在 Windows 设置中配对；程序会自动识别名称含 Boogie / Improv / Sync 的设备。";
            bluetoothHint.AutoSize = true;
            bluetoothHint.ForeColor = Color.DimGray;
            AddWideRow(inputTable, bluetoothHint);

            Label hotkeyHint = new Label();
            hotkeyHint.Text = "全局热键：Ctrl+Alt+S 设置 / P 暂停 / M 数位板↔鼠标 / R 旋转";
            hotkeyHint.AutoSize = true;
            hotkeyHint.ForeColor = Color.DimGray;
            AddWideRow(inputTable, hotkeyHint);

            AddSectionContent(inputGroup, inputTable);
            root.Controls.Add(inputGroup);
        }

        private void BuildMappingTab(FlowLayoutPanel root)
        {
            SectionPanel displayGroup = MakeSection("屏幕映射");
            TableLayoutPanel displayTable = MakeTable();

            screenCombo = new ComboBox();
            screenCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            screenCombo.SelectedIndexChanged += delegate(object sender, EventArgs e) { ScreenComboChanged(); };
            AddRow(displayTable, "映射屏幕", screenCombo);

            mappingPreview = new MappingPreview(owner.Runtime);
            mappingPreview.Width = 620;
            mappingPreview.Margin = new Padding(0, 6, 0, 8);
            AddWideRow(displayTable, mappingPreview);

            rotationCombo = new ComboBox();
            rotationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            rotationCombo.Items.Add("0°");
            rotationCombo.Items.Add("90°");
            rotationCombo.Items.Add("180°");
            rotationCombo.Items.Add("270°");
            rotationCombo.SelectedIndex = 0;
            rotationCombo.SelectedIndexChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            AddRow(displayTable, "旋转", rotationCombo);

            mapFitCombo = new ComboBox();
            mapFitCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            mapFitCombo.Items.Add("拉伸铺满");
            mapFitCombo.Items.Add("等比适应（保持平板纵横比）");
            mapFitCombo.Items.Add("等比填充（裁剪到屏幕比例）");
            mapFitCombo.SelectedIndex = 0;
            mapFitCombo.SelectedIndexChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            AddRow(displayTable, "映射比例", mapFitCombo);

            mapXNumeric = MakeIntegerNumeric(0, 65535, 10);
            mapYNumeric = MakeIntegerNumeric(0, 65535, 10);
            mapWNumeric = MakeIntegerNumeric(0, 65535, 10);
            mapHNumeric = MakeIntegerNumeric(0, 65535, 10);
            mapXNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            mapYNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            mapWNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            mapHNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            mapXNumeric.Width = 90;
            mapYNumeric.Width = 90;
            mapWNumeric.Width = 90;
            mapHNumeric.Width = 90;

            FlowLayoutPanel mapPosFlow = new FlowLayoutPanel();
            mapPosFlow.AutoSize = true;
            mapPosFlow.WrapContents = false;
            mapPosFlow.Margin = new Padding(0);
            mapPosFlow.Controls.Add(MakeInlineLabel("X", new Padding(0, 8, 4, 0)));
            mapPosFlow.Controls.Add(mapXNumeric);
            mapPosFlow.Controls.Add(MakeInlineLabel("Y", new Padding(12, 8, 4, 0)));
            mapPosFlow.Controls.Add(mapYNumeric);
            AddRow(displayTable, "区域左上", mapPosFlow);

            FlowLayoutPanel mapSizeFlow = new FlowLayoutPanel();
            mapSizeFlow.AutoSize = true;
            mapSizeFlow.WrapContents = false;
            mapSizeFlow.Margin = new Padding(0);
            mapSizeFlow.Controls.Add(MakeInlineLabel("宽", new Padding(0, 8, 4, 0)));
            mapSizeFlow.Controls.Add(mapWNumeric);
            mapSizeFlow.Controls.Add(MakeInlineLabel("高", new Padding(12, 8, 4, 0)));
            mapSizeFlow.Controls.Add(mapHNumeric);
            AddRow(displayTable, "区域大小", mapSizeFlow);

            Label mapHint = new Label();
            mapHint.Text = "区域坐标相对所选屏幕左上角；宽/高填 0 表示铺满整块屏幕。拉伸会变形；等比适应会留边；等比填充会裁掉平板超出的部分，屏幕填满且不变形。";
            mapHint.AutoSize = true;
            mapHint.MaximumSize = new Size(620, 0);
            mapHint.ForeColor = Color.DimGray;
            mapHint.Margin = new Padding(0, 6, 0, 2);
            AddWideRow(displayTable, mapHint);

            FlowLayoutPanel mapButtons = new FlowLayoutPanel();
            mapButtons.AutoSize = true;
            mapButtons.WrapContents = false;
            mapButtons.Margin = new Padding(0, 6, 0, 0);
            Button pickButton = MakeButton("框选屏幕区域…");
            pickButton.Click += delegate(object sender, EventArgs e) { PickScreenRegion(); };
            Button fillScreenButton = MakeButton("铺满当前屏幕");
            fillScreenButton.Click += delegate(object sender, EventArgs e) { ResetMapRegion(); };
            Button previewButton = MakeButton("预览区域（3 秒）");
            previewButton.Click += delegate(object sender, EventArgs e) { PreviewMappingRegion(); };
            mapButtons.Controls.Add(pickButton);
            mapButtons.Controls.Add(fillScreenButton);
            mapButtons.Controls.Add(previewButton);
            AddWideRow(displayTable, mapButtons);

            AddSectionContent(displayGroup, displayTable);
            root.Controls.Add(displayGroup);

            SectionPanel tabletGroup = MakeSection("平板范围");
            TableLayoutPanel tabletTable = MakeTable();

            rawMinXNumeric = MakeIntegerNumeric(0, 65535, 10);
            rawMaxXNumeric = MakeIntegerNumeric(1, 65535, 10);
            rawMinYNumeric = MakeIntegerNumeric(0, 65535, 10);
            rawMaxYNumeric = MakeIntegerNumeric(1, 65535, 10);
            rawMinXNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            rawMaxXNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            rawMinYNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            rawMaxYNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            rawMinXNumeric.Width = 90;
            rawMaxXNumeric.Width = 90;
            rawMinYNumeric.Width = 90;
            rawMaxYNumeric.Width = 90;

            FlowLayoutPanel rawXFlow = new FlowLayoutPanel();
            rawXFlow.AutoSize = true;
            rawXFlow.WrapContents = false;
            rawXFlow.Margin = new Padding(0);
            rawXFlow.Controls.Add(rawMinXNumeric);
            rawXFlow.Controls.Add(MakeDashLabel());
            rawXFlow.Controls.Add(rawMaxXNumeric);
            AddRow(tabletTable, "平板 X 范围", rawXFlow);

            FlowLayoutPanel rawYFlow = new FlowLayoutPanel();
            rawYFlow.AutoSize = true;
            rawYFlow.WrapContents = false;
            rawYFlow.Margin = new Padding(0);
            rawYFlow.Controls.Add(rawMinYNumeric);
            rawYFlow.Controls.Add(MakeDashLabel());
            rawYFlow.Controls.Add(rawMaxYNumeric);
            AddRow(tabletTable, "平板 Y 范围", rawYFlow);

            Label tabletHint = new Label();
            tabletHint.Text = "把平板当作一个矩形区域使用：点“捕获平板范围”后在板上沿边缘画一圈，自动填入实际坐标范围。";
            tabletHint.AutoSize = true;
            tabletHint.MaximumSize = new Size(620, 0);
            tabletHint.ForeColor = Color.DimGray;
            tabletHint.Margin = new Padding(0, 6, 0, 2);
            AddWideRow(tabletTable, tabletHint);

            FlowLayoutPanel tabletButtons = new FlowLayoutPanel();
            tabletButtons.AutoSize = true;
            tabletButtons.WrapContents = false;
            tabletButtons.Margin = new Padding(0, 6, 0, 0);
            Button captureButton = MakeButton("捕获平板范围…");
            captureButton.Click += delegate(object sender, EventArgs e) { CaptureTabletRange(); };
            Button resetTabletButton = MakeButton("重置为整块平板");
            resetTabletButton.Click += delegate(object sender, EventArgs e) { ResetTabletRange(); };
            tabletButtons.Controls.Add(captureButton);
            tabletButtons.Controls.Add(resetTabletButton);
            AddWideRow(tabletTable, tabletButtons);

            AddSectionContent(tabletGroup, tabletTable);
            root.Controls.Add(tabletGroup);
        }

        private void BuildPressureTab(FlowLayoutPanel root)
        {
            SectionPanel pressureGroup = MakeSection("力度映射");
            TableLayoutPanel pressureTable = MakeTable();

            pressureCurve = new PressureCurve(owner.Runtime);
            pressureCurve.Width = 620;
            pressureCurve.Margin = new Padding(0, 4, 0, 8);
            AddWideRow(pressureTable, pressureCurve);

            curveCombo = new ComboBox();
            curveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            curveCombo.Items.Add("柔和（gamma 0.70）");
            curveCombo.Items.Add("线性（gamma 1.00）");
            curveCombo.Items.Add("硬朗（gamma 1.40）");
            curveCombo.Items.Add("自定义");
            curveCombo.SelectedIndexChanged += delegate(object sender, EventArgs e)
            {
                if (loading || curveCombo.SelectedIndex < 0 || curveCombo.SelectedIndex > 2)
                {
                    return;
                }
                double gamma = 1.0;
                if (curveCombo.SelectedIndex == 0) gamma = 0.70;
                if (curveCombo.SelectedIndex == 2) gamma = 1.40;
                loading = true;
                gammaNumeric.Value = ClampDecimal((decimal)gamma, gammaNumeric.Minimum, gammaNumeric.Maximum);
                loading = false;
                ApplyFromControls();
            };
            AddRow(pressureTable, "曲线预设", curveCombo);

            minNumeric = MakeIntegerNumeric(0, 65535, 10);
            maxNumeric = MakeIntegerNumeric(1, 65535, 10);
            gammaNumeric = MakeDecimalNumeric(0.10M, 4.00M, 0.05M);
            deadzoneNumeric = MakeDecimalNumeric(0.00M, 0.95M, 0.01M);
            smoothingNumeric = MakeDecimalNumeric(0.00M, 0.95M, 0.01M);
            minNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            maxNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            gammaNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            deadzoneNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            smoothingNumeric.ValueChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            minNumeric.Width = 100;
            maxNumeric.Width = 100;
            FlowLayoutPanel rangeFlow = new FlowLayoutPanel();
            rangeFlow.AutoSize = true;
            rangeFlow.WrapContents = false;
            rangeFlow.Margin = new Padding(0);
            rangeFlow.Controls.Add(minNumeric);
            rangeFlow.Controls.Add(MakeDashLabel());
            rangeFlow.Controls.Add(maxNumeric);
            AddRow(pressureTable, "原始范围", rangeFlow);
            AddRow(pressureTable, "Gamma 曲线", gammaNumeric);

            deadzoneNumeric.Width = 82;
            smoothingNumeric.Width = 82;
            FlowLayoutPanel extraFlow = new FlowLayoutPanel();
            extraFlow.AutoSize = true;
            extraFlow.WrapContents = false;
            extraFlow.Margin = new Padding(0);
            extraFlow.Controls.Add(MakeInlineLabel("死区", new Padding(0, 8, 6, 0)));
            extraFlow.Controls.Add(deadzoneNumeric);
            extraFlow.Controls.Add(MakeInlineLabel("平滑", new Padding(12, 8, 6, 0)));
            extraFlow.Controls.Add(smoothingNumeric);
            AddRow(pressureTable, "微调", extraFlow);

            pressureMonitor = new PressureMonitor();
            pressureMonitor.Width = 620;
            pressureMonitor.Margin = new Padding(0, 8, 0, 4);
            AddWideRow(pressureTable, pressureMonitor);

            Label hint = new Label();
            hint.Text = "柔和曲线更省力；硬朗曲线需要更大力度。平滑默认关闭，不增加输入延迟。";
            hint.AutoSize = true;
            hint.ForeColor = Color.DimGray;
            hint.Margin = new Padding(0, 8, 0, 2);
            AddWideRow(pressureTable, hint);

            FlowLayoutPanel pressureButtons = new FlowLayoutPanel();
            pressureButtons.AutoSize = true;
            pressureButtons.WrapContents = false;
            pressureButtons.Margin = new Padding(0, 8, 0, 0);
            Button calibrateButton = MakeButton("力度校准…");
            calibrateButton.Click += delegate(object sender, EventArgs e) { CalibratePressure(); };
            Button resetButton = MakeButton("恢复默认力度");
            resetButton.Click += delegate(object sender, EventArgs e) { ResetPressure(); };
            pressureButtons.Controls.Add(calibrateButton);
            pressureButtons.Controls.Add(resetButton);
            AddWideRow(pressureTable, pressureButtons);

            AddSectionContent(pressureGroup, pressureTable);
            root.Controls.Add(pressureGroup);
        }

        private void BuildDiagnosticsTab(FlowLayoutPanel root)
        {
            SectionPanel startupGroup = MakeSection("启动与配置");
            TableLayoutPanel startupTable = MakeTable();
            autoStartCheck = new CheckBox();
            autoStartCheck.Text = "登录 Windows 后自动启动托盘程序";
            autoStartCheck.AutoSize = true;
            autoStartCheck.CheckedChanged += delegate(object sender, EventArgs e)
            {
                if (loading)
                {
                    return;
                }
                owner.SetAutoStartEnabled(autoStartCheck.Checked);
            };
            AddWideRow(startupTable, autoStartCheck);
            AddSectionContent(startupGroup, startupTable);
            root.Controls.Add(startupGroup);

            SectionPanel recoveryGroup = MakeSection("诊断与恢复");
            FlowLayoutPanel recoveryFlow = new FlowLayoutPanel();
            recoveryFlow.AutoSize = true;
            recoveryFlow.WrapContents = true;
            recoveryFlow.MaximumSize = new Size(620, 0);
            recoveryFlow.Margin = new Padding(0);

            Button reconnectButton = MakeButton("重新连接");
            reconnectButton.Click += delegate(object sender, EventArgs e)
            {
                AppLog.Write("manual reconnect requested from settings window");
                Program.RequestTransportRefresh();
            };
            Button diagnosticsButton = MakeButton("诊断信息…");
            diagnosticsButton.Click += delegate(object sender, EventArgs e) { owner.ShowDiagnostics(); };
            Button logButton = MakeButton("打开日志");
            logButton.Click += delegate(object sender, EventArgs e) { owner.OpenLogFile(); };
            Button folderButton = MakeButton("打开设置文件夹");
            folderButton.Click += delegate(object sender, EventArgs e) { owner.OpenSettingsFolder(); };
            Button bluetoothButton = MakeButton("打开蓝牙设置");
            bluetoothButton.Click += delegate(object sender, EventArgs e)
            {
                try
                {
                    System.Diagnostics.Process.Start("ms-settings:bluetooth");
                }
                catch (Exception)
                {
                }
            };

            recoveryFlow.Controls.Add(reconnectButton);
            recoveryFlow.Controls.Add(diagnosticsButton);
            recoveryFlow.Controls.Add(logButton);
            recoveryFlow.Controls.Add(folderButton);
            recoveryFlow.Controls.Add(bluetoothButton);
            AddSectionContent(recoveryGroup, recoveryFlow);
            root.Controls.Add(recoveryGroup);

            Label versionHint = new Label();
            versionHint.Text = AppInfo.DisplayName + " · USB HID + 蓝牙（HID / RFCOMM） · 日志自动轮转到 1 MB";
            versionHint.AutoSize = true;
            versionHint.ForeColor = Color.DimGray;
            versionHint.Margin = new Padding(4, 8, 0, 2);
            root.Controls.Add(versionHint);

            Label hotkeyHint = new Label();
            hotkeyHint.Text = "全局热键：Ctrl+Alt+S 设置 / P 暂停 / M 模式 / R 旋转";
            hotkeyHint.AutoSize = true;
            hotkeyHint.ForeColor = Color.DimGray;
            hotkeyHint.Margin = new Padding(4, 4, 0, 2);
            root.Controls.Add(hotkeyHint);
        }

        private static SectionPanel MakeSection(string title)
        {
            SectionPanel panel = new SectionPanel(title);
            panel.Width = 660;
            panel.Margin = new Padding(0, 0, 0, 10);
            return panel;
        }

        private static void AddSectionContent(SectionPanel section, Control content)
        {
            content.Location = new Point(section.Padding.Left, section.Padding.Top);
            section.Controls.Add(content);
            section.Height = content.PreferredSize.Height + section.Padding.Top + section.Padding.Bottom;
        }

        private sealed class SectionPanel : Panel
        {
            private readonly Label header;

            public SectionPanel(string title)
            {
                BackColor = Color.White;
                Padding = new Padding(14, 34, 14, 14);
                header = new Label();
                header.Text = title;
                header.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
                header.AutoSize = true;
                header.Location = new Point(14, 10);
                header.ForeColor = Color.FromArgb(45, 52, 62);
                Controls.Add(header);
                Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (Pen pen = new Pen(Color.FromArgb(230, 234, 240)))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                    }
                };
            }
        }

        private static TableLayoutPanel MakeTable()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.None;
            table.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.MinimumSize = new Size(620, 0);
            table.Width = 620;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
            table.Padding = new Padding(2);
            return table;
        }

        private static void AddRow(TableLayoutPanel table, string labelText, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 8, 8, 4);
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 4, 8, 4);
            if (control is ComboBox || control is NumericUpDown)
            {
                control.Width = 220;
            }
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static void AddWideRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 2);
        }

        private static NumericUpDown MakeIntegerNumeric(int min, int max, int increment)
        {
            NumericUpDown numeric = new NumericUpDown();
            numeric.Minimum = min;
            numeric.Maximum = max;
            numeric.Increment = increment;
            numeric.DecimalPlaces = 0;
            numeric.TextAlign = HorizontalAlignment.Right;
            numeric.ThousandsSeparator = false;
            return numeric;
        }

        private static NumericUpDown MakeDecimalNumeric(decimal min, decimal max, decimal increment)
        {
            NumericUpDown numeric = new NumericUpDown();
            numeric.Minimum = min;
            numeric.Maximum = max;
            numeric.Increment = increment;
            numeric.DecimalPlaces = 2;
            numeric.TextAlign = HorizontalAlignment.Right;
            return numeric;
        }

        private static Button MakeButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(120, 30);
            button.FlatStyle = FlatStyle.System;
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private static Label MakeInlineLabel(string text, Padding margin)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Margin = margin;
            return label;
        }

        private static Label MakeDashLabel()
        {
            Label dash = new Label();
            dash.Text = "—";
            dash.AutoSize = false;
            dash.Width = 20;
            dash.Height = 26;
            dash.TextAlign = ContentAlignment.MiddleCenter;
            dash.Margin = new Padding(2, 4, 2, 0);
            return dash;
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void WithInjectionPaused(Action action)
        {
            bool wasPaused = owner.Runtime.Paused;
            owner.Runtime.SetPaused(true);
            try
            {
                action();
            }
            finally
            {
                owner.Runtime.SetPaused(wasPaused);
                loading = true;
                pausedCheck.Checked = wasPaused;
                loading = false;
                UpdateStatus();
            }
        }

        private void PickScreenRegion()
        {
            int screenIndex = SelectedScreenIndex();
            Screen[] screens = Screen.AllScreens;
            Rectangle bounds;
            string name;
            if (screenIndex >= 0 && screenIndex < screens.Length)
            {
                bounds = screens[screenIndex].Bounds;
                name = ScreenUtil.ShortLabel(screens[screenIndex]);
            }
            else
            {
                bounds = SystemInformation.VirtualScreen;
                name = "全部屏幕";
            }

            DriverRuntime runtime = owner.Runtime;
            bool wasPaused = runtime.Paused;
            int previousX = runtime.MapX;
            int previousY = runtime.MapY;
            int previousWidth = runtime.MapWidth;
            int previousHeight = runtime.MapHeight;
            string previousFit = runtime.MapFit;

            Rectangle selected = Rectangle.Empty;
            bool picked = false;
            bool penMode = false;

            Action<bool> onPenMode = delegate(bool enabled)
            {
                penMode = enabled;
                if (enabled)
                {
                    runtime.SetPaused(false);
                    runtime.SetMapRegion(0, 0, 0, 0);
                    runtime.SetMapFit("stretch");
                }
                else
                {
                    runtime.SetPaused(true);
                    runtime.SetMapRegion(previousX, previousY, previousWidth, previousHeight);
                    runtime.SetMapFit(previousFit);
                }
            };

            runtime.SetPaused(true);
            try
            {
                using (RegionPickerForm picker = new RegionPickerForm(bounds, name, runtime, onPenMode))
                {
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        selected = picker.SelectedRect;
                        picked = true;
                    }
                }
            }
            finally
            {
                runtime.SetPaused(wasPaused);
                if (!picked && penMode)
                {
                    runtime.SetMapRegion(previousX, previousY, previousWidth, previousHeight);
                    runtime.SetMapFit(previousFit);
                }
                loading = true;
                pausedCheck.Checked = wasPaused;
                loading = false;
                UpdateStatus();
            }

            if (!picked)
            {
                LoadFromRuntime();
                return;
            }

            int x = Math.Max(0, selected.Left - bounds.Left);
            int y = Math.Max(0, selected.Top - bounds.Top);
            int width = Math.Min(selected.Width, Math.Max(0, bounds.Width - x));
            int height = Math.Min(selected.Height, Math.Max(0, bounds.Height - y));

            loading = true;
            mapFitCombo.SelectedIndex = MapFitIndex(previousFit);
            mapXNumeric.Value = ClampDecimal(x, mapXNumeric.Minimum, mapXNumeric.Maximum);
            mapYNumeric.Value = ClampDecimal(y, mapYNumeric.Minimum, mapYNumeric.Maximum);
            mapWNumeric.Value = ClampDecimal(width, mapWNumeric.Minimum, mapWNumeric.Maximum);
            mapHNumeric.Value = ClampDecimal(height, mapHNumeric.Minimum, mapHNumeric.Maximum);
            loading = false;
            ApplyFromControls();
        }

        private void PreviewMappingRegion()
        {
            DriverRuntime runtime = owner.Runtime;
            Screen[] screens = Screen.AllScreens;
            Rectangle bounds;
            if (runtime.ScreenIndex >= 0 && runtime.ScreenIndex < screens.Length)
            {
                bounds = screens[runtime.ScreenIndex].Bounds;
            }
            else
            {
                bounds = SystemInformation.VirtualScreen;
            }

            int regionX = 0;
            int regionY = 0;
            int regionWidth = bounds.Width;
            int regionHeight = bounds.Height;
            if (runtime.MapWidth > 0 && runtime.MapHeight > 0)
            {
                regionX = Math.Min(runtime.MapX, Math.Max(0, bounds.Width - 1));
                regionY = Math.Min(runtime.MapY, Math.Max(0, bounds.Height - 1));
                regionWidth = Math.Min(runtime.MapWidth, bounds.Width - regionX);
                regionHeight = Math.Min(runtime.MapHeight, bounds.Height - regionY);
                if (regionWidth <= 0 || regionHeight <= 0)
                {
                    regionX = 0;
                    regionY = 0;
                    regionWidth = bounds.Width;
                    regionHeight = bounds.Height;
                }
            }

            string label = string.Format("{0} · 区域 {1},{2} {3}×{4} · {5}",
                ScreenUtil.LabelForIndex(runtime.ScreenIndex),
                regionX,
                regionY,
                regionWidth,
                regionHeight,
                DriverRuntime.DescribeFit(runtime.MapFit));
            MappingBorderForm form = new MappingBorderForm(
                bounds,
                bounds.Left + regionX,
                bounds.Top + regionY,
                regionWidth,
                regionHeight,
                runtime.RawMaxX - runtime.RawMinX,
                runtime.RawMaxY - runtime.RawMinY,
                runtime.Rotate,
                runtime.MapFit == "uniform",
                label);
            form.Show(this);
            AppLog.Write("mapping border preview shown");
        }

        private void CaptureTabletRange()
        {
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;
            bool captured = false;
            WithInjectionPaused(delegate
            {
                using (CalibrationForm form = new CalibrationForm(owner.Runtime, CalibrationMode.TabletRange))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        minX = form.SelectedMinX;
                        maxX = form.SelectedMaxX;
                        minY = form.SelectedMinY;
                        maxY = form.SelectedMaxY;
                        captured = true;
                    }
                }
            });
            if (!captured)
            {
                return;
            }

            loading = true;
            rawMinXNumeric.Value = ClampDecimal(minX, rawMinXNumeric.Minimum, rawMinXNumeric.Maximum);
            rawMaxXNumeric.Value = ClampDecimal(maxX, rawMaxXNumeric.Minimum, rawMaxXNumeric.Maximum);
            rawMinYNumeric.Value = ClampDecimal(minY, rawMinYNumeric.Minimum, rawMinYNumeric.Maximum);
            rawMaxYNumeric.Value = ClampDecimal(maxY, rawMaxYNumeric.Minimum, rawMaxYNumeric.Maximum);
            loading = false;
            ApplyFromControls();
        }

        private void CalibratePressure()
        {
            int light = 0;
            int heavy = 0;
            bool captured = false;
            WithInjectionPaused(delegate
            {
                using (CalibrationForm form = new CalibrationForm(owner.Runtime, CalibrationMode.Pressure))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        light = form.SelectedPressureMin;
                        heavy = form.SelectedPressureMax;
                        captured = true;
                    }
                }
            });
            if (!captured)
            {
                return;
            }

            loading = true;
            minNumeric.Value = ClampDecimal(light, minNumeric.Minimum, minNumeric.Maximum);
            maxNumeric.Value = ClampDecimal(heavy, maxNumeric.Minimum, maxNumeric.Maximum);
            loading = false;
            ApplyFromControls();
        }

        private int SelectedScreenIndex()
        {
            ScreenOption option = screenCombo.SelectedItem as ScreenOption;
            return option == null ? -1 : option.Index;
        }

        private void ScreenComboChanged()
        {
            if (loading || owner == null)
            {
                return;
            }

            int newIndex = SelectedScreenIndex();
            int oldIndex = owner.Runtime.ScreenIndex;
            if (newIndex != oldIndex)
            {
                Rectangle oldBounds = BoundsForScreenIndex(oldIndex);
                Rectangle newBounds = BoundsForScreenIndex(newIndex);
                if (MapWidthOfControls() > 0 && MapHeightOfControls() > 0
                    && oldBounds.Width > 0 && oldBounds.Height > 0)
                {
                    double scaleX = newBounds.Width / (double)oldBounds.Width;
                    double scaleY = newBounds.Height / (double)oldBounds.Height;
                    loading = true;
                    mapXNumeric.Value = ClampDecimal(
                        (decimal)Math.Round((double)mapXNumeric.Value * scaleX),
                        mapXNumeric.Minimum, mapXNumeric.Maximum);
                    mapYNumeric.Value = ClampDecimal(
                        (decimal)Math.Round((double)mapYNumeric.Value * scaleY),
                        mapYNumeric.Minimum, mapYNumeric.Maximum);
                    mapWNumeric.Value = ClampDecimal(
                        (decimal)Math.Max(1, Math.Round((double)mapWNumeric.Value * scaleX)),
                        mapWNumeric.Minimum, mapWNumeric.Maximum);
                    mapHNumeric.Value = ClampDecimal(
                        (decimal)Math.Max(1, Math.Round((double)mapHNumeric.Value * scaleY)),
                        mapHNumeric.Minimum, mapHNumeric.Maximum);
                    loading = false;
                }
            }

            ApplyFromControls();
        }

        private int MapWidthOfControls()
        {
            return Decimal.ToInt32(mapWNumeric.Value);
        }

        private int MapHeightOfControls()
        {
            return Decimal.ToInt32(mapHNumeric.Value);
        }

        private static Rectangle BoundsForScreenIndex(int index)
        {
            Screen[] screens = Screen.AllScreens;
            if (index >= 0 && index < screens.Length)
            {
                return screens[index].Bounds;
            }
            return SystemInformation.VirtualScreen;
        }

        private void LoadFromRuntime()
        {
            loading = true;
            try
            {
                DriverRuntime runtime = owner.Runtime;
                penRadio.Checked = !runtime.MouseMode;
                mouseRadio.Checked = runtime.MouseMode;
                pausedCheck.Checked = runtime.Paused;
                if (connectionCombo != null)
                {
                    connectionCombo.SelectedIndex = ConnectionIndex(runtime.ConnectionMode);
                }

                if (rotationCombo != null)
                {
                    rotationCombo.SelectedIndex = RotationIndex(runtime.Rotate);
                }

                if (screenCombo != null)
                {
                    screenCombo.Items.Clear();
                    ScreenOption all = new ScreenOption(-1, "全部屏幕");
                    screenCombo.Items.Add(all);
                    Screen[] screens = Screen.AllScreens;
                    int[] order = ScreenUtil.IndicesSortedByDisplayNumber();
                    for (int k = 0; k < order.Length; k++)
                    {
                        int i = order[k];
                        screenCombo.Items.Add(new ScreenOption(i, ScreenUtil.Describe(screens[i])));
                    }
                    screenCombo.SelectedItem = FindScreenOption(runtime.ScreenIndex);
                }

                if (mapFitCombo != null)
                {
                    mapFitCombo.SelectedIndex = MapFitIndex(runtime.MapFit);
                }
                if (mapXNumeric != null)
                {
                    mapXNumeric.Value = ClampDecimal(runtime.MapX, mapXNumeric.Minimum, mapXNumeric.Maximum);
                    mapYNumeric.Value = ClampDecimal(runtime.MapY, mapYNumeric.Minimum, mapYNumeric.Maximum);
                    mapWNumeric.Value = ClampDecimal(runtime.MapWidth, mapWNumeric.Minimum, mapWNumeric.Maximum);
                    mapHNumeric.Value = ClampDecimal(runtime.MapHeight, mapHNumeric.Minimum, mapHNumeric.Maximum);
                    rawMinXNumeric.Value = ClampDecimal(runtime.RawMinX, rawMinXNumeric.Minimum, rawMinXNumeric.Maximum);
                    rawMaxXNumeric.Value = ClampDecimal(runtime.RawMaxX, rawMaxXNumeric.Minimum, rawMaxXNumeric.Maximum);
                    rawMinYNumeric.Value = ClampDecimal(runtime.RawMinY, rawMinYNumeric.Minimum, rawMinYNumeric.Maximum);
                    rawMaxYNumeric.Value = ClampDecimal(runtime.RawMaxY, rawMaxYNumeric.Minimum, rawMaxYNumeric.Maximum);
                }

                minNumeric.Value = ClampDecimal(runtime.PressureMin, minNumeric.Minimum, minNumeric.Maximum);
                maxNumeric.Value = ClampDecimal(runtime.PressureMax, maxNumeric.Minimum, maxNumeric.Maximum);
                gammaNumeric.Value = ClampDecimal((decimal)runtime.PressureGamma, gammaNumeric.Minimum, gammaNumeric.Maximum);
                deadzoneNumeric.Value = ClampDecimal((decimal)runtime.PressureDeadzone, deadzoneNumeric.Minimum, deadzoneNumeric.Maximum);
                smoothingNumeric.Value = ClampDecimal((decimal)runtime.PressureSmoothing, smoothingNumeric.Minimum, smoothingNumeric.Maximum);
                SelectCurveIndex(runtime.PressureGamma);
                autoStartCheck.Checked = owner.IsAutoStartEnabled();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                AppLog.Write("settings form load failed: " + ex.Message);
            }
            finally
            {
                loading = false;
            }
        }

        private void ApplyFromControls()
        {
            if (loading || owner == null)
            {
                return;
            }

            try
            {
                int min = Decimal.ToInt32(minNumeric.Value);
                int max = Decimal.ToInt32(maxNumeric.Value);
                if (max <= min)
                {
                    max = Math.Min(65535, min + 1);
                    loading = true;
                    maxNumeric.Value = max;
                    loading = false;
                }

                int screen = SelectedScreenIndex();

                int rawMinX = Decimal.ToInt32(rawMinXNumeric.Value);
                int rawMaxX = Decimal.ToInt32(rawMaxXNumeric.Value);
                if (rawMaxX <= rawMinX)
                {
                    rawMaxX = Math.Min(65535, rawMinX + 1);
                    loading = true;
                    rawMaxXNumeric.Value = rawMaxX;
                    loading = false;
                }
                int rawMinY = Decimal.ToInt32(rawMinYNumeric.Value);
                int rawMaxY = Decimal.ToInt32(rawMaxYNumeric.Value);
                if (rawMaxY <= rawMinY)
                {
                    rawMaxY = Math.Min(65535, rawMinY + 1);
                    loading = true;
                    rawMaxYNumeric.Value = rawMaxY;
                    loading = false;
                }

                int mapX = Decimal.ToInt32(mapXNumeric.Value);
                int mapY = Decimal.ToInt32(mapYNumeric.Value);
                int mapWidth = Decimal.ToInt32(mapWNumeric.Value);
                int mapHeight = Decimal.ToInt32(mapHNumeric.Value);
                string mapFit = MapFitValue(mapFitCombo.SelectedIndex);

                double gamma = (double)gammaNumeric.Value;
                owner.ApplySettings(
                    mouseRadio.Checked,
                    pausedCheck.Checked,
                    ConnectionValue(connectionCombo.SelectedIndex),
                    RotationValue(rotationCombo.SelectedIndex),
                    screen,
                    mapX,
                    mapY,
                    mapWidth,
                    mapHeight,
                    mapFit,
                    rawMinX,
                    rawMaxX,
                    rawMinY,
                    rawMaxY,
                    min,
                    max,
                    gamma,
                    (double)deadzoneNumeric.Value,
                    (double)smoothingNumeric.Value);

                loading = true;
                SelectCurveIndex(gamma);
                loading = false;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                AppLog.Write("settings form apply failed: " + ex.Message);
                MessageBox.Show(this, "保存设置失败：" + ex.Message, "BoogieBoardSync",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetPressure()
        {
            loading = true;
            minNumeric.Value = 0;
            maxNumeric.Value = 1023;
            gammaNumeric.Value = 1.00M;
            deadzoneNumeric.Value = 0.00M;
            smoothingNumeric.Value = 0.00M;
            SelectCurveIndex(1.0);
            loading = false;
            ApplyFromControls();
        }

        private void ResetMapRegion()
        {
            loading = true;
            mapXNumeric.Value = 0;
            mapYNumeric.Value = 0;
            mapWNumeric.Value = 0;
            mapHNumeric.Value = 0;
            loading = false;
            ApplyFromControls();
        }

        private void ResetTabletRange()
        {
            loading = true;
            rawMinXNumeric.Value = 0;
            rawMaxXNumeric.Value = 20280;
            rawMinYNumeric.Value = 0;
            rawMaxYNumeric.Value = 13942;
            loading = false;
            ApplyFromControls();
        }

        private void UpdateStatus()
        {
            if (statusLabel == null || owner == null)
            {
                return;
            }

            DriverRuntime runtime = owner.Runtime;
            string state = runtime.Paused
                ? "已暂停"
                : (runtime.MouseMode ? "鼠标模式" : "数位板模式");
            string link = string.IsNullOrEmpty(runtime.ActiveLink) ? "等待设备" : runtime.ActiveLink;
            statusLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}    {1}    旋转 {2}°    gamma {3:0.00}",
                state,
                link,
                runtime.Rotate,
                runtime.PressureGamma);
            statusLabel.ForeColor = runtime.Paused
                ? Color.Firebrick
                : (string.IsNullOrEmpty(runtime.ActiveLink) ? Color.DarkOrange : Color.SeaGreen);
            if (detailLabel != null)
            {
                detailLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "最近输入 {0}    力度 {1} → {2}    {3}",
                    FormatInputAge(runtime.LastInputAgeMs),
                    runtime.LastRawPressure,
                    runtime.LastMappedPressure,
                    runtime.MouseMode ? "侧键 = 右键" : "侧键 = 笔按钮");
            }
            if (footerLabel != null)
            {
                footerLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "链路：{0}    最近输入 {1}    映射 {2}    力度 gamma {3:0.00}",
                    string.IsNullOrEmpty(runtime.ActiveLink) ? "未连接" : runtime.ActiveLink,
                    FormatInputAge(runtime.LastInputAgeMs),
                    string.Format("{0} · {1}",
                        ScreenUtil.LabelForIndex(runtime.ScreenIndex),
                        DriverRuntime.DescribeFit(runtime.MapFit)),
                    runtime.PressureGamma);
            }
        }

        private static string FormatInputAge(int ageMs)
        {
            if (ageMs == int.MaxValue)
            {
                return "尚无";
            }
            if (ageMs < 1000)
            {
                return ageMs + " ms 前";
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} 秒前", ageMs / 1000.0);
        }

        private void SelectCurveIndex(double gamma)
        {
            if (gammaNumeric == null || curveCombo == null)
            {
                return;
            }

            int index = 3;
            if (Math.Abs(gamma - 0.70) < 0.005) index = 0;
            else if (Math.Abs(gamma - 1.00) < 0.005) index = 1;
            else if (Math.Abs(gamma - 1.40) < 0.005) index = 2;
            int old = curveCombo.SelectedIndex;
            curveCombo.SelectedIndex = index;
            if (old != index)
            {
                AppLog.Write("pressure preset selected " + index);
            }
        }

        private static int MapFitIndex(string fit)
        {
            if (fit == "uniform") return 1;
            if (fit == "fill") return 2;
            return 0;
        }

        private static string MapFitValue(int index)
        {
            if (index == 1) return "uniform";
            if (index == 2) return "fill";
            return "stretch";
        }

        private static int ConnectionIndex(string connection)
        {
            if (connection == "usb") return 1;
            if (connection == "bluetooth") return 2;
            return 0;
        }

        private static string ConnectionValue(int index)
        {
            if (index == 1) return "usb";
            if (index == 2) return "bluetooth";
            return "auto";
        }

        private static int RotationIndex(int rotation)
        {
            if (rotation == 90) return 1;
            if (rotation == 180) return 2;
            if (rotation == 270) return 3;
            return 0;
        }

        private static int RotationValue(int index)
        {
            if (index == 1) return 90;
            if (index == 2) return 180;
            if (index == 3) return 270;
            return 0;
        }

        private ScreenOption FindScreenOption(int index)
        {
            for (int i = 0; i < screenCombo.Items.Count; i++)
            {
                ScreenOption option = screenCombo.Items[i] as ScreenOption;
                if (option != null && option.Index == index)
                {
                    return option;
                }
            }
            return screenCombo.Items.Count > 0 ? screenCombo.Items[0] as ScreenOption : null;
        }

        private sealed class ScreenOption
        {
            public readonly int Index;
            public readonly string Name;

            public ScreenOption(int index, string name)
            {
                Index = index;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}
