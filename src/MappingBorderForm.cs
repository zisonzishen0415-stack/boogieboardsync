using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class MappingBorderForm : Form
    {
        private readonly Rectangle screenBounds;
        private readonly int regionX;
        private readonly int regionY;
        private readonly int regionWidth;
        private readonly int regionHeight;
        private readonly int rawWidth;
        private readonly int rawHeight;
        private readonly int rotate;
        private readonly bool uniform;
        private readonly string label;
        private readonly Font labelFont;
        private readonly Timer closeTimer;

        public MappingBorderForm(
            Rectangle screenBounds,
            int regionX, int regionY, int regionWidth, int regionHeight,
            int rawWidth, int rawHeight, int rotate, bool uniform, string label)
        {
            this.screenBounds = screenBounds;
            this.regionX = regionX;
            this.regionY = regionY;
            this.regionWidth = regionWidth;
            this.regionHeight = regionHeight;
            this.rawWidth = Math.Max(1, rawWidth);
            this.rawHeight = Math.Max(1, rawHeight);
            this.rotate = rotate;
            this.uniform = uniform;
            this.label = label;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenBounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            labelFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);

            closeTimer = new Timer();
            closeTimer.Interval = 3000;
            closeTimer.Tick += delegate(object sender, EventArgs e)
            {
                closeTimer.Stop();
                Close();
            };
            closeTimer.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x20;
                parameters.ExStyle |= 0x80000;
                return parameters;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (closeTimer != null)
                {
                    closeTimer.Stop();
                    closeTimer.Dispose();
                }
                if (labelFont != null)
                {
                    labelFont.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int x = regionX - screenBounds.Left;
            int y = regionY - screenBounds.Top;
            Rectangle region = new Rectangle(x, y, regionWidth, regionHeight);
            region.Inflate(-1, -1);

            using (Pen border = new Pen(Color.FromArgb(255, 70, 190, 255), 3f))
            {
                g.DrawRectangle(border, region);
            }

            if (uniform)
            {
                int contentWidth = region.Width;
                int contentHeight = region.Height;
                bool rotated = rotate == 90 || rotate == 270;
                double aspect = rotated
                    ? rawHeight / (double)rawWidth
                    : rawWidth / (double)rawHeight;
                if (contentWidth / (double)Math.Max(1, contentHeight) > aspect)
                {
                    contentWidth = Math.Max(1, (int)Math.Round(contentHeight * aspect));
                }
                else
                {
                    contentHeight = Math.Max(1, (int)Math.Round(contentWidth / aspect));
                }
                Rectangle content = new Rectangle(
                    region.Left + (region.Width - contentWidth) / 2,
                    region.Top + (region.Height - contentHeight) / 2,
                    contentWidth,
                    contentHeight);
                using (Pen inner = new Pen(Color.FromArgb(220, 150, 255, 170), 2f))
                {
                    inner.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(inner, content);
                }
            }

            Size textSize = TextRenderer.MeasureText(label, labelFont);
            int labelWidth = textSize.Width + 24;
            int labelHeight = textSize.Height + 14;
            int labelX = Math.Max(region.Left, Math.Min(region.Right - labelWidth, region.Left + 10));
            int labelY = Math.Max(region.Top, Math.Min(region.Bottom - labelHeight, region.Top + 10));
            Rectangle labelRect = new Rectangle(labelX, labelY, labelWidth, labelHeight);
            using (SolidBrush background = new SolidBrush(Color.FromArgb(235, 18, 22, 30)))
            {
                g.FillRectangle(background, labelRect);
            }
            using (Pen labelBorder = new Pen(Color.FromArgb(255, 70, 190, 255)))
            {
                g.DrawRectangle(labelBorder, labelRect);
            }
            TextRenderer.DrawText(g, label, labelFont, labelRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
