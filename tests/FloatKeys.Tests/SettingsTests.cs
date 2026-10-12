using System.Text.Json;
using FloatKeys.Models;
using FloatKeys.Services;
using Xunit;

namespace FloatKeys.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FloatKeys-tests-" + Guid.NewGuid());
    private string SettingsPath => Path.Combine(directory, "settings.json");
    public SettingsTests() => Directory.CreateDirectory(directory);
    [Fact]
    public void MissingSettingsCreateDefaultsOnFirstSave()
    {
        var storage = new SettingsService(SettingsPath);
        var settings = storage.Load();
        Assert.Equal(new AppSettings(), settings);
        Assert.True(storage.Save(settings)); Assert.True(File.Exists(SettingsPath));
    }
    [Theory]
    [InlineData("{broken")] [InlineData("null")] [InlineData("[]")]
    [InlineData("{\"Launcher\":null,\"Keyboard\":null}")]
    public void CorruptSettingsRecoverAndCanBeReplaced(string json)
    {
        File.WriteAllText(SettingsPath, json);
        var storage = new SettingsService(SettingsPath);
        Assert.Equal(new AppSettings(), storage.Load());
        Assert.True(storage.Save(new()));
        Assert.Equal(new AppSettings(), new SettingsService(SettingsPath).Load());
    }
    [Fact]
    public void WrongFieldTypesPreserveOtherValidFieldsAndAreRepaired()
    {
        File.WriteAllText(SettingsPath, "{\"Launcher\":{\"XPhysical\":\"bad\",\"YPhysical\":120,\"WidthDip\":64},\"Keyboard\":{\"XPhysical\":-800,\"WidthDip\":960,\"HeightDip\":null},\"Symbols\":true,\"LauncherOpacity\":\"bad\"}");
        var storage = new SettingsService(SettingsPath);
        var loaded = storage.Load();
        Assert.Null(loaded.Launcher.XPhysical); Assert.Equal(120, loaded.Launcher.YPhysical);
        Assert.Equal(64, loaded.Launcher.WidthDip); Assert.Equal(-800, loaded.Keyboard.XPhysical);
        Assert.Equal(960, loaded.Keyboard.WidthDip); Assert.Equal(480, loaded.Keyboard.HeightDip);
        Assert.True(loaded.Symbols); Assert.Equal(.85, loaded.LauncherOpacity);
        Assert.True(storage.Save(loaded));
        Assert.DoesNotContain("bad", File.ReadAllText(SettingsPath));
    }
    [Fact]
    public void PositionDimensionsAndPreferencesSurviveRestart()
    {
        var expected = new AppSettings { Launcher = new(-1800, 120, 64, 64), Keyboard = new(-1500, 600, 960, 480),
            LauncherOpacity = .75, Symbols = true };
        var storage = new SettingsService(SettingsPath);
        Assert.True(storage.Save(expected));
        Assert.Equal(expected, new SettingsService(SettingsPath).Load());
        Assert.Single(Directory.GetFiles(directory));
        Assert.DoesNotContain("Caps", File.ReadAllText(SettingsPath));
    }
    [Fact]
    public void UnchangedSavesDoNotWriteAndReplacementLeavesValidJson()
    {
        var storage = new SettingsService(SettingsPath);
        Assert.True(storage.Save(new()));
        DateTime marker = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(SettingsPath, marker);
        for (int i = 0; i < 100; i++) Assert.True(storage.Save(new()));
        Assert.Equal(marker, File.GetLastWriteTimeUtc(SettingsPath));
        Assert.True(storage.Save(new() { Keyboard = new(400, 200, 1000, 500) }));
        Assert.Equal(1000, new SettingsService(SettingsPath).Load().Keyboard.WidthDip);
        using var json = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Single(Directory.GetFiles(directory));
    }
    [Fact]
    public void SaveFailureDoesNotDestroyPreviousSettingsAndCanBeRetried()
    {
        var storage = new SettingsService(SettingsPath);
        Assert.True(storage.Save(new()));
        using (var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(storage.Save(new() { Symbols = true }));
        Assert.False(new SettingsService(SettingsPath).Load().Symbols);
        Assert.True(storage.Save(new() { Symbols = true }));
        Assert.True(new SettingsService(SettingsPath).Load().Symbols);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData("chat", false)] [InlineData("compact", false)] [InlineData("compact", true)]
    public void PrimaryLayoutAndSymbolsRestoreIndependently(string layout, bool symbols)
    {
        var expected = new AppSettings { KeyboardLayout = layout, Symbols = symbols, StartWithWindows = true,
            Keyboard = new(-400, 50, 840, 480) };
        Assert.True(new SettingsService(SettingsPath).Save(expected));
        var actual = new SettingsService(SettingsPath).Load();
        Assert.Equal(expected, actual);
        using var controller = new FloatKeys.Input.KeyboardController(new NeverInput(), new NeverRepeat(),
            symbols: actual.Symbols, layout: FloatKeys.Input.KeyboardSizing.Parse(actual.KeyboardLayout));
        Assert.Equal(layout, FloatKeys.Input.KeyboardSizing.Name(controller.SelectedLayout));
        Assert.Equal(symbols, controller.Symbols);
    }

    [Theory]
    [InlineData("{}")] [InlineData("{\"Layout\":\"uk\",\"Keyboard\":{\"HeightDip\":360}}")]
    [InlineData("{\"KeyboardLayout\":\"unknown\"}")] [InlineData("{\"KeyboardLayout\":null}")]
    [InlineData("{\"KeyboardLayout\":123}")]
    public void LegacyOrInvalidPrimarySelectionDefaultsToChat(string json)
    {
        File.WriteAllText(SettingsPath, json);
        var settings = new SettingsService(SettingsPath).Load();
        Assert.Equal("chat", settings.KeyboardLayout); Assert.Equal("uk", settings.Layout);
        Assert.True(settings.Keyboard.HeightDip >= 432);
        Assert.True(new SettingsService(SettingsPath).Save(settings));
        Assert.Equal(settings, new SettingsService(SettingsPath).Load());
    }

    [Fact]
    public void CompactWithoutGeometryUsesItsOwnDefaultHeight()
    {
        File.WriteAllText(SettingsPath, "{\"KeyboardLayout\":\"compact\"}");
        Assert.Equal(420, new SettingsService(SettingsPath).Load().Keyboard.HeightDip);
    }

    private sealed class NeverRepeat : FloatKeys.Input.IRepeatScheduler
    {
        public IDisposable Schedule(TimeSpan delay, Action callback) => throw new InvalidOperationException();
    }
    private sealed class NeverInput : FloatKeys.Input.IInputBackend
    {
        public FloatKeys.Input.InputResult KeyDown(FloatKeys.Input.InputKey key) => throw new InvalidOperationException();
        public FloatKeys.Input.InputResult KeyUp(FloatKeys.Input.InputKey key) => throw new InvalidOperationException();
        public FloatKeys.Input.InputResult KeyPress(FloatKeys.Input.InputKey key) => throw new InvalidOperationException();
        public FloatKeys.Input.InputResult KeyPressChord(FloatKeys.Input.InputKey key, IReadOnlyList<FloatKeys.Input.InputKey> modifiers) => throw new InvalidOperationException();
        public FloatKeys.Input.InputResult SendUnicodeText(string text) => throw new InvalidOperationException();
        public FloatKeys.Input.InputResult ReleaseAllKeys() => new(0, 0, 0, false, null);
        public void Dispose() { }
    }
    public void Dispose() => Directory.Delete(directory, true);
}
