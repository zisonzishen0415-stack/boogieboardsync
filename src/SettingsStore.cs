using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace BoogieBoardSync
{
    internal static class SettingsStore
    {
        private const string AutoStartValueName = "BoogieBoardSync";
        private const string AutoStartRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string FolderPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BoogieBoardSync");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.ini"); }
        }

        public static void Load(DriverRuntime runtime)
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(FilePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }
                values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }

            string value;
            if (values.TryGetValue("mouse", out value))
            {
                runtime.SetMouseMode(ParseBool(value, runtime.MouseMode));
            }
            if (values.TryGetValue("connection", out value))
            {
                runtime.SetConnectionMode(value);
            }
            if (values.TryGetValue("rotate", out value))
            {
                runtime.SetRotate(ParseInt(value, runtime.Rotate));
            }
            if (values.TryGetValue("screen", out value))
            {
                runtime.SetScreenIndex(ParseInt(value, -1));
            }
            bool flipX = runtime.FlipX;
            bool flipY = runtime.FlipY;
            if (values.TryGetValue("flipx", out value))
            {
                flipX = ParseBool(value, flipX);
            }
            if (values.TryGetValue("flipy", out value))
            {
                flipY = ParseBool(value, flipY);
            }
            runtime.SetFlip(flipX, flipY);

            int mapX = runtime.MapX;
            int mapY = runtime.MapY;
            int mapWidth = runtime.MapWidth;
            int mapHeight = runtime.MapHeight;
            if (values.TryGetValue("mapx", out value))
            {
                mapX = ParseInt(value, mapX);
            }
            if (values.TryGetValue("mapy", out value))
            {
                mapY = ParseInt(value, mapY);
            }
            if (values.TryGetValue("mapwidth", out value))
            {
                mapWidth = ParseInt(value, mapWidth);
            }
            if (values.TryGetValue("mapheight", out value))
            {
                mapHeight = ParseInt(value, mapHeight);
            }
            runtime.SetMapRegion(mapX, mapY, mapWidth, mapHeight);
            if (values.TryGetValue("mapfit", out value))
            {
                runtime.SetMapFit(value);
            }

            int rawMinX = runtime.RawMinX;
            int rawMaxX = runtime.RawMaxX;
            int rawMinY = runtime.RawMinY;
            int rawMaxY = runtime.RawMaxY;
            if (values.TryGetValue("rawminx", out value))
            {
                rawMinX = ParseInt(value, rawMinX);
            }
            if (values.TryGetValue("rawmaxx", out value))
            {
                rawMaxX = ParseInt(value, rawMaxX);
            }
            if (values.TryGetValue("rawminy", out value))
            {
                rawMinY = ParseInt(value, rawMinY);
            }
            if (values.TryGetValue("rawmaxy", out value))
            {
                rawMaxY = ParseInt(value, rawMaxY);
            }
            runtime.SetRawRange(rawMinX, rawMaxX, rawMinY, rawMaxY);

            int min = runtime.PressureMin;
            int max = runtime.PressureMax;
            double gamma = runtime.PressureGamma;
            double deadzone = runtime.PressureDeadzone;
            double smoothing = runtime.PressureSmoothing;
            if (values.TryGetValue("pressuremin", out value))
            {
                min = ParseInt(value, min);
            }
            if (values.TryGetValue("pressuremax", out value))
            {
                max = ParseInt(value, max);
            }
            if (values.TryGetValue("pressuregamma", out value))
            {
                gamma = ParseDouble(value, gamma);
            }
            if (values.TryGetValue("pressuredeadzone", out value))
            {
                deadzone = ParseDouble(value, deadzone);
            }
            if (values.TryGetValue("pressuresmoothing", out value))
            {
                smoothing = ParseDouble(value, smoothing);
            }
            runtime.SetPressure(min, max, gamma, deadzone, smoothing);
        }

        public static void Save(DriverRuntime runtime)
        {
            Directory.CreateDirectory(FolderPath);
            StringBuilder text = new StringBuilder();
            text.AppendLine("# BoogieBoardSync settings");
            text.AppendLine("mouse=" + runtime.MouseMode.ToString().ToLowerInvariant());
            text.AppendLine("connection=" + runtime.ConnectionMode);
            text.AppendLine("rotate=" + runtime.Rotate.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("screen=" + runtime.ScreenIndex.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("flipx=" + runtime.FlipX.ToString().ToLowerInvariant());
            text.AppendLine("flipy=" + runtime.FlipY.ToString().ToLowerInvariant());
            text.AppendLine("mapx=" + runtime.MapX.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("mapy=" + runtime.MapY.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("mapwidth=" + runtime.MapWidth.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("mapheight=" + runtime.MapHeight.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("mapfit=" + runtime.MapFit);
            text.AppendLine("rawminx=" + runtime.RawMinX.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("rawmaxx=" + runtime.RawMaxX.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("rawminy=" + runtime.RawMinY.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("rawmaxy=" + runtime.RawMaxY.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("pressuremin=" + runtime.PressureMin.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("pressuremax=" + runtime.PressureMax.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("pressuregamma=" + runtime.PressureGamma.ToString("0.###", CultureInfo.InvariantCulture));
            text.AppendLine("pressuredeadzone=" + runtime.PressureDeadzone.ToString("0.###", CultureInfo.InvariantCulture));
            text.AppendLine("pressuresmoothing=" + runtime.PressureSmoothing.ToString("0.###", CultureInfo.InvariantCulture));
            File.WriteAllText(FilePath, text.ToString(), Encoding.UTF8);
        }

        public static bool IsAutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutoStartRunKey, false))
            {
                if (key == null)
                {
                    return false;
                }
                return key.GetValue(AutoStartValueName) != null;
            }
        }

        public static void SetAutoStart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AutoStartRunKey))
            {
                if (key == null)
                {
                    return;
                }
                if (enabled)
                {
                    key.SetValue(AutoStartValueName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                }
                else
                {
                    key.DeleteValue(AutoStartValueName, false);
                }
            }
        }

        private static bool ParseBool(string text, bool fallback)
        {
            bool value;
            return bool.TryParse(text, out value) ? value : fallback;
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static double ParseDouble(string text, double fallback)
        {
            double value;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
