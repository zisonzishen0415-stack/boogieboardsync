using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class PressureCurve : Control
    {
        private static readonly Color BorderColor = Color.FromArgb(215, 221, 230);
        private static readonly Color TextColor = Color.FromArgb(55, 62, 72);
        private static readonly Color MutedColor = Color.FromArgb(120, 128, 140);
        private static readonly Color CurveColor = Color.FromArgb(45, 110, 220);
        private static readonly Color DotColor = Color.FromArgb(32, 145, 94);
        private static readonly Color DeadzoneColor = Color.FromArgb(28, 240, 170, 90);

        private readonly DriverRuntime runtime;

        public PressureCurve(DriverRuntime runtime)
        {
            this.runtime = runtime;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            Height = 170;
            BackColor = Color.White;
            AccessibleName = "力度曲线";
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

            int left = 46;
            int top = 24;
            int right = Width - 16;
            int bottom = Height - 26;
            if (right - left < 40 || bottom - top < 40)
            {
                return;
            }
            Rectangle plot = new Rectangle(left, top, right - left, bottom - top);

            int xMax = Math.Max(1024, runtime.PressureMax);
            using (SolidBrush plotBrush = new SolidBrush(Color.FromArgb(251, 252, 254)))
            {
                g.FillRectangle(plotBrush, plot);
            }
            using (Pen gridPen = new Pen(Color.FromArgb(232, 236, 242)))
            {
                for (int i = 1; i < 4; i++)
                {
                    int gx = plot.Left + plot.Width * i / 4;
                    int gy = plot.Top + plot.Height * i / 4;
                    g.DrawLine(gridPen, gx, plot.Top, gx, plot.Bottom);
                    g.DrawLine(gridPen, plot.Left, gy, plot.Right, gy);
                }
            }
            using (Pen axisPen = new Pen(Color.FromArgb(170, 178, 190)))
            {
                g.DrawRectangle(axisPen, plot);
            }

            int pressureMin = runtime.PressureMin;
            int pressureMax = Math.Max(pressureMin + 1, runtime.PressureMax);
            double deadzone = runtime.PressureDeadzone;
            int deadRaw = pressureMin + (int)Math.Round(deadzone * (pressureMax - pressureMin));
            if (deadRaw > 0)
            {
                int deadX = plot.Left + (int)Math.Round(Math.Min(1.0, deadRaw / (double)xMax) * plot.Width);
                using (SolidBrush deadBrush = new SolidBrush(DeadzoneColor))
                {
                    g.FillRectangle(deadBrush, new Rectangle(plot.Left, plot.Top, Math.Max(0, deadX - plot.Left), plot.Height));
                }
            }

            PressureMapper mapper = new PressureMapper(
                pressureMin,
                pressureMax,
                runtime.PressureGamma,
                runtime.PressureDeadzone,
                0.0);

            Point[] points = new Point[plot.Width + 1];
            for (int i = 0; i <= plot.Width; i++)
            {
                int raw = (int)Math.Round(i / (double)Math.Max(1, plot.Width) * xMax);
                int mapped = mapper.Map(raw);
                int y = plot.Bottom - (int)Math.Round(Math.Min(1.0, mapped / 1024.0) * plot.Height);
                points[i] = new Point(plot.Left + i, y);
            }
            using (Pen curvePen = new Pen(CurveColor, 2f))
            {
                g.DrawLines(curvePen, points);
            }

            int rawX = runtime.LastRawX;
            int rawPressure = runtime.LastRawPressure;
            int age = runtime.LastInputAgeMs;
            if (rawX >= 0 && age < 3000)
            {
                int mapped = runtime.LastMappedPressure;
                int dx = plot.Left + (int)Math.Round(Math.Min(1.0, rawPressure / (double)xMax) * plot.Width);
                int dy = plot.Bottom - (int)Math.Round(Math.Min(1.0, mapped / 1024.0) * plot.Height);
                Rectangle dot = new Rectangle(dx - 5, dy - 5, 10, 10);
                using (SolidBrush dotBrush = new SolidBrush(runtime.LastTouch ? DotColor : Color.White))
                {
                    g.FillEllipse(dotBrush, dot);
                }
                using (Pen dotPen = new Pen(runtime.LastTouch ? Color.FromArgb(20, 105, 70) : MutedColor, 2f))
                {
                    g.DrawEllipse(dotPen, dot);
                }
            }

            TextRenderer.DrawText(g, "输出", Font,
                new Rectangle(6, 2, left - 12, 18), TextColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "1024", Font,
                new Rectangle(6, plot.Top - 2, left - 12, 16), MutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "0", Font,
                new Rectangle(6, plot.Bottom - 8, left - 12, 16), MutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "原始力度", Font,
                new Rectangle(plot.Left, plot.Bottom + 4, Math.Max(40, plot.Width - 60), 16), MutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "0", Font,
                new Rectangle(plot.Left - 4, plot.Bottom + 4, 24, 16), MutedColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, xMax.ToString(), Font,
                new Rectangle(plot.Right - 60, plot.Bottom + 4, 60, 16), MutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            string preset = runtime.PressureGamma < 0.95 ? "柔和" : (runtime.PressureGamma > 1.05 ? "硬朗" : "线性");
            string summary = string.Format(
                "原始 {0}–{1} · gamma {2:0.00} · 死区 {3:0.00} · {4}",
                pressureMin, pressureMax, runtime.PressureGamma, runtime.PressureDeadzone, preset);
            TextRenderer.DrawText(g, summary, Font,
                new Rectangle(plot.Left, 2, plot.Width, 18), TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
