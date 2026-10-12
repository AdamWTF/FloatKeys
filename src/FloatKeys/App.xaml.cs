using System.Windows;
using System.Windows.Threading;
using FloatKeys.Input;
using FloatKeys.Platform;
using FloatKeys.Services;

namespace FloatKeys;

public partial class App : Application
{
    private AppLog? log;
    private SingleInstance? instance;
    private SendInputBackend? input;
    private OverlayService? overlay;
    private TrayService? tray;
    private ShutdownCoordinator? lifetime;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            instance = new SingleInstance();
            if (!instance.Owned) { instance.Dispose(); instance = null; Shutdown(); return; }
            log = new AppLog();
            lifetime = new ShutdownCoordinator(log.Write);
            lifetime.Add("overlay shutdown", () => overlay?.Dispose());
            lifetime.Add("input shutdown", () => input?.Dispose());
            lifetime.Add("tray shutdown", () => tray?.Dispose());
            lifetime.Add("application subscriptions", () =>
            {
                DispatcherUnhandledException -= Unhandled;
                SessionEnding -= Ending;
            });
            lifetime.Add("instance release", () => { instance?.Dispose(); instance = null; });
            DispatcherUnhandledException += Unhandled;
            SessionEnding += Ending;
            input = new SendInputBackend(new NativeInputTransport(), log.Write);
            overlay = new OverlayService(input, log);
            overlay.Show();
            var startup = new StartupService(new RegistryStartupStore(), Environment.ProcessPath, log.Write);
            var controller = new TrayController(overlay, startup, () => lifetime.Stopping, RequestExit,
                message => tray?.Report(message));
            tray = new TrayService(controller, log.Write);
            controller.Refresh();
        }
        catch (Exception exception)
        {
            log ??= new AppLog();
            log.Write($"startup failed: {exception.GetType().Name}");
            Stop(1);
        }
    }

    private void RequestExit() => Stop(0);
    private void Stop(int code)
    {
        lifetime?.Stop();
        if (lifetime is null && instance is not null)
            ShutdownCoordinator.Try("instance release", () => { instance.Dispose(); instance = null; }, log!.Write);
        Shutdown(code);
    }
    private void Unhandled(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        log?.Write($"unhandled dispatcher failure: {args.Exception.GetType().Name}");
        args.Handled = true;
        Stop(1);
    }
    private void Ending(object sender, SessionEndingCancelEventArgs e)
    {
        // WM_QUERYENDSESSION can be cancelled by another application. Keep HWNDs and
        // ownership alive until WPF receives WM_ENDSESSION and calls OnExit.
        if (overlay is not null) ShutdownCoordinator.Try("session preparation", overlay.PrepareForSessionEnding, log!.Write);
        if (input is not null) ShutdownCoordinator.Try("session input release", () => input.ReleaseAllKeys(), log!.Write);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        lifetime?.Stop();
        log?.Write("shutdown");
        base.OnExit(e);
    }
}
