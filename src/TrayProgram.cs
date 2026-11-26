using System;
using System.Threading;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal static class TrayProgram
    {
        private static bool ShowExistingSettings(int tabIndex)
        {
            IntPtr wparam = new IntPtr(tabIndex + 1);
            for (int i = 0; i < 20; i++)
            {
                IntPtr window = NativeMethods.FindWindow(null, "BoogieBoardSyncHotkeys");
                if (window != IntPtr.Zero)
                {
                    NativeMethods.PostMessage(window, NativeMethods.WM_SHOW_SETTINGS, wparam, IntPtr.Zero);
                    return true;
                }
                Thread.Sleep(100);
            }

            IntPtr broadcast = new IntPtr(0xFFFF);
            NativeMethods.PostMessage(broadcast, NativeMethods.WM_SHOW_SETTINGS, wparam, IntPtr.Zero);
            Thread.Sleep(250);
            return NativeMethods.FindWindow(null, "BoogieBoardSyncHotkeys") != IntPtr.Zero;
        }

        [STAThread]
        public static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "BoogieBoardSync", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\BoogieBoardSyncTray", out createdNew))
            {
                if (!createdNew)
                {
                    if (!ShowExistingSettings(options.SettingsTab))
                    {
                        MessageBox.Show(
                            AppInfo.DisplayName + " 托盘程序已经在运行，但设置窗口暂时无法唤醒。" + Environment.NewLine +
                            "请右键单击系统托盘中的程序图标，选择“打开设置窗口”，或者按 Ctrl+Alt+S。",
                            AppInfo.DisplayName,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    return 0;
                }

                Program.EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (TrayContext context = new TrayContext(options))
                {
                    Application.Run(context);
                }

                mutex.ReleaseMutex();
                return 0;
            }
        }
    }
}
