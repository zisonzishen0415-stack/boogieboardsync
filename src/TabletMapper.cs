using System;

namespace BoogieBoardSync
{
    internal sealed class TabletMapper
    {
        private readonly int rawMinX;
        private readonly int rawMaxX;
        private readonly int rawMinY;
        private readonly int rawMaxY;
        private readonly double rawWidth;
        private readonly double rawHeight;
        private readonly int rotate;
        private readonly bool flipX;
        private readonly bool flipY;
        private readonly int drawLeft;
        private readonly int drawTop;
        private readonly int drawWidth;
        private readonly int drawHeight;
        private readonly string fit;

        public TabletMapper(
            int rawMinX, int rawMaxX, int rawMinY, int rawMaxY,
            int rotate, bool flipX, bool flipY,
            int left, int top, int width, int height, string fit)
        {
            this.rawMinX = rawMinX;
            this.rawMaxX = rawMaxX;
            this.rawMinY = rawMinY;
            this.rawMaxY = rawMaxY;
            this.rawWidth = Math.Max(1, rawMaxX - rawMinX);
            this.rawHeight = Math.Max(1, rawMaxY - rawMinY);
            this.rotate = rotate;
            this.flipX = flipX;
            this.flipY = flipY;
            this.fit = fit ?? "stretch";

            int contentWidth = Math.Max(1, width);
            int contentHeight = Math.Max(1, height);
            if (this.fit == "uniform")
            {
                double aspect = ContentAspect;
                if (contentWidth / (double)contentHeight > aspect)
                {
                    contentWidth = Math.Max(1, (int)Math.Round(contentHeight * aspect));
                }
                else
                {
                    contentHeight = Math.Max(1, (int)Math.Round(contentWidth / aspect));
                }
            }

            drawLeft = left + (width - contentWidth) / 2;
            drawTop = top + (height - contentHeight) / 2;
            drawWidth = contentWidth;
            drawHeight = contentHeight;
        }

        private double ContentAspect
        {
            get
            {
                return (rotate == 90 || rotate == 270)
                    ? rawHeight / rawWidth
                    : rawWidth / rawHeight;
            }
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

            if (fit == "fill")
            {
                double targetAspect = drawWidth / (double)Math.Max(1, drawHeight);
                double aspect = ContentAspect;
                if (aspect > targetAspect)
                {
                    double visible = targetAspect / aspect;
                    double offset = (1.0 - visible) / 2.0;
                    rx = (rx - offset) / visible;
                }
                else
                {
                    double visible = aspect / targetAspect;
                    double offset = (1.0 - visible) / 2.0;
                    ry = (ry - offset) / visible;
                }
                rx = Clamp01(rx);
                ry = Clamp01(ry);
            }

            int x = drawLeft + (int)Math.Round(rx * Math.Max(0, drawWidth - 1));
            int y = drawTop + (int)Math.Round(ry * Math.Max(0, drawHeight - 1));
            return new NativeMethods.POINT(x, y);
        }

        public string Describe()
        {
            return string.Format(
                "tablet raw=({0},{1})-({2},{3}) rotate={4} flipX={5} flipY={6} draw=({7},{8}) {9}x{10} fit={11}",
                rawMinX,
                rawMinY,
                rawMaxX,
                rawMaxY,
                rotate,
                flipX,
                flipY,
                drawLeft,
                drawTop,
                drawWidth,
                drawHeight,
                fit);
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

        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }
    }
}
