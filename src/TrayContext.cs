using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

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
        private System.Windows.Forms.Timer statusTimer;
        private bool shuttingDown;

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
            reconnectItem.Click += delegate(object sender, EventArgs e) { Program.RequestTransportRefresh(); };
            menu.Items.Add(reconnectItem);

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
            statusTimer.Tick += delegate(object sender, EventArgs e) { EnsureDriverThread(); UpdateUi(); };
            statusTimer.Start();
            AppLog.Write("tray started");
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
