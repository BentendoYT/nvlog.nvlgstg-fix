
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace nvlog.nvlgstg_fix
{
    public partial class DebugForm : Form
    {
        private TextBox logBox;

        public DebugForm()
        {
            InitializeComponent();

            Text = "Debug Window";
            Size = new Size(650, 400);
            StartPosition = FormStartPosition.CenterScreen;

            BackColor = ColorTranslator.FromHtml("#212121");
            ForeColor = ColorTranslator.FromHtml("#7AB547");

            logBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#212121"),
                ForeColor = ColorTranslator.FromHtml("#7AB547"),
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None
            };

            Controls.Add(logBox);

            Log("Debug window initialized.");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowTheme.Apply(this);
        }

        public void Log(string message)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => Log(message)));
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                return;
            }

            if (!IsDisposed && logBox != null)
            {
                logBox.AppendText(
                    $"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            }
        }
    }

    internal static class WindowTheme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int attributeValue,
            int attributeSize);

        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;

            int darkMode = 1;

            int result = DwmSetWindowAttribute(
                form.Handle,
                20,
                ref darkMode,
                sizeof(int));

            if (result != 0)
            {
                DwmSetWindowAttribute(
                    form.Handle,
                    19,
                    ref darkMode,
                    sizeof(int));
            }
        }
    }
}
