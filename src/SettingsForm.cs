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
        private ComboBox connectionCombo;
        private RadioButton penRadio;
        private RadioButton mouseRadio;
        private CheckBox pausedCheck;
        private ComboBox rotationCombo;
        private ComboBox screenCombo;
        private ComboBox curveCombo;
        private NumericUpDown minNumeric;
        private NumericUpDown maxNumeric;
        private NumericUpDown gammaNumeric;
        private NumericUpDown deadzoneNumeric;
        private NumericUpDown smoothingNumeric;
        private CheckBox autoStartCheck;
        private PressureMonitor pressureMonitor;
        private Timer refreshTimer;

        public SettingsForm(TrayContext owner)
        {
            this.owner = owner;
            Text = AppInfo.DisplayName + " 设置";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(680, 860);
            MinimumSize = new Size(640, 620);
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

        private void BuildContent()
        {
            FlowLayoutPanel root = new FlowLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.FlowDirection = FlowDirection.TopDown;
            root.WrapContents = false;
            root.AutoScroll = true;
            root.Padding = new Padding(18, 14, 18, 14);
            root.BackColor = Color.FromArgb(247, 248, 250);
            Controls.Add(root);

            GroupBox inputGroup = MakeGroup("输入状态");
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
            liveHint.Text = "所有修改会立即生效，不需要保存按钮。";
            liveHint.AutoSize = true;
            liveHint.ForeColor = Color.DimGray;
            AddWideRow(inputTable, liveHint);

            Label bluetoothHint = new Label();
            bluetoothHint.Text = "蓝牙需要先在 Windows 设置中配对；程序会自动识别名称含 Boogie / Improv / Sync 的设备。";
            bluetoothHint.AutoSize = true;
            bluetoothHint.ForeColor = Color.DimGray;
            AddWideRow(inputTable, bluetoothHint);
            inputGroup.Controls.Add(inputTable);
            root.Controls.Add(inputGroup);

            GroupBox displayGroup = MakeGroup("显示");
            TableLayoutPanel displayTable = MakeTable();
            rotationCombo = new ComboBox();
            rotationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            rotationCombo.Items.Add("0°");
            rotationCombo.Items.Add("90°");
            rotationCombo.Items.Add("180°");
            rotationCombo.Items.Add("270°");
            rotationCombo.SelectedIndex = 0;
            rotationCombo.SelectedIndexChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            AddRow(displayTable, "旋转", rotationCombo);

            screenCombo = new ComboBox();
            screenCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            screenCombo.SelectedIndexChanged += delegate(object sender, EventArgs e) { ApplyFromControls(); };
            AddRow(displayTable, "映射屏幕", screenCombo);
            displayGroup.Controls.Add(displayTable);
            root.Controls.Add(displayGroup);

            GroupBox pressureGroup = MakeGroup("力度映射");
            TableLayoutPanel pressureTable = MakeTable();

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
            Label rangeDash = new Label();
            rangeDash.Text = "—";
            rangeDash.AutoSize = false;
            rangeDash.Width = 24;
            rangeDash.Height = 26;
            rangeDash.TextAlign = ContentAlignment.MiddleCenter;
            rangeDash.Margin = new Padding(4, 4, 4, 0);
            rangeFlow.Controls.Add(rangeDash);
            rangeFlow.Controls.Add(maxNumeric);
            AddRow(pressureTable, "原始范围", rangeFlow);
            AddRow(pressureTable, "Gamma 曲线", gammaNumeric);

            deadzoneNumeric.Width = 82;
            smoothingNumeric.Width = 82;
            FlowLayoutPanel extraFlow = new FlowLayoutPanel();
            extraFlow.AutoSize = true;
            extraFlow.WrapContents = false;
            extraFlow.Margin = new Padding(0);
            Label deadzoneLabel = new Label();
            deadzoneLabel.Text = "死区";
            deadzoneLabel.AutoSize = true;
            deadzoneLabel.Margin = new Padding(0, 8, 6, 0);
            extraFlow.Controls.Add(deadzoneLabel);
            extraFlow.Controls.Add(deadzoneNumeric);
            Label smoothingLabel = new Label();
            smoothingLabel.Text = "平滑";
            smoothingLabel.AutoSize = true;
            smoothingLabel.Margin = new Padding(12, 8, 6, 0);
            extraFlow.Controls.Add(smoothingLabel);
            extraFlow.Controls.Add(smoothingNumeric);
            AddRow(pressureTable, "微调", extraFlow);

            pressureMonitor = new PressureMonitor();
            pressureMonitor.Width = 520;
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
            Button resetButton = MakeButton("恢复默认力度");
            resetButton.Click += delegate(object sender, EventArgs e) { ResetPressure(); };
            pressureButtons.Controls.Add(resetButton);
            AddWideRow(pressureTable, pressureButtons);

            pressureGroup.Controls.Add(pressureTable);
            root.Controls.Add(pressureGroup);

            GroupBox startupGroup = MakeGroup("启动与配置");
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

            startupGroup.Controls.Add(startupTable);
            root.Controls.Add(startupGroup);

            GroupBox recoveryGroup = MakeGroup("诊断与恢复");
            FlowLayoutPanel recoveryFlow = new FlowLayoutPanel();
            recoveryFlow.AutoSize = true;
            recoveryFlow.WrapContents = true;
            recoveryFlow.MaximumSize = new Size(540, 0);
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
            recoveryGroup.Controls.Add(recoveryFlow);
            root.Controls.Add(recoveryGroup);

            Label versionHint = new Label();
            versionHint.Text = AppInfo.DisplayName + " · USB HID + 经典蓝牙 RFCOMM · 日志自动轮转到 1 MB";
            versionHint.AutoSize = true;
            versionHint.ForeColor = Color.DimGray;
            versionHint.Margin = new Padding(4, 8, 0, 2);
            root.Controls.Add(versionHint);

            Label hotkeyHint = new Label();
            hotkeyHint.Text = "全局热键：Ctrl+Alt+S 设置 / P 暂停 / M 模式 / R 旋转";
            hotkeyHint.AutoSize = true;
            hotkeyHint.ForeColor = Color.DimGray;
            hotkeyHint.Margin = new Padding(4, 8, 0, 6);
            root.Controls.Add(hotkeyHint);

            Button closeButton = MakeButton("关闭设置窗口");
            closeButton.Margin = new Padding(4, 4, 0, 8);
            closeButton.Click += delegate(object sender, EventArgs e) { Hide(); };
            root.Controls.Add(closeButton);
        }

        private static GroupBox MakeGroup(string title)
        {
            GroupBox group = new GroupBox();
            group.Text = title;
            group.Width = 570;
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.Padding = new Padding(10, 8, 10, 10);
            group.Margin = new Padding(0, 0, 0, 10);
            return group;
        }

        private static TableLayoutPanel MakeTable()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.None;
            table.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.MinimumSize = new Size(540, 0);
            table.Width = 540;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
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

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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
                    for (int i = 0; i < screens.Length; i++)
                    {
                        Rectangle bounds = screens[i].Bounds;
                        screenCombo.Items.Add(new ScreenOption(i, string.Format(
                            CultureInfo.InvariantCulture,
                            "显示器 {0}  {1}x{2}",
                            i + 1,
                            bounds.Width,
                            bounds.Height)));
                    }
                    screenCombo.SelectedItem = FindScreenOption(runtime.ScreenIndex);
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

                int screen = -1;
                ScreenOption screenOption = screenCombo.SelectedItem as ScreenOption;
                if (screenOption != null)
                {
                    screen = screenOption.Index;
                }

                double gamma = (double)gammaNumeric.Value;
                owner.ApplySettings(
                    mouseRadio.Checked,
                    pausedCheck.Checked,
                    ConnectionValue(connectionCombo.SelectedIndex),
                    RotationValue(rotationCombo.SelectedIndex),
                    screen,
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