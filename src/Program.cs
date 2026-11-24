using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace BoogieBoardSync
{
    internal sealed class Options
    {
        public bool Probe;
        public bool List;
        public bool Pointers;
        public bool PointerWatch;
        public bool Diag;
        public bool Caps;
        public bool Features;
        public bool Doctor;
        public bool SelfTest;
        public bool Verify;
        public bool Blind;
        public int SetMode = -1;
        public int Mode = 3;
        public int Retries = 80;
        public int RetryDelay = 60;
        public string EjectDrive = "";
        public bool AutoEject = true;
        public bool NoEject;
        public bool NoHandshake;
        public string ModeTransport = "auto";
        public int HostType = 8;
        public bool FeaturePad;
        public bool Test;
        public bool Mouse;
        public bool Debug;
        public bool Help;
        public bool NoTabletMode;
        public int Rotate;
        public bool FlipX;
        public bool FlipY;
        public int RawMinX = 0;
        public int RawMaxX = 20280;
        public int RawMinY = 0;
        public int RawMaxY = 13942;
        public int PressureMax = 1023;
        public int DurationSeconds = 15;
        public string Collection = "auto";

        public static Options Parse(string[] args)
        {
            Options options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--probe")
                {
                    options.Probe = true;
                }
                else if (arg == "--list")
                {
                    options.List = true;
                }
                else if (arg == "--pointers")
                {
                    options.Pointers = true;
                }
                else if (arg == "--pointer-watch")
                {
                    options.PointerWatch = true;
                }
                else if (arg == "--diag")
                {
                    options.Diag = true;
                }
                else if (arg == "--caps")
                {
                    options.Caps = true;
                }
                else if (arg == "--features")
                {
                    options.Features = true;
                }
                else if (arg.StartsWith("--set-mode=", StringComparison.OrdinalIgnoreCase))
                {
                    options.SetMode = ParseInt(Value(arg), -1);
                    if (!IsSupportedMode(options.SetMode))
                    {
                        throw new ArgumentException("--set-mode must be 1, 3, 4 or 5");
                    }
                }
                else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                {
                    options.Mode = ParseInt(Value(arg), options.Mode);
                    if (!IsSupportedMode(options.Mode))
                    {
                        throw new ArgumentException("--mode must be 1, 3, 4 or 5");
                    }
                }
                else if (arg.StartsWith("--eject=", StringComparison.OrdinalIgnoreCase))
                {
                    options.EjectDrive = Value(arg);
                }
                else if (arg == "--no-eject")
                {
                    options.NoEject = true;
                    options.AutoEject = false;
                }
                else if (arg == "--no-handshake")
                {
                    options.NoHandshake = true;
                }
                else if (arg.StartsWith("--transport=", StringComparison.OrdinalIgnoreCase))
                {
                    options.ModeTransport = Value(arg).ToLowerInvariant();
                }
                else if (arg.StartsWith("--retry-delay=", StringComparison.OrdinalIgnoreCase))
                {
                    options.RetryDelay = Math.Max(0, ParseInt(Value(arg), options.RetryDelay));
                }
                else if (arg.StartsWith("--host-type=", StringComparison.OrdinalIgnoreCase))
                {
                    options.HostType = Math.Max(0, Math.Min(255, ParseInt(Value(arg), options.HostType)));
                }
                else if (arg == "--feature-pad")
                {
                    options.FeaturePad = true;
                }
                else if (arg.StartsWith("--retries=", StringComparison.OrdinalIgnoreCase))
                {
                    options.Retries = Math.Max(1, ParseInt(Value(arg), options.Retries));
                }
                else if (arg == "--doctor")
                {
                    options.Doctor = true;
                }
                else if (arg == "--selftest")
                {
                    options.SelfTest = true;
                }
                else if (arg == "--verify")
                {
                    options.Verify = true;
                }
                else if (arg == "--blind")
                {
                    options.Blind = true;
                }
                else if (arg == "--test")
                {
                    options.Test = true;
                }
                else if (arg == "--mouse")
                {
                    options.Mouse = true;
                }
                else if (arg == "--debug")
                {
                    options.Debug = true;
                }
                else if (arg == "--help" || arg == "-h" || arg == "/?")
                {
                    options.Help = true;
                }
                else if (arg == "--no-tablet-mode")
                {
                    options.NoTabletMode = true;
                }
                else if (arg == "--flip-x")
                {
                    options.FlipX = true;
                }
                else if (arg == "--flip-y")
                {
                    options.FlipY = true;
                }
                else if (arg.StartsWith("--rotate=", StringComparison.OrdinalIgnoreCase))
                {
                    options.Rotate = ParseInt(Value(arg), 0);
                    if (options.Rotate != 0 && options.Rotate != 90 && options.Rotate != 180 && options.Rotate != 270)
                    {
                        throw new ArgumentException("--rotate must be 0, 90, 180 or 270");
                    }
                }
                else if (arg.StartsWith("--raw-max=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] pair = Value(arg).Split(new char[] { 'x', 'X', ',' });
                    if (pair.Length != 2)
                    {
                        throw new ArgumentException("--raw-max must be WIDTHxHEIGHT, for example 19780x13442");
                    }
                    options.RawMaxX = ParseInt(pair[0], options.RawMaxX);
                    options.RawMaxY = ParseInt(pair[1], options.RawMaxY);
                }
                else if (arg.StartsWith("--raw-min=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] pair = Value(arg).Split(new char[] { 'x', 'X', ',' });
                    if (pair.Length != 2)
                    {
                        throw new ArgumentException("--raw-min must be XxY");
                    }
                    options.RawMinX = ParseInt(pair[0], options.RawMinX);
                    options.RawMinY = ParseInt(pair[1], options.RawMinY);
                }
                else if (arg.StartsWith("--pressure-max=", StringComparison.OrdinalIgnoreCase))
                {
                    options.PressureMax = ParseInt(Value(arg), options.PressureMax);
                    if (options.PressureMax <= 0)
                    {
                        throw new ArgumentException("--pressure-max must be greater than zero");
                    }
                }
                else if (arg.StartsWith("--duration=", StringComparison.OrdinalIgnoreCase))
                {
                    options.DurationSeconds = Math.Max(1, ParseInt(Value(arg), options.DurationSeconds));
                }
                else if (arg.StartsWith("--collection=", StringComparison.OrdinalIgnoreCase))
                {
                    options.Collection = Value(arg).ToLowerInvariant();
                }
                else
                {
                    throw new ArgumentException("Unknown option: " + arg);
                }
            }

            return options;
        }

        private static string Value(string arg)
        {
            int index = arg.IndexOf('=');
            return index < 0 ? "" : arg.Substring(index + 1);
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, out value) ? value : fallback;
        }

        internal static bool IsSupportedMode(int mode)
        {
            return mode == 1 || mode == 3 || mode == 4 || mode == 5;
        }
    }

    internal static class Program
    {
        private static volatile HidDevice currentDevice;
        private static volatile bool stopRequested;

        private static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                PrintUsage();
                return 1;
            }

            if (options.Help)
            {
                PrintUsage();
                return 0;
            }

            EnableDpiAwareness();

            if (options.List)
            {
                HidDevice.ListAll();
                return 0;
            }

            if (options.Pointers)
            {
                return RunPointerList();
            }

            if (options.PointerWatch)
            {
                return RunPointerWatch(options);
            }

            if (options.Diag)
            {
                return RunDiag();
            }

            if (options.Caps)
            {
                return RunCaps();
            }

            if (options.Features)
            {
                return RunFeatures();
            }

            if (options.Doctor)
            {
                return RunDoctor(options);
            }

            if (options.SelfTest)
            {
                return RunSelfTest();
            }

            if (options.Verify)
            {
                return RunVerify(options);
            }

            if (options.SetMode >= 0)
            {
                return RunSetMode(options.SetMode, options);
            }

            if (options.Test)
            {
                return RunInjectionTest(options);
            }

            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            if (devices.Length == 0)
            {
                Console.Error.WriteLine("Boogie Board Sync not found (VID_2914 PID_0100).");
                return 2;
            }

            Console.WriteLine("Found {0} HID collection(s):", devices.Length);
            foreach (HidDevice device in devices)
            {
                Console.WriteLine("  " + device);
            }

            if (options.Probe)
            {
                return RunProbe(devices, options);
            }

            return RunDriver(options);
        }

        private static int RunSetMode(int mode, Options options)
        {
            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            HidDevice device = SelectCollection(devices, "vendor");
            if (device == null)
            {
                Console.Error.WriteLine("Vendor collection not found.");
                return 2;
            }
            if (!device.Open(true))
            {
                Console.Error.WriteLine("Open failed: {0}", device.LastError);
                return 2;
            }

            try
            {
                bool ok = SetSyncMode(device, mode, options);
                if (!ok)
                {
                    Console.Error.WriteLine("Mode did not change to {0}; feature 5 reads {1}.", mode, ReadSyncModeRaw(device));
                    return 3;
                }
                Console.WriteLine("Sync mode is now {0}.", mode);
                return 0;
            }
            finally
            {
                device.Dispose();
            }
        }

        private static bool SetSyncMode(HidDevice device, int mode, Options options)
        {
            bool verbose = true;

            if (options.Blind)
            {
                Console.WriteLine("Blind mode: sending mode {0} without relying on feature 5 readback.", mode);
                return SendModeBlind(device, mode, options);
            }

            if (!string.IsNullOrEmpty(options.EjectDrive))
            {
                Console.WriteLine("Releasing drive {0}...", options.EjectDrive);
                StorageUtil.Release(options.EjectDrive, verbose);
                Thread.Sleep(500);
            }
            else if (options.AutoEject && !options.NoEject)
            {
                Console.WriteLine("Releasing Boogie Board storage so the firmware can leave file mode...");
                StorageUtil.ReleaseBoogieStorage(verbose);
                Thread.Sleep(500);
            }

            if (!options.NoHandshake)
            {
                SendHandshake(device, options, verbose);
            }

            int quickAttempts = Math.Max(2, Math.Min(6, options.Retries));
            if (TrySetMode(device, mode, quickAttempts, options, verbose, options.ModeTransport))
            {
                return true;
            }

            if (TrySetMode(device, mode, options.Retries, options, verbose, options.ModeTransport))
            {
                return true;
            }

            if (mode != 5)
            {
                Console.WriteLine("Direct change failed; trying MODE_FILE(5) then {0}.", mode);
                TrySetMode(device, 5, Math.Max(10, options.Retries / 2), options, verbose, options.ModeTransport);
                if (TrySetMode(device, mode, options.Retries, options, verbose, options.ModeTransport))
                {
                    return true;
                }
            }

            if (options.AutoEject && !options.NoEject)
            {
                Console.WriteLine("Releasing storage again and retrying...");
                StorageUtil.ReleaseBoogieStorage(verbose);
                Thread.Sleep(800);
                if (TrySetMode(device, mode, options.Retries, options, verbose, options.ModeTransport))
                {
                    return true;
                }
            }

            Console.WriteLine("Trying MODE_NONE(1) -> MODE_FILE(5) -> {0}.", mode);
            TrySetMode(device, 1, 8, options, verbose, options.ModeTransport);
            TrySetMode(device, 5, Math.Max(10, options.Retries / 2), options, verbose, options.ModeTransport);
            return TrySetMode(device, mode, options.Retries, options, verbose, options.ModeTransport);
        }

        private static void SendHandshake(HidDevice device, Options options, bool verbose)
        {
            DateTime now = DateTime.Now;
            int second = now.Second / 2;
            int minute = now.Minute;
            int hour = now.Hour;
            int day = now.Day;
            int month = now.Month;
            int year = now.Year - 1980;
            if (year < 0)
            {
                year = 0;
            }

            byte b1 = (byte)((minute << 5) | second);
            byte b2 = (byte)((hour << 3) | (minute >> 3));
            byte b3 = (byte)((month << 5) | day);
            byte b4 = (byte)((year << 1) | (month >> 3));

            byte[] date = new byte[] { 6, 6, 0, b1, b2, b3, b4 };
            bool dateOk = device.SendFeature(date, false);
            if (verbose)
            {
                Console.WriteLine("Handshake date: ok={0} method={1} err={2} hex={3}",
                    dateOk, device.LastFeatureMethod, device.LastError, Hex(date, 0, date.Length));
            }
            Thread.Sleep(options.RetryDelay);

            byte[] host = new byte[] { 8, 8, 0, (byte)options.HostType, 0, 0, 0 };
            bool hostOk = device.SendFeature(host, false);
            if (verbose)
            {
                Console.WriteLine("Handshake host type {0}: ok={1} method={2} err={3} hex={4}",
                    options.HostType, hostOk, device.LastFeatureMethod, device.LastError, Hex(host, 0, host.Length));
            }
            Thread.Sleep(options.RetryDelay);
        }

        private static bool SendModeBlind(HidDevice device, int mode, Options options)
        {
            string[] strategies = GetModeStrategies(options.ModeTransport);
            int attempts = Math.Max(5, Math.Min(options.Retries, 40));
            bool any = false;
            foreach (string strategy in strategies)
            {
                Console.WriteLine("Blind mode {0} via {1} ({2} attempts).", mode, strategy, attempts);
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    if (SendModeCommand(device, mode, strategy))
                    {
                        any = true;
                    }
                    if (attempt <= 2 || attempt % 20 == 0)
                    {
                        string method = strategy.StartsWith("output", StringComparison.Ordinal)
                            ? device.LastWriteMethod
                            : device.LastFeatureMethod;
                        Console.WriteLine("  blind {0} try {1}/{2}: method={3} err={4}",
                            strategy, attempt, attempts, method, device.LastError);
                    }
                    Thread.Sleep(options.RetryDelay);
                }
            }
            Console.WriteLine(any
                ? "Blind mode commands sent. Run --verify --duration=20 and draw on the board to confirm."
                : "Blind mode: every transport failed to send.");
            return any;
        }

        private static int RunVerify(Options options)
        {
            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            HidDevice device = SelectCollection(devices, "vendor");
            if (device == null)
            {
                Console.Error.WriteLine("Vendor collection not found.");
                return 2;
            }
            if (!device.Open(true))
            {
                Console.Error.WriteLine("Open failed: {0}", device.LastError);
                return 2;
            }

            try
            {
                bool confirmed = SetSyncMode(device, options.Mode, options);
                if (options.Blind)
                {
                    Console.WriteLine("Blind mode commands were sent for mode {0}; testing input reports.", options.Mode);
                }
                else
                {
                    Console.WriteLine(confirmed
                        ? "Mode {0} confirmed by feature 5."
                        : "Mode {0} not confirmed by feature 5; testing input reports anyway.", options.Mode);
                }
                Console.WriteLine("Draw on the board now for {0} seconds...", options.DurationSeconds);

                int reports;
                int parsed;
                int touches;
                CountReports(device, options.DurationSeconds, options.Debug, out reports, out parsed, out touches);
                Console.WriteLine("Input result: reports={0} parsed={1} touch={2}", reports, parsed, touches);

                if (parsed > 0)
                {
                    Console.WriteLine("VERIFY PASS: the device is sending pen reports.");
                    return 0;
                }

                Console.WriteLine("VERIFY FAIL: no pen reports arrived.");
                Console.WriteLine("Try: --verify --mode=4 --duration=30 (press Save or Erase on the board)");
                Console.WriteLine("Or:  --verify --blind --mode=3 --duration=30");
                return 3;
            }
            finally
            {
                device.Dispose();
            }
        }

        private static void CountReports(HidDevice device, int seconds, bool verbose, out int reports, out int parsed, out int touches)
        {
            reports = 0;
            parsed = 0;
            touches = 0;
            byte[] buffer = new byte[Math.Max(64, (int)device.InputReportByteLength)];
            StopFlag stop = new StopFlag();

            Thread timer = new Thread(delegate()
            {
                Thread.Sleep(Math.Max(1, seconds) * 1000);
                stop.Value = true;
                device.CancelRead();
            });
            timer.IsBackground = true;
            timer.Start();

            while (!stop.Value)
            {
                int count = device.Read(buffer);
                if (count < 0)
                {
                    break;
                }
                if (count == 0)
                {
                    continue;
                }

                reports++;
                int x;
                int y;
                int pressure;
                bool touch;
                bool hover;
                bool side;
                if (TryParseReport(buffer, count, out x, out y, out pressure, out touch, out hover, out side))
                {
                    parsed++;
                    if (touch)
                    {
                        touches++;
                    }
                    if (verbose)
                    {
                        Console.WriteLine("x={0} y={1} p={2} touch={3} hover={4} side={5}",
                            x, y, pressure, touch, hover, side);
                    }
                }
            }

            stop.Value = true;
            device.CancelRead();
        }

        private sealed class StopFlag
        {
            public volatile bool Value;
        }

        private static bool TrySetMode(HidDevice device, int mode, int retries, Options options, bool verbose, string transport)
        {
            int current = ReadSyncMode(device);
            if (current == mode)
            {
                if (verbose)
                {
                    Console.WriteLine("Mode already {0}.", mode);
                }
                return true;
            }

            string[] strategies = GetModeStrategies(transport);
            for (int s = 0; s < strategies.Length; s++)
            {
                string strategy = strategies[s];
                if (verbose)
                {
                    Console.WriteLine("Mode {0} via {1} (up to {2} attempts).", mode, strategy, retries);
                }

                for (int attempt = 1; attempt <= retries; attempt++)
                {
                    bool sent = SendModeCommand(device, mode, strategy);
                    Thread.Sleep(options.RetryDelay);
                    current = ReadSyncMode(device);

                    if (verbose && (attempt <= 2 || attempt % 20 == 0 || current == mode))
                    {
                        Console.WriteLine("  mode {0} try {1}/{2}: sent={3} strategy={4} current={5} err={6}",
                            mode, attempt, retries, sent, strategy, current, device.LastError);
                    }

                    if (current == mode)
                    {
                        if (verbose)
                        {
                            Console.WriteLine("Mode {0} accepted via {1} after {2} attempt(s).", mode, strategy, attempt);
                        }
                        return true;
                    }

                    Thread.Sleep(options.RetryDelay);
                }
            }

            return false;
        }

        private static string[] GetModeStrategies(string transport)
        {
            if (transport == "feature")
            {
                return new string[] { "feature-pad", "feature", "feature-ioctl" };
            }
            if (transport == "output")
            {
                return new string[] { "output", "output-write", "output-id", "output-pad" };
            }
            if (transport == "both")
            {
                return new string[] { "feature-pad", "feature", "feature-ioctl", "output", "output-write", "output-pad", "output-id" };
            }
            return new string[] { "feature-pad", "feature", "feature-ioctl", "output", "output-write", "output-pad", "output-id" };
        }

        private static bool SendModeCommand(HidDevice device, int mode, string strategy)
        {
            byte modeByte = (byte)mode;
            if (strategy == "feature")
            {
                return device.SendFeature(new byte[] { 5, 5, 0, modeByte }, false);
            }
            if (strategy == "feature-ioctl")
            {
                return device.SendFeatureIoctl(new byte[] { 5, 5, 0, modeByte });
            }
            if (strategy == "feature-pad")
            {
                int featureLength = device.FeatureReportByteLength;
                if (featureLength < 4)
                {
                    featureLength = 4;
                }
                byte[] feature = new byte[featureLength];
                feature[0] = 5;
                feature[1] = 5;
                feature[2] = 0;
                feature[3] = modeByte;
                return device.SendFeature(feature, false);
            }
            if (strategy == "output")
            {
                return device.SendOutput(new byte[] { 5, 0, modeByte });
            }
            if (strategy == "output-write")
            {
                return device.SendOutputWriteFile(new byte[] { 5, 0, modeByte });
            }
            if (strategy == "output-id")
            {
                return device.SendOutput(new byte[] { 5, 5, 0, modeByte });
            }
            if (strategy == "output-pad")
            {
                int length = device.OutputReportByteLength;
                if (length < 3)
                {
                    length = 3;
                }
                byte[] report = new byte[length];
                report[0] = 5;
                report[1] = 0;
                report[2] = modeByte;
                return device.SendOutput(report);
            }
            return false;
        }

        private static int ReadSyncMode(HidDevice device)
        {
            int[] lengths = new int[] { 7, 4, device.FeatureReportByteLength };
            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];
                if (length < 4)
                {
                    continue;
                }

                byte[] status = new byte[length];
                status[0] = 5;
                if (device.GetFeature(status))
                {
                    int mode = ParseSyncMode(status);
                    if (mode >= 0)
                    {
                        return mode;
                    }
                }
                Thread.Sleep(20);
            }
            return -1;
        }

        private static int ParseSyncMode(byte[] status)
        {
            if (status.Length >= 4 && status[0] == 5 && status[1] == 5)
            {
                return status[3];
            }
            if (status.Length >= 3 && status[0] == 5 && status[1] == 0)
            {
                return status[2];
            }
            if (status.Length >= 4)
            {
                return status[3];
            }
            if (status.Length >= 3)
            {
                return status[2];
            }
            return -1;
        }

        private static string ReadSyncModeRaw(HidDevice device)
        {
            int[] lengths = new int[] { 7, 4, device.FeatureReportByteLength };
            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];
                if (length < 4)
                {
                    continue;
                }
                byte[] status = new byte[length];
                status[0] = 5;
                bool ok = device.GetFeature(status);
                if (ok)
                {
                    return string.Format("ok=True len={0} hex={1}", length, Hex(status, 0, status.Length));
                }
            }
            return string.Format("ok=False err={0}", device.LastError);
        }

        private static int RunFeatures()
        {
            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            HidDevice device = SelectCollection(devices, "vendor");
            if (device == null)
            {
                Console.Error.WriteLine("Vendor collection not found.");
                return 2;
            }
            if (!device.Open(true))
            {
                Console.Error.WriteLine("Open failed: {0}", device.LastError);
                return 2;
            }

            try
            {
                Console.WriteLine("vendor caps: in={0} out={1} feature={2}",
                    device.InputReportByteLength,
                    device.OutputReportByteLength,
                    device.FeatureReportByteLength);
                int[] ids = new int[] { 1, 4, 5, 6, 7, 8 };
                for (int i = 0; i < ids.Length; i++)
                {
                    ReadFeatureAndPrint(device, ids[i]);
                }
            }
            finally
            {
                device.Dispose();
            }
            return 0;
        }

        private static void ReadFeatureAndPrint(HidDevice device, int id)
        {
            int[] lengths = new int[] { 7, device.FeatureReportByteLength, 4 };
            for (int i = 0; i < lengths.Length; i++)
            {
                int length = lengths[i];
                if (length <= 0)
                {
                    continue;
                }

                byte[] buffer = new byte[length];
                buffer[0] = (byte)id;
                bool ok = device.GetFeature(buffer);
                if (ok)
                {
                    Console.WriteLine("feature {0} len={1}: ok hex={2}",
                        id, length, Hex(buffer, 0, buffer.Length));
                    return;
                }
            }
            Console.WriteLine("feature {0}: read failed err={1}", id, device.LastError);
        }

        private static int RunSelfTest()
        {
            int failures = 0;
            int x;
            int y;
            int pressure;
            bool touch;
            bool hover;
            bool side;

            byte[] touchReport = new byte[] { 3, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x05 };
            bool parsed = TryParseReport(touchReport, touchReport.Length, out x, out y, out pressure, out touch, out hover, out side);
            failures = Check(parsed, "parse touch report", failures);
            failures = Check(x == 0x2010, "parse x", failures);
            failures = Check(y == 0x4030, "parse y", failures);
            failures = Check(pressure == 0x6050, "parse pressure", failures);
            failures = Check(touch && hover && !side, "parse touch/hover flags", failures);

            byte[] sideReport = new byte[] { 3, 0, 0, 0, 0, 0, 0, 0x02 };
            parsed = TryParseReport(sideReport, sideReport.Length, out x, out y, out pressure, out touch, out hover, out side);
            failures = Check(parsed && side && !touch && !hover, "parse side button", failures);

            byte[] bareReport = new byte[] { 0x34, 0x12, 0x78, 0x56, 0xFF, 0x03, 0x01 };
            parsed = TryParseReport(bareReport, bareReport.Length, out x, out y, out pressure, out touch, out hover, out side);
            failures = Check(parsed && x == 0x1234 && y == 0x5678 && pressure == 0x03FF && touch, "parse report without id", failures);

            failures = Check(ParseSyncMode(new byte[] { 5, 5, 0, 4, 0, 0, 0 }) == 4, "parse mode feature", failures);
            failures = Check(ParseSyncMode(new byte[] { 5, 0, 3 }) == 3, "parse mode without id", failures);
            failures = Check(ScalePressure(0, 1023) == 0, "scale pressure 0", failures);
            failures = Check(ScalePressure(1023, 1023) == 1024, "scale pressure max", failures);
            failures = Check(ScalePressure(512, 1023) >= 500 && ScalePressure(512, 1023) <= 520, "scale pressure mid", failures);

            if (failures == 0)
            {
                Console.WriteLine("SELFTEST PASS");
                return 0;
            }
            Console.WriteLine("SELFTEST FAIL: {0} check(s) failed", failures);
            return 1;
        }

        private static int Check(bool condition, string name, int failures)
        {
            if (condition)
            {
                Console.WriteLine("PASS {0}", name);
                return failures;
            }
            Console.WriteLine("FAIL {0}", name);
            return failures + 1;
        }

        private static int RunDoctor(Options options)
        {
            Console.WriteLine("== BoogieBoardSync doctor ==");
            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            Console.WriteLine("HID collections: {0}", devices.Length);
            foreach (HidDevice candidate in devices)
            {
                Console.WriteLine("  " + candidate);
            }

            HidDevice device = SelectCollection(devices, "vendor");
            if (device == null)
            {
                Console.WriteLine("Vendor collection not found. Check that the board is powered on and the USB cable supports data.");
                return 2;
            }
            if (!device.Open(true))
            {
                Console.WriteLine("Cannot open vendor collection (error {0}).", device.LastError);
                return 2;
            }

            try
            {
                Console.WriteLine("vendor caps: in={0} out={1} feature={2}",
                    device.InputReportByteLength,
                    device.OutputReportByteLength,
                    device.FeatureReportByteLength);
                ReadFeatureAndPrint(device, 5);
                Console.WriteLine("Mode before: {0} raw={1}", ReadSyncMode(device), ReadSyncModeRaw(device));

                if (!string.IsNullOrEmpty(options.EjectDrive))
                {
                    StorageUtil.Release(options.EjectDrive, true);
                    Thread.Sleep(500);
                }
                else if (!options.NoEject)
                {
                    StorageUtil.ReleaseBoogieStorage(true);
                    Thread.Sleep(500);
                }

                if (!options.NoHandshake)
                {
                    SendHandshake(device, options, true);
                }

                int[] candidates = new int[] { options.Mode, 4, 5, 3 };
                int selected = -1;
                for (int i = 0; i < candidates.Length; i++)
                {
                    int mode = candidates[i];
                    bool duplicate = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (candidates[j] == mode)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (duplicate)
                    {
                        continue;
                    }

                    Console.WriteLine("Trying mode {0}...", mode);
                    int attempts = Math.Max(8, Math.Min(20, options.Retries / 8));
                    if (TrySetMode(device, mode, attempts, options, true, options.ModeTransport))
                    {
                        selected = mode;
                        break;
                    }
                }

                if (selected < 0)
                {
                    Console.WriteLine("UNCONFIRMED: feature 5 did not read back any candidate mode. Last value: {0}", ReadSyncModeRaw(device));
                    Console.WriteLine("Some firmware keeps feature 5 at 0x01 even after a successful switch.");
                    Console.WriteLine("Run: --verify --duration=20 and draw on the board to test real input reports.");
                    Console.WriteLine("If that fails, try:");
                    Console.WriteLine("  --verify --blind --duration=20");
                    Console.WriteLine("  --verify --transport=output --duration=20");
                    Console.WriteLine("  --verify --no-handshake --transport=feature --retries=200 --duration=20");
                    return 4;
                }

                Console.WriteLine("PASS: device accepted mode {0}.", selected);
                Console.WriteLine("Next: run --probe --duration=20 --debug and draw on the board.");
                return 0;
            }
            finally
            {
                device.Dispose();
            }
        }

        private static int RunCaps()
        {
            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            if (devices.Length == 0)
            {
                Console.Error.WriteLine("Boogie Board Sync not found (VID_2914 PID_0100).");
                return 2;
            }
            foreach (HidDevice device in devices)
            {
                Console.WriteLine("{0} {1}", device.CollectionName, device.Path);
                if (!device.Open(false))
                {
                    Console.WriteLine("  open failed: {0}", device.LastError);
                    continue;
                }
                device.DumpCaps();
                device.Dispose();
            }
            return 0;
        }
        private static int RunDiag()
        {
            HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
            HidDevice device = null;
            foreach (HidDevice candidate in devices)
            {
                if (candidate.CollectionName == "vendor")
                {
                    device = candidate;
                    break;
                }
            }
            if (device == null)
            {
                Console.Error.WriteLine("Vendor collection not found.");
                return 2;
            }
            if (!device.Open(true))
            {
                Console.Error.WriteLine("Open failed: {0}", device.LastError);
                return 2;
            }

            Console.WriteLine("in={0} feature={1} canWrite={2}",
                device.InputReportByteLength,
                device.FeatureReportByteLength,
                device.CanWrite);

            byte[] feature = new byte[Math.Max(7, (int)device.FeatureReportByteLength)];
            feature[0] = 5;
            bool ok = device.GetFeature(feature);
            Console.WriteLine("GetFeature(5) before: {0} err={1} hex={2}",
                ok, device.LastError, Hex(feature, 0, feature.Length));

            byte[] input = new byte[Math.Max(8, (int)device.InputReportByteLength)];
            ok = device.GetInputReport(input);
            Console.WriteLine("GetInputReport before: {0} err={1} hex={2}",
                ok, device.LastError, Hex(input, 0, input.Length));

            int[] modes = new int[] { 3, 4 };
            string[] strategies = GetModeStrategies("both");
            foreach (int mode in modes)
            {
                foreach (string strategy in strategies)
                {
                    bool sent = SendModeCommand(device, mode, strategy);
                    string method = strategy.StartsWith("output", StringComparison.Ordinal)
                        ? device.LastWriteMethod
                        : device.LastFeatureMethod;
                    Console.WriteLine("mode={0} strategy={1}: sent={2} method={3} len={4} err={5}",
                        mode,
                        strategy,
                        sent,
                        method,
                        strategy.StartsWith("output", StringComparison.Ordinal) ? device.LastOutputLength : device.LastFeatureLength,
                        device.LastError);

                    Thread.Sleep(250);
                    Console.WriteLine("  feature 5: {0}", ReadSyncModeRaw(device));

                    Array.Clear(input, 0, input.Length);
                    ok = device.GetInputReport(input);
                    Console.WriteLine("  GetInputReport: {0} err={1} hex={2}",
                        ok, device.LastError, Hex(input, 0, input.Length));
                }
            }

            device.Dispose();
            return 0;
        }
        private static int RunPointerList()
        {
            uint count = 0;
            NativeMethods.GetPointerDevices(ref count, null);
            if (count == 0)
            {
                Console.WriteLine("No pointer devices.");
                return 0;
            }

            NativeMethods.POINTER_DEVICE_INFO[] devices = new NativeMethods.POINTER_DEVICE_INFO[count];
            if (!NativeMethods.GetPointerDevices(ref count, devices))
            {
                Console.Error.WriteLine("GetPointerDevices failed: {0}", Marshal.GetLastWin32Error());
                return 1;
            }

            for (int i = 0; i < count; i++)
            {
                NativeMethods.POINTER_DEVICE_INFO info = devices[i];
                Console.WriteLine("type={0} product=\"{1}\" startId={2} maxContacts={3}",
                    PointerTypeName(info.pointerDeviceType),
                    info.productString,
                    info.startingCursorId,
                    info.maxActiveContacts);
            }

            return 0;
        }

        private static int RunPointerWatch(Options options)
        {
            uint count = 0;
            NativeMethods.GetPointerDevices(ref count, null);
            uint[] starts = new uint[0];
            if (count > 0)
            {
                NativeMethods.POINTER_DEVICE_INFO[] devices = new NativeMethods.POINTER_DEVICE_INFO[count];
                if (NativeMethods.GetPointerDevices(ref count, devices))
                {
                    starts = new uint[count];
                    for (int i = 0; i < count; i++)
                    {
                        starts[i] = devices[i].startingCursorId;
                        Console.WriteLine("device type={0} product=\"{1}\" startId={2}",
                            PointerTypeName(devices[i].pointerDeviceType),
                            devices[i].productString,
                            devices[i].startingCursorId);
                    }
                }
            }

            Console.WriteLine("Watching pen pointers for {0} seconds. Draw on the tablet now.",
                options.DurationSeconds);
            Stopwatch clock = Stopwatch.StartNew();
            int lastX = int.MinValue;
            int lastY = int.MinValue;
            int events = 0;

            while (options.DurationSeconds <= 0 || clock.Elapsed.TotalSeconds < options.DurationSeconds)
            {
                foreach (uint start in starts)
                {
                    for (uint id = start; id < start + 32; id++)
                    {
                        NativeMethods.POINTER_INFO pointer = new NativeMethods.POINTER_INFO();
                        if (!NativeMethods.GetPointerInfo(id, ref pointer))
                        {
                            continue;
                        }

                        NativeMethods.PEN_INFO pen = new NativeMethods.PEN_INFO();
                        NativeMethods.GetPointerPenInfo(id, ref pen);
                        int x = pointer.ptPixelLocation.X;
                        int y = pointer.ptPixelLocation.Y;
                        if (x == lastX && y == lastY)
                        {
                            continue;
                        }
                        lastX = x;
                        lastY = y;
                        events++;
                        Console.WriteLine("ptr={0} type={1} flags={2:X} x={3} y={4} pressure={5}",
                            id,
                            PointerTypeName(pointer.pointerType),
                            pointer.pointerFlags,
                            x,
                            y,
                            pen.pressure);
                    }
                }
                Thread.Sleep(10);
            }

            Console.WriteLine("Pointer events: {0}", events);
            return 0;
        }

        private static string PointerTypeName(uint type)
        {
            if (type == 2) return "touch";
            if (type == 3) return "pen";
            if (type == 4) return "mouse";
            if (type == 5) return "touchpad";
            return "type-" + type;
        }
        private static int RunDriver(Options options)
        {
            TabletMapper mapper = new TabletMapper(options);
            Console.WriteLine(mapper.Describe());

            using (PenInjector injector = new PenInjector(options.Mouse))
            {
                Console.WriteLine("Injection mode: {0}", injector.ModeName);
                Console.WriteLine("Press Ctrl+C to stop.");

                Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    stopRequested = true;
                    HidDevice device = currentDevice;
                    if (device != null)
                    {
                        device.CancelRead();
                    }
                };

                while (!stopRequested)
                {
                    HidDevice[] devices = HidDevice.FindAll(HidDevice.BoogieVendorId, HidDevice.BoogieProductId);
                    HidDevice target = SelectCollection(devices, options.Collection);
                    if (target == null)
                    {
                        Console.WriteLine("Device not found; waiting...");
                        Wait(2000);
                        continue;
                    }

                    Console.WriteLine("Using {0} collection: {1}", target.CollectionName, target.Path);
                    if (!target.Open(true))
                    {
                        Console.WriteLine("Cannot open HID collection (error {0}).", target.LastError);
                        target.Dispose();
                        Wait(2000);
                        continue;
                    }

                    currentDevice = target;
                    if (!options.NoTabletMode)
                    {
                        bool modeOk = SetSyncMode(target, options.Mode, options);
                        if (!modeOk)
                        {
                            Console.WriteLine("WARN: mode {0} was not confirmed by feature 5; continuing to read input reports.", options.Mode);
                            Console.WriteLine("      Use --verify --duration=20 to test whether the board is actually sending pen data.");
                        }
                    }

                    ReadReports(target, mapper, injector, options);
                    injector.Release();
                    currentDevice = null;
                    target.Dispose();

                    if (!stopRequested)
                    {
                        Console.WriteLine("Device disconnected or read failed; reconnecting...");
                        Wait(1500);
                    }
                }
            }

            return 0;
        }

        private static void ReadReports(HidDevice device, TabletMapper mapper, PenInjector injector, Options options)
        {
            byte[] buffer = new byte[Math.Max(64, (int)device.InputReportByteLength)];
            int reports = 0;
            int parsedReports = 0;
            bool lastInRange = false;
            Stopwatch clock = Stopwatch.StartNew();
            long lastDebugMs = -1000;

            while (!stopRequested)
            {
                int count = device.Read(buffer);
                if (count == -1)
                {
                    break;
                }
                if (count < 0)
                {
                    if (!stopRequested)
                    {
                        Console.WriteLine("Read failed (error {0}).", device.LastError);
                    }
                    break;
                }
                if (count == 0)
                {
                    continue;
                }

                reports++;
                int x;
                int y;
                int pressure;
                bool touch;
                bool hover;
                bool side;
                if (!TryParseReport(buffer, count, out x, out y, out pressure, out touch, out hover, out side))
                {
                    if (options.Debug && clock.ElapsedMilliseconds - lastDebugMs > 250)
                    {
                        lastDebugMs = clock.ElapsedMilliseconds;
                        Console.WriteLine("Unparsed report ({0} bytes): {1}", count, Hex(buffer, 0, count));
                    }
                    continue;
                }

                parsedReports++;
                if (options.Debug && clock.ElapsedMilliseconds - lastDebugMs > 100)
                {
                    lastDebugMs = clock.ElapsedMilliseconds;
                    Console.WriteLine("raw x={0} y={1} p={2} touch={3} hover={4} side={5} bytes={6}",
                        x, y, pressure, touch, hover, side, Hex(buffer, 0, count));
                }

                if (touch || hover)
                {
                    int scaledPressure = touch ? ScalePressure(pressure, options.PressureMax) : 0;
                    injector.Update(mapper.Map(x, y), scaledPressure, touch, hover, side);
                    lastInRange = true;
                }
                else if (lastInRange)
                {
                    injector.Update(mapper.Map(x, y), 0, false, false, false);
                    lastInRange = false;
                }
            }

            Console.WriteLine("Stopped. reports={0} parsed={1} injected={2} failed={3}",
                reports, parsedReports, injector.InjectedCount, injector.FailedCount);
        }

        private static int RunProbe(HidDevice[] devices, Options options)
        {
            List<HidDevice> selected = new List<HidDevice>();
            foreach (HidDevice device in devices)
            {
                if (options.Collection != "auto" && device.CollectionName != options.Collection)
                {
                    continue;
                }
                if (device.InputReportByteLength >= 8)
                {
                    selected.Add(device);
                }
            }

            if (selected.Count == 0)
            {
                Console.WriteLine("No collection with an input report of at least 8 bytes.");
                return 3;
            }

            Console.WriteLine("");
            Console.WriteLine("Probe mode: draw on the Boogie Board now.");
            Console.WriteLine("Collecting for {0} seconds.", options.DurationSeconds);
            Console.WriteLine("");

            List<ProbeState> states = new List<ProbeState>();
            List<Thread> threads = new List<Thread>();
            foreach (HidDevice device in selected)
            {
                if (!device.Open(true))
                {
                    Console.WriteLine("Cannot open {0} collection (error {1}).", device.CollectionName, device.LastError);
                    continue;
                }

                if (!options.NoTabletMode)
                {
                    bool modeOk = SetSyncMode(device, options.Mode, options);
                    Console.WriteLine("Mode {0} on {1}: {2}",
                        options.Mode,
                        device.CollectionName,
                        modeOk ? "confirmed" : "unconfirmed (feature 5 may be write-only)");
                }

                ProbeState state = new ProbeState();
                state.Device = device;
                states.Add(state);
                Thread thread = new Thread(ProbeReader);
                thread.IsBackground = true;
                thread.Start(state);
                threads.Add(thread);
            }

            if (states.Count == 0)
            {
                return 4;
            }

            Thread timer = new Thread(delegate()
            {
                int duration = options.DurationSeconds;
                if (duration <= 0)
                {
                    return;
                }
                Thread.Sleep(duration * 1000);
                foreach (ProbeState state in states)
                {
                    state.Stop = true;
                    state.Device.CancelRead();
                }
            });
            timer.IsBackground = true;
            timer.Start();

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                stopRequested = true;
                foreach (ProbeState state in states)
                {
                    state.Stop = true;
                    state.Device.CancelRead();
                }
            };

            int joinTimeout = options.DurationSeconds > 0
                ? (options.DurationSeconds + 10) * 1000
                : Timeout.Infinite;
            foreach (Thread thread in threads)
            {
                thread.Join(joinTimeout);
            }

            Console.WriteLine("");
            foreach (ProbeState state in states)
            {
                lock (state.Sync)
                {
                    Console.WriteLine("{0}: reports={1} parsed={2} touch={3} hover={4} side={5} x={6}..{7} y={8}..{9} pressure={10}..{11}",
                        state.Device.CollectionName,
                        state.Reports,
                        state.Parsed,
                        state.TouchReports,
                        state.HoverReports,
                        state.SideReports,
                        state.MinX,
                        state.MaxX,
                        state.MinY,
                        state.MaxY,
                        state.MinPressure,
                        state.MaxPressure);
                }
                state.Device.Dispose();
            }

            return 0;
        }

        private static void ProbeReader(object argument)
        {
            ProbeState state = (ProbeState)argument;
            HidDevice device = state.Device;
            byte[] buffer = new byte[Math.Max(64, (int)device.InputReportByteLength)];
            Stopwatch clock = Stopwatch.StartNew();
            long lastPrint = -1000;

            while (!state.Stop)
            {
                int count = device.Read(buffer);
                if (count == -1)
                {
                    break;
                }
                if (count < 0)
                {
                    if (!state.Stop)
                    {
                        Console.WriteLine("{0}: read failed (error {1})", device.CollectionName, device.LastError);
                    }
                    break;
                }
                if (count == 0)
                {
                    continue;
                }

                int x;
                int y;
                int pressure;
                bool touch;
                bool hover;
                bool side;
                bool parsed = TryParseReport(buffer, count, out x, out y, out pressure, out touch, out hover, out side);

                lock (state.Sync)
                {
                    state.Reports++;
                    if (parsed)
                    {
                        state.Parsed++;
                        state.MinX = Math.Min(state.MinX, x);
                        state.MaxX = Math.Max(state.MaxX, x);
                        state.MinY = Math.Min(state.MinY, y);
                        state.MaxY = Math.Max(state.MaxY, y);
                        state.MinPressure = Math.Min(state.MinPressure, pressure);
                        state.MaxPressure = Math.Max(state.MaxPressure, pressure);
                        if (touch)
                        {
                            state.TouchReports++;
                        }
                        if (hover)
                        {
                            state.HoverReports++;
                        }
                        if (side)
                        {
                            state.SideReports++;
                        }
                    }
                }

                long now = clock.ElapsedMilliseconds;
                if (now - lastPrint >= 100)
                {
                    lastPrint = now;
                    Console.WriteLine("{0} {1} {2}",
                        device.CollectionName,
                        Hex(buffer, 0, count),
                        parsed
                            ? string.Format("x={0} y={1} p={2} touch={3} hover={4} side={5}", x, y, pressure, touch, hover, side)
                            : "unparsed");
                }
            }
        }

        private static int RunInjectionTest(Options options)
        {
            using (PenInjector injector = new PenInjector(options.Mouse))
            {
                Console.WriteLine("Injection test mode: {0}. Drawing a square for 2 seconds.", injector.ModeName);
                int left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
                int top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
                int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
                int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
                int marginX = Math.Max(50, width / 4);
                int marginY = Math.Max(50, height / 4);

                NativeMethods.POINT[] points = new NativeMethods.POINT[]
                {
                    new NativeMethods.POINT(left + marginX, top + marginY),
                    new NativeMethods.POINT(left + width - marginX, top + marginY),
                    new NativeMethods.POINT(left + width - marginX, top + height - marginY),
                    new NativeMethods.POINT(left + marginX, top + height - marginY),
                    new NativeMethods.POINT(left + marginX, top + marginY)
                };

                for (int i = 0; i < points.Length; i++)
                {
                    injector.Update(points[i], 512, true, true, false);
                    Thread.Sleep(250);
                }
                injector.Update(points[points.Length - 1], 0, false, false, false);
                Thread.Sleep(100);

                Console.WriteLine("Injected: {0} failed: {1}", injector.InjectedCount, injector.FailedCount);
                return injector.FailedCount == 0 ? 0 : 5;
            }
        }

        private static HidDevice SelectCollection(HidDevice[] devices, string requested)
        {
            HidDevice vendor = null;
            HidDevice pen = null;
            HidDevice largest = null;

            foreach (HidDevice device in devices)
            {
                if (requested != "auto" && device.CollectionName != requested)
                {
                    continue;
                }
                if (device.CollectionName == "vendor" && vendor == null)
                {
                    vendor = device;
                }
                if (device.CollectionName == "pen" && pen == null)
                {
                    pen = device;
                }
                if (device.InputReportByteLength >= 8 && (largest == null || device.InputReportByteLength > largest.InputReportByteLength))
                {
                    largest = device;
                }
            }

            if (vendor != null)
            {
                return vendor;
            }
            if (pen != null)
            {
                return pen;
            }
            return largest;
        }

        private static bool TryParseReport(byte[] buffer, int count, out int x, out int y, out int pressure, out bool touch, out bool hover, out bool side)
        {
            x = 0;
            y = 0;
            pressure = 0;
            touch = false;
            hover = false;
            side = false;

            int offset = 0;
            if (count >= 8 && buffer[0] == 3)
            {
                offset = 1;
            }

            if (count - offset < 7)
            {
                return false;
            }

            x = buffer[offset] | (buffer[offset + 1] << 8);
            y = buffer[offset + 2] | (buffer[offset + 3] << 8);
            pressure = buffer[offset + 4] | (buffer[offset + 5] << 8);
            int flags = buffer[offset + 6];
            touch = (flags & 0x01) != 0;
            side = (flags & 0x02) != 0;
            hover = (flags & 0x04) != 0;
            return true;
        }

        private static int ScalePressure(int raw, int maximum)
        {
            if (maximum <= 0)
            {
                maximum = 1023;
            }
            double value = Math.Min(1.0, Math.Max(0.0, raw / (double)maximum));
            return (int)Math.Round(value * 1024.0);
        }

        private static string Hex(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return "";
            }
            return BitConverter.ToString(buffer, offset, count);
        }

        private static void Wait(int milliseconds)
        {
            int elapsed = 0;
            while (!stopRequested && elapsed < milliseconds)
            {
                Thread.Sleep(100);
                elapsed += 100;
            }
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch (EntryPointNotFoundException)
            {
                NativeMethods.SetProcessDPIAware();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("BoogieBoardSync user-space pen driver for Windows");
            Console.WriteLine("");
            Console.WriteLine("Usage: BoogieBoardSync.exe [options]");
            Console.WriteLine("");
            Console.WriteLine("  --list                  List every HID collection and open status");
            Console.WriteLine("  --pointers              List Windows pointer devices");
            Console.WriteLine("  --pointer-watch         Poll Windows pen pointers for --duration seconds");
            Console.WriteLine("  --diag                  Read/write HID feature and input reports");
            Console.WriteLine("  --caps                  Dump HID report value caps");
            Console.WriteLine("  --features              Read all feature reports");
            Console.WriteLine("  --doctor                Diagnose storage, handshake and mode switching");
            Console.WriteLine("  --verify                Set mode, then verify real input reports");
            Console.WriteLine("  --blind                 Send mode commands without feature 5 readback");
            Console.WriteLine("  --mode=N                Mode for normal/probe run, default 3 (digitizer)");
            Console.WriteLine("  --set-mode=N            Retry setting mode N and exit");
            Console.WriteLine("  --eject=D               Release this drive before changing mode");
            Console.WriteLine("  --no-eject              Do not auto-release the device's U-disk volume");
            Console.WriteLine("  --no-handshake          Do not send date/host feature reports");
            Console.WriteLine("  --transport=MODE        auto, feature, output or both");
            Console.WriteLine("  --feature-pad           Compat: also try the 7-byte padded feature report");
            Console.WriteLine("  --retries=N             Attempts per transport, default 80");
            Console.WriteLine("  --retry-delay=N         Milliseconds between attempts, default 60");
            Console.WriteLine("  --host-type=N           Host type sent in feature 8, default 8 (Android)");
            Console.WriteLine("  --probe                 Dump raw HID reports for --duration seconds");
            Console.WriteLine("  --test                  Inject a test stroke without the tablet");
            Console.WriteLine("  --mouse                 Inject as mouse instead of Windows Ink pen");
            Console.WriteLine("  --debug                 Log raw reports while running");
            Console.WriteLine("  --rotate=0|90|180|270   Rotate tablet coordinates");
            Console.WriteLine("  --flip-x / --flip-y     Mirror an axis");
            Console.WriteLine("  --raw-max=WxH           Raw tablet maximum, default 20280x13942");
            Console.WriteLine("  --raw-min=XxY           Raw tablet minimum, default 0x0");
            Console.WriteLine("  --pressure-max=N        Raw pressure maximum, default 1023");
            Console.WriteLine("  --duration=N            Probe duration in seconds, default 15");
            Console.WriteLine("  --collection=NAME       auto, vendor or pen");
            Console.WriteLine("  --no-tablet-mode        Do not switch the device mode automatically");
            Console.WriteLine("  --help                  Show this help");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  BoogieBoardSync.exe --doctor");
            Console.WriteLine("  BoogieBoardSync.exe --verify --duration=20");
            Console.WriteLine("  BoogieBoardSync.exe --probe --duration=20");
            Console.WriteLine("  BoogieBoardSync.exe --rotate=90");
            Console.WriteLine("  BoogieBoardSync.exe --test --mouse");
        }
    }

    internal sealed class ProbeState
    {
        public HidDevice Device;
        public volatile bool Stop;
        public readonly object Sync = new object();
        public int Reports;
        public int Parsed;
        public int TouchReports;
        public int HoverReports;
        public int SideReports;
        public int MinX = int.MaxValue;
        public int MaxX = int.MinValue;
        public int MinY = int.MaxValue;
        public int MaxY = int.MinValue;
        public int MinPressure = int.MaxValue;
        public int MaxPressure = int.MinValue;
    }
}
