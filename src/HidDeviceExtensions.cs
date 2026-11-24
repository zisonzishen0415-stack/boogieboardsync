using System;

namespace BoogieBoardSync
{
    internal static class HidDeviceExtensions
    {
        public static void CancelRead(this HidDevice device)
        {
            if (device != null && device.IsOpen)
            {
                NativeMethods.CancelIoEx(device.Handle, IntPtr.Zero);
            }
        }
    }
}
