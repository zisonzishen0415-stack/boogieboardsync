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

        private static void RotateIfNeeded()
        {
            try
            {
                FileInfo info = new FileInfo(FilePath);
                if (!info.Exists || info.Length < 1024 * 1024)
                {
                    return;
                }
                string oldPath = FilePath + ".old";
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
                File.Move(FilePath, oldPath);
            }
            catch (Exception)
            {
            }
        }
        public static string ReadTail(int maxChars)
        {
            lock (sync)
            {
                try
                {
                    if (!File.Exists(FilePath))
                    {
                        return "(日志尚未创建)";
                    }
                    using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long length = stream.Length;
                        long offset = Math.Max(0, length - Math.Max(1, maxChars));
                        stream.Seek(offset, SeekOrigin.Begin);
                        byte[] buffer = new byte[length - offset];
                        int total = 0;
                        while (total < buffer.Length)
                        {
                            int read = stream.Read(buffer, total, buffer.Length - total);
                            if (read <= 0)
                            {
                                break;
                            }
                            total += read;
                        }
                        string text = Encoding.UTF8.GetString(buffer, 0, total).TrimStart('\uFEFF');
                        if (offset > 0)
                        {
                            text = "...(更早的日志已省略)" + Environment.NewLine + text;
                        }
                        return text;
                    }
                }
                catch (Exception ex)
                {
                    return "(无法读取日志: " + ex.Message + ")";
                }
            }
        }

        public static void Write(string message)
        {
            lock (sync)
            {
                try
                {
                    Directory.CreateDirectory(SettingsStore.FolderPath);
                    RotateIfNeeded();
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
