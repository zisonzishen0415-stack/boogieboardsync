using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class MappingPreview : Control
    {
        private static readonly Color BorderColor = Color.FromArgb(215, 221, 230);
        private static readonly Color PanelColor = Color.FromArgb(250, 251, 253);
        private static readonly Color TextColor = Color.FromArgb(55, 62, 72);
        private static readonly Color MutedColor = Color.FromArgb(120, 128, 140);
        private static readonly Color AccentColor = Color.FromArgb(45, 110, 220);
        private static readonly Color DotColor = Color.FromArgb(32, 145, 94);

        private readonly DriverRuntime runtime;

        public MappingPreview(DriverRuntime runtime)
        {
            this.runtime = runtime;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            Height = 220;
            BackColor = Color.White;
            AccessibleName = "平板到屏幕的映射预览";
        }

        public void UpdateFromRuntime()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen borderPen = new Pen(BorderColor))
            {
                g.DrawRectangle(borderPen, new Rectangle(0, 0, Width - 1, Height - 1));
            }

            int pad = 12;
            int labelHeight = 20;
            int contentTop = pad + labelHeight;
            int contentHeight = Math.Max(40, Height - contentTop - 34);
            int gap = 50;
            int half = Math.Max(80, (Width - pad * 2 - gap) / 2);

            int rawWidth = Math.Max(1, runtime.RawMaxX - runtime.RawMinX);
            int rawHeight = Math.Max(1, runtime.RawMaxY - runtime.RawMinY);
            Rectangle tabletRect = FitRect(
                new Rectangle(pad, contentTop, half, contentHeight),
                rawWidth / (double)rawHeight);

            Rectangle screenBounds;
            Screen[] screens = Screen.AllScreens;
            if (runtime.ScreenIndex >= 0 && runtime.ScreenIndex < screens.Length)
            {
                screenBounds = screens[runtime.ScreenIndex].Bounds;
            }
            else
            {
                screenBounds = SystemInformation.VirtualScreen;
            }

            int regionX = 0;
            int regionY = 0;
            int regionWidth = screenBounds.Width;
            int regionHeight = screenBounds.Height;
            if (runtime.MapWidth > 0 && runtime.MapHeight > 0)
            {
                regionX = Math.Min(runtime.MapX, Math.Max(0, screenBounds.Width - 1));
                regionY = Math.Min(runtime.MapY, Math.Max(0, screenBounds.Height - 1));
                regionWidth = Math.Min(runtime.MapWidth, screenBounds.Width - regionX);
                regionHeight = Math.Min(runtime.MapHeight, screenBounds.Height - regionY);
                if (regionWidth <= 0 || regionHeight <= 0)
                {
                    regionX = 0;
                    regionY = 0;
                    regionWidth = screenBounds.Width;
                    regionHeight = screenBounds.Height;
                }
            }

            Rectangle screenBox = new Rectangle(pad + half + gap, contentTop, half, contentHeight);
            Rectangle screenRect = FitRect(
                screenBox,
                screenBounds.Width / (double)Math.Max(1, screenBounds.Height));

            Rectangle regionRect = new Rectangle(
                screenRect.Left + (int)Math.Round(regionX / (double)screenBounds.Width * screenRect.Width),
                screenRect.Top + (int)Math.Round(regionY / (double)screenBounds.Height * screenRect.Height),
                Math.Max(1, (int)Math.Round(regionWidth / (double)screenBounds.Width * screenRect.Width)),
                Math.Max(1, (int)Math.Round(regionHeight / (double)screenBounds.Height * screenRect.Height)));

            using (SolidBrush panelBrush = new SolidBrush(PanelColor))
            {
                g.FillRectangle(panelBrush, tabletRect);
                g.FillRectangle(panelBrush, screenRect);
            }
            using (Pen outline = new Pen(Color.FromArgb(160, 160, 172)))
            {
                g.DrawRectangle(outline, tabletRect);
                g.DrawRectangle(outline, screenRect);
            }

            using (SolidBrush regionBrush = new SolidBrush(Color.FromArgb(42, AccentColor)))
            {
                g.FillRectangle(regionBrush, regionRect);
            }
            using (Pen regionPen = new Pen(AccentColor, 1.6f))
            {
                g.DrawRectangle(regionPen, regionRect);
            }

            if (runtime.MapFit == "uniform")
            {
                int contentRectWidth = regionRect.Width;
                int contentRectHeight = regionRect.Height;
                bool rotated = runtime.Rotate == 90 || runtime.Rotate == 270;
                double aspect = rotated ? rawHeight / (double)rawWidth : rawWidth / (double)rawHeight;
                if (contentRectWidth / (double)Math.Max(1, contentRectHeight) > aspect)
                {
                    contentRectWidth = Math.Max(1, (int)Math.Round(contentRectHeight * aspect));
                }
                else
                {
                    contentRectHeight = Math.Max(1, (int)Math.Round(contentRectWidth / aspect));
                }
                Rectangle contentRect = new Rectangle(
                    regionRect.Left + (regionRect.Width - contentRectWidth) / 2,
                    regionRect.Top + (regionRect.Height - contentRectHeight) / 2,
                    contentRectWidth,
                    contentRectHeight);
                using (Pen contentPen = new Pen(Color.FromArgb(150, AccentColor)))
                {
                    contentPen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(contentPen, contentRect);
                }
            }

            if (runtime.MapFit == "fill")
            {
                double targetAspect = regionRect.Width / (double)Math.Max(1, regionRect.Height);
                bool rotated = runtime.Rotate == 90 || runtime.Rotate == 270;
                double contentAspect = rotated
                    ? rawHeight / (double)rawWidth
                    : rawWidth / (double)rawHeight;
                double usedX = 1.0;
                double usedY = 1.0;
                if (contentAspect > targetAspect)
                {
                    double visible = targetAspect / contentAspect;
                    if (rotated) usedY = visible; else usedX = visible;
                }
                else
                {
                    double visible = contentAspect / targetAspect;
                    if (rotated) usedX = visible; else usedY = visible;
                }

                Rectangle used = new Rectangle(
                    tabletRect.Left + (int)Math.Round((1.0 - usedX) / 2.0 * tabletRect.Width),
                    tabletRect.Top + (int)Math.Round((1.0 - usedY) / 2.0 * tabletRect.Height),
                    Math.Max(1, (int)Math.Round(usedX * tabletRect.Width)),
                    Math.Max(1, (int)Math.Round(usedY * tabletRect.Height)));
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(60, 30, 34, 40)))
                {
                    g.FillRectangle(shade, tabletRect.Left, tabletRect.Top, tabletRect.Width, used.Top - tabletRect.Top);
                    g.FillRectangle(shade, tabletRect.Left, used.Bottom, tabletRect.Width, tabletRect.Bottom - used.Bottom);
                    g.FillRectangle(shade, tabletRect.Left, used.Top, used.Left - tabletRect.Left, used.Height);
                    g.FillRectangle(shade, used.Right, used.Top, tabletRect.Right - used.Right, used.Height);
                }
                using (Pen usedPen = new Pen(Color.FromArgb(180, AccentColor)))
                {
                    usedPen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(usedPen, used);
                }
            }

            DrawArrow(g, new Point(tabletRect.Right + 8, tabletRect.Top + tabletRect.Height / 2),
                new Point(screenRect.Left - 8, screenRect.Top + screenRect.Height / 2));

            TextRenderer.DrawText(g, "平板（原始）", Font,
                new Rectangle(tabletRect.Left, pad - 2, tabletRect.Width, labelHeight),
                TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            string screenName = ScreenUtil.LabelForIndex(runtime.ScreenIndex);
            TextRenderer.DrawText(g, "屏幕 · " + screenName, Font,
                new Rectangle(screenRect.Left, pad - 2, screenRect.Width, labelHeight),
                TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g,
                string.Format("{0} × {1}", rawWidth, rawHeight),
                Font,
                new Rectangle(tabletRect.Left, tabletRect.Bottom + 2, tabletRect.Width, 16),
                MutedColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            string regionText = runtime.MapWidth > 0 && runtime.MapHeight > 0
                ? string.Format("区域 {0},{1} {2}×{3} · {4}",
                    regionX, regionY, regionWidth, regionHeight,
                    DriverRuntime.DescribeFit(runtime.MapFit))
                : string.Format("{0}×{1} · 铺满 · {2}",
                    screenBounds.Width, screenBounds.Height,
                    DriverRuntime.DescribeFit(runtime.MapFit));
            TextRenderer.DrawText(g, regionText, Font,
                new Rectangle(screenRect.Left, screenRect.Bottom + 2, screenRect.Width, 16),
                MutedColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            DrawLiveDots(g, tabletRect, screenRect, screenBounds, regionRect);
        }

        private void DrawLiveDots(Graphics g, Rectangle tabletRect, Rectangle screenRect, Rectangle screenBounds, Rectangle regionRect)
        {
            int rawX = runtime.LastRawX;
            int rawY = runtime.LastRawY;
            int age = runtime.LastInputAgeMs;
            if (rawX < 0 || rawY < 0 || age > 5000)
            {
                return;
            }

            bool touch = runtime.LastTouch;
            int rawWidth = Math.Max(1, runtime.RawMaxX - runtime.RawMinX);
            int rawHeight = Math.Max(1, runtime.RawMaxY - runtime.RawMinY);
            double nx = Clamp01((rawX - runtime.RawMinX) / (double)rawWidth);
            double ny = Clamp01((rawY - runtime.RawMinY) / (double)rawHeight);
            int tx = tabletRect.Left + (int)Math.Round(nx * Math.Max(0, tabletRect.Width - 1));
            int ty = tabletRect.Top + (int)Math.Round(ny * Math.Max(0, tabletRect.Height - 1));
            DrawDot(g, tx, ty, touch, age < 1200);

            NativeMethods.POINT mapped = runtime.Current.Mapper.Map(rawX, rawY);
            double sx = Clamp01((mapped.X - screenBounds.Left) / (double)Math.Max(1, screenBounds.Width));
            double sy = Clamp01((mapped.Y - screenBounds.Top) / (double)Math.Max(1, screenBounds.Height));
            int px = screenRect.Left + (int)Math.Round(sx * Math.Max(0, screenRect.Width - 1));
            int py = screenRect.Top + (int)Math.Round(sy * Math.Max(0, screenRect.Height - 1));
            DrawDot(g, px, py, touch, age < 1200);
        }

        private static void DrawDot(Graphics g, int x, int y, bool touch, bool fresh)
        {
            int size = touch ? 10 : 8;
            Rectangle dot = new Rectangle(x - size / 2, y - size / 2, size, size);
            using (SolidBrush brush = new SolidBrush(touch ? DotColor : Color.White))
            {
                g.FillEllipse(brush, dot);
            }
            using (Pen pen = new Pen(touch ? Color.FromArgb(20, 105, 70) : Color.FromArgb(120, 128, 140), fresh ? 2f : 1f))
            {
                g.DrawEllipse(pen, dot);
            }
        }

        private static void DrawArrow(Graphics g, Point from, Point to)
        {
            if (to.X <= from.X)
            {
                return;
            }
            using (Pen pen = new Pen(Color.FromArgb(150, 160, 172), 1.4f))
            {
                pen.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(pen, from, to);
            }
        }

        private static Rectangle FitRect(Rectangle box, double aspect)
        {
            if (aspect <= 0.0)
            {
                aspect = 1.0;
            }
            double width = box.Width;
            double height = width / aspect;
            if (height > box.Height)
            {
                height = box.Height;
                width = height * aspect;
            }
            int w = Math.Max(1, (int)Math.Round(width));
            int h = Math.Max(1, (int)Math.Round(height));
            return new Rectangle(
                box.Left + (box.Width - w) / 2,
                box.Top + (box.Height - h) / 2,
                w,
                h);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }
    }
}
