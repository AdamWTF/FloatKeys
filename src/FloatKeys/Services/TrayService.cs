using Forms = System.Windows.Forms;

namespace FloatKeys.Services;

internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly System.Drawing.Icon branding;
    private readonly Dictionary<TrayCommand, Forms.ToolStripMenuItem> items = [];
    private readonly TrayController controller;
    private readonly Action<string> log;
    private bool disposed;
    internal TrayService(TrayController controller, Action<string> log)
    {
        this.controller = controller;
        this.log = log;
        using var stream = typeof(TrayService).Assembly.GetManifestResourceStream("FloatKeys.Resources.FloatKeys.ico")
            ?? throw new InvalidOperationException("Application icon unavailable.");
        branding = new System.Drawing.Icon(stream);
        icon = new Forms.NotifyIcon { Icon = branding, Text = "FloatKeys", ContextMenuStrip = menu };
        try
        {
            foreach (var command in Enum.GetValues<TrayCommand>())
            {
                var item = new Forms.ToolStripMenuItem(command switch
                {
                    TrayCommand.Startup => "Start with Windows", TrayCommand.Exit => "Exit FloatKeys", _ => command.ToString()
                });
                item.Click += (_, _) => Guard(() => controller.Execute(command));
                items.Add(command, item);
                menu.Items.Add(item);
            }
            menu.Opening += Opening;
            icon.Visible = true;
        }
        catch { Dispose(); throw; }
    }
    private void Opening(object? sender, System.ComponentModel.CancelEventArgs e) => Guard(() =>
    {
        controller.Refresh();
        items[TrayCommand.Keyboard].Text = controller.KeyboardLabel;
        items[TrayCommand.Launcher].Text = controller.LauncherLabel;
        items[TrayCommand.Startup].Checked = controller.StartupEnabled;
    });
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { log($"tray command failed: {e.GetType().Name}"); Report("FloatKeys could not complete this command. See logs."); }
    }
    internal void Report(string message)
    {
        if (!disposed) ShutdownCoordinator.Try("tray notification", () => icon.ShowBalloonTip(5000, "FloatKeys", message, Forms.ToolTipIcon.Warning), log);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        menu.Opening -= Opening;
        ShutdownCoordinator.Try("tray removal", () => icon.Visible = false, log);
        ShutdownCoordinator.Try("tray icon disposal", icon.Dispose, log);
        ShutdownCoordinator.Try("tray menu disposal", menu.Dispose, log);
        ShutdownCoordinator.Try("tray branding disposal", branding.Dispose, log);
    }
}
