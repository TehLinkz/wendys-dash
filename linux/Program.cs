using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace WendysDash;

internal static class LinuxProgram
{
    [STAThread]
    public static int Main(string[] args)
    {
        return AppBuilder.Configure<LinuxApp>().UsePlatformDetect().LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}

public sealed class LinuxApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string[] args = desktop.Args ?? Array.Empty<string>();
            bool smoke = args.Length == 2 && args[0] == "--smoke-test";
            SmokeFixture fixture = smoke ? new SmokeFixture() : null;
            string folder = fixture != null ? fixture.Root :
                args.Length == 2 && args[0] == "--game-dir" ? args[1] : "";
            PatchService service = fixture != null ? new PatchService(() => false) : new PatchService();
            var window = new MainWindow(service, folder);
            desktop.MainWindow = window;
            if (fixture != null)
            {
                // This mode only ever edits the synthetic folder created above.
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => {
                    int code = 0;
                    try { window.VerifyAndCapture(fixture, args[1]); }
                    catch (Exception e) { Console.Error.WriteLine(e); code = 1; }
                    finally { fixture.Dispose(); desktop.Shutdown(code); }
                }, TimeSpan.FromSeconds(1));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
