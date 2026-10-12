namespace FloatKeys.Services;

internal interface IOverlayCommands
{
    bool KeyboardVisible { get; }
    bool LauncherVisible { get; }
    void ShowKeyboard();
    void HideKeyboard();
    void ShowLauncher();
    void HideLauncher();
    void ShowSettings();
    void SetStartupPreference(bool enabled);
}

internal enum TrayCommand { Keyboard, Launcher, Startup, Settings, Exit }

internal sealed class TrayController(IOverlayCommands overlay, StartupService startup,
    Func<bool> stopping, Action exit, Action<string> report)
{
    internal string KeyboardLabel => overlay.KeyboardVisible ? "Hide Keyboard" : "Show Keyboard";
    internal string LauncherLabel => overlay.LauncherVisible ? "Hide Launcher" : "Show Launcher";
    internal bool StartupEnabled { get; private set; }
    internal void Refresh()
    {
        if (!stopping()) Apply(startup.Reconcile());
    }
    private void Apply(StartupStatus status)
    {
        if (status.Error is not null) { report(status.Error); return; }
        StartupEnabled = status.Enabled;
        overlay.SetStartupPreference(status.Enabled);
    }
    internal void Execute(TrayCommand command)
    {
        if (stopping()) return;
        switch (command)
        {
            case TrayCommand.Keyboard:
                if (overlay.KeyboardVisible) overlay.HideKeyboard(); else overlay.ShowKeyboard(); break;
            case TrayCommand.Launcher:
                if (overlay.LauncherVisible) overlay.HideLauncher(); else overlay.ShowLauncher(); break;
            case TrayCommand.Startup: Apply(startup.SetEnabled(!StartupEnabled)); break;
            case TrayCommand.Settings: overlay.ShowSettings(); break;
            case TrayCommand.Exit: exit(); break;
        }
    }
}
