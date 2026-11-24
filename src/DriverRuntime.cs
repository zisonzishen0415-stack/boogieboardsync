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
        private int pressureMin;
        private int pressureMax;
        private double pressureGamma;
        private double pressureDeadzone;
        private double pressureSmoothing;
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

        public void ReportInput(int rawPressure, int mappedPressure, bool touch, bool hover)
        {
            lock (sync)
            {
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

            Options mapping = new Options();
            mapping.RawMinX = options.RawMinX;
            mapping.RawMaxX = options.RawMaxX;
            mapping.RawMinY = options.RawMinY;
            mapping.RawMaxY = options.RawMaxY;
            mapping.Rotate = rotate;
            mapping.FlipX = flipX;
            mapping.FlipY = flipY;

            TabletMapper mapper = new TabletMapper(mapping, left, top, width, height);
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
