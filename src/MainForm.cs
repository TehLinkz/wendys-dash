using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WendysDash
{
    public sealed class MainForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(39, 39, 43);
        private static readonly Color Muted = Color.FromArgb(108, 108, 115);
        private static readonly Color Red = Color.FromArgb(198, 43, 57);
        private readonly PatchService service;
        private readonly TextBox folder;
        private readonly Label statusTitle, statusBody, statusDot;
        private readonly Button apply, undo;
        private readonly StringBuilder activity = new StringBuilder();
        private readonly Timer poll;
        private bool busy, lastRunning;

        public MainForm(PatchService patchService, string gameFolder)
        {
            service = patchService;
            Text = "Wendy's Dash";
            Font = new Font("Segoe UI", 10F);
            ForeColor = Ink;
            BackColor = Color.FromArgb(247, 246, 244);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(820, 626);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }

            Label mark = LabelAt(this, "»", 32, 32, 58, 58, 32, FontStyle.Bold, Color.White);
            mark.BackColor = Red;
            mark.TextAlign = ContentAlignment.MiddleCenter;
            mark.AccessibleName = "Dash Patcher";
            LabelAt(this, "Wendy's Dash", 108, 28, 650, 37, 24, FontStyle.Bold, Ink);
            LabelAt(this, "FOR WENDY QUEST", 110, 69, 480, 23, 11, FontStyle.Bold, Red);
            LabelAt(this, "A little more speed for your next adventure.", 32, 104, 740, 22, 10, FontStyle.Regular, Muted);

            Panel location = Card(32, 146, 756, 122);
            LabelAt(location, "GAME LOCATION", 22, 16, 500, 23, 9, FontStyle.Bold, Muted);
            LinkLabel detect = LinkAt(location, "Auto-detect", 632, 14, 105, 25);
            detect.LinkClicked += delegate {
                string found = SteamLocator.FindGame();
                if (String.IsNullOrEmpty(found)) { Log("No Steam installation found. Use Browse to select your game folder."); SetStatus("Choose your game folder", "Steam auto-detection did not find Wendy Quest. Use Browse to select it.", Color.DarkGoldenrod); }
                else { folder.Text = found; RefreshStatus(); }
            };
            folder = new TextBox { Left = 22, Top = 49, Width = 589, Height = 30, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F), Text = gameFolder ?? "", AccessibleName = "Game folder", TabIndex = 0 };
            location.Controls.Add(folder);
            Button browse = ButtonAt(location, "Browse...", 625, 44, 109, 36, false);
            browse.AccessibleName = "Browse for game folder";
            browse.Click += delegate {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
                    dialog.Description = "Select your Wendy Quest game folder (the folder containing package.nw).";
                    dialog.ShowNewFolderButton = false;
                    if (Directory.Exists(folder.Text)) dialog.SelectedPath = folder.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; RefreshStatus(); }
                }
            };
            LabelAt(location, "Select the folder containing WendyQuest.exe and package.nw.", 22, 88, 700, 20, 9, FontStyle.Regular, Muted);
            folder.TextChanged += delegate { apply.Enabled = false; undo.Enabled = false; };
            folder.Leave += delegate { RefreshStatus(); };
            folder.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RefreshStatus(); } };

            Panel status = Card(32, 284, 756, 154);
            statusDot = LabelAt(status, "●", 21, 15, 24, 28, 14, FontStyle.Bold, Red);
            statusTitle = LabelAt(status, "Checking installation", 49, 18, 565, 29, 14, FontStyle.Bold, Ink);
            statusTitle.AccessibleName = "Patch status";
            LinkLabel refresh = LinkAt(status, "Refresh", 657, 18, 78, 25);
            refresh.LinkClicked += delegate { RefreshStatus(); };
            statusBody = LabelAt(status, "", 22, 56, 710, 41, 10, FontStyle.Regular, Muted);
            LabelAt(status, "X  /  SHIFT", 22, 113, 185, 23, 10, FontStyle.Bold, Ink);
            LabelAt(status, "Hold to run", 187, 113, 190, 23, 10, FontStyle.Regular, Muted);
            LabelAt(status, "2× movement speed", 505, 113, 225, 23, 10, FontStyle.Bold, Ink);
            status.Paint += delegate(object sender, PaintEventArgs e) { using (Pen p = new Pen(Color.FromArgb(235, 233, 230))) e.Graphics.DrawLine(p, 22, 102, status.Width - 22, 102); };

            apply = ButtonAt(this, "Apply patch", 32, 458, 466, 50, true);
            apply.AccessibleName = "Apply patch";
            apply.Click += delegate { RunChange(false); };
            undo = ButtonAt(this, "Undo patch", 514, 458, 274, 50, false);
            undo.AccessibleName = "Undo patch";
            undo.Click += delegate { RunChange(true); };
            LabelAt(this, "Your game file is backed up before each change. Saves stay untouched.", 32, 520, 756, 25, 9, FontStyle.Regular, Muted);
            LinkLabel details = LinkAt(this, "View activity", 32, 555, 130, 25);
            details.LinkClicked += delegate { ShowActivity(); };
            LabelAt(this, "Unofficial community patch. Not affiliated with the game's creators.", 32, 592, 675, 20, 8.5F, FontStyle.Regular, Muted);
            LabelAt(this, "v1.0.0", 732, 592, 56, 20, 8.5F, FontStyle.Regular, Muted);
            Log("Wendy's Dash 1.0.0 started.");
            Shown += delegate { RefreshStatus(); };
            Activated += delegate { if (!busy) RefreshStatus(); };
            poll = new Timer { Interval = 2000 };
            poll.Tick += delegate {
                if (busy) return;
                try { if (service.Running != lastRunning) RefreshStatus(); } catch (InvalidOperationException) { }
            };
            poll.Start();
            FormClosed += delegate { poll.Dispose(); };
        }

        private void RefreshStatus()
        {
            if (busy || apply == null) return;
            apply.Enabled = undo.Enabled = false;
            try
            {
                PatchInfo info = service.Inspect(folder.Text.Trim().Trim('"'));
                lastRunning = service.Running;
                bool installed = info.State == PatchState.Installed || info.State == PatchState.LegacyInstalled;
                if (lastRunning && (installed || info.State == PatchState.Ready))
                    SetStatus("Close Wendy Quest to continue", installed ? "The patch is installed. Close the game to make changes." : "The game is running. Close it, then apply the patch.", Color.FromArgb(163, 104, 24));
                else if (installed) {
                    SetStatus("Patch installed", info.Message, Color.FromArgb(32, 126, 86)); undo.Enabled = true;
                }
                else if (info.State == PatchState.Ready) {
                    SetStatus("Ready to patch", info.Message, Color.FromArgb(32, 126, 86)); apply.Enabled = true;
                }
                else SetStatus(info.State == PatchState.Missing ? "Choose your game folder" : "Installation needs attention", info.Message, Color.FromArgb(163, 104, 24));
            }
            catch (Exception e) { SetStatus("Couldn't check this folder", FriendlyError(e), Red); }
        }

        private void RunChange(bool remove)
        {
            busy = true; apply.Enabled = undo.Enabled = false; UseWaitCursor = true;
            try
            {
                PatchResult result = remove ? service.Undo(folder.Text.Trim().Trim('"')) : service.Apply(folder.Text.Trim().Trim('"'));
                Log(result.Message);
                if (!String.IsNullOrEmpty(result.BackupPath)) Log("Backup verified: " + result.BackupPath);
                busy = false; RefreshStatus();
                if (remove) statusBody.Text = result.Message;
            }
            catch (Exception e) { Log("Stopped: " + e.Message); SetStatus("No change completed", FriendlyError(e), Red); }
            finally { busy = false; UseWaitCursor = false; }
        }

        private static string FriendlyError(Exception e)
        {
            if (e is UnauthorizedAccessException) return "Windows denied access to the game folder. Check that your account can write to it.";
            return e.Message;
        }

        private void SetStatus(string title, string description, Color color)
        { statusTitle.Text = title; statusBody.Text = description; statusDot.ForeColor = color; }
        private void Log(string text) { activity.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  " + text); }

        private void ShowActivity()
        {
            using (Form dialog = new Form { Text = "Patch activity", ClientSize = new Size(700, 350), StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, Font = Font, BackColor = BackColor })
            {
                TextBox text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                    Dock = DockStyle.Fill, Font = new Font("Consolas", 9F), BackColor = Color.White, Text = activity.ToString() };
                dialog.Padding = new Padding(16);
                dialog.Controls.Add(text);
                dialog.ShowDialog(this);
            }
        }

        private Panel Card(int x, int y, int width, int height)
        {
            Panel panel = new Panel { Left = x, Top = y, Width = width, Height = height, BackColor = Color.White };
            panel.Paint += delegate(object sender, PaintEventArgs e) { using (Pen p = new Pen(Color.FromArgb(226, 223, 219))) e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1); };
            Controls.Add(panel); return panel;
        }

        private static Label LabelAt(Control parent, string text, int x, int y, int w, int h, float size, FontStyle style, Color color)
        {
            Label label = new Label { Text = text, Left = x, Top = y, Width = w, Height = h, Font = new Font("Segoe UI", size, style), ForeColor = color, AutoEllipsis = true };
            parent.Controls.Add(label); return label;
        }

        private static LinkLabel LinkAt(Control parent, string text, int x, int y, int w, int h)
        {
            LinkLabel link = new LinkLabel { Text = text, Left = x, Top = y, Width = w, Height = h, Font = new Font("Segoe UI", 9F),
                LinkColor = Red, ActiveLinkColor = Red, VisitedLinkColor = Red, LinkBehavior = LinkBehavior.HoverUnderline };
            parent.Controls.Add(link); return link;
        }

        private static Button ButtonAt(Control parent, string text, int x, int y, int w, int h, bool primary)
        {
            Button button = new Button { Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Red : Color.White, ForeColor = primary ? Color.White : Ink, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(213, 209, 204);
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(173, 33, 47) : Color.FromArgb(240, 238, 235);
            parent.Controls.Add(button); return button;
        }
    }
}
