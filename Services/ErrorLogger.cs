using System;
using System.IO;
using System.Text;

namespace NNotify.Services;

public static class ErrorLogger
{
    private static readonly object Sync = new();

    public static string BaseDirectory
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NNotify");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static void Log(string context, Exception exception)
    {
        try
        {
            lock (Sync)
            {
                var path = Path.Combine(BaseDirectory, "log.txt");
                var builder = new StringBuilder();
                builder.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {context}");
                builder.AppendLine(exception.ToString());
                builder.AppendLine(new string('-', 80));
                File.AppendAllText(path, builder.ToString());
            }
        }
        catch
        {
            // Swallow logging failures to keep app lightweight and resilient.
        }
    }
}
