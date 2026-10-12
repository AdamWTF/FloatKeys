using System.IO;
using System.Runtime.InteropServices;

namespace FloatKeys.Services;

internal sealed class PrototypeLog
{
    private readonly object gate = new();
    private readonly string path = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "FloatKeys", "prototype.log");

    internal PrototypeLog() => Write($"startup architecture={RuntimeInformation.ProcessArchitecture} os={Environment.OSVersion}");

    internal void Write(string message)
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Bounded diagnostics without settings or target text collection.
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch { /* An unwritable log must not affect input or cleanup. */ }
    }
}
