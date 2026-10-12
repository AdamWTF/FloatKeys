using System.IO;
using System.Text.Json;
using FloatKeys.Models;

namespace FloatKeys.Services;

internal sealed class SettingsService
{
    private readonly string path;
    private readonly Action<string>? log;
    private AppSettings? saved;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    internal SettingsService(string? path = null, Action<string>? log = null)
    {
        this.path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatKeys", "settings.json");
        this.log = log;
    }
    internal AppSettings Load()
    {
        saved = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new();
            bool repaired = false;
            T Read<T>(JsonElement parent, string name, T fallback)
            {
                if (!parent.TryGetProperty(name, out var property)) return fallback;
                try { return property.Deserialize<T>() ?? fallback; }
                catch (JsonException) { repaired = true; return fallback; }
            }
            WindowSettings Window(string name, WindowSettings fallback)
            {
                if (!document.RootElement.TryGetProperty(name, out var element)) return fallback;
                if (element.ValueKind != JsonValueKind.Object) { repaired = true; return fallback; }
                return new(Read<double?>(element, "XPhysical", null), Read<double?>(element, "YPhysical", null),
                    Read(element, "WidthDip", fallback.WidthDip), Read(element, "HeightDip", fallback.HeightDip));
            }
            var defaults = new AppSettings();
            var raw = new AppSettings
            {
                Launcher = Window("Launcher", defaults.Launcher), Keyboard = Window("Keyboard", defaults.Keyboard),
                LauncherOpacity = Read(document.RootElement, "LauncherOpacity", .85),
                Symbols = Read(document.RootElement, "Symbols", false),
                Layout = Read(document.RootElement, "Layout", "uk"), Theme = Read(document.RootElement, "Theme", "dark")
            };
            var valid = raw.Validate();
            saved = !repaired && raw == valid ? valid : null;
            return valid;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            log?.Invoke($"settings defaults: {e.GetType().Name}");
            return new();
        }
    }
    internal bool Save(AppSettings settings)
    {
        settings = settings.Validate();
        if (settings == saved) return true;
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, Options);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            saved = settings;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"settings save failed: {e.GetType().Name}");
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
