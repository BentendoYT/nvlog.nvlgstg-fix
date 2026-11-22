
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nvlog.nvlgstg_fix
{
    public partial class Form1 : Form
    {
        private const string LogPath =
            @"C:\ProgramData\NVIDIA Corporation\nvtopps\nct\nvlog.nvlgstg";

        private const string HotfixUrl =
            "https://international.download.nvidia.com/Windows/621.35hf/621.35-desktop-notebook-win10-win11-64bit-international-dch.hf.exe";

        private bool debugMode;
        private bool isScanning;
        private int fileChangeCount;

        private DebugForm debugWindow;

        public Form1()
        {
            InitializeComponent();
            debugMode = debugwindow_chkbox.Checked;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowTheme.Apply(this);
        }

        private async void btnScan_Click(object sender, EventArgs e)
        {
            if (isScanning)
                return;

            isScanning = true;

            btnScan.Enabled = false;
            debugwindow_chkbox.Enabled = false;
            btnScan.Text = "Scanning...";

            if (debugMode)
            {
                debugWindow = new DebugForm();
                debugWindow.Show(this);
                DebugLog("Scan started.");
            }

            try
            {
                DebugLog("Checking NVIDIA graphics hardware...");

                string gpuName = null;
                string driverVersion = "Unknown";

                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name, DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject gpu in searcher.Get())
                    {
                        string name = gpu["Name"]?.ToString();

                        if (!string.IsNullOrEmpty(name) &&
                            name.IndexOf(
                                "NVIDIA",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            gpuName = name;
                            driverVersion =
                                gpu["DriverVersion"]?.ToString() ?? "Unknown";

                            break;
                        }
                    }
                }

                DebugLog("GPU: " + (gpuName ?? "Not found"));
                DebugLog("Driver version: " + driverVersion);

                if (string.IsNullOrEmpty(gpuName))
                {
                    DebugLog("No NVIDIA GPU found.");

                    MessageBox.Show(
                        "No NVIDIA graphics card was found.\n\n" +
                        "Make sure an NVIDIA graphics card and its driver are installed.",
                        "NVIDIA GPU Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                DebugLog("Checking NVIDIA log file...");

                if (!File.Exists(LogPath))
                {
                    DebugLog("NVIDIA log file not found.");

                    MessageBox.Show(
                        "The NVIDIA log file could not be found.\n\n" +
                        "The scan cannot continue without this file.",
                        "Log File Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                FileInfo initialInfo = new FileInfo(LogPath);

                DateTime initialWriteTime = initialInfo.LastWriteTimeUtc;
                long initialSize = initialInfo.Length;

                DebugLog("NVIDIA log file found.");
                DebugLog("File size: " + initialSize + " bytes");
                DebugLog("Monitoring file activity for 30 seconds...");

                fileChangeCount = 0;

                using (FileSystemWatcher watcher = new FileSystemWatcher())
                {
                    watcher.Path = Path.GetDirectoryName(LogPath);
                    watcher.Filter = Path.GetFileName(LogPath);
                    watcher.NotifyFilter =
                        NotifyFilters.LastWrite | NotifyFilters.Size;

                    watcher.Changed += (s, args) =>
                    {
                        Interlocked.Increment(ref fileChangeCount);
                        DebugLog("Log file was modified.");
                    };

                    watcher.Error += (s, args) =>
                    {
                        DebugLog(
                            "File monitoring error: " +
                            args.GetException().Message);
                    };

                    watcher.EnableRaisingEvents = true;

                    await Task.Delay(30000);
                }

                FileInfo finalInfo = new FileInfo(LogPath);

                bool timestampChanged =
                    finalInfo.LastWriteTimeUtc != initialWriteTime;

                bool sizeChanged =
                    finalInfo.Length != initialSize;

                int changes = Interlocked.CompareExchange(
                    ref fileChangeCount, 0, 0);

                bool activityDetected =
                    changes > 0 || timestampChanged || sizeChanged;

                DebugLog("File monitoring completed.");
                DebugLog("Detected changes: " + changes);
                DebugLog("Timestamp changed: " + timestampChanged);
                DebugLog("File size changed: " + sizeChanged);

                if (activityDetected)
                {
                    DebugLog("Log file activity detected.");

                    DialogResult result = MessageBox.Show(
                        "Changes were detected in the NVIDIA log file.\n\n" +
                        "This does not automatically mean that your system is affected by the SSD write issue.\n\n" +
                        "Would you like to download the NVIDIA hotfix?",
                        "File Activity Detected",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        DebugLog("Hotfix download requested.");
                        await DownloadHotfix();
                    }
                    else
                    {
                        DebugLog("Hotfix download declined.");
                    }
                }
                else
                {
                    DebugLog("No changes detected.");

                    MessageBox.Show(
                        "No changes were detected in the NVIDIA log file.\n\n" +
                        "No issue indicators were found. Your system looks good!",
                        "Scan Complete - No Issues Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                DebugLog("Scan error: " + ex.Message);

                MessageBox.Show(
                    "Something went wrong while scanning your system.\n\n" +
                    ex.Message,
                    "Scan Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                DebugLog("Scan finished.");

                isScanning = false;

                btnScan.Enabled = true;
                debugwindow_chkbox.Enabled = true;
                btnScan.Text = "Scan";
            }
        }

        private async Task DownloadHotfix()
        {
            if (string.IsNullOrWhiteSpace(HotfixUrl))
            {
                DebugLog("Hotfix URL is missing.");

                MessageBox.Show(
                    "The NVIDIA hotfix download is not available right now.",
                    "Download Unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            if (!Uri.TryCreate(
                HotfixUrl,
                UriKind.Absolute,
                out Uri downloadUri) ||
                downloadUri.Scheme != Uri.UriSchemeHttps)
            {
                DebugLog("Invalid download URL.");

                MessageBox.Show(
                    "The NVIDIA download URL is invalid.",
                    "Download Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            string downloadPath = Path.Combine(
                Path.GetTempPath(),
                "NVIDIA-Hotfix.exe");

            try
            {
                DebugLog("Downloading NVIDIA hotfix...");

                using (WebClient client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(
                        downloadUri,
                        downloadPath);
                }

                if (!File.Exists(downloadPath) ||
                    new FileInfo(downloadPath).Length == 0)
                {
                    throw new IOException(
                        "The downloaded installer is missing or empty.");
                }

                DebugLog("Download completed.");

                DialogResult result = MessageBox.Show(
                    "The NVIDIA installer has been downloaded.\n\n" +
                    "Would you like to start the installer?",
                    "Download Complete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    DebugLog("Installer launch cancelled.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadPath,
                    UseShellExecute = true
                });

                DebugLog("NVIDIA installer started.");

                ShowInstructions();
            }
            catch (Exception ex)
            {
                DebugLog("Download error: " + ex.Message);

                MessageBox.Show(
                    "The NVIDIA hotfix could not be downloaded or started.\n\n" +
                    ex.Message,
                    "Download Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void DebugLog(string message)
        {
            if (debugMode &&
                debugWindow != null &&
                !debugWindow.IsDisposed)
            {
                debugWindow.Log(message);
            }
        }

        private void ShowInstructions()
        {
            using (Form instructions = new Form())
            {
                instructions.Text = "Installation Instructions";
                instructions.Size = new Size(480, 330);
                instructions.StartPosition = FormStartPosition.CenterParent;
                instructions.FormBorderStyle = FormBorderStyle.FixedDialog;
                instructions.MaximizeBox = false;
                instructions.MinimizeBox = false;
                instructions.BackColor = ColorTranslator.FromHtml("#212121");
                instructions.ForeColor = ColorTranslator.FromHtml("#7AB547");

                instructions.Shown += (s, e) =>
                {
                    WindowTheme.Apply(instructions);
                };

                Label title = new Label
                {
                    Text = "NVIDIA Hotfix Installation",
                    Font = new Font("Segoe UI", 15, FontStyle.Bold),
                    ForeColor = ColorTranslator.FromHtml("#7AB547"),
                    BackColor = Color.Transparent,
                    Location = new Point(20, 20),
                    AutoSize = true
                };

                Label description = new Label
                {
                    Text =
                        "Follow these steps during installation:\n\n" +
                        "1. Wait for the NVIDIA installer to open.\n" +
                        "2. Select Custom (Advanced), not Express.\n" +
                        "3. Enable 'Perform a clean installation'.\n" +
                        "4. Continue with the installation.\n",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Location = new Point(20, 75),
                    Size = new Size(420, 160)
                };

                Button okButton = new Button
                {
                    Text = "Got it",
                    Size = new Size(100, 32),
                    Location = new Point(340, 245),
                    DialogResult = DialogResult.OK
                };

                instructions.Controls.Add(title);
                instructions.Controls.Add(description);
                instructions.Controls.Add(okButton);

                instructions.AcceptButton = okButton;
                instructions.ShowDialog(this);
            }
        }

        private void debugwindow_chkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (isScanning)
            {
                debugwindow_chkbox.Checked = debugMode;
                return;
            }

            debugMode = debugwindow_chkbox.Checked;
        }
    }
}
