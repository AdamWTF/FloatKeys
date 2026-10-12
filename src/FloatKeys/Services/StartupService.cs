using System.IO;
using System.Security;
using Microsoft.Win32;

namespace FloatKeys.Services;

internal interface IStartupStore
{
    string? Read();
    void Write(string command);
    void Remove();
}

internal sealed class RegistryStartupStore : IStartupStore
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "FloatKeys";
    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        var value = key?.GetValue(Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is not null and not string) throw new InvalidDataException("Unexpected startup value type.");
        return (string?)value;
    }
    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        key.SetValue(Name, command, RegistryValueKind.String);
    }
    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key, true);
        key?.DeleteValue(Name, false);
    }
}

internal sealed record StartupStatus(bool Enabled, string? Error = null);

internal sealed class StartupService(IStartupStore store, string? executable, Action<string> log)
{
    internal static string QuoteExecutable(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') ||
            !Path.GetFileName(executable).Equals("FloatKeys.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Startup requires the published FloatKeys executable.");
        return $"\"{executable}\"";
    }

    private static bool IsFloatKeysEntry(string command)
    {
        // Accept only a quoted executable without arguments, or a legacy unquoted full path.
        string path = command.Trim();
        if (path.StartsWith('"') && path.EndsWith('"')) path = path[1..^1];
        return Path.IsPathFullyQualified(path) && !path.Contains('"') &&
            Path.GetFileName(path).Equals("FloatKeys.exe", StringComparison.OrdinalIgnoreCase);
    }

    internal StartupStatus Reconcile() => Execute(() =>
    {
        string? existing = store.Read();
        if (existing is null) return new(false);
        if (!IsFloatKeysEntry(existing)) return Unexpected();
        string expected = QuoteExecutable(executable ?? "");
        if (!StringComparer.OrdinalIgnoreCase.Equals(existing, expected)) store.Write(expected);
        return store.Read() is string actual && StringComparer.OrdinalIgnoreCase.Equals(actual, expected)
            ? new(true) : ConfirmationFailure();
    });

    internal StartupStatus SetEnabled(bool enabled) => Execute(() =>
    {
        string? existing = store.Read();
        if (existing is not null && !IsFloatKeysEntry(existing)) return Unexpected();
        if (enabled) store.Write(QuoteExecutable(executable ?? ""));
        else store.Remove();
        string? actual = store.Read();
        bool confirmed = enabled ? StringComparer.OrdinalIgnoreCase.Equals(actual, QuoteExecutable(executable ?? "")) : actual is null;
        return confirmed ? new(enabled) : ConfirmationFailure();
    });

    private StartupStatus ConfirmationFailure()
    {
        log("startup registration confirmation failed");
        return new(false, "Windows startup configuration could not be confirmed. See logs.");
    }

    private StartupStatus Unexpected()
    {
        log("startup registration unexpected entry");
        return new(false, "Unexpected FloatKeys startup entry. It was left unchanged; see logs.");
    }
    private StartupStatus Execute(Func<StartupStatus> action)
    {
        try { return action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            log($"startup registration failed: {e.GetType().Name}");
            return new(false, "Could not configure Start with Windows. See FloatKeys logs.");
        }
    }
}
