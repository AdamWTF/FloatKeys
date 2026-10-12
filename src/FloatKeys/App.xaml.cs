using System.Windows;
using FloatKeys.Input;
using FloatKeys.Services;

namespace FloatKeys;

public partial class App : Application
{
    private PrototypeLog? log;
    private SendInputBackend? input;
    private OverlayService? overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        log = new PrototypeLog();
        input = new SendInputBackend(new NativeInputTransport(), log.Write);
        DispatcherUnhandledException += (_, args) =>
        {
            log.Write($"unhandled exception={args.Exception.GetType().Name}: {args.Exception.Message}");
            overlay?.Dispose();
            input.ReleaseAllKeys();
            args.Handled = true;
            Shutdown(1);
        };
        try
        {
            overlay = new OverlayService(input, log);
            overlay.Show();
        }
        catch (Exception exception)
        {
            log.Write($"startup failed={exception.GetType().Name}: {exception.Message}");
            Shutdown(1); // No activating message box on startup failure.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        input?.Dispose();
        overlay?.Dispose();
        log?.Write("shutdown");
        base.OnExit(e);
    }
}
