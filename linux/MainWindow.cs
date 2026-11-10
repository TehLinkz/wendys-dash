using System;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace WendysDash;

public sealed class MainWindow : Window
{
    private static readonly IBrush Red = Brush.Parse("#C62B39");
    private static readonly IBrush Muted = Brush.Parse("#6C6C73");
    private readonly PatchService service;
    private readonly TextBox folder = new() { Watermark = "Select your Wendy Quest game folder", MinHeight = 44 };
    private readonly TextBlock statusTitle = new() { FontSize = 21, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock statusBody = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap, Foreground = Muted };
    private readonly Button apply = new() { Content = "Apply patch", Height = 48, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Red, Foreground = Brushes.White };
    private readonly Button undo = new() { Content = "Undo patch", Height = 48, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox activity = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120 };
    private readonly StringBuilder log = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool busy, lastRunning;

    public MainWindow(PatchService patchService, string gameFolder)
    {
        service = patchService;
        Title = "Wendy's Dash";
        Width = 820; Height = 650; MinWidth = 600; MinHeight = 520;
        Background = Brush.Parse("#F7F6F4");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new StackPanel { Spacing = 20, Margin = new Thickness(28) };
        root.Children.Add(new StackPanel { Spacing = 5, Children = {
            new TextBlock { Text = "Wendy's Dash", FontSize = 31, FontWeight = FontWeight.Bold },
            new TextBlock { Text = "FOR WENDY QUEST  /  LINUX + STEAM DECK", FontSize = 13, Foreground = Red, FontWeight = FontWeight.SemiBold },
            Text("A little more speed for your next adventure.")
        }});

        var folderGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        folderGrid.Children.Add(folder);
        var browse = new Button { Content = "Browse...", MinHeight = 44, MinWidth = 110 };
        Grid.SetColumn(browse, 1); folderGrid.Children.Add(browse);
        browse.Click += async (_, _) => {
            try {
                var options = new FolderPickerOpenOptions { Title = "Choose the Wendy Quest game folder", AllowMultiple = false };
                if (Directory.Exists(folder.Text)) options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(folder.Text);
                var selected = await StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count > 0 && selected[0].Path.IsFile) { folder.Text = selected[0].Path.LocalPath; RefreshState(); }
            } catch (Exception e) { ShowError(e); }
        };
        var find = new Button { Content = "Auto-detect Steam libraries", MinHeight = 36 };
        find.Click += (_, _) => Detect();
        root.Children.Add(Card(new StackPanel { Spacing = 10, Children = {
            new TextBlock { Text = "GAME LOCATION", FontWeight = FontWeight.SemiBold, FontSize = 12, Foreground = Muted },
            folderGrid, find,
            Text("Select the game folder containing package.nw, including SD-card libraries.", 13)
        }}));

