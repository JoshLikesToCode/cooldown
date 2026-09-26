namespace Cooldown.Core;

/// <summary>Tiny file logger. Tail the file over SSH while testing on the gaming PC.</summary>
public static class Log
{
    private static readonly Lock Gate = new();
    private static string? _path;

    public static void Init(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _path = path;
    }

    public static void Info(string message) => Write("INF", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}";
        Console.WriteLine(line);
        if (_path is null) return;
        lock (Gate)
        {
            try { File.AppendAllText(_path, line + Environment.NewLine); }
            catch (IOException) { /* never crash over logging */ }
        }
    }
}
