using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace BoogieBoardSync
{
    internal sealed class BluetoothDeviceInfo
    {
        public ulong Address;
        public string Name = "";
        public bool Connected;
        public bool Remembered;
        public bool Authenticated;

        public string AddressText
        {
            get
            {
                byte[] bytes = BitConverter.GetBytes(Address);
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                    bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0]);
            }
        }

        public bool IsLikelyBoogie
        {
            get
            {
                string name = (Name ?? "").ToLowerInvariant();
                return name.Contains("boogie") || name.Contains("improv") || name.Contains("sync");
            }
        }

        public override string ToString()
        {
            return string.Format(
                "{0}  {1}  connected={2} remembered={3} authenticated={4}",
                AddressText,
                string.IsNullOrEmpty(Name) ? "(no name)" : Name,
                Connected,
                Remembered,
                Authenticated);
        }
    }

    internal static class BluetoothDiscovery
    {
        private const int ErrorNoMoreItems = 259;

        public static string LastError = "";

        public static BluetoothDeviceInfo[] FindDevices(bool inquiry)
        {
            LastError = "";
            List<BluetoothDeviceInfo> result = new List<BluetoothDeviceInfo>();
            Dictionary<ulong, bool> seen = new Dictionary<ulong, bool>();

            FindPass(false, false, result, seen);
            if (inquiry)
            {
                FindPass(true, true, result, seen);
            }
            return result.ToArray();
        }

        public static BluetoothDeviceInfo FindPreferred(string requestedAddress, string requestedName)
        {
            BluetoothDeviceInfo[] devices = FindDevices(false);
            BluetoothDeviceInfo best = null;
            int bestScore = 0;
            ulong requested = 0;
            bool hasAddress = TryParseAddress(requestedAddress, out requested);

            for (int i = 0; i < devices.Length; i++)
            {
                BluetoothDeviceInfo device = devices[i];
                int score = ScoreDevice(device, requested, hasAddress, requestedName);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = device;
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static readonly byte[] StreamingServiceSdpUuid = new byte[]
        {
            0xD6, 0xA5, 0x6F, 0x80, 0x88, 0xF8, 0x11, 0xE3,
            0xBA, 0xA8, 0x08, 0x00, 0x20, 0x0C, 0x9A, 0x66
        };

        public static bool TryParseAddress(string text, out ulong address)
        {
            address = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string hex = text.Replace(":", "").Replace("-", "").Replace(" ", "").Trim();
            return ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        public static int ResolveRfcommChannel(ulong address)
        {
            int channel = FindCachedChannel(address, true);
            if (channel > 0)
            {
                return channel;
            }
            channel = FindCachedChannel(address, false);
            if (channel > 0)
            {
                return channel;
            }
            return 3;
        }

        public static bool TryFindRfcommChannel(byte[] record, out int channel)
        {
            channel = 0;
            if (record == null || record.Length < 5)
            {
                return false;
            }
            if (!Contains(record, StreamingServiceSdpUuid) && !ContainsAscii(record, "HID over SPP"))
            {
                return false;
            }
            return TryFindRfcommChannelRaw(record, out channel);
        }

        public static bool TryFindRfcommChannelRaw(byte[] record, out int channel)
        {
            channel = 0;
            if (record == null)
            {
                return false;
            }
            for (int i = 0; i + 4 < record.Length; i++)
            {
                if (record[i] == 0x19 && record[i + 1] == 0x00 && record[i + 2] == 0x03 && record[i + 3] == 0x08)
                {
                    int value = record[i + 4];
                    if (value > 0 && value < 32)
                    {
                        channel = value;
                        return true;
                    }
                }
            }
            return false;
        }

        private static int FindCachedChannel(ulong address, bool customOnly)
        {
            string addressText = address.ToString("x12", CultureInfo.InvariantCulture);
            string basePath = "SYSTEM\\CurrentControlSet\\Services\\BTHPORT\\Parameters\\Devices\\" + addressText;
            string[] names = new string[] { "CachedServices", "DynamicCachedServices" };
            for (int n = 0; n < names.Length; n++)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(basePath + "\\" + names[n]))
                    {
                        if (key == null)
                        {
                            continue;
                        }
                        string[] valueNames = key.GetValueNames();
                        for (int i = 0; i < valueNames.Length; i++)
                        {
                            byte[] record = key.GetValue(valueNames[i]) as byte[];
                            if (record == null)
                            {
                                continue;
                            }
                            int channel;
                            if (customOnly)
                            {
                                if (TryFindRfcommChannel(record, out channel))
                                {
                                    return channel;
                                }
                            }
                            else if (TryFindRfcommChannelRaw(record, out channel))
                            {
                                return channel;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            return 0;
        }

        private static bool Contains(byte[] data, byte[] pattern)
        {
            if (data == null || pattern == null || data.Length < pattern.Length)
            {
                return false;
            }
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsAscii(byte[] data, string text)
        {
            if (data == null || text == null)
            {
                return false;
            }
            return Contains(data, Encoding.ASCII.GetBytes(text));
        }

        private static void FindPass(bool inquiry, bool includeUnknown, List<BluetoothDeviceInfo> result,
            Dictionary<ulong, bool> seen)
        {
            NativeMethodsBluetooth.BluetoothDeviceSearchParams search = new NativeMethodsBluetooth.BluetoothDeviceSearchParams();
            search.Size = Marshal.SizeOf(typeof(NativeMethodsBluetooth.BluetoothDeviceSearchParams));
            search.ReturnAuthenticated = true;
            search.ReturnRemembered = true;
            search.ReturnConnected = true;
            search.ReturnUnknown = includeUnknown;
            search.IssueInquiry = inquiry;
            search.TimeoutMultiplier = inquiry ? (byte)6 : (byte)0;
            search.Radio = IntPtr.Zero;

            NativeMethodsBluetooth.BluetoothDeviceInfo info = new NativeMethodsBluetooth.BluetoothDeviceInfo();
            info.Size = Marshal.SizeOf(typeof(NativeMethodsBluetooth.BluetoothDeviceInfo));

            IntPtr find = NativeMethodsBluetooth.BluetoothFindFirstDevice(ref search, ref info);
            if (find == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ErrorNoMoreItems && LastError.Length == 0)
                {
                    LastError = "蓝牙设备枚举失败，错误 " + error;
                }
                return;
            }

            try
            {
                while (true)
                {
                    if (!seen.ContainsKey(info.Address))
                    {
                        seen[info.Address] = true;
                        BluetoothDeviceInfo device = new BluetoothDeviceInfo();
                        device.Address = info.Address;
                        device.Name = info.Name ?? "";
                        device.Connected = info.Connected;
                        device.Remembered = info.Remembered;
                        device.Authenticated = info.Authenticated;
                        result.Add(device);
                    }

                    info.Size = Marshal.SizeOf(typeof(NativeMethodsBluetooth.BluetoothDeviceInfo));
                    if (!NativeMethodsBluetooth.BluetoothFindNextDevice(find, ref info))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ErrorNoMoreItems && LastError.Length == 0)
                        {
                            LastError = "蓝牙设备枚举中断，错误 " + error;
                        }
                        break;
                    }
                }
            }
            finally
            {
                NativeMethodsBluetooth.BluetoothFindDeviceClose(find);
            }
        }

        private static int ScoreDevice(BluetoothDeviceInfo device, ulong requestedAddress, bool hasAddress,
            string requestedName)
        {
            int score = 0;
            if (hasAddress)
            {
                return device.Address == requestedAddress ? 1000 : 0;
            }

            if (!string.IsNullOrEmpty(requestedName))
            {
                if (device.Name == null || device.Name.IndexOf(requestedName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return 0;
                }
                score += 200;
            }

            string name = (device.Name ?? "").ToLowerInvariant();
            if (name.Contains("boogie"))
            {
                score += 100;
            }
            if (name.Contains("improv"))
            {
                score += 90;
            }
            if (name.Contains("sync"))
            {
                score += 60;
            }

            if (score > 0)
            {
                if (device.Connected) score += 8;
                if (device.Remembered) score += 4;
                if (device.Authenticated) score += 2;
            }
            return score;
        }
    }

    internal sealed class BluetoothTransport : IDisposable
    {
        private static readonly Guid StreamingServiceUuid =
            new Guid("d6a56f80-88f8-11e3-baa8-0800200c9a66");

        private readonly object socketSync = new object();
        private readonly object writeSync = new object();
        private readonly Queue<byte[]> pending = new Queue<byte[]>();
        private readonly BluetoothFrameParser parser = new BluetoothFrameParser();
        private readonly byte[] readBuffer = new byte[2048];
        private readonly int channelOverride;
        private IntPtr socket = NativeMethodsBluetooth.InvalidSocket;
        private Thread keepAliveThread;
        private volatile bool stopping;

        public int Channel;
        public int LastSocketError;
        public string LastErrorText = "";

        public BluetoothTransport()
            : this(0)
        {
        }

        public BluetoothTransport(int channel)
        {
            channelOverride = channel;
        }

        public bool IsConnected
        {
            get
            {
                lock (socketSync)
                {
                    return socket != NativeMethodsBluetooth.InvalidSocket;
                }
            }
        }

        public int BadFrames
        {
            get { return parser.BadFrames; }
        }

        public int ControlFrames
        {
            get { return parser.ControlFrames; }
        }

        public bool Open(BluetoothDeviceInfo device, out string error)
        {
            error = "";
            LastSocketError = 0;
            LastErrorText = "";
            stopping = false;
            pending.Clear();
            parser.Reset();

            string winsockError;
            if (!NativeMethodsBluetooth.EnsureWinsock(out winsockError))
            {
                error = winsockError;
                LastErrorText = error;
                return false;
            }

            IntPtr created = NativeMethodsBluetooth.socket(
                NativeMethodsBluetooth.AfBth,
                NativeMethodsBluetooth.SockStream,
                NativeMethodsBluetooth.BthProtoRfcomm);
            if (created == NativeMethodsBluetooth.InvalidSocket)
            {
                int code = NativeMethodsBluetooth.WSAGetLastError();
                LastSocketError = code;
                error = DescribeSocketError(code);
                LastErrorText = error;
                return false;
            }

            int channel = channelOverride;
            if (channel <= 0)
            {
                channel = BluetoothDiscovery.ResolveRfcommChannel(device.Address);
            }
            Channel = channel;

            NativeMethodsBluetooth.SockAddrBth address = new NativeMethodsBluetooth.SockAddrBth();
            address.AddressFamily = NativeMethodsBluetooth.AfBth;
            address.BluetoothAddress = device.Address;
            address.ServiceClassId = Guid.Empty;
            address.Port = (uint)channel;

            int result = NativeMethodsBluetooth.connect(
                created,
                ref address,
                Marshal.SizeOf(typeof(NativeMethodsBluetooth.SockAddrBth)));
            if (result == NativeMethodsBluetooth.SocketError)
            {
                int code = NativeMethodsBluetooth.WSAGetLastError();
                NativeMethodsBluetooth.closesocket(created);
                LastSocketError = code;
                error = DescribeSocketError(code);
                LastErrorText = error;
                return false;
            }

            lock (socketSync)
            {
                socket = created;
            }

            int timeout = 500;
            NativeMethodsBluetooth.setsockopt(
                created,
                NativeMethodsBluetooth.SolSocket,
                NativeMethodsBluetooth.SoRcvTimeout,
                ref timeout,
                Marshal.SizeOf(typeof(int)));

            keepAliveThread = new Thread(KeepAliveLoop);
            keepAliveThread.IsBackground = true;
            keepAliveThread.Name = "BoogieBoardSync Bluetooth keepalive";
            keepAliveThread.Start();
            return true;
        }

        public bool ConfigureForTablet(int mode, bool noHandshake, int hostType)
        {
            bool ok = SendFeatureReport(0x05, new byte[] { 5 });
            if (!noHandshake)
            {
                ok = SendFeatureReport(0x06, BuildDatePayload()) && ok;
                ok = SendFeatureReport(0x08, new byte[] { (byte)hostType, 0x00, 0x00, 0x00 }) && ok;
            }
            Thread.Sleep(120);
            return SendFeatureReport(0x05, new byte[] { (byte)mode }) && ok;
        }

        public bool SendFeatureReport(byte reportId, byte[] payload)
        {
            byte[] frame = BluetoothProtocol.BuildSetReportFrame(reportId, payload);
            return SendRaw(frame);
        }

        public int Read(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return -2;
            }

            while (!stopping)
            {
                if (pending.Count > 0)
                {
                    return CopyPending(buffer);
                }

                IntPtr current = IntPtr.Zero;
                lock (socketSync)
                {
                    current = socket;
                }
                if (current == NativeMethodsBluetooth.InvalidSocket)
                {
                    return stopping ? -1 : -2;
                }

                int count = NativeMethodsBluetooth.recv(current, readBuffer, readBuffer.Length, 0);
                if (count > 0)
                {
                    List<byte[]> reports = new List<byte[]>();
                    for (int i = 0; i < count; i++)
                    {
                        parser.Feed(readBuffer[i], reports);
                    }
                    for (int i = 0; i < reports.Count; i++)
                    {
                        pending.Enqueue(reports[i]);
                    }
                    if (pending.Count > 0)
                    {
                        return CopyPending(buffer);
                    }
                    continue;
                }

                if (count == 0)
                {
                    LastSocketError = 0;
                    return stopping ? -1 : -2;
                }

                int error = NativeMethodsBluetooth.WSAGetLastError();
                if (error == NativeMethodsBluetooth.WsaETimedOut ||
                    error == NativeMethodsBluetooth.WsaEWouldBlock)
                {
                    continue;
                }
                if (stopping)
                {
                    return -1;
                }

                LastSocketError = error;
                LastErrorText = DescribeSocketError(error);
                return -2;
            }

            return -1;
        }

        public void CancelRead()
        {
            stopping = true;
            CloseSocket();
        }

        public void Dispose()
        {
            stopping = true;
            CloseSocket();
            if (keepAliveThread != null)
            {
                keepAliveThread.Join(1000);
                keepAliveThread = null;
            }
        }

        private int CopyPending(byte[] buffer)
        {
            byte[] report = pending.Dequeue();
            int count = Math.Min(buffer.Length, report.Length);
            Array.Copy(report, 0, buffer, 0, count);
            return count;
        }

        private void KeepAliveLoop()
        {
            byte[] keepAlive = new byte[] { BluetoothProtocol.Fend };
            while (!stopping)
            {
                for (int i = 0; i < 30 && !stopping; i++)
                {
                    Thread.Sleep(100);
                }
                if (stopping)
                {
                    break;
                }
                if (!SendRaw(keepAlive))
                {
                    stopping = true;
                    CloseSocket();
                    break;
                }
            }
        }

        private bool SendRaw(byte[] data)
        {
            lock (writeSync)
            {
                IntPtr current;
                lock (socketSync)
                {
                    current = socket;
                }
                if (current == NativeMethodsBluetooth.InvalidSocket)
                {
                    LastSocketError = NativeMethodsBluetooth.WsaENotSock;
                    return false;
                }

                int sent = NativeMethodsBluetooth.send(current, data, data.Length, 0);
                if (sent == data.Length)
                {
                    return true;
                }

                LastSocketError = sent < 0 ? NativeMethodsBluetooth.WSAGetLastError() : 0;
                LastErrorText = sent < 0 ? DescribeSocketError(LastSocketError) : "蓝牙发送不完整";
                return false;
            }
        }

        private void CloseSocket()
        {
            lock (socketSync)
            {
                if (socket != NativeMethodsBluetooth.InvalidSocket)
                {
                    NativeMethodsBluetooth.closesocket(socket);
                    socket = NativeMethodsBluetooth.InvalidSocket;
                }
            }
        }

        private static byte[] BuildDatePayload()
        {
            DateTime now = DateTime.Now;
            int second = now.Second / 2;
            int minute = now.Minute;
            int hour = now.Hour;
            int day = now.Day;
            int month = now.Month;
            int year = now.Year - 1980;

            byte[] payload = new byte[4];
            payload[0] = (byte)((minute << 5) | second);
            payload[1] = (byte)((hour << 3) | (minute >> 3));
            payload[2] = (byte)((month << 5) | day);
            payload[3] = (byte)((year << 1) | (month >> 3));
            return payload;
        }

        private static string DescribeSocketError(int code)
        {
            if (code == 10049) return "找不到蓝牙地址，请先配对并确认板子已开机";
            if (code == 10060) return "蓝牙连接超时，请确认板子已开机且在范围内";
            if (code == 10061) return "蓝牙连接被拒绝，板子可能已被其他设备占用";
            if (code == 10050) return "蓝牙网络不可用，请检查蓝牙适配器";
            if (code == 10051) return "蓝牙网络不可达";
            if (code == 10054) return "蓝牙连接已被设备断开";
            if (code == 10053) return "蓝牙连接已中止";
            if (code == 10038) return "蓝牙套接字已关闭";
            return "蓝牙错误 " + code.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal static class NativeMethodsBluetooth
    {
        public const int AfBth = 32;
        public const int SockStream = 1;
        public const int BthProtoRfcomm = 3;
        public const int SolSocket = 0xFFFF;
        public const int SoRcvTimeout = 0x1006;
        public const int SocketError = -1;
        public const int WsaETimedOut = 10060;
        public const int WsaEWouldBlock = 10035;
        public const int WsaENotSock = 10038;
        public static readonly IntPtr InvalidSocket = new IntPtr(-1);

        private static readonly object WinsockSync = new object();
        private static bool winsockReady;
        private static string winsockError = "";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SockAddrBth
        {
            public ushort AddressFamily;
            public ulong BluetoothAddress;
            public Guid ServiceClassId;
            public uint Port;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WsaData
        {
            public ushort Version;
            public ushort HighVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            public string Description;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
            public string SystemStatus;
            public ushort MaxSockets;
            public ushort MaxUdpDatagram;
            public IntPtr VendorInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Milliseconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BluetoothDeviceSearchParams
        {
            public int Size;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ReturnAuthenticated;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ReturnRemembered;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ReturnUnknown;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ReturnConnected;
            [MarshalAs(UnmanagedType.Bool)]
            public bool IssueInquiry;
            public byte TimeoutMultiplier;
            public IntPtr Radio;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct BluetoothDeviceInfo
        {
            public int Size;
            public ulong Address;
            public uint ClassOfDevice;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Connected;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Remembered;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Authenticated;
            public SystemTime LastSeen;
            public SystemTime LastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string Name;
        }

        [DllImport("BluetoothApis.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr BluetoothFindFirstDevice(
            ref BluetoothDeviceSearchParams searchParams,
            ref BluetoothDeviceInfo deviceInfo);

        [DllImport("BluetoothApis.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool BluetoothFindNextDevice(
            IntPtr find,
            ref BluetoothDeviceInfo deviceInfo);

        [DllImport("BluetoothApis.dll", SetLastError = true)]
        public static extern bool BluetoothFindDeviceClose(IntPtr find);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int WSAStartup(ushort version, out WsaData data);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern IntPtr socket(int addressFamily, int socketType, int protocol);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int connect(IntPtr socket, ref SockAddrBth address, int addressLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int send(IntPtr socket, byte[] buffer, int length, int flags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int recv(IntPtr socket, byte[] buffer, int length, int flags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int setsockopt(IntPtr socket, int level, int optionName, ref int value, int length);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int closesocket(IntPtr socket);

        [DllImport("ws2_32.dll", SetLastError = true)]
        public static extern int WSAGetLastError();

        public static bool EnsureWinsock(out string error)
        {
            lock (WinsockSync)
            {
                if (winsockReady)
                {
                    error = "";
                    return true;
                }

                WsaData data = new WsaData();
                int result = WSAStartup(0x0202, out data);
                if (result != 0)
                {
                    winsockError = "Winsock 初始化失败，错误 " + result.ToString(CultureInfo.InvariantCulture);
                    error = winsockError;
                    return false;
                }

                winsockReady = true;
                winsockError = "";
                error = "";
                return true;
            }
        }
    }
}