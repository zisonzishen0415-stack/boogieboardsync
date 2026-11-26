using System;
using System.Runtime.InteropServices;

namespace BoogieBoardSync
{
    internal sealed class PenInjector : IDisposable
    {
        private readonly object sync = new object();
        private bool mouseMode;
        private IntPtr penDevice;
        private bool penFirstContact;
        private bool down;
        private bool rightDown;
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
            EnsurePenDevice();
            if (penDevice == IntPtr.Zero)
            {
                Console.WriteLine("WARN: CreateSyntheticPointerDevice failed (error {0}); using mouse injection.",
                    Marshal.GetLastWin32Error());
                mouseMode = true;
            }
        }

        public string ModeName
        {
            get
            {
                lock (sync)
                {
                    return mouseMode ? "mouse" : "pen";
                }
            }
        }

        public int InjectedCount
        {
            get { lock (sync) { return injectedCount; } }
        }

        public int FailedCount
        {
            get { lock (sync) { return failedCount; } }
        }

        public void SetMouseMode(bool useMouse)
        {
            lock (sync)
            {
                if (mouseMode == useMouse)
                {
                    return;
                }

                if (!useMouse)
                {
                    EnsurePenDevice();
                    if (penDevice == IntPtr.Zero)
                    {
                        return;
                    }
                }

                ReleaseInternal();
                mouseMode = useMouse;
            }
        }

        public void Update(NativeMethods.POINT point, int pressure, bool isDown, bool hover, bool side)
        {
            lock (sync)
            {
                pressure = Clamp(pressure, 0, 1024);

                if (mouseMode)
                {
                    UpdateMouse(point, isDown, side);
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
                        NativeMethods.POINT upPoint = new NativeMethods.POINT(lastX, lastY);
                        InjectPen(upPoint, 0, upFlags, false);
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
                    else if (inRange)
                    {
                        uint leaveFlags = NativeMethods.POINTER_FLAG_INRANGE
                            | NativeMethods.POINTER_FLAG_PRIMARY
                            | NativeMethods.POINTER_FLAG_CONFIDENCE
                            | NativeMethods.POINTER_FLAG_UP;
                        NativeMethods.POINT leavePoint = new NativeMethods.POINT(lastX, lastY);
                        InjectPen(leavePoint, 0, leaveFlags, false);
                        penFirstContact = false;
                    }

                    inRange = hover;
                }

                lastX = point.X;
                lastY = point.Y;
                lastPressure = pressure;
                lastSide = side;
            }
        }

        public void Release()
        {
            lock (sync)
            {
                ReleaseInternal();
            }
        }

        private void ReleaseInternal()
        {
            if (mouseMode)
            {
                if (down)
                {
                    SendMouseButton(NativeMethods.MOUSEEVENTF_LEFTUP);
                    down = false;
                }
                if (rightDown)
                {
                    SendMouseButton(NativeMethods.MOUSEEVENTF_RIGHTUP);
                    rightDown = false;
                }
                return;
            }

            if (down || inRange)
            {
                NativeMethods.POINT point = new NativeMethods.POINT(lastX, lastY);
                uint flags = NativeMethods.POINTER_FLAG_INRANGE
                    | NativeMethods.POINTER_FLAG_PRIMARY
                    | NativeMethods.POINTER_FLAG_CONFIDENCE
                    | NativeMethods.POINTER_FLAG_UP;
                InjectPen(point, 0, flags, false);
            }

            down = false;
            inRange = false;
            penFirstContact = false;
        }

        private void EnsurePenDevice()
        {
            if (penDevice != IntPtr.Zero)
            {
                return;
            }
            penDevice = NativeMethods.CreateSyntheticPointerDevice(
                NativeMethods.PT_PEN,
                1,
                NativeMethods.POINTER_INPUT_MODE_ABSOLUTE);
        }

        private void InjectPen(NativeMethods.POINT point, int pressure, uint flags, bool side)
        {
            if (penDevice == IntPtr.Zero)
            {
                failedCount++;
                return;
            }

            point.X -= NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            point.Y -= NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);

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
            info.penInfo.penFlags = side ? NativeMethods.PEN_FLAG_BARREL : 0;
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

        private void UpdateMouse(NativeMethods.POINT point, bool isDown, bool side)
        {
            NativeMethods.SetCursorPos(point.X, point.Y);

            if (side && !rightDown)
            {
                SendMouseButton(NativeMethods.MOUSEEVENTF_RIGHTDOWN);
                rightDown = true;
                injectedCount++;
            }
            else if (!side && rightDown)
            {
                SendMouseButton(NativeMethods.MOUSEEVENTF_RIGHTUP);
                rightDown = false;
                injectedCount++;
            }

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
            lock (sync)
            {
                ReleaseInternal();
                if (penDevice != IntPtr.Zero)
                {
                    NativeMethods.DestroySyntheticPointerDevice(penDevice);
                    penDevice = IntPtr.Zero;
                }
            }
        }

        public string LastState
        {
            get
            {
                lock (sync)
                {
                    return string.Format("x={0} y={1} p={2} down={3} hover={4} side={5} mouse={6}",
                        lastX, lastY, lastPressure, down, inRange, lastSide, mouseMode);
                }
            }
        }
    }
}
