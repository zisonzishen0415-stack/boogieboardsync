using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class PressureMonitor : Control
    {
        private int rawPressure;
        private int mappedPressure;
        private bool touch;
        private bool hover;
        private int ageMs = int.MaxValue;

        public PressureMonitor()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            Height = 70;
            BackColor = Color.White;
            AccessibleName = "实时力度";
        }

        public void UpdateFromRuntime(DriverRuntime runtime)
        {
            int newRaw = runtime.LastRawPressure;
            int newMapped = runtime.LastMappedPressure;
            bool newTouch = runtime.LastTouch;
            bool newHover = runtime.LastHover;
            int newAge = runtime.LastInputAgeMs;
            if (newRaw != rawPressure || newMapped != mappedPressure
                || newTouch != touch || newHover != hover || Math.Abs(newAge - ageMs) > 100)
            {
                rawPressure = newRaw;
                mappedPressure = newMapped;
                touch = newTouch;
                hover = newHover;
                ageMs = newAge;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle border = new Rectangle(0, 0, Width - 1, Height - 1);
            using (Pen borderPen = new Pen(Color.FromArgb(215, 221, 230)))
            {
                g.DrawRectangle(borderPen, border);
            }

            bool active = ageMs < 1200;
            string state = !active ? "等待在板上书写..." : (touch ? "接触" : (hover ? "悬停" : "离板"));
            string values = active
                ? string.Format("原始 {0}    输出 {1} / 1024", rawPressure, mappedPressure)
                : "在板上画几笔，力度会实时变化";
            Color stateColor = touch ? Color.FromArgb(32, 145, 94) : Color.FromArgb(110, 118, 130);

            TextRenderer.DrawText(
                g,
                "实时力度",
                Font,
                new Rectangle(12, 7, 90, 20),
                Color.FromArgb(55, 62, 72),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(
                g,
                values,
                Font,
                new Rectangle(92, 7, Math.Max(40, Width - 210), 20),
                Color.FromArgb(55, 62, 72),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                g,
                state,
                Font,
                new Rectangle(Math.Max(10, Width - 100), 7, 88, 20),
                stateColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            Rectangle bar = new Rectangle(12, 34, Math.Max(10, Width - 24), 20);
            using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(233, 237, 242)))
            {
                FillRoundedRectangle(g, backBrush, bar, 8);
            }

            int ratio = mappedPressure;
            if (ratio < 0) ratio = 0;
            if (ratio > 1024) ratio = 1024;
            int fillWidth = (int)Math.Round(bar.Width * (ratio / 1024.0));
            if (fillWidth > 0)
            {
                Rectangle fill = new Rectangle(bar.Left, bar.Top, fillWidth, bar.Height);
                Color start = touch ? Color.FromArgb(45, 170, 110) : Color.FromArgb(150, 163, 180);
                Color end = touch ? Color.FromArgb(23, 126, 82) : Color.FromArgb(120, 132, 148);
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    fill, start, end, LinearGradientMode.Horizontal))
                {
                    FillRoundedRectangle(g, brush, fill, 8);
                }
            }

            using (SolidBrush dotBrush = new SolidBrush(stateColor))
            {
                int dotSize = ageMs < 250 ? 9 : 7;
                g.FillEllipse(dotBrush, new Rectangle(bar.Right + 12, bar.Top + 6, dotSize, dotSize));
            }
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }
            int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }
}