using System;
using System.Runtime.InteropServices;

namespace BoogieBoardSync
{
    internal sealed class PenInjector : IDisposable
    {
        private readonly bool mouseMode;
        private IntPtr penDevice;
        private bool penFirstContact;
        private bool down;
        private bool inRange;
        private bool lastSide;
        private int lastX;
        private int lastY;
        private int lastPressure;
        private int injectedCount;
        private int failedCount;

        public PenInjector(bool useMouse)
        {
            mouseMode = useMouse;
            if (!mouseMode)
            {
                penDevice = NativeMethods.CreateSyntheticPointerDevice(
                    NativeMethods.PT_PEN,
                    1,
                    NativeMethods.POINTER_INPUT_MODE_ABSOLUTE);

                if (penDevice == IntPtr.Zero)
                {
                    Console.WriteLine("WARN: CreateSyntheticPointerDevice failed (error {0}); falling back to mouse injection.",
                        Marshal.GetLastWin32Error());
                    mouseMode = true;
                }
            }
        }

        public string ModeName
        {
            get { return mouseMode ? "mouse" : "pen"; }
        }

        public int InjectedCount
        {
            get { return injectedCount; }
        }

        public int FailedCount
        {
            get { return failedCount; }
        }

        public void Update(NativeMethods.POINT point, int pressure, bool isDown, bool hover, bool side)
        {
            pressure = Clamp(pressure, 0, 1024);

            if (mouseMode)
            {
                UpdateMouse(point, isDown);
                inRange = hover || isDown;
                lastX = point.X;
                lastY = point.Y;
                lastPressure = pressure;
                lastSide = side;
                return;
            }

            if (isDown)
            {
                uint flags = NativeMethods.POINTER_FLAG_INRANGE
                    | NativeMethods.POINTER_FLAG_INCONTACT
                    | NativeMethods.POINTER_FLAG_PRIMARY
                    | NativeMethods.POINTER_FLAG_CONFIDENCE
                    | NativeMethods.POINTER_FLAG_FIRSTBUTTON
                    | NativeMethods.POINTER_FLAG_UPDATE;

                if (!down)
                {
                    flags |= NativeMethods.POINTER_FLAG_DOWN;
                    if (!penFirstContact)
                    {
                        flags |= NativeMethods.POINTER_FLAG_NEW;
                        penFirstContact = true;
                    }
                }

                InjectPen(point, pressure, flags, side);
                down = true;
                inRange = true;
            }
            else
            {
                bool wasDown = down;
                if (down)
                {
                    uint upFlags = NativeMethods.POINTER_FLAG_INRANGE
                        | NativeMethods.POINTER_FLAG_PRIMARY
                        | NativeMethods.POINTER_FLAG_CONFIDENCE
                        | NativeMethods.POINTER_FLAG_UP;
                    InjectPen(point, 0, upFlags, false);
                    down = false;
                }

                if (hover)
                {
                    uint hoverFlags = NativeMethods.POINTER_FLAG_INRANGE
                        | NativeMethods.POINTER_FLAG_PRIMARY
                        | NativeMethods.POINTER_FLAG_CONFIDENCE
                        | NativeMethods.POINTER_FLAG_UPDATE;
                    if (!inRange && !wasDown)
                    {
                        hoverFlags |= NativeMethods.POINTER_FLAG_NEW;
                    }
                    InjectPen(point, 0, hoverFlags, side);
                }
                else if (inRange && !wasDown)
                {
                    uint leaveFlags = NativeMethods.POINTER_FLAG_INRANGE
                        | NativeMethods.POINTER_FLAG_PRIMARY
                        | NativeMethods.POINTER_FLAG_CONFIDENCE
                        | NativeMethods.POINTER_FLAG_UP;
                    InjectPen(point, 0, leaveFlags, false);
                    penFirstContact = false;
                }

                inRange = hover;
            }

            lastX = point.X;
            lastY = point.Y;
            lastPressure = pressure;
            lastSide = side;
        }

