using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class RegionPickerForm : Form
    {
        private readonly string targetName;
        private readonly DriverRuntime runtime;
        private readonly Action<bool> penModeChanged;
        private readonly Font titleFont;
        private readonly Font hintFont;
        private readonly CheckBox penCheck;
        private readonly Timer timer;
        private Point firstPoint;
        private Point currentPoint;
        private bool hasFirst;
        private bool penMode;
        private bool lastTouch;
        private bool armed = true;
        private long lastConfirmTicks;
        private string hint = "";

        public Rectangle SelectedRect { get; private set; }

        public RegionPickerForm(Rectangle bounds, string targetName, DriverRuntime runtime, Action<bool> penModeChanged)
        {
            this.targetName = targetName;
            this.runtime = runtime;
            this.penModeChanged = penModeChanged;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(14, 16, 20);
            Opacity = 0.5;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            titleFont = new Font(Font.FontFamily, 14f, FontStyle.Bold);
            hintFont = new Font(Font.FontFamily, 10f);

            penCheck = new CheckBox();
            penCheck.Text = "用笔点选（移动笔定位，落笔确认）";
            penCheck.AutoSize = true;
            penCheck.ForeColor = Color.White;
            penCheck.BackColor = Color.Transparent;
            penCheck.Location = new Point(24, 104);
            penCheck.CheckedChanged += delegate(object sender, EventArgs e) { OnPenModeChanged(); };
            Controls.Add(penCheck);

            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer, true);

            timer = new Timer();
            timer.Interval = 40;
            timer.Tick += delegate(object sender, EventArgs e) { OnTick(); };
            timer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            currentPoint = Cursor.Position;
            Activate();
            BringToFront();
            Focus();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                }
                if (titleFont != null)
                {
                    titleFont.Dispose();
                }
                if (hintFont != null)
                {
                    hintFont.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void OnPenModeChanged()
        {
            penMode = penCheck.Checked;
            lastTouch = runtime.LastTouch;
            armed = !runtime.LastTouch;
            hint = "";
            if (penModeChanged != null)
            {
                penModeChanged(penMode);
            }
            Invalidate();
        }

        private void OnTick()
        {
            currentPoint = Cursor.Position;
            if (penMode)
            {
                bool touch = runtime.LastTouch;
                if (!touch)
                {
                    armed = true;
                }
                else if (!lastTouch && armed
                    && runtime.LastInputAgeMs < 400
                    && DateTime.UtcNow.Ticks - lastConfirmTicks > TimeSpan.TicksPerMillisecond * 500)
                {
                    ConfirmPoint(Cursor.Position);
                    lastConfirmTicks = DateTime.UtcNow.Ticks;
                    armed = false;
                }
                lastTouch = touch;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            currentPoint = Cursor.Position;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                Cancel();
                return;
            }
            if (e.Button != MouseButtons.Left || penMode)
            {
                return;
            }
            ConfirmPoint(Cursor.Position);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }
        }

        private void ConfirmPoint(Point screenPoint)
        {
            if (!hasFirst)
            {
                firstPoint = screenPoint;
                hasFirst = true;
                hint = "";
                Invalidate();
                return;
            }

            Rectangle rect = RectFromPoints(firstPoint, screenPoint);
            if (rect.Width < 50 || rect.Height < 50)
            {
                hint = "区域太小了，请再选对角";
                Invalidate();
                return;
            }

            SelectedRect = rect;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Point cursor = new Point(currentPoint.X - Bounds.Left, currentPoint.Y - Bounds.Top);

            Point first = new Point(firstPoint.X - Bounds.Left, firstPoint.Y - Bounds.Top);
            if (hasFirst)
            {
                using (Pen firstPen = new Pen(Color.FromArgb(255, 120, 200, 255), 2f))
                {
                    g.DrawEllipse(firstPen, first.X - 7, first.Y - 7, 14, 14);
                    g.DrawLine(firstPen, first.X - 12, first.Y, first.X + 12, first.Y);
                    g.DrawLine(firstPen, first.X, first.Y - 12, first.X, first.Y + 12);
                }

                Rectangle rubber = RectFromPoints(first, cursor);
                using (Pen rubberPen = new Pen(Color.FromArgb(255, 120, 200, 255), 2f))
                {
                    rubberPen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(rubberPen, rubber);
                }
            }

            using (Pen crossPen = new Pen(Color.FromArgb(230, 255, 255, 255), 1f))
            {
                g.DrawLine(crossPen, cursor.X, 0, cursor.X, Height);
                g.DrawLine(crossPen, 0, cursor.Y, Width, cursor.Y);
            }

            string title = "框选映射区域 · " + targetName;
            string line1;
            if (penMode)
            {
                line1 = hasFirst
                    ? "再移动笔到对角，落笔确认；Esc 取消"
                    : "移动笔把光标移到第一个角（左上角），落笔确认";
            }
            else
            {
                line1 = hasFirst
                    ? "再点击对角（右下角）完成；右键或 Esc 取消"
                    : "点击区域的第一个角（左上角）；右键或 Esc 取消";
            }
            string line2 = penMode
                ? "笔点选期间会把平板临时铺满这块目标屏幕"
                : string.Format("当前：{0}, {1}    区域：{2} × {3}",
                    cursor.X, cursor.Y,
                    hasFirst ? Math.Abs(cursor.X - first.X) : 0,
                    hasFirst ? Math.Abs(cursor.Y - first.Y) : 0);

            TextRenderer.DrawText(g, title, titleFont,
                new Rectangle(24, 20, Width - 48, 28), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, line1, hintFont,
                new Rectangle(24, 50, Width - 48, 22), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, line2, Font,
                new Rectangle(24, 76, Width - 48, 20), Color.FromArgb(220, 255, 255, 255),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (hint.Length > 0)
            {
                TextRenderer.DrawText(g, hint, Font,
                    new Rectangle(24, 136, Width - 48, 22), Color.FromArgb(255, 230, 140),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            TextRenderer.DrawText(g,
                string.Format("屏幕坐标：{0}, {1}    相对目标：{2}, {3}",
                    cursor.X + Bounds.Left, cursor.Y + Bounds.Top,
                    cursor.X, cursor.Y),
                Font,
                new Rectangle(cursor.X + 18, cursor.Y + 18, 340, 20),
                Color.FromArgb(240, 255, 255, 255),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private static Rectangle RectFromPoints(Point a, Point b)
        {
            return new Rectangle(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X),
                Math.Abs(a.Y - b.Y));
        }
    }
}
