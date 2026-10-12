using FloatKeys.Platform;
using FloatKeys.Services;
using FloatKeys.Models;
using Xunit;

namespace FloatKeys.Tests;

public sealed class WindowsIntegrationTests
{
    [Fact]
    public void ReleaseIdentityAndIconAreEmbedded()
    {
        var assembly = typeof(FloatKeys.App).Assembly;
        Assert.Equal(new Version(0, 1, 0, 0), assembly.GetName().Version);
        Assert.Equal("FloatKeys", assembly.GetName().Name);
        Assert.Equal("FloatKeys", assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyProductAttribute), false)
            .Cast<System.Reflection.AssemblyProductAttribute>().Single().Product);
        using var icon = assembly.GetManifestResourceStream("FloatKeys.Resources.FloatKeys.ico");
        Assert.NotNull(icon);
        using var reader = new BinaryReader(icon!);
        Assert.Equal(0, reader.ReadUInt16()); Assert.Equal(1, reader.ReadUInt16());
        Assert.Equal(7, reader.ReadUInt16());
    }
    private sealed class StartupStore : IStartupStore
    {
        internal string? Command;
        internal int Writes, Removes;
        internal bool Fail;
        public string? Read() => Fail ? throw new UnauthorizedAccessException() : Command;
        public void Write(string command) { Command = command; Writes++; }
        public void Remove() { Command = null; Removes++; }
    }
    private sealed class Overlay : IOverlayCommands
    {
        public bool KeyboardVisible { get; private set; }
        public bool LauncherVisible { get; private set; } = true;
        internal bool Settings, Startup;
        public void ShowKeyboard() => KeyboardVisible = true;
        public void HideKeyboard() => KeyboardVisible = false;
        public void ShowLauncher() => LauncherVisible = true;
        public void HideLauncher() => LauncherVisible = false;
        public void ShowSettings() { ShowKeyboard(); Settings = true; }
        public void SetStartupPreference(bool enabled) => Startup = enabled;
    }
    private static StartupService Service(StartupStore store, Action<string>? log = null) =>
        new(store, @"C:\Portable Apps\FloatKeys\FloatKeys.exe", log ?? (_ => { }));

    [Theory]
    [InlineData(@"C:\FloatKeys.exe")]
    [InlineData(@"C:\Portable Apps\FloatKeys.exe")]
    [InlineData(@"C:\£ Portable\FloatKeys.exe")]
    public void StartupQuotesFullExecutablePath(string path) => Assert.Equal($"\"{path}\"", StartupService.QuoteExecutable(path));

    [Theory]
    [InlineData("FloatKeys.exe")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData("C:\\bad\"path\\FloatKeys.exe")]
    public void StartupRejectsUnsafeOrDevelopmentPaths(string path) => Assert.Throws<ArgumentException>(() => StartupService.QuoteExecutable(path));

    [Fact]
    public void EnableReconcileAndDisableUseOneEntry()
    {
        var store = new StartupStore(); var service = Service(store);
        Assert.False(service.Reconcile().Enabled); Assert.Equal(0, store.Writes);
        Assert.True(service.SetEnabled(true).Enabled);
        Assert.Equal("\"C:\\Portable Apps\\FloatKeys\\FloatKeys.exe\"", store.Command);
        for (int i = 0; i < 50; i++) Assert.True(service.Reconcile().Enabled);
        Assert.Equal(1, store.Writes);
        Assert.False(service.SetEnabled(false).Enabled); Assert.Null(store.Command);
        Assert.False(service.SetEnabled(false).Enabled);
    }

    [Theory]
    [InlineData("\"C:\\Old Folder\\FloatKeys.exe\"")]
    [InlineData(@"C:\Old Folder\FloatKeys.exe")]
    public void ExistingEntryIsRepairedAfterRelocation(string old)
    {
        var store = new StartupStore { Command = old };
        Assert.True(Service(store).Reconcile().Enabled);
        Assert.Equal(1, store.Writes);
        Assert.Equal("\"C:\\Portable Apps\\FloatKeys\\FloatKeys.exe\"", store.Command);
    }

    [Theory]
    [InlineData("\"C:\\Other.exe\"")]
    [InlineData("\"C:\\FloatKeys.exe\" --unexpected")]
    public void UnexpectedEntriesAreNeverOverwrittenOrRemoved(string existing)
    {
        var store = new StartupStore { Command = existing }; var service = Service(store);
        Assert.NotNull(service.Reconcile().Error);
        Assert.NotNull(service.SetEnabled(true).Error);
        Assert.NotNull(service.SetEnabled(false).Error);
        Assert.Equal(existing, store.Command); Assert.Equal(0, store.Writes + store.Removes);
    }

    [Fact]
    public void RegistrationFailureIsReportedWithoutThrowing()
    {
        var logs = new List<string>(); var store = new StartupStore { Fail = true };
        Assert.NotNull(Service(store, logs.Add).Reconcile().Error);
        Assert.NotNull(Service(store, logs.Add).SetEnabled(true).Error);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public void TrayCommandsRestoreBothWindowsAndRespectExternalStartupRemoval()
    {
        var overlay = new Overlay(); var store = new StartupStore(); int exits = 0;
        var controller = new TrayController(overlay, Service(store), () => false, () => exits++, _ => { });
        Assert.Equal("Show Keyboard", controller.KeyboardLabel);
        controller.Execute(TrayCommand.Keyboard); controller.Execute(TrayCommand.Launcher);
        Assert.True(overlay.KeyboardVisible); Assert.False(overlay.LauncherVisible);
        controller.Execute(TrayCommand.Keyboard);
        Assert.False(overlay.KeyboardVisible || overlay.LauncherVisible); Assert.Equal(0, exits);
        controller.Execute(TrayCommand.Launcher); controller.Execute(TrayCommand.Settings);
        Assert.True(overlay.LauncherVisible && overlay.KeyboardVisible && overlay.Settings);
        controller.Execute(TrayCommand.Startup); Assert.True(controller.StartupEnabled && overlay.Startup);
        store.Command = null; controller.Refresh();
        Assert.False(controller.StartupEnabled || overlay.Startup); Assert.Equal(1, store.Writes);
        controller.Execute(TrayCommand.Exit); Assert.Equal(1, exits);
    }

    [Fact]
    public void FailedStartupChangeKeepsLastConfirmedCheckmark()
    {
        var overlay = new Overlay(); var store = new StartupStore(); var reports = new List<string>();
        var controller = new TrayController(overlay, Service(store), () => false, () => { }, reports.Add);
        controller.Execute(TrayCommand.Startup); store.Fail = true;
        controller.Execute(TrayCommand.Startup);
        Assert.True(controller.StartupEnabled && overlay.Startup); Assert.Single(reports);
    }

    [Fact]
    public void ShutdownRunsAllStagesOnceAndRejectsTrayCommands()
    {
        var calls = new List<string>(); var lifetime = new ShutdownCoordinator(calls.Add);
        lifetime.Add("cancel", () => calls.Add("cancel"));
        lifetime.Add("save", () => throw new IOException());
        lifetime.Add("release", () => calls.Add("release"));
        lifetime.Add("tray", () => calls.Add("tray"));
        lifetime.Stop(); lifetime.Stop();
        Assert.Equal(new[] { "cancel", "save failed: IOException", "release", "tray" }, calls);
        var overlay = new Overlay(); var store = new StartupStore();
        var controller = new TrayController(overlay, Service(store), () => lifetime.Stopping, () => throw new Exception(), _ => { });
        foreach (var command in Enum.GetValues<TrayCommand>()) controller.Execute(command);
        controller.Refresh(); Assert.False(overlay.KeyboardVisible); Assert.True(overlay.LauncherVisible);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ShutdownSavesStartupAndGeometryTogether()
    {
        string path = Path.Combine(Path.GetTempPath(), "FloatKeys-exit-" + Guid.NewGuid(), "settings.json");
        try
        {
            var storage = new SettingsService(path);
            var settings = new AppSettings { StartWithWindows = true, Launcher = new(100, 200, 64, 64), Symbols = true };
            var lifetime = new ShutdownCoordinator(_ => { });
            lifetime.Add("settings", () => Assert.True(storage.Save(settings)));
            lifetime.Stop(); Assert.Equal(settings, new SettingsService(path).Load());
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void LegacySettingsDefaultStartupOff()
    {
        string path = Path.Combine(Path.GetTempPath(), "FloatKeys-legacy-" + Guid.NewGuid() + ".json");
        try { File.WriteAllText(path, "{\"Symbols\":true}"); var settings = new SettingsService(path).Load(); Assert.False(settings.StartWithWindows); Assert.True(settings.Symbols); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MutexRejectsAnotherThreadThenAllowsReacquisition()
    {
        string name = @"Local\FloatKeys.Test." + Guid.NewGuid();
        var first = new SingleInstance(name); Assert.True(first.Owned);
        bool? secondOwned = null;
        var thread = new Thread(() => { using var second = new SingleInstance(name); secondOwned = second.Owned; });
        thread.Start(); Assert.True(thread.Join(5000)); Assert.False(secondOwned);
        first.Dispose(); first.Dispose();
        using var next = new SingleInstance(name); Assert.True(next.Owned);
    }

    [Fact]
    public void AbandonedMutexCanBeAcquired()
    {
        string name = @"Local\FloatKeys.Test." + Guid.NewGuid();
        using var anchor = new Mutex(false, name);
        Mutex? abandoned = null;
        var thread = new Thread(() => { abandoned = Mutex.OpenExisting(name); abandoned.WaitOne(); });
        thread.Start(); Assert.True(thread.Join(5000));
        using var next = new SingleInstance(name); Assert.True(next.Owned);
        // Do not call ReleaseMutex on the dead owner; close only its abandoned handle.
        abandoned!.Dispose();
    }

    [Fact]
    public void DiagnosticsAreBoundedAndUnwritableDestinationIsNonfatal()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FloatKeys-log-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "test.log"); var log = new AppLog(path);
            for (int i = 0; i < 1200; i++) log.Write(new string('x', 2048));
            Assert.True(new FileInfo(path).Length < 1_048_576);
            Assert.True(new FileInfo(path + ".previous").Length < 1_048_576);
            Assert.Equal(2, Directory.GetFiles(directory).Length);
            var failed = new AppLog(directory); failed.Write("failure ignored");
        }
        finally { Directory.Delete(directory, true); }
    }
}