        public void Release()
        {
            if (mouseMode)
            {
                if (down)
                {
                    SendMouseButton(NativeMethods.MOUSEEVENTF_LEFTUP);
                    down = false;
                }
                return;
            }

            if (down)
            {
                NativeMethods.POINT point = new NativeMethods.POINT(lastX, lastY);
                uint flags = NativeMethods.POINTER_FLAG_INRANGE
                    | NativeMethods.POINTER_FLAG_PRIMARY
                    | NativeMethods.POINTER_FLAG_CONFIDENCE
                    | NativeMethods.POINTER_FLAG_UP;
                InjectPen(point, 0, flags, false);
                down = false;
            }

            if (inRange)
            {
                NativeMethods.POINT point = new NativeMethods.POINT(lastX, lastY);
                uint flags = NativeMethods.POINTER_FLAG_INRANGE
                    | NativeMethods.POINTER_FLAG_PRIMARY
                    | NativeMethods.POINTER_FLAG_CONFIDENCE
                    | NativeMethods.POINTER_FLAG_UP;
                InjectPen(point, 0, flags, false);
                inRange = false;
                penFirstContact = false;
            }
        }

        private void InjectPen(NativeMethods.POINT point, int pressure, uint flags, bool side)
        {
            NativeMethods.POINTER_TYPE_INFO info = new NativeMethods.POINTER_TYPE_INFO();
            info.type = NativeMethods.PT_PEN;
            info.penInfo.pointerInfo.pointerType = NativeMethods.PT_PEN;
            info.penInfo.pointerInfo.pointerId = 1;
            info.penInfo.pointerInfo.pointerFlags = flags;
            if (side)
            {
                info.penInfo.pointerInfo.pointerFlags |= NativeMethods.POINTER_FLAG_SECONDBUTTON;
            }
            info.penInfo.pointerInfo.ptPixelLocation = point;
            info.penInfo.pointerInfo.ptPixelLocationRaw = point;
            info.penInfo.penMask = NativeMethods.PEN_MASK_PRESSURE;
            info.penInfo.pressure = (uint)pressure;

            bool ok = NativeMethods.InjectSyntheticPointerInput(penDevice, ref info, 1);
            if (ok)
            {
                injectedCount++;
            }
            else
            {
                failedCount++;
                if (failedCount <= 5)
                {
                    Console.WriteLine("WARN: InjectSyntheticPointerInput failed (error {0}).",
                        Marshal.GetLastWin32Error());
                }
            }
        }

        private void UpdateMouse(NativeMethods.POINT point, bool isDown)
        {
            NativeMethods.SetCursorPos(point.X, point.Y);

            if (isDown && !down)
            {
                SendMouseButton(NativeMethods.MOUSEEVENTF_LEFTDOWN);
                down = true;
                injectedCount++;
            }
            else if (!isDown && down)
            {
                SendMouseButton(NativeMethods.MOUSEEVENTF_LEFTUP);
                down = false;
                injectedCount++;
            }
        }

        private static void SendMouseButton(uint flag)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = NativeMethods.INPUT_MOUSE;
            input.mi.dwFlags = flag;
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            if (NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT))) == 0)
            {
                Console.WriteLine("WARN: SendInput failed (error {0}).", Marshal.GetLastWin32Error());
            }
        }

        private static int Clamp(int value, int min, int max)
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

        public void Dispose()
        {
            Release();
            if (penDevice != IntPtr.Zero)
            {
                NativeMethods.DestroySyntheticPointerDevice(penDevice);
                penDevice = IntPtr.Zero;
            }
        }

        public string LastState
        {
            get
            {
                return string.Format("x={0} y={1} p={2} down={3} hover={4} side={5}", lastX, lastY, lastPressure, down, inRange, lastSide);
            }
        }
    }
}