        var statusRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        statusRow.Children.Add(statusTitle);
        var refresh = new Button { Content = "Refresh", MinHeight = 36 };
        Grid.SetColumn(refresh, 1); statusRow.Children.Add(refresh);
        refresh.Click += (_, _) => RefreshState();
        root.Children.Add(Card(new StackPanel { Spacing = 12, Children = {
            statusRow, statusBody,
            new Separator(),
            new TextBlock { Text = "Hold X or Shift     ·     2× movement speed", FontSize = 15, FontWeight = FontWeight.SemiBold }
        }}));

        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), ColumnSpacing = 16 };
        buttons.Children.Add(apply); Grid.SetColumn(undo, 1); buttons.Children.Add(undo);
        apply.Click += (_, _) => Change(false);
        undo.Click += (_, _) => Change(true);
        root.Children.Add(buttons);
        root.Children.Add(Text("Verified backups before every change. Save files stay untouched.", 13));
        root.Children.Add(new Expander { Header = "Activity and backup locations", Content = activity, HorizontalAlignment = HorizontalAlignment.Stretch });
        root.Children.Add(Text("v1.1.0 · Unofficial community patch · Use in Desktop Mode", 12));
        Content = new ScrollViewer { Content = root, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        folder.TextChanged += (_, _) => { apply.IsEnabled = false; undo.IsEnabled = false; };
        folder.LostFocus += (_, _) => RefreshState();
        folder.Text = gameFolder;
        Opened += (_, _) => { if (String.IsNullOrWhiteSpace(folder.Text)) Detect(); else RefreshState(); };
        Activated += (_, _) => { if (!busy) RefreshState(); };
        timer.Tick += (_, _) => { if (!busy && service.Running != lastRunning) RefreshState(); };
        timer.Start();
        Closed += (_, _) => timer.Stop();
        Log("Wendy's Dash 1.1.0 for Linux started.");
    }

    private void Detect()
    {
        try {
            var games = LinuxSteamLocator.FindGames();
            if (games.Count == 0) { Log("No installation found automatically. Browse to your game folder."); RefreshState(); return; }
            folder.Text = games[0];
            Log("Detected " + games.Count + " installation path(s). Selected: " + games[0]);
            RefreshState();
        } catch (Exception e) { ShowError(e); }
    }

    private void RefreshState()
    {
        if (busy) return;
        apply.IsEnabled = undo.IsEnabled = false;
        try {
            PatchInfo info = service.Inspect((folder.Text ?? "").Trim());
            lastRunning = service.Running;
            bool installed = info.State == PatchState.Installed || info.State == PatchState.LegacyInstalled;
            if (lastRunning && (installed || info.State == PatchState.Ready)) {
                statusTitle.Text = "Close Wendy Quest to continue";
                statusBody.Text = "The game is running through Steam or Proton. Close it before changing the patch.";
            } else if (installed) {
                statusTitle.Text = "Patch installed"; statusBody.Text = info.Message; undo.IsEnabled = true;
            } else if (info.State == PatchState.Ready) {
                statusTitle.Text = "Ready to patch"; statusBody.Text = info.Message; apply.IsEnabled = true;
            } else {
                statusTitle.Text = info.State == PatchState.Missing ? "Choose your game folder" : "Installation needs attention";
                statusBody.Text = info.Message;
            }
        } catch (Exception e) { ShowError(e); }
    }

    private void Change(bool remove)
    {
        busy = true; apply.IsEnabled = undo.IsEnabled = false;
        try {
            var result = remove ? service.Undo(folder.Text.Trim()) : service.Apply(folder.Text.Trim());
            Log(result.Message);
            if (!String.IsNullOrEmpty(result.BackupPath)) Log("Backup verified: " + result.BackupPath);
            busy = false; RefreshState();
        } catch (Exception e) { ShowError(e); }
        finally { busy = false; }
    }

    private void ShowError(Exception e)
    {
        apply.IsEnabled = undo.IsEnabled = false;
        statusTitle.Text = "Couldn't complete the operation";
        statusBody.Text = e is UnauthorizedAccessException ? "The game folder is not writable. Check its permissions and try again." : e.Message;
        Log(e.Message);
    }
    private void Log(string message) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  " + message); activity.Text = log.ToString(); }
    private static TextBlock Text(string text, double size = 15) => new() { Text = text, FontSize = size, Foreground = Muted, TextWrapping = TextWrapping.Wrap };
    private static Border Card(Control child) => new() { Background = Brushes.White, BorderBrush = Brush.Parse("#E2DFDB"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(20), Child = child };

    internal void VerifyAndCapture(SmokeFixture fixture, string output)
    {
        RefreshState();
        if (!apply.IsEnabled || undo.IsEnabled) throw new Exception("Ready-state buttons are incorrect.");
        Change(false);
        if (apply.IsEnabled || !undo.IsEnabled || statusTitle.Text != "Patch installed") throw new Exception("Apply action failed in the GUI.");
        UpdateLayout();
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96))) {
            bitmap.Render(this); bitmap.Save(output);
        }
        Change(true);
        if (!apply.IsEnabled || undo.IsEnabled || File.ReadAllText(fixture.Target) != SmokeFixture.Map) throw new Exception("GUI undo did not restore the fixture.");
        Console.WriteLine("PASS Linux GUI: ready, apply, installed, screenshot, exact undo.");
    }
}
