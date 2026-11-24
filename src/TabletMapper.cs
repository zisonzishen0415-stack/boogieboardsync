using System;

namespace BoogieBoardSync
{
    internal sealed class TabletMapper
    {
        private readonly int rawMinX;
        private readonly int rawMaxX;
        private readonly int rawMinY;
        private readonly int rawMaxY;
        private readonly int rotate;
        private readonly bool flipX;
        private readonly bool flipY;
        private readonly int screenLeft;
        private readonly int screenTop;
        private readonly int screenWidth;
        private readonly int screenHeight;

        public TabletMapper(Options options)
        {
            rawMinX = options.RawMinX;
            rawMaxX = options.RawMaxX;
            rawMinY = options.RawMinY;
            rawMaxY = options.RawMaxY;
            rotate = options.Rotate;
            flipX = options.FlipX;
            flipY = options.FlipY;

            screenLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            screenTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        }

        public NativeMethods.POINT Map(int rawX, int rawY)
        {
            double nx = Normalize(rawX, rawMinX, rawMaxX);
            double ny = Normalize(rawY, rawMinY, rawMaxY);

            if (flipX)
            {
                nx = 1.0 - nx;
            }
            if (flipY)
            {
                ny = 1.0 - ny;
            }

            double rx = nx;
            double ry = ny;
            if (rotate == 90)
            {
                rx = ny;
                ry = 1.0 - nx;
            }
            else if (rotate == 180)
            {
                rx = 1.0 - nx;
                ry = 1.0 - ny;
            }
            else if (rotate == 270)
            {
                rx = 1.0 - ny;
                ry = nx;
            }

            int x = screenLeft + (int)Math.Round(rx * Math.Max(0, screenWidth - 1));
            int y = screenTop + (int)Math.Round(ry * Math.Max(0, screenHeight - 1));
            return new NativeMethods.POINT(x, y);
        }

        public string Describe()
        {
            return string.Format(
                "tablet raw=({0},{1})-({2},{3}) rotate={4} flipX={5} flipY={6} screen=({7},{8}) {9}x{10}",
                rawMinX,
                rawMinY,
                rawMaxX,
                rawMaxY,
                rotate,
                flipX,
                flipY,
                screenLeft,
                screenTop,
                screenWidth,
                screenHeight);
        }

        private static double Normalize(int value, int min, int max)
        {
            if (max <= min)
            {
                return 0.5;
            }

            double normalized = (value - min) / (double)(max - min);
            if (normalized < 0.0)
            {
                return 0.0;
            }
            if (normalized > 1.0)
            {
                return 1.0;
            }
            return normalized;
        }
    }
}
