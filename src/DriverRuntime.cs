using System;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class DriverRuntime
    {
        internal sealed class Snapshot
        {
            public readonly TabletMapper Mapper;
            public readonly PressureMapper Pressure;
            public readonly bool MouseMode;
            public readonly bool Paused;

            public Snapshot(TabletMapper mapper, PressureMapper pressure, bool mouseMode, bool paused)
            {
                Mapper = mapper;
                Pressure = pressure;
                MouseMode = mouseMode;
                Paused = paused;
            }
        }

        private readonly object sync = new object();
        private readonly Options options;
        private volatile Snapshot current;
        private bool mouseMode;
        private bool paused;
        private string connection = "auto";
        private string activeLink = "";
        private int rotate;
        private int screenIndex;
        private bool flipX;
        private bool flipY;
        private int mapX;
        private int mapY;
        private int mapWidth;
        private int mapHeight;
        private string mapFit = "stretch";
        private int rawMinX;
        private int rawMaxX;
        private int rawMinY;
        private int rawMaxY;
        private int pressureMin;
        private int pressureMax;
        private double pressureGamma;
        private double pressureDeadzone;
        private double pressureSmoothing;
        private int lastRawX = -1;
        private int lastRawY = -1;
        private int lastRawPressure;
        private int lastMappedPressure;
        private bool lastTouch;
        private bool lastHover;
        private DateTime lastInputUtc = DateTime.MinValue;

        public DriverRuntime(Options options)
        {
            this.options = options;
            mouseMode = options.Mouse;
            paused = false;
            connection = NormalizeConnection(options.Connection);
            rotate = options.Rotate;
            screenIndex = options.ScreenIndex;
            flipX = options.FlipX;
            flipY = options.FlipY;
            mapX = Math.Max(0, options.MapX);
            mapY = Math.Max(0, options.MapY);
            mapWidth = Math.Max(0, options.MapWidth);
            mapHeight = Math.Max(0, options.MapHeight);
            mapFit = NormalizeMapFit(options.MapFit);
            rawMinX = Math.Max(0, options.RawMinX);
            rawMaxX = Math.Max(rawMinX + 1, options.RawMaxX);
            rawMinY = Math.Max(0, options.RawMinY);
            rawMaxY = Math.Max(rawMinY + 1, options.RawMaxY);
            pressureMin = options.PressureMin;
            pressureMax = options.PressureMax;
            pressureGamma = options.PressureGamma;
            pressureDeadzone = options.PressureDeadzone;
            pressureSmoothing = options.PressureSmoothing;
            Rebuild();
        }

        public Snapshot Current
        {
            get { return current; }
        }

        public bool MouseMode
        {
            get { lock (sync) { return mouseMode; } }
        }

        public bool Paused
        {
            get { lock (sync) { return paused; } }
        }

        public string ConnectionMode
        {
            get { lock (sync) { return connection; } }
        }

        public string ActiveLink
        {
            get { lock (sync) { return activeLink; } }
        }

        public int Rotate
        {
            get { lock (sync) { return rotate; } }
        }

        public int ScreenIndex
        {
            get { lock (sync) { return screenIndex; } }
        }

        public bool FlipX
        {
            get { lock (sync) { return flipX; } }
        }

        public bool FlipY
        {
            get { lock (sync) { return flipY; } }
        }

        public int MapX
        {
            get { lock (sync) { return mapX; } }
        }

        public int MapY
        {
            get { lock (sync) { return mapY; } }
        }

        public int MapWidth
        {
            get { lock (sync) { return mapWidth; } }
        }

        public int MapHeight
        {
            get { lock (sync) { return mapHeight; } }
        }

        public string MapFit
        {
            get { lock (sync) { return mapFit; } }
        }

        public int RawMinX
        {
            get { lock (sync) { return rawMinX; } }
        }

        public int RawMaxX
        {
            get { lock (sync) { return rawMaxX; } }
        }

        public int RawMinY
        {
            get { lock (sync) { return rawMinY; } }
        }

        public int RawMaxY
        {
            get { lock (sync) { return rawMaxY; } }
        }

        public int PressureMin
        {
            get { lock (sync) { return pressureMin; } }
        }

        public int PressureMax
        {
            get { lock (sync) { return pressureMax; } }
        }

        public double PressureGamma
        {
            get { lock (sync) { return pressureGamma; } }
        }

        public double PressureDeadzone
        {
            get { lock (sync) { return pressureDeadzone; } }
        }

        public double PressureSmoothing
        {
            get { lock (sync) { return pressureSmoothing; } }
        }

        public int LastRawPressure
        {
            get { lock (sync) { return lastRawPressure; } }
        }

        public int LastRawX
        {
            get { lock (sync) { return lastRawX; } }
        }

        public int LastRawY
        {
            get { lock (sync) { return lastRawY; } }
        }

        public int LastMappedPressure
        {
            get { lock (sync) { return lastMappedPressure; } }
        }

        public bool LastTouch
        {
            get { lock (sync) { return lastTouch; } }
        }

        public bool LastHover
        {
            get { lock (sync) { return lastHover; } }
        }

        public int LastInputAgeMs
        {
            get
            {
                lock (sync)
                {
                    if (lastInputUtc == DateTime.MinValue)
                    {
                        return int.MaxValue;
                    }
                    double ms = (DateTime.UtcNow - lastInputUtc).TotalMilliseconds;
                    if (ms > int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                    return (int)ms;
                }
            }
        }

        public void ReportInput(int rawX, int rawY, int rawPressure, int mappedPressure, bool touch, bool hover)
        {
            lock (sync)
            {
                lastRawX = rawX;
                lastRawY = rawY;
                lastRawPressure = rawPressure;
                lastMappedPressure = mappedPressure;
                lastTouch = touch;
                lastHover = hover;
                lastInputUtc = DateTime.UtcNow;
            }
        }

        public void SetMouseMode(bool value)
        {
            lock (sync)
            {
                if (mouseMode == value)
                {
                    return;
                }
                mouseMode = value;
                RebuildLocked();
            }
        }

        public void SetPaused(bool value)
        {
            lock (sync)
            {
                if (paused == value)
                {
                    return;
                }
                paused = value;
                RebuildLocked();
            }
        }

        public void SetConnectionMode(string value)
        {
            lock (sync)
            {
                connection = NormalizeConnection(value);
            }
        }

        public void SetActiveLink(string value)
        {
            lock (sync)
            {
                activeLink = value ?? "";
            }
        }

        public static string NormalizeConnection(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "auto";
            }
            value = value.ToLowerInvariant();
            if (value == "usb" || value == "serial" || value == "wired")
            {
                return "usb";
            }
            if (value == "bluetooth" || value == "bt" || value == "rfcomm")
            {
                return "bluetooth";
            }
            return "auto";
        }

        public void SetRotate(int value)
        {
            if (value != 0 && value != 90 && value != 180 && value != 270)
            {
                return;
            }
            lock (sync)
            {
                rotate = value;
                RebuildLocked();
            }
        }

        public void SetScreenIndex(int value)
        {
            lock (sync)
            {
                Screen[] screens = Screen.AllScreens;
                if (value < -1 || value >= screens.Length)
                {
                    value = -1;
                }
                screenIndex = value;
                RebuildLocked();
            }
        }

        public void SetFlip(bool x, bool y)
        {
            lock (sync)
            {
                flipX = x;
                flipY = y;
                RebuildLocked();
            }
        }

        public void SetMapRegion(int x, int y, int width, int height)
        {
            lock (sync)
            {
                mapX = Math.Max(0, x);
                mapY = Math.Max(0, y);
                mapWidth = Math.Max(0, width);
                mapHeight = Math.Max(0, height);
                RebuildLocked();
            }
        }

        public void SetMapFit(string fit)
        {
            lock (sync)
            {
                mapFit = NormalizeMapFit(fit);
                RebuildLocked();
            }
        }

        public void SetRawRange(int minX, int maxX, int minY, int maxY)
        {
            lock (sync)
            {
                rawMinX = Math.Max(0, minX);
                rawMaxX = Math.Max(rawMinX + 1, maxX);
                rawMinY = Math.Max(0, minY);
                rawMaxY = Math.Max(rawMinY + 1, maxY);
                RebuildLocked();
            }
        }

        public static string NormalizeMapFit(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "stretch";
            }
            value = value.ToLowerInvariant();
            if (value == "uniform" || value == "equal" || value == "fit" || value == "aspect")
            {
                return "uniform";
            }
            if (value == "fill" || value == "cover" || value == "crop")
            {
                return "fill";
            }
            return "stretch";
        }

        public static string DescribeFit(string fit)
        {
            if (fit == "uniform")
            {
                return "等比适应";
            }
            if (fit == "fill")
            {
                return "等比填充";
            }
            return "拉伸";
        }

        public void SetPressure(int min, int max, double gamma, double deadzone, double smoothing)
        {
            lock (sync)
            {
                pressureMin = Math.Max(0, min);
                pressureMax = Math.Max(pressureMin + 1, max);
                pressureGamma = Clamp(gamma, 0.1, 4.0);
                pressureDeadzone = Clamp(deadzone, 0.0, 0.95);
                pressureSmoothing = Clamp(smoothing, 0.0, 0.95);
                RebuildLocked();
            }
        }

        public void RefreshMapping()
        {
            lock (sync)
            {
                RebuildLocked();
            }
        }

        public void SetPressurePreset(double gamma)
        {
            lock (sync)
            {
                pressureGamma = Clamp(gamma, 0.1, 4.0);
                RebuildLocked();
            }
        }

        private void Rebuild()
        {
            lock (sync)
            {
                RebuildLocked();
            }
        }

        private void RebuildLocked()
        {
            int left;
            int top;
            int width;
            int height;
            GetScreenBounds(screenIndex, out left, out top, out width, out height);

            int regionLeft = left;
            int regionTop = top;
            int regionWidth = width;
            int regionHeight = height;
            if (mapWidth > 0 && mapHeight > 0)
            {
                int x = Math.Min(mapX, Math.Max(0, width - 1));
                int y = Math.Min(mapY, Math.Max(0, height - 1));
                int w = Math.Min(mapWidth, width - x);
                int h = Math.Min(mapHeight, height - y);
                if (w > 0 && h > 0)
                {
                    regionLeft = left + x;
                    regionTop = top + y;
                    regionWidth = w;
                    regionHeight = h;
                }
            }

            TabletMapper mapper = new TabletMapper(
                rawMinX,
                rawMaxX,
                rawMinY,
                rawMaxY,
                rotate,
                flipX,
                flipY,
                regionLeft,
                regionTop,
                regionWidth,
                regionHeight,
                mapFit);
            PressureMapper pressure = new PressureMapper(
                pressureMin,
                pressureMax,
                pressureGamma,
                pressureDeadzone,
                pressureSmoothing);
            current = new Snapshot(mapper, pressure, mouseMode, paused);
        }

        private static void GetScreenBounds(int index, out int left, out int top, out int width, out int height)
        {
            Screen[] screens = Screen.AllScreens;
            if (index >= 0 && index < screens.Length)
            {
                left = screens[index].Bounds.Left;
                top = screens[index].Bounds.Top;
                width = screens[index].Bounds.Width;
                height = screens[index].Bounds.Height;
                return;
            }

            System.Drawing.Rectangle bounds = SystemInformation.VirtualScreen;
            left = bounds.Left;
            top = bounds.Top;
            width = bounds.Width;
            height = bounds.Height;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}
