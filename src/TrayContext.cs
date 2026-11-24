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
        private readonly Thread driverThread;
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
        private bool shuttingDown;

        public TrayContext(Options options)
        {
            this.options = options;
            runtime = new DriverRuntime(options);
            try
            {
                SettingsStore.Load(runtime);
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

            pauseItem = new ToolStripMenuItem("暂停输入  (Ctrl+Alt+P)");
            pauseItem.Click += delegate(object sender, EventArgs e) { TogglePause(); };
            menu.Items.Add(pauseItem);

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
            icon.DoubleClick += delegate(object sender, EventArgs e) { TogglePause(); };
            icon.Visible = true;

            hotkeys = new HotkeyWindow(this);
            UpdateUi();

            driverThread = new Thread(delegate() { Program.RunDriver(options, runtime); });
            driverThread.IsBackground = true;
            driverThread.Name = "BoogieBoardSync driver";
            driverThread.Start();
            AppLog.Write("tray started; driver thread=started");
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
                    "settings saved; mouse={0} rotate={1} screen={2} gamma={3}",
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
            statusItem.Text = "状态：" + state;
            if (icon != null)
            {
                icon.Text = "BoogieBoardSync - " + state;
            }
        }

        private static int NextRotation(int current)
        {
            if (current == 0) return 90;
            if (current == 90) return 180;
            if (current == 180) return 270;
            return 0;
        }

        private void OpenSettingsFolder()
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
            if (hotkeys != null)
            {
                hotkeys.Close();
            }
            if (icon != null)
            {
                icon.Visible = false;
                icon.Dispose();
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
                AppLog.Write(string.Format("hotkeys registered p={0} m={1} r={2} handle={3}", p, m, r, Handle));
            }

            public void Close()
            {
                if (Handle != IntPtr.Zero)
                {
                    NativeMethods.UnregisterHotKey(Handle, 1);
                    NativeMethods.UnregisterHotKey(Handle, 2);
                    NativeMethods.UnregisterHotKey(Handle, 3);
                    DestroyHandle();
                }
            }

            protected override void WndProc(ref Message message)
            {
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
