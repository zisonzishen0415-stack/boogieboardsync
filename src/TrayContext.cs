using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BoogieBoardSync
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly Options options;
        private readonly DriverRuntime runtime;
        private Thread driverThread;
        private readonly NotifyIcon icon;
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip menu;
        private readonly HotkeyWindow hotkeys;
        private readonly ToolStripMenuItem statusItem;
        private readonly ToolStripMenuItem pauseItem;
        private readonly ToolStripMenuItem penItem;
        private readonly ToolStripMenuItem mouseItem;
        private readonly ToolStripMenuItem autoStartItem;
        private readonly List<ToolStripMenuItem> rotateItems = new List<ToolStripMenuItem>();
        private readonly List<ToolStripMenuItem> screenItems = new List<ToolStripMenuItem>();
        private readonly List<ToolStripMenuItem> curveItems = new List<ToolStripMenuItem>();
        private readonly List<ToolStripMenuItem> connectionItems = new List<ToolStripMenuItem>();
        private SettingsForm settingsForm;
        private DiagnosticsForm diagnosticsForm;
        private System.Windows.Forms.Timer statusTimer;
        private System.Windows.Forms.Timer startupShowTimer;
        private volatile bool mappingRefreshPending;
        private bool shuttingDown;
        private readonly object diagnosticsSync = new object();
        private Task<string> bluetoothDiagnosticsTask;
        private DateTime bluetoothDiagnosticsUtc = DateTime.MinValue;

        public TrayContext(Options options)
        {
            this.options = options;
            runtime = new DriverRuntime(options);
            try
            {
                SettingsStore.Load(runtime);
                if (options.Connection != "auto")
                {
                    runtime.SetConnectionMode(options.Connection);
                }
                SettingsStore.Save(runtime);
            }
            catch (Exception ex)
            {
                AppLog.Write("settings load/save failed: " + ex.Message);
            }

            menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;

            statusItem = new ToolStripMenuItem("正在启动");
            statusItem.Enabled = false;
            menu.Items.Add(statusItem);

            ToolStripMenuItem settingsItem = new ToolStripMenuItem("打开设置窗口  (Ctrl+Alt+S)");
            settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
            settingsItem.Click += delegate(object sender, EventArgs e) { ShowSettings(); };
            menu.Items.Add(settingsItem);

            pauseItem = new ToolStripMenuItem("暂停输入  (Ctrl+Alt+P)");
            pauseItem.Click += delegate(object sender, EventArgs e) { TogglePause(); };
            menu.Items.Add(pauseItem);

            ToolStripMenuItem connectionMenu = new ToolStripMenuItem("连接方式");
            AddConnectionItem(connectionMenu, "自动（USB 优先）", "auto");
            AddConnectionItem(connectionMenu, "仅 USB", "usb");
            AddConnectionItem(connectionMenu, "仅蓝牙", "bluetooth");
            menu.Items.Add(connectionMenu);

            ToolStripMenuItem reconnectItem = new ToolStripMenuItem("重新连接");
            reconnectItem.Click += delegate(object sender, EventArgs e)
            {
                AppLog.Write("manual reconnect requested from tray");
                Program.RequestTransportRefresh();
            };
            menu.Items.Add(reconnectItem);

            ToolStripMenuItem diagnosticsItem = new ToolStripMenuItem("诊断信息…");
            diagnosticsItem.Click += delegate(object sender, EventArgs e) { ShowDiagnostics(); };
            menu.Items.Add(diagnosticsItem);

            ToolStripMenuItem logItem = new ToolStripMenuItem("打开日志");
            logItem.Click += delegate(object sender, EventArgs e) { OpenLogFile(); };
            menu.Items.Add(logItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem modeMenu = new ToolStripMenuItem("输入方式");
            penItem = new ToolStripMenuItem("数位板（Windows Ink 压感）");
            penItem.Click += delegate(object sender, EventArgs e) { SetMouseMode(false); };
            mouseItem = new ToolStripMenuItem("鼠标（侧键右键）");
            mouseItem.Click += delegate(object sender, EventArgs e) { SetMouseMode(true); };
            modeMenu.DropDownItems.Add(penItem);
            modeMenu.DropDownItems.Add(mouseItem);
            menu.Items.Add(modeMenu);

            ToolStripMenuItem rotateMenu = new ToolStripMenuItem("旋转");
            int[] rotations = new int[] { 0, 90, 180, 270 };
            for (int i = 0; i < rotations.Length; i++)
            {
                int value = rotations[i];
                ToolStripMenuItem item = new ToolStripMenuItem(value + "°");
                item.Tag = value;
                item.Click += delegate(object sender, EventArgs e) { SetRotation(value); };
                rotateItems.Add(item);
                rotateMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(rotateMenu);

            ToolStripMenuItem screenMenu = new ToolStripMenuItem("映射屏幕");
            ToolStripMenuItem allScreens = new ToolStripMenuItem("全部屏幕");
            allScreens.Tag = -1;
            allScreens.Click += delegate(object sender, EventArgs e) { SetScreen(-1); };
            screenItems.Add(allScreens);
            screenMenu.DropDownItems.Add(allScreens);
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                int index = i;
                Rectangle bounds = screens[i].Bounds;
                ToolStripMenuItem item = new ToolStripMenuItem(string.Format(
                    "显示器 {0}  {1}x{2}", i + 1, bounds.Width, bounds.Height));
                item.Tag = index;
                item.Click += delegate(object sender, EventArgs e) { SetScreen(index); };
                screenItems.Add(item);
                screenMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(screenMenu);

            ToolStripMenuItem pressureMenu = new ToolStripMenuItem("力度曲线");
            AddCurveItem(pressureMenu, "柔和", 0.70);
            AddCurveItem(pressureMenu, "线性", 1.00);
            AddCurveItem(pressureMenu, "硬朗", 1.40);
            menu.Items.Add(pressureMenu);

            menu.Items.Add(new ToolStripSeparator());

            autoStartItem = new ToolStripMenuItem("开机自动启动");
            autoStartItem.Click += delegate(object sender, EventArgs e) { ToggleAutoStart(); };
            menu.Items.Add(autoStartItem);

            ToolStripMenuItem openSettings = new ToolStripMenuItem("打开设置文件夹");
            openSettings.Click += delegate(object sender, EventArgs e) { OpenSettingsFolder(); };
            menu.Items.Add(openSettings);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("关于 " + AppInfo.DisplayName);
            aboutItem.Click += delegate(object sender, EventArgs e)
            {
                MessageBox.Show(
                    AppInfo.DisplayName + Environment.NewLine + Environment.NewLine +
                    "Boogie Board Sync 用户态笔驱动" + Environment.NewLine +
                    "USB HID 与经典蓝牙 RFCOMM" + Environment.NewLine + Environment.NewLine +
                    "配置文件：" + SettingsStore.FolderPath + Environment.NewLine +
                    "日志文件：" + AppLog.FilePath,
                    AppInfo.DisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            menu.Items.Add(aboutItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate(object sender, EventArgs e) { ExitApplication(); };
            menu.Items.Add(exitItem);

            trayIcon = new Icon(SystemIcons.Application, 16, 16);
            icon = new NotifyIcon();
            icon.Icon = trayIcon;
            icon.Text = "BoogieBoardSync";
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate(object sender, EventArgs e) { ShowSettings(); };
            icon.Visible = true;

            hotkeys = new HotkeyWindow(this);
            UpdateUi();

            StartDriverThread();

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 1000;
            statusTimer.Tick += delegate(object sender, EventArgs e)
            {
                EnsureDriverThread();
                RefreshPendingMapping();
                UpdateUi();
            };
            statusTimer.Start();
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            AppLog.Write("tray started");
            if (options.ShowSettings)
            {
                startupShowTimer = new System.Windows.Forms.Timer();
                startupShowTimer.Interval = 350;
                startupShowTimer.Tick += delegate(object sender, EventArgs e)
                {
                    startupShowTimer.Stop();
                    startupShowTimer.Dispose();
                    startupShowTimer = null;
                    ShowSettings();
                };
                startupShowTimer.Start();
            }
        }

        private void StartDriverThread()
        {
            driverThread = new Thread(delegate()
            {
                try
                {
                    Program.RunDriver(options, runtime);
                }
                catch (Exception ex)
                {
                    AppLog.Write("driver thread crashed: " + ex);
                }
                finally
                {
                    AppLog.Write("driver thread exited");
                }
            });
            driverThread.IsBackground = true;
            driverThread.Name = "BoogieBoardSync driver";
            driverThread.Start();
            AppLog.Write("driver thread started");
        }

        private void EnsureDriverThread()
        {
            if (shuttingDown)
            {
                return;
            }
            if (driverThread != null && driverThread.IsAlive)
            {
                return;
            }
            AppLog.Write("driver thread not running; restarting");
            StartDriverThread();
        }
        public void HandleHotkey(int id)
        {
            AppLog.Write("hotkey " + id);
            if (id == 1)
            {
                TogglePause();
            }
            else if (id == 2)
            {
                SetMouseMode(!runtime.MouseMode);
            }
            else if (id == 3)
            {
                SetRotation(NextRotation(runtime.Rotate));
            }
            else if (id == 4)
            {
                ShowSettings();
            }
        }

        internal DriverRuntime Runtime
        {
            get { return runtime; }
        }

        internal void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this);
            }
            if (!settingsForm.Visible)
            {
                settingsForm.Show();
            }
            if (settingsForm.WindowState == FormWindowState.Minimized)
            {
                settingsForm.WindowState = FormWindowState.Normal;
            }
            settingsForm.Activate();
            settingsForm.BringToFront();
            AppLog.Write("settings window shown");
        }

        internal void ApplySettings(bool mouse, bool paused, string connection, int rotate, int screenIndex,
            int pressureMin, int pressureMax, double pressureGamma,
            double pressureDeadzone, double pressureSmoothing)
        {
            string previousConnection = runtime.ConnectionMode;
            runtime.SetMouseMode(mouse);
            runtime.SetPaused(paused);
            runtime.SetConnectionMode(connection);
            runtime.SetRotate(rotate);
            runtime.SetScreenIndex(screenIndex);
            runtime.SetPressure(pressureMin, pressureMax, pressureGamma, pressureDeadzone, pressureSmoothing);
            SaveAndRefresh();
            if (previousConnection != runtime.ConnectionMode)
            {
                Program.RequestTransportRefresh();
            }
            AppLog.Write("settings applied from window");
        }
        internal bool IsAutoStartEnabled()
        {
            try
            {
                return SettingsStore.IsAutoStartEnabled();
            }
            catch (Exception ex)
            {
                AppLog.Write("autostart read failed: " + ex.Message);
                return false;
            }
        }

        internal void SetAutoStartEnabled(bool enabled)
        {
            try
            {
                SettingsStore.SetAutoStart(enabled);
                AppLog.Write("autostart set to " + enabled);
            }
            catch (Exception ex)
            {
                AppLog.Write("autostart write failed: " + ex.Message);
            }
            UpdateUi();
        }

        private void AddConnectionItem(ToolStripMenuItem parent, string label, string value)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Tag = value;
            item.Click += delegate(object sender, EventArgs e) { SetConnectionMode(value); };
            connectionItems.Add(item);
            parent.DropDownItems.Add(item);
        }

        private void AddCurveItem(ToolStripMenuItem parent, string label, double gamma)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Tag = gamma;
            item.Click += delegate(object sender, EventArgs e) { SetPressurePreset(gamma); };
            curveItems.Add(item);
            parent.DropDownItems.Add(item);
        }

        private void TogglePause()
        {
            runtime.SetPaused(!runtime.Paused);
            SaveAndRefresh();
        }

        private void SetConnectionMode(string value)
        {
            string previous = runtime.ConnectionMode;
            runtime.SetConnectionMode(value);
            if (previous != runtime.ConnectionMode)
            {
                Program.RequestTransportRefresh();
            }
            SaveAndRefresh();
        }

        private void SetMouseMode(bool mouse)
        {
            runtime.SetMouseMode(mouse);
            SaveAndRefresh();
        }

        private void SetRotation(int angle)
        {
            runtime.SetRotate(angle);
            SaveAndRefresh();
        }

        private void SetScreen(int index)
        {
            runtime.SetScreenIndex(index);
            SaveAndRefresh();
        }

        private void SetPressurePreset(double gamma)
        {
            runtime.SetPressurePreset(gamma);
            SaveAndRefresh();
        }

        private void ToggleAutoStart()
        {
            try
            {
                SettingsStore.SetAutoStart(!SettingsStore.IsAutoStartEnabled());
            }
            catch (Exception)
            {
            }
            UpdateUi();
        }

        private void SaveAndRefresh()
        {
            try
            {
                SettingsStore.Save(runtime);
                AppLog.Write(string.Format(
                    "settings saved; paused={0} mouse={1} rotate={2} screen={3} gamma={4}",
                    runtime.Paused,
                    runtime.MouseMode,
                    runtime.Rotate,
                    runtime.ScreenIndex,
                    runtime.PressureGamma));
            }
            catch (Exception ex)
            {
                AppLog.Write("settings save failed: " + ex.Message);
            }
            UpdateUi();
        }

        private void UpdateUi()
        {
            pauseItem.Checked = runtime.Paused;
            penItem.Checked = !runtime.MouseMode;
            mouseItem.Checked = runtime.MouseMode;

            for (int i = 0; i < rotateItems.Count; i++)
            {
                rotateItems[i].Checked = ((int)rotateItems[i].Tag) == runtime.Rotate;
            }
            for (int i = 0; i < screenItems.Count; i++)
            {
                screenItems[i].Checked = ((int)screenItems[i].Tag) == runtime.ScreenIndex;
            }
            for (int i = 0; i < connectionItems.Count; i++)
            {
                connectionItems[i].Checked = (string)connectionItems[i].Tag == runtime.ConnectionMode;
            }
            for (int i = 0; i < curveItems.Count; i++)
            {
                double gamma = (double)curveItems[i].Tag;
                curveItems[i].Checked = Math.Abs(gamma - runtime.PressureGamma) < 0.005;
            }

            try
            {
                autoStartItem.Checked = SettingsStore.IsAutoStartEnabled();
            }
            catch (Exception)
            {
                autoStartItem.Checked = false;
            }

            string state = runtime.Paused
                ? "已暂停"
                : (runtime.MouseMode ? "鼠标模式" : "数位板模式");
            string link = string.IsNullOrEmpty(runtime.ActiveLink) ? "未连接" : runtime.ActiveLink;
            statusItem.Text = "状态：" + state + " / " + link;
            if (icon != null)
            {
                string trayText = "BoogieBoardSync - " + state + " / " + link;
                if (trayText.Length > 63)
                {
                    trayText = trayText.Substring(0, 63);
                }
                icon.Text = trayText;
            }
        }

        private static int NextRotation(int current)
        {
            if (current == 0) return 90;
            if (current == 90) return 180;
            if (current == 180) return 270;
            return 0;
        }

        internal void OpenSettingsFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", SettingsStore.FolderPath);
            }
            catch (Exception)
            {
            }
        }

        internal void ShowDiagnostics()
        {
            if (diagnosticsForm == null || diagnosticsForm.IsDisposed)
            {
                diagnosticsForm = new DiagnosticsForm(this);
            }
            if (!diagnosticsForm.Visible)
            {
                diagnosticsForm.Show();
            }
            diagnosticsForm.Activate();
            diagnosticsForm.BringToFront();
        }


        internal void OpenLogFile()
        {
            try
            {
                AppLog.Write("log opened from tray");
                ProcessStartInfo start = new ProcessStartInfo(AppLog.FilePath);
                start.UseShellExecute = true;
                Process.Start(start);
            }
            catch (Exception ex)
            {
                AppLog.Write("open log failed: " + ex.Message);
                try
                {
                    Process.Start("notepad.exe", AppLog.FilePath);
                }
                catch (Exception inner)
                {
                    MessageBox.Show(
                        "无法打开日志：" + inner.Message + Environment.NewLine + AppLog.FilePath,
                        AppInfo.DisplayName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        internal string BuildDiagnostics()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(AppInfo.DisplayName + " 诊断信息");
            text.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine("Windows: " + GetWindowsDescription());
            text.AppendLine("系统架构: " + (Environment.Is64BitOperatingSystem ? "x64" : "x86"));
            text.AppendLine("程序架构: " + (Environment.Is64BitProcess ? "x64" : "x86"));
            text.AppendLine("程序路径: " + Application.ExecutablePath);
            text.AppendLine("配置目录: " + SettingsStore.FolderPath);
            text.AppendLine("日志文件: " + AppLog.FilePath);
            text.AppendLine();

            text.AppendLine("[运行状态]");
            text.AppendLine("连接方式: " + runtime.ConnectionMode);
            text.AppendLine("当前链路: " + (string.IsNullOrEmpty(runtime.ActiveLink) ? "未连接" : runtime.ActiveLink));
            text.AppendLine("输入模式: " + (runtime.MouseMode ? "鼠标" : "Windows Ink 数位板"));
            text.AppendLine("暂停: " + (runtime.Paused ? "是" : "否"));
            text.AppendLine("旋转: " + runtime.Rotate + "°");
            text.AppendLine("屏幕: " + (runtime.ScreenIndex < 0 ? "全部屏幕" : "显示器 " + (runtime.ScreenIndex + 1)));
            text.AppendLine(string.Format(
                "力度: 原始 {0}-{1}, gamma {2:0.00}, 死区 {3:0.00}, 平滑 {4:0.00}",
                runtime.PressureMin,
                runtime.PressureMax,
                runtime.PressureGamma,
                runtime.PressureDeadzone,
                runtime.PressureSmoothing));
            text.AppendLine(string.Format(
                "最近输入: {0}; 原始力度 {1} -> 输出 {2}; touch={3} hover={4}",
                FormatInputAge(runtime.LastInputAgeMs),
                runtime.LastRawPressure,
                runtime.LastMappedPressure,
                runtime.LastTouch,
                runtime.LastHover));
            text.AppendLine();

            text.AppendLine("[USB HID]");
            try
            {
                HidDevice[] usbDevices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
                if (usbDevices.Length == 0)
                {
                    text.AppendLine("未发现 VID_2914 PID_0100（未插线或固件未枚举）。");
                }
                else
                {
                    for (int i = 0; i < usbDevices.Length; i++)
                    {
                        text.AppendLine(usbDevices[i].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                text.AppendLine("USB 枚举失败: " + ex.Message);
            }
            text.AppendLine();

            text.AppendLine("[蓝牙]");
            text.AppendLine(GetBluetoothDiagnosticsText());
            text.AppendLine();

            text.AppendLine("[最近日志]");
            text.Append(AppLog.ReadTail(16000));
            return text.ToString();
        }

        private string GetBluetoothDiagnosticsText()
        {
            Task<string> task;
            lock (diagnosticsSync)
            {
                bool expired = bluetoothDiagnosticsTask != null &&
                    bluetoothDiagnosticsTask.IsCompleted &&
                    (DateTime.UtcNow - bluetoothDiagnosticsUtc).TotalSeconds >= 20.0;
                if (bluetoothDiagnosticsTask == null ||
                    bluetoothDiagnosticsTask.IsFaulted ||
                    bluetoothDiagnosticsTask.IsCanceled ||
                    expired)
                {
                    bluetoothDiagnosticsUtc = DateTime.UtcNow;
                    bluetoothDiagnosticsTask = Task.Factory.StartNew<string>(delegate() { return BuildBluetoothDiagnosticsText(); });
                }
                task = bluetoothDiagnosticsTask;
            }

            try
            {
                if (!task.Wait(5000))
                {
                    return "蓝牙枚举超时（系统蓝牙栈可能正忙）；数据会在后台继续查询，稍后刷新即可。";
                }
                return task.Result;
            }
            catch (AggregateException ex)
            {
                Exception inner = ex.InnerException == null ? ex : ex.InnerException;
                return "蓝牙枚举失败: " + inner.Message;
            }
        }

        private static string BuildBluetoothDiagnosticsText()
        {
            StringBuilder text = new StringBuilder();
            try
            {
                BluetoothDeviceInfo[] devices = BluetoothDiscovery.FindDevices(false);
                if (devices.Length == 0)
                {
                    text.AppendLine("未发现已配对/已连接的蓝牙设备。");
                }
                else
                {
                    for (int i = 0; i < devices.Length; i++)
                    {
                        BluetoothDeviceInfo device = devices[i];
                        text.AppendLine(device.ToString() + (device.IsLikelyBoogie ? "  [likely Boogie]" : ""));
                    }
                }
            }
            catch (Exception ex)
            {
                text.AppendLine("蓝牙枚举失败: " + ex.Message);
            }
            return text.ToString().TrimEnd();
        }

        private static string GetWindowsDescription()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key != null)
                    {
                        string product = key.GetValue("ProductName") as string;
                        string display = key.GetValue("DisplayVersion") as string;
                        string buildText = key.GetValue("CurrentBuildNumber") as string;
                        object ubrValue = key.GetValue("UBR");
                        int buildNumber = 0;
                        int.TryParse(buildText, out buildNumber);
                        if (buildNumber >= 22000 && !string.IsNullOrEmpty(product))
                        {
                            product = product.Replace("Windows 10", "Windows 11");
                        }
                        string ubr = ubrValue == null ? "" : "." + ubrValue;
                        return string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} {1} (build {2}{3})",
                            product,
                            display,
                            buildText,
                            ubr).Trim();
                    }
                }
            }
            catch (Exception)
            {
            }
            return Environment.OSVersion.VersionString;
        }

        private static string FormatInputAge(int ageMs)
        {
            if (ageMs == int.MaxValue)
            {
                return "尚无输入";
            }
            if (ageMs < 1000)
            {
                return ageMs + " ms 前";
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} 秒前", ageMs / 1000.0);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume)
            {
                return;
            }
            AppLog.Write("system resumed; refreshing transport");
            mappingRefreshPending = true;
            Program.RequestTransportRefresh();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason != SessionSwitchReason.SessionUnlock)
            {
                return;
            }
            AppLog.Write("session unlocked; refreshing transport");
            mappingRefreshPending = true;
            Program.RequestTransportRefresh();
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            mappingRefreshPending = true;
            AppLog.Write("display settings changed; mapping refresh queued");
        }

        private void RefreshPendingMapping()
        {
            if (!mappingRefreshPending)
            {
                return;
            }
            mappingRefreshPending = false;
            try
            {
                runtime.RefreshMapping();
                AppLog.Write("display mapping refreshed");
                UpdateUi();
            }
            catch (Exception ex)
            {
                AppLog.Write("display mapping refresh failed: " + ex.Message);
            }
        }

        private void ExitApplication()
        {
            Shutdown();
            ExitThread();
        }

        private void Shutdown()
        {
            if (shuttingDown)
            {
                return;
            }
            shuttingDown = true;
            AppLog.Write("tray stopping");
            try
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            }
            catch (Exception)
            {
            }
            Program.RequestStop();
            if (driverThread != null)
            {
                driverThread.Join(2000);
            }
            if (statusTimer != null)
            {
                statusTimer.Stop();
                statusTimer.Dispose();
            }
            if (hotkeys != null)
            {
                hotkeys.Close();
            }
            if (icon != null)
            {
                icon.Visible = false;
                icon.Dispose();
            }
            if (startupShowTimer != null)
            {
                startupShowTimer.Stop();
                startupShowTimer.Dispose();
                startupShowTimer = null;
            }
            if (diagnosticsForm != null)
            {
                diagnosticsForm.Hide();
                diagnosticsForm.Dispose();
                diagnosticsForm = null;
            }
            if (settingsForm != null)
            {
                settingsForm.Hide();
                settingsForm.Dispose();
                settingsForm = null;
            }
            if (trayIcon != null)
            {
                trayIcon.Dispose();
            }
        }

        protected override void ExitThreadCore()
        {
            Shutdown();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Shutdown();
                if (menu != null)
                {
                    menu.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private sealed class HotkeyWindow : NativeWindow
        {
            private readonly TrayContext owner;

            public HotkeyWindow(TrayContext owner)
            {
                this.owner = owner;
                CreateParams parameters = new CreateParams();
                parameters.Caption = "BoogieBoardSyncHotkeys";
                CreateHandle(parameters);

                uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
                bool p = NativeMethods.RegisterHotKey(Handle, 1, modifiers, (uint)Keys.P);
                bool m = NativeMethods.RegisterHotKey(Handle, 2, modifiers, (uint)Keys.M);
                bool r = NativeMethods.RegisterHotKey(Handle, 3, modifiers, (uint)Keys.R);
                bool s = NativeMethods.RegisterHotKey(Handle, 4, modifiers, (uint)Keys.S);
                AppLog.Write(string.Format("hotkeys registered p={0} m={1} r={2} s={3} handle={4}", p, m, r, s, Handle));
            }

            public void Close()
            {
                if (Handle != IntPtr.Zero)
                {
                    NativeMethods.UnregisterHotKey(Handle, 1);
                    NativeMethods.UnregisterHotKey(Handle, 2);
                    NativeMethods.UnregisterHotKey(Handle, 3);
                    NativeMethods.UnregisterHotKey(Handle, 4);
                    DestroyHandle();
                }
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == NativeMethods.WM_SHOW_SETTINGS)
                {
                    owner.ShowSettings();
                    return;
                }
                if (message.Msg == NativeMethods.WM_HOTKEY)
                {
                    owner.HandleHotkey(message.WParam.ToInt32());
                    return;
                }
                base.WndProc(ref message);
            }
        }
    }
}
