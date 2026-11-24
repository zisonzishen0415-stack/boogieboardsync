using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BoogieBoardSync
{
    internal static class AppLog
    {
        private static readonly object sync = new object();

        public static string FilePath
        {
            get { return Path.Combine(SettingsStore.FolderPath, "boogieboardsync.log"); }
        }

        public static void Write(string message)
        {
            lock (sync)
            {
                try
                {
                    Directory.CreateDirectory(SettingsStore.FolderPath);
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                        + " " + message + Environment.NewLine;
                    File.AppendAllText(FilePath, line, Encoding.UTF8);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
