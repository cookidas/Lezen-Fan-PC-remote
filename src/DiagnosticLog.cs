using System.Text.Json;

namespace LezenTray;

internal static class DiagnosticLog
{
    private static readonly object Gate = new();
    internal static string PathName => Path.Combine(AppContext.BaseDirectory, "data", "debug.log");

    internal static void Write(string stage, object? details = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
                if (File.Exists(PathName) && new FileInfo(PathName).Length > 1_000_000)
                    File.Move(PathName, PathName + ".previous", true);
                File.AppendAllText(PathName, JsonSerializer.Serialize(new
                {
                    Time = DateTimeOffset.Now, Process = Environment.ProcessId, Stage = stage, Details = details
                }) + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Logging must not interrupt control. */ }
    }
}
