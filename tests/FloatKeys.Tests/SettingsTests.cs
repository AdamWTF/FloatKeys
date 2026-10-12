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
        Assert.Equal(960, loaded.Keyboard.WidthDip); Assert.Equal(360, loaded.Keyboard.HeightDip);
        Assert.True(loaded.Symbols); Assert.Equal(.85, loaded.LauncherOpacity);
        Assert.True(storage.Save(loaded));
        Assert.DoesNotContain("bad", File.ReadAllText(SettingsPath));
    }
    [Fact]
    public void PositionDimensionsAndPreferencesSurviveRestart()
    {
        var expected = new AppSettings { Launcher = new(-1800, 120, 64, 64), Keyboard = new(-1500, 600, 960, 420),
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
    public void Dispose() => Directory.Delete(directory, true);
}
