using System;
using System.Threading;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal static class TrayProgram
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\BoogieBoardSyncTray", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "BoogieBoardSync 托盘程序已经在运行。",
                        "BoogieBoardSync",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
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
