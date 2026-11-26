using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BoogieBoardSync
{
    internal sealed class HidDevice : IDisposable
    {
        public const ushort BoogieVendorId = 0x2914;
        public const ushort BoogieBluetoothVendorId = 0x00F3;
        public const ushort BoogieProductId = 0x0100;

        private const int HIDP_STATUS_SUCCESS = 0x00110000;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        public string Path;
        public ushort VendorId;
        public ushort ProductId;
        public ushort Version;
        public ushort UsagePage;
        public ushort Usage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        public string ProductName = "";
        public string Manufacturer = "";
        public string SerialNumber = "";
        public IntPtr Handle = IntPtr.Zero;
        public bool CanWrite;
        public int LastError;

        public bool IsOpen
        {
            get { return Handle != IntPtr.Zero && Handle != InvalidHandle; }
        }

        public string CollectionName
        {
            get
            {
                if (UsagePage == 0x0D && Usage == 0x02)
                {
                    return "pen";
                }
                if (UsagePage >= 0xFF00)
                {
                    return "vendor";
                }
                return string.Format("usage-{0:X4}-{1:X4}", UsagePage, Usage);
            }
        }

        public static HidDevice[] FindAll(ushort vendorId, ushort productId)
        {
            List<HidDevice> result = new List<HidDevice>();
            foreach (string path in EnumeratePaths())
            {
                HidDevice device = ReadInfo(path);
                if (device != null && device.VendorId == vendorId && device.ProductId == productId)
                {
                    result.Add(device);
                }
            }
            return result.ToArray();
        }

        public static void ListAll()
        {
            foreach (string path in EnumeratePaths())
            {
                IntPtr handle = NativeMethods.CreateFile(
                    path,
                    NativeMethods.GENERIC_READ,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                if (handle == InvalidHandle || handle == IntPtr.Zero)
                {
                    Console.WriteLine("OPEN FAILED error={0} path={1}", Marshal.GetLastWin32Error(), path);
                    continue;
                }

                try
                {
                    NativeMethods.HIDD_ATTRIBUTES attributes = new NativeMethods.HIDD_ATTRIBUTES();
                    attributes.Size = (uint)Marshal.SizeOf(typeof(NativeMethods.HIDD_ATTRIBUTES));
                    bool attrsOk = NativeMethods.HidD_GetAttributes(handle, ref attributes);

                    NativeMethods.HIDP_CAPS caps = new NativeMethods.HIDP_CAPS();
                    bool capsOk = false;
                    IntPtr preparsedData;
                    if (NativeMethods.HidD_GetPreparsedData(handle, out preparsedData))
                    {
                        try
                        {
                            capsOk = NativeMethods.HidP_GetCaps(preparsedData, ref caps) == HIDP_STATUS_SUCCESS;
                        }
                        finally
                        {
                            NativeMethods.HidD_FreePreparsedData(preparsedData);
                        }
                    }

                    Console.WriteLine("ok attrs={0} vid={1:X4} pid={2:X4} caps={3} usage={4:X4}:{5:X4} in={6} feature={7} path={8}",
                        attrsOk,
                        attributes.VendorID,
                        attributes.ProductID,
                        capsOk,
                        caps.UsagePage,
                        caps.Usage,
                        caps.InputReportByteLength,
                        caps.FeatureReportByteLength,
                        path);
                }
                finally
                {
                    NativeMethods.CloseHandle(handle);
                }
            }
        }

        private static string[] EnumeratePaths()
        {
            List<string> result = new List<string>();
            Guid hidGuid;
            NativeMethods.HidD_GetHidGuid(out hidGuid);
            IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref hidGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == InvalidHandle || deviceInfoSet == IntPtr.Zero)
            {
                return result.ToArray();
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA));
                    if (!NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                    {
                        break;
                    }

                    uint requiredSize;
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref interfaceData,
                        IntPtr.Zero,
                        0,
                        out requiredSize,
                        IntPtr.Zero);

                    if (requiredSize == 0)
                    {
                        continue;
                    }

                    IntPtr detail = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                                deviceInfoSet,
                                ref interfaceData,
                                detail,
                                requiredSize,
                                out requiredSize,
                                IntPtr.Zero))
                        {
                            continue;
                        }

                        string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                        if (!string.IsNullOrEmpty(path))
                        {
                            result.Add(path);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return result.ToArray();
        }

        private static HidDevice ReadInfo(string path)
        {
            IntPtr handle = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (handle == InvalidHandle || handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                NativeMethods.HIDD_ATTRIBUTES attributes = new NativeMethods.HIDD_ATTRIBUTES();
                attributes.Size = (uint)Marshal.SizeOf(typeof(NativeMethods.HIDD_ATTRIBUTES));
                if (!NativeMethods.HidD_GetAttributes(handle, ref attributes))
                {
                    return null;
                }

                HidDevice device = new HidDevice();
                device.Path = path;
                device.VendorId = attributes.VendorID;
                device.ProductId = attributes.ProductID;
                device.Version = attributes.VersionNumber;

                IntPtr preparsedData;
                if (NativeMethods.HidD_GetPreparsedData(handle, out preparsedData))
                {
                    try
                    {
                        NativeMethods.HIDP_CAPS caps = new NativeMethods.HIDP_CAPS();
                        int status = NativeMethods.HidP_GetCaps(preparsedData, ref caps);
                        if (status == HIDP_STATUS_SUCCESS)
                        {
                            device.Usage = caps.Usage;
                            device.UsagePage = caps.UsagePage;
                            device.InputReportByteLength = caps.InputReportByteLength;
                            device.OutputReportByteLength = caps.OutputReportByteLength;
                            device.FeatureReportByteLength = caps.FeatureReportByteLength;
                        }
                    }
                    finally
                    {
                        NativeMethods.HidD_FreePreparsedData(preparsedData);
                    }
                }

                device.ProductName = ReadString(handle, true);
                device.Manufacturer = ReadString(handle, false);
                device.SerialNumber = ReadSerial(handle);
                return device;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        private static string ReadString(IntPtr handle, bool product)
        {
            StringBuilder buffer = new StringBuilder(256);
            bool ok = product
                ? NativeMethods.HidD_GetProductString(handle, buffer, 512)
                : NativeMethods.HidD_GetManufacturerString(handle, buffer, 512);
            return ok ? buffer.ToString().Trim() : "";
        }

        private static string ReadSerial(IntPtr handle)
        {
            StringBuilder buffer = new StringBuilder(256);
            bool ok = NativeMethods.HidD_GetSerialNumberString(handle, buffer, 512);
            return ok ? buffer.ToString().Trim() : "";
        }

        public bool Open(bool write)
        {
            Close();
            uint access = write
                ? NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE
                : NativeMethods.GENERIC_READ;

            Handle = NativeMethods.CreateFile(
                Path,
                access,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            CanWrite = IsOpen;
            if (!IsOpen && write)
            {
                LastError = Marshal.GetLastWin32Error();
                Handle = NativeMethods.CreateFile(
                    Path,
                    NativeMethods.GENERIC_READ,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);
                CanWrite = false;
            }

            if (!IsOpen)
            {
                LastError = Marshal.GetLastWin32Error();
                return false;
            }

            return true;
        }

        public void DumpCaps()
        {
            if (!IsOpen && !Open(false))
            {
                Console.WriteLine("open failed: {0}", LastError);
                return;
            }

            IntPtr preparsedData;
            if (!NativeMethods.HidD_GetPreparsedData(Handle, out preparsedData))
            {
                Console.WriteLine("HidD_GetPreparsedData failed: {0}", Marshal.GetLastWin32Error());
                return;
            }

            try
            {
                NativeMethods.HIDP_CAPS caps = new NativeMethods.HIDP_CAPS();
                int status = NativeMethods.HidP_GetCaps(preparsedData, ref caps);
                Console.WriteLine("caps status=0x{0:X8} usage={1:X4}:{2:X4} in={3} out={4} feature={5}",
                    status, caps.UsagePage, caps.Usage,
                    caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength);
                Console.WriteLine("valueCaps input={0} output={1} feature={2}",
                    caps.NumberInputValueCaps, caps.NumberOutputValueCaps, caps.NumberFeatureValueCaps);
                Console.WriteLine("buttonCaps input={0} output={1} feature={2}",
                    caps.NumberInputButtonCaps, caps.NumberOutputButtonCaps, caps.NumberFeatureButtonCaps);

                DumpValueCaps(preparsedData, 0, caps.NumberInputValueCaps, "input");
                DumpValueCaps(preparsedData, 1, caps.NumberOutputValueCaps, "output");
                DumpValueCaps(preparsedData, 2, caps.NumberFeatureValueCaps, "feature");
            }
            finally
            {
                NativeMethods.HidD_FreePreparsedData(preparsedData);
            }
        }

        private static void DumpValueCaps(IntPtr preparsedData, int reportType, ushort count, string label)
        {
            if (count == 0)
            {
                Console.WriteLine("  {0}: none", label);
                return;
            }

            NativeMethods.HIDP_VALUE_CAPS[] caps = new NativeMethods.HIDP_VALUE_CAPS[count];
            ushort length = count;
            int status = NativeMethods.HidP_GetValueCaps(reportType, caps, ref length, preparsedData);
            Console.WriteLine("  {0}: status=0x{1:X8} count={2}", label, status, length);
            for (int i = 0; i < length; i++)
            {
                NativeMethods.HIDP_VALUE_CAPS cap = caps[i];
                if (cap.Range == null)
                {
                    cap.Range = new ushort[8];
                }
                Console.WriteLine("    usagePage={0:X4} reportId={1} bitSize={2} count={3} logical={4}..{5} range={6} usage={7:X4} usageMax={8:X4} absolute={9}",
                    cap.UsagePage,
                    cap.ReportID,
                    cap.BitSize,
                    cap.ReportCount,
                    cap.LogicalMin,
                    cap.LogicalMax,
                    cap.IsRange,
                    cap.Range[0],
                    cap.Range[1],
                    cap.IsAbsolute);
            }
        }
        public bool GetFeature(byte[] buffer)
        {
            if (!IsOpen)
            {
                return false;
            }
            bool ok = NativeMethods.HidD_GetFeature(Handle, buffer, (uint)buffer.Length);
            LastError = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }

        public bool GetInputReport(byte[] buffer)
        {
            if (!IsOpen)
            {
                return false;
            }
            bool ok = NativeMethods.HidD_GetInputReport(Handle, buffer, (uint)buffer.Length);
            LastError = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }
        public bool SendFeature(byte[] report)
        {
            return SendFeature(report, true);
        }

        public bool SendFeature(byte[] report, bool allowPad)
        {
            if (!IsOpen || !CanWrite)
            {
                LastError = 5;
                LastFeatureMethod = "none";
                return false;
            }

            bool ok = TryFeatureBuffer(report, false, "hid");
            if (ok)
            {
                return true;
            }

            ok = TryFeatureBuffer(report, true, "ioctl");
            if (ok)
            {
                return true;
            }

            if (allowPad && FeatureReportByteLength > report.Length)
            {
                byte[] buffer = new byte[FeatureReportByteLength];
                Array.Copy(report, buffer, report.Length);
                ok = TryFeatureBuffer(buffer, false, "hid-padded");
                if (ok)
                {
                    return true;
                }
                ok = TryFeatureBuffer(buffer, true, "ioctl-padded");
                if (ok)
                {
                    return true;
                }
            }

            LastFeatureMethod = "none";
            return false;
        }

        private bool TryFeatureBuffer(byte[] buffer, bool ioctl, string label)
        {
            bool ok;
            if (ioctl)
            {
                uint returned;
                ok = NativeMethods.DeviceIoControl(
                    Handle,
                    NativeMethods.IOCTL_HID_SET_FEATURE,
                    buffer,
                    (uint)buffer.Length,
                    null,
                    0,
                    out returned,
                    IntPtr.Zero);
            }
            else
            {
                ok = NativeMethods.HidD_SetFeature(Handle, buffer, (uint)buffer.Length);
            }

            LastError = ok ? 0 : Marshal.GetLastWin32Error();
            if (ok)
            {
                LastFeatureLength = buffer.Length;
                LastFeatureMethod = label;
            }
            return ok;
        }

        public bool SendFeatureIoctl(byte[] report)
        {
            if (!IsOpen || !CanWrite)
            {
                LastError = 5;
                LastFeatureMethod = "none";
                return false;
            }
            return TryFeatureBuffer(report, true, "ioctl");
        }

        public bool SendOutput(byte[] report)
        {
            if (SendOutputHid(report))
            {
                return true;
            }
            return SendOutputWriteFile(report);
        }

        public bool SendOutputHid(byte[] report)
        {
            if (!IsOpen || !CanWrite)
            {
                LastError = 5;
                LastWriteMethod = "none";
                return false;
            }

            bool ok = NativeMethods.HidD_SetOutputReport(Handle, report, (uint)report.Length);
            LastError = ok ? 0 : Marshal.GetLastWin32Error();
            LastWriteMethod = ok ? "hid-output" : "none";
            if (ok)
            {
                LastOutputLength = report.Length;
            }
            return ok;
        }

        public bool SendOutputWriteFile(byte[] report)
        {
            if (!IsOpen || !CanWrite)
            {
                LastError = 5;
                LastWriteMethod = "none";
                return false;
            }

            uint written;
            bool ok = NativeMethods.WriteFile(Handle, report, (uint)report.Length, out written, IntPtr.Zero);
            LastError = ok ? 0 : Marshal.GetLastWin32Error();
            LastWriteMethod = ok ? "writefile" : "none";
            if (ok)
            {
                LastOutputLength = report.Length;
            }
            return ok;
        }

        public int LastFeatureLength;
        public string LastFeatureMethod = "none";
        public int LastOutputLength;
        public string LastWriteMethod = "none";

        public int Read(byte[] buffer)
        {
            if (!IsOpen)
            {
                LastError = 6;
                return -2;
            }

            uint bytesRead;
            if (!NativeMethods.ReadFile(Handle, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero))
            {
                LastError = Marshal.GetLastWin32Error();
                return LastError == NativeMethods.ERROR_OPERATION_ABORTED ? -1 : -2;
            }

            return (int)bytesRead;
        }

        public void Close()
        {
            if (IsOpen)
            {
                NativeMethods.CancelIoEx(Handle, IntPtr.Zero);
                NativeMethods.CloseHandle(Handle);
            }
            Handle = IntPtr.Zero;
            CanWrite = false;
        }

        public void Dispose()
        {
            Close();
        }

        public override string ToString()
        {
            return string.Format(
                "{0,-6} usage={1:X4}:{2:X4} in={3} out={4} feature={5} path={6}",
                CollectionName,
                UsagePage,
                Usage,
                InputReportByteLength,
                OutputReportByteLength,
                FeatureReportByteLength,
                Path);
        }
    }
}
