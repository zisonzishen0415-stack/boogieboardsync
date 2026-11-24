using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal sealed class DiagnosticsForm : Form
    {
        private readonly TrayContext owner;
        private TextBox text;
        private Label hint;
        private volatile bool refreshing;

        public DiagnosticsForm(TrayContext owner)
        {
            this.owner = owner;
            Text = AppInfo.DisplayName + " 诊断";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(780, 640);
            MinimumSize = new Size(600, 440);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = true;
            Icon = SystemIcons.Application;

            BuildContent();
            text.Text = "正在收集诊断信息…";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            AppLog.Write("diagnostics form shown");
            RefreshText();
        }

        private void BuildContent()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            hint = new Label();
            hint.AutoSize = true;
            hint.Text = "这里包含连接方式、设备枚举和最近日志。复制后可附在 issue 中；不包含笔迹内容。";
            hint.ForeColor = Color.DimGray;
            hint.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(hint, 0, 0);

            text = new TextBox();
            text.Multiline = true;
            text.ReadOnly = true;
            text.ScrollBars = ScrollBars.Both;
            text.WordWrap = false;
            text.Dock = DockStyle.Fill;
            text.BackColor = Color.White;
            text.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            text.Margin = new Padding(0);
            root.Controls.Add(text, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0, 10, 0, 0);

            Button closeButton = MakeButton("关闭");
            closeButton.Click += delegate(object sender, EventArgs e) { Close(); };
            Button refreshButton = MakeButton("刷新");
            refreshButton.Click += delegate(object sender, EventArgs e) { RefreshText(); };
            Button folderButton = MakeButton("打开设置文件夹");
            folderButton.Click += delegate(object sender, EventArgs e) { owner.OpenSettingsFolder(); };
            Button logButton = MakeButton("打开日志");
            logButton.Click += delegate(object sender, EventArgs e) { owner.OpenLogFile(); };
            Button copyButton = MakeButton("复制全部");
            copyButton.Click += delegate(object sender, EventArgs e) { CopyAll(); };

            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(refreshButton);
            buttons.Controls.Add(folderButton);
            buttons.Controls.Add(logButton);
            buttons.Controls.Add(copyButton);
            root.Controls.Add(buttons, 0, 2);
        }

        private static Button MakeButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(100, 30);
            button.FlatStyle = FlatStyle.System;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }

        private void RefreshText()
        {
            if (refreshing)
            {
                return;
            }
            refreshing = true;
            AppLog.Write("diagnostics refresh started");
            hint.ForeColor = Color.DimGray;
            hint.Text = "正在收集诊断信息，请稍候…";
            if (string.IsNullOrEmpty(text.Text) || text.Text.StartsWith("正在收集", StringComparison.Ordinal))
            {
                text.Text = "正在收集诊断信息…";
            }

            Thread worker = new Thread(delegate()
            {
                string value;
                try
                {
                    AppLog.Write("diagnostics build started");
                    value = owner.BuildDiagnostics();
                    AppLog.Write("diagnostics build finished");
                }
                catch (Exception ex)
                {
                    value = "生成诊断信息失败: " + ex;
                }

                try
                {
                    BeginInvoke(new Action(delegate()
                    {
                        if (IsDisposed)
                        {
                            return;
                        }
                        refreshing = false;
                        AppLog.Write("diagnostics refresh applied");
                        text.Text = value;
                        text.SelectionStart = 0;
                        text.SelectionLength = 0;
                        hint.ForeColor = Color.DimGray;
                        hint.Text = "这里包含连接方式、设备枚举和最近日志。复制后可附在 issue 中；不包含笔迹内容。";
                    }));
                }
                catch (Exception)
                {
                    refreshing = false;
                }
            });
            worker.IsBackground = true;
            worker.Name = "BoogieBoardSync diagnostics";
            worker.Start();
        }

        private void CopyAll()
        {
            try
            {
                Clipboard.SetText(text.Text);
                hint.ForeColor = Color.SeaGreen;
                hint.Text = "已复制到剪贴板。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复制失败：" + ex.Message, AppInfo.DisplayName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}