using System;
using System.Threading;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal static class TrayProgram
    {
        private static bool ShowExistingSettings()
        {
            for (int i = 0; i < 20; i++)
            {
                IntPtr window = NativeMethods.FindWindow(null, "BoogieBoardSyncHotkeys");
                if (window != IntPtr.Zero)
                {
                    NativeMethods.PostMessage(window, NativeMethods.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
                Thread.Sleep(100);
            }
            return false;
        }

        [STAThread]
        public static int Main(string[] args)
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\BoogieBoardSyncTray", out createdNew))
            {
                if (!createdNew)
                {
                    if (!ShowExistingSettings())
                    {
                        MessageBox.Show(
                            "BoogieBoardSync 托盘程序已经在运行，但没有找到设置窗口。",
                            "BoogieBoardSync",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    return 0;
                }

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
