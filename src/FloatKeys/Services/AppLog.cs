using System.IO;
using System.Runtime.InteropServices;

namespace FloatKeys.Services;

internal sealed class AppLog
{
    private readonly object gate = new();
    private readonly string path = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "FloatKeys", "logs", "FloatKeys.log");

    internal AppLog(string? path = null)
    {
        if (path is not null) this.path = path;
        Write($"startup architecture={RuntimeInformation.ProcessArchitecture} os={Environment.OSVersion}");
    }

    internal void Write(string message)
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Bounded diagnostics without settings or target text collection.
                if (message.Length > 2048) message = message[..2048];
                if (File.Exists(path) && new FileInfo(path).Length > 1_044_000)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch { /* An unwritable log must not affect input or cleanup. */ }
    }
}
