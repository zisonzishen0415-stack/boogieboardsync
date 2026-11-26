using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal enum CalibrationMode
    {
        TabletRange,
        Pressure
    }

    internal sealed class CalibrationForm : Form
    {
        private static readonly Color BorderColor = Color.FromArgb(215, 221, 230);
        private static readonly Color TextColor = Color.FromArgb(55, 62, 72);
        private static readonly Color MutedColor = Color.FromArgb(120, 128, 140);
        private static readonly Color AccentColor = Color.FromArgb(45, 110, 220);
        private static readonly Color DotColor = Color.FromArgb(32, 145, 94);

        private readonly DriverRuntime runtime;
        private readonly CalibrationMode mode;
        private readonly Timer timer;
        private readonly PaintSurface surface;
        private readonly Label instructionLabel;
        private readonly Label valueLabel;
        private readonly Button primaryButton;
        private readonly Button secondaryButton;
        private readonly Button applyButton;
        private readonly Button cancelButton;

        private int minX = int.MaxValue;
        private int maxX = int.MinValue;
        private int minY = int.MaxValue;
        private int maxY = int.MinValue;

        private readonly List<int> lightSamples = new List<int>();
        private readonly List<int> heavySamples = new List<int>();
        private int lightPressure;
        private int heavyPressure;
        private bool lightDone;
        private bool heavyDone;
        private int samplingPhase;
        private long samplingStartTicks;

        public int SelectedMinX { get; private set; }
        public int SelectedMaxX { get; private set; }
        public int SelectedMinY { get; private set; }
        public int SelectedMaxY { get; private set; }
        public int SelectedPressureMin { get; private set; }
        public int SelectedPressureMax { get; private set; }

        public CalibrationForm(DriverRuntime runtime, CalibrationMode mode)
        {
            this.runtime = runtime;
            this.mode = mode;

            Text = mode == CalibrationMode.TabletRange ? "捕获平板范围" : "力度校准";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(612, 408);
            MinimumSize = new Size(560, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            instructionLabel = new Label();
            instructionLabel.AutoSize = false;
            instructionLabel.Bounds = new Rectangle(16, 12, 580, 46);
            instructionLabel.Text = mode == CalibrationMode.TabletRange
                ? "用笔沿你想使用的平板区域最外圈慢慢画一整圈。程序只统计笔尖接触的坐标，画完点“完成”。"
                : "先用最轻的力度在板上画线并采样，再用最大力度画线并采样。程序只统计笔尖接触的力度。";
            Controls.Add(instructionLabel);

            surface = new PaintSurface(delegate(Graphics g) { DrawSurface(g); });
            surface.Bounds = new Rectangle(16, 62, 580, 220);
            Controls.Add(surface);

            valueLabel = new Label();
            valueLabel.AutoSize = false;
            valueLabel.Bounds = new Rectangle(16, 290, 580, 24);
            valueLabel.ForeColor = TextColor;
            Controls.Add(valueLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 56;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(12, 10, 12, 10);
            buttons.WrapContents = false;

            cancelButton = MakeButton("取消", 90);
            cancelButton.Click += delegate(object sender, EventArgs e)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            applyButton = MakeButton("应用", 90);
            applyButton.Click += delegate(object sender, EventArgs e) { Apply(); };
            secondaryButton = MakeButton(
                mode == CalibrationMode.TabletRange ? "重新采样" : "采样重力度（3 秒）", 150);
            secondaryButton.Click += delegate(object sender, EventArgs e) { OnSecondary(); };
            primaryButton = MakeButton(
                mode == CalibrationMode.TabletRange ? "完成" : "采样轻力度（3 秒）", 150);
            primaryButton.Click += delegate(object sender, EventArgs e) { OnPrimary(); };

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(applyButton);
            buttons.Controls.Add(secondaryButton);
            buttons.Controls.Add(primaryButton);
            Controls.Add(buttons);

            if (mode == CalibrationMode.TabletRange)
            {
                applyButton.Visible = false;
            }
            else
            {
                primaryButton.Text = "采样轻力度（3 秒）";
                secondaryButton.Text = "采样重力度（3 秒）";
            }
            UpdateButtons();

            timer = new Timer();
            timer.Interval = 60;
            timer.Tick += delegate(object sender, EventArgs e) { OnTick(); };
            timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null)
            {
                timer.Stop();
                timer.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Button MakeButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.FlatStyle = FlatStyle.System;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }

        private void OnPrimary()
        {
            if (mode == CalibrationMode.TabletRange)
            {
                FinishTablet();
                return;
            }
            if (samplingPhase != 0)
            {
                return;
            }
            lightSamples.Clear();
            lightDone = false;
            lightPressure = 0;
            samplingPhase = 1;
            samplingStartTicks = DateTime.UtcNow.Ticks;
            instructionLabel.Text = "第 1 步采样中：请用最轻的力度连续画线（3 秒）…";
            UpdateButtons();
        }

        private void OnSecondary()
        {
            if (mode == CalibrationMode.TabletRange)
            {
                minX = int.MaxValue;
                maxX = int.MinValue;
                minY = int.MaxValue;
                maxY = int.MinValue;
                instructionLabel.Text = "采样已重置，请重新沿边缘画一圈。";
                UpdateButtons();
                return;
            }
            if (samplingPhase != 0 || !lightDone)
            {
                return;
            }
            heavySamples.Clear();
            heavyDone = false;
            heavyPressure = 0;
            samplingPhase = 2;
            samplingStartTicks = DateTime.UtcNow.Ticks;
            instructionLabel.Text = "第 2 步采样中：请用最大力度连续画线（3 秒）…";
            UpdateButtons();
        }

        private void FinishTablet()
        {
            int width = maxX - minX;
            int height = maxY - minY;
            if (minX == int.MaxValue || width < 1000 || height < 1000)
            {
                MessageBox.Show(this,
                    "还没有捕获到足够的范围。请用笔沿区域边缘画一圈，让 X/Y 范围都拉开后再点“完成”。",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SelectedMinX = minX;
            SelectedMaxX = maxX;
            SelectedMinY = minY;
            SelectedMaxY = maxY;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Apply()
        {
            if (!lightDone || !heavyDone)
            {
                MessageBox.Show(this, "请先完成轻力度和重力度两次采样。", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (heavyPressure <= lightPressure + 50)
            {
                MessageBox.Show(this, "重力度采样没有明显高于轻力度，请重新采样。", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SelectedPressureMin = lightPressure;
            SelectedPressureMax = heavyPressure;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnTick()
        {
            if (mode == CalibrationMode.TabletRange)
            {
                int age = runtime.LastInputAgeMs;
                int x = runtime.LastRawX;
                int y = runtime.LastRawY;
                if (age < 400 && runtime.LastTouch && x >= 0 && y >= 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            else if (samplingPhase != 0)
            {
                bool recent = runtime.LastInputAgeMs < 400 && runtime.LastTouch;
                if (recent)
                {
                    if (samplingPhase == 1)
                    {
                        lightSamples.Add(runtime.LastRawPressure);
                    }
                    else
                    {
                        heavySamples.Add(runtime.LastRawPressure);
                    }
                }
                if (DateTime.UtcNow.Ticks - samplingStartTicks >= TimeSpan.TicksPerSecond * 3)
                {
                    if (samplingPhase == 1)
                    {
                        lightPressure = Percentile(lightSamples, 0.10);
                        lightDone = lightSamples.Count > 0;
                        samplingPhase = 0;
                        instructionLabel.Text = lightDone
                            ? "轻力度采样完成。请点“采样重力度”，用最大力度画线。"
                            : "没有采到笔迹，请重新采样轻力度。";
                    }
                    else
                    {
                        heavyPressure = Percentile(heavySamples, 0.90);
                        heavyDone = heavySamples.Count > 0;
                        samplingPhase = 0;
                        instructionLabel.Text = heavyDone
                            ? "采样完成：轻 " + lightPressure + " / 重 " + heavyPressure + "，点“应用”生效。"
                            : "没有采到笔迹，请重新采样重力度。";
                    }
                    UpdateButtons();
                }
            }
            UpdateValueLabel();
            surface.Invalidate();
        }

        private void UpdateButtons()
        {
            if (mode == CalibrationMode.TabletRange)
            {
                primaryButton.Enabled = true;
                secondaryButton.Enabled = true;
                return;
            }
            bool sampling = samplingPhase != 0;
            primaryButton.Enabled = !sampling;
            secondaryButton.Enabled = !sampling && lightDone;
            applyButton.Enabled = !sampling && lightDone && heavyDone;
        }

        private void UpdateValueLabel()
        {
            int age = runtime.LastInputAgeMs;
            string state = age > 1200 ? "等待笔迹…" : (runtime.LastTouch ? "接触" : "悬停");
            if (mode == CalibrationMode.TabletRange)
            {
                string captured = minX == int.MaxValue
                    ? "尚未捕获"
                    : string.Format("X {0}–{1}   Y {2}–{3}", minX, maxX, minY, maxY);
                valueLabel.Text = string.Format("已捕获：{0}    当前 {1}（{2}, {3}）",
                    captured, state, runtime.LastRawX, runtime.LastRawY);
            }
            else
            {
                string light = lightDone ? lightPressure.ToString() : "—";
                string heavy = heavyDone ? heavyPressure.ToString() : "—";
                valueLabel.Text = string.Format("轻 {0}   重 {1}    当前原始力度 {2}（{3}）",
                    light, heavy, runtime.LastRawPressure, state);
            }
        }

        private static int Percentile(List<int> values, double fraction)
        {
            if (values.Count == 0)
            {
                return 0;
            }
            List<int> copy = new List<int>(values);
            copy.Sort();
            int index = (int)Math.Round((copy.Count - 1) * fraction);
            if (index < 0) index = 0;
            if (index >= copy.Count) index = copy.Count - 1;
            return copy[index];
        }

        private void DrawSurface(Graphics g)
        {
            Rectangle box = new Rectangle(0, 0, surface.Width - 1, surface.Height - 1);
            using (Pen borderPen = new Pen(BorderColor))
            {
                g.DrawRectangle(borderPen, box);
            }

            if (mode == CalibrationMode.TabletRange)
            {
                DrawTablet(g);
            }
            else
            {
                DrawPressure(g);
            }
        }

        private void DrawTablet(Graphics g)
        {
            int rawWidth = Math.Max(1, runtime.RawMaxX - runtime.RawMinX);
            int rawHeight = Math.Max(1, runtime.RawMaxY - runtime.RawMinY);
            Rectangle area = FitRect(new Rectangle(24, 20, surface.Width - 48, surface.Height - 52),
                rawWidth / (double)rawHeight);

            using (SolidBrush panel = new SolidBrush(Color.FromArgb(250, 251, 253)))
            {
                g.FillRectangle(panel, area);
            }
            using (Pen outline = new Pen(Color.FromArgb(150, 160, 172)))
            {
                g.DrawRectangle(outline, area);
            }

            if (minX != int.MaxValue)
            {
                Rectangle captured = new Rectangle(
                    area.Left + (int)Math.Round((minX - runtime.RawMinX) / (double)rawWidth * area.Width),
                    area.Top + (int)Math.Round((minY - runtime.RawMinY) / (double)rawHeight * area.Height),
                    Math.Max(1, (int)Math.Round((maxX - minX) / (double)rawWidth * area.Width)),
                    Math.Max(1, (int)Math.Round((maxY - minY) / (double)rawHeight * area.Height)));
                captured.Intersect(area);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(40, AccentColor)))
                {
                    g.FillRectangle(brush, captured);
                }
                using (Pen pen = new Pen(AccentColor, 2f))
                {
                    g.DrawRectangle(pen, captured);
                }
            }

            int age = runtime.LastInputAgeMs;
            if (age < 1200 && runtime.LastRawX >= 0)
            {
                double nx = Clamp01((runtime.LastRawX - runtime.RawMinX) / (double)rawWidth);
                double ny = Clamp01((runtime.LastRawY - runtime.RawMinY) / (double)rawHeight);
                int px = area.Left + (int)Math.Round(nx * Math.Max(0, area.Width - 1));
                int py = area.Top + (int)Math.Round(ny * Math.Max(0, area.Height - 1));
                Rectangle dot = new Rectangle(px - 5, py - 5, 10, 10);
                using (SolidBrush dotBrush = new SolidBrush(runtime.LastTouch ? DotColor : Color.White))
                {
                    g.FillEllipse(dotBrush, dot);
                }
                using (Pen dotPen = new Pen(runtime.LastTouch ? Color.FromArgb(20, 105, 70) : MutedColor, 2f))
                {
                    g.DrawEllipse(dotPen, dot);
                }
            }

            TextRenderer.DrawText(g, "平板原始区域", Font,
                new Rectangle(area.Left, area.Top - 20, area.Width, 16), TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void DrawPressure(Graphics g)
        {
            int maxRaw = Math.Max(1024, runtime.PressureMax);
            if (heavyDone && heavyPressure > maxRaw) maxRaw = heavyPressure;
            int left = 30;
            int top = 36;
            int width = surface.Width - 60;
            int height = 28;

            Rectangle bar = new Rectangle(left, top, width, height);
            using (SolidBrush back = new SolidBrush(Color.FromArgb(233, 237, 242)))
            {
                g.FillRectangle(back, bar);
            }

            int age = runtime.LastInputAgeMs;
            if (age < 1200)
            {
                int fill = (int)Math.Round(Math.Min(1.0, runtime.LastRawPressure / (double)maxRaw) * width);
                Color fillColor = runtime.LastTouch ? DotColor : Color.FromArgb(150, 163, 180);
                using (SolidBrush brush = new SolidBrush(fillColor))
                {
                    g.FillRectangle(brush, new Rectangle(bar.Left, bar.Top, Math.Max(0, fill), bar.Height));
                }
            }

            DrawMarker(g, bar, maxRaw, lightDone ? lightPressure : 0, lightDone, "轻");
            DrawMarker(g, bar, maxRaw, heavyDone ? heavyPressure : 0, heavyDone, "重");

            using (Pen borderPen = new Pen(Color.FromArgb(170, 178, 190)))
            {
                g.DrawRectangle(borderPen, bar);
            }

            using (SolidBrush guide = new SolidBrush(Color.FromArgb(236, 240, 246)))
            {
                g.FillRectangle(guide, new Rectangle(left, top + height + 34, width, 8));
            }
            int lightWidth = lightDone ? (int)Math.Round(lightPressure / (double)maxRaw * width) : 0;
            int heavyWidth = heavyDone ? (int)Math.Round(heavyPressure / (double)maxRaw * width) : 0;
            using (SolidBrush lightBrush = new SolidBrush(Color.FromArgb(120, 170, 230)))
            {
                g.FillRectangle(lightBrush, new Rectangle(left, top + height + 34, Math.Max(0, lightWidth), 8));
            }
            using (SolidBrush heavyBrush = new SolidBrush(Color.FromArgb(45, 110, 220)))
            {
                g.FillRectangle(heavyBrush, new Rectangle(left + Math.Max(0, lightWidth), top + height + 34,
                    Math.Max(0, heavyWidth - lightWidth), 8));
            }
            TextRenderer.DrawText(g, "0", Font, new Rectangle(left - 4, bar.Bottom + 2, 40, 16), MutedColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, maxRaw.ToString(), Font,
                new Rectangle(left + width - 60, bar.Bottom + 2, 60, 16), MutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "当前原始力度", Font,
                new Rectangle(left, top - 24, width, 18), TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "采样区间：浅色 = 轻力度到重力度", Font,
                new Rectangle(left, top + height + 46, width, 18), MutedColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private static void DrawMarker(Graphics g, Rectangle bar, int maxRaw, int value, bool visible, string label)
        {
            if (!visible || maxRaw <= 0)
            {
                return;
            }
            int x = bar.Left + (int)Math.Round(Math.Min(1.0, value / (double)maxRaw) * bar.Width);
            using (Pen pen = new Pen(AccentColor, 2f))
            {
                g.DrawLine(pen, x, bar.Top - 6, x, bar.Bottom + 6);
            }
            TextRenderer.DrawText(g, label, SystemFonts.DefaultFont,
                new Rectangle(x - 8, bar.Top - 24, 26, 16), AccentColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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

        private sealed class PaintSurface : Control
        {
            private readonly Action<Graphics> paintAction;

            public PaintSurface(Action<Graphics> paintAction)
            {
                this.paintAction = paintAction;
                SetStyle(ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (paintAction != null)
                {
                    paintAction(e.Graphics);
                }
            }
        }
    }
}
