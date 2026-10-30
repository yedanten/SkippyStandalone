using System.Collections.Concurrent;
using System.Text;

namespace Skippy.Core.Diagnostics;

public enum AppLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

public sealed record AppLogEntry(DateTimeOffset Timestamp, AppLogLevel Level, string Message);

/// <summary>
/// 轻量的文件日志。原生组件回传的日志也统一写入这里，便于用户一键导出。
/// </summary>
public sealed class AppLogger : IDisposable
{
    private readonly BlockingCollection<AppLogEntry> queue = new(new ConcurrentQueue<AppLogEntry>());
    private readonly ConcurrentQueue<AppLogEntry> recent = new();
    private readonly Thread writerThread;
    private readonly StreamWriter? writer;
    private readonly object gate = new();
    private bool disposed;

    public AppLogger(string directory, string filePrefix = "skippy")
    {
        this.LogDirectory = directory;

        try
        {
            Directory.CreateDirectory(directory);
            this.LogFile = Path.Combine(directory, $"{filePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            this.writer = new StreamWriter(this.LogFile, append: true, Encoding.UTF8) { AutoFlush = true };
        }
        catch
        {
            this.writer = null;
        }

        this.writerThread = new Thread(this.WriteLoop)
        {
            IsBackground = true,
            Name = "Skippy.LogWriter",
        };
        this.writerThread.Start();
    }

    public string LogDirectory { get; }

    public string? LogFile { get; }

    public event Action<AppLogEntry>? EntryWritten;

    public IReadOnlyList<AppLogEntry> Recent => this.recent.ToArray();

    public void Debug(string message) => this.Log(AppLogLevel.Debug, message);

    public void Info(string message) => this.Log(AppLogLevel.Info, message);

    public void Warning(string message) => this.Log(AppLogLevel.Warning, message);

    public void Error(string message) => this.Log(AppLogLevel.Error, message);

    public void Error(Exception ex, string message) => this.Log(AppLogLevel.Error, $"{message}: {ex}");

    public void Log(AppLogLevel level, string message)
    {
        var entry = new AppLogEntry(DateTimeOffset.Now, level, message);

        this.recent.Enqueue(entry);
        while (this.recent.Count > 400 && this.recent.TryDequeue(out _))
        {
        }

        this.queue.Add(entry);
        this.EntryWritten?.Invoke(entry);
    }

    private void WriteLoop()
    {
        try
        {
            foreach (var entry in this.queue.GetConsumingEnumerable())
            {
                var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level.ToString().ToUpperInvariant()}] {entry.Message}";

                lock (this.gate)
                {
                    this.writer?.WriteLine(line);
                }
            }
        }
        catch
        {
            // 日志失败不应影响主流程。
        }
    }

    /// <summary>导出当前日志到指定目录，返回新文件路径。</summary>
    public string? ExportTo(string destinationDirectory)
    {
        if (this.LogFile is null || !File.Exists(this.LogFile))
            return null;

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            var target = Path.Combine(
                destinationDirectory,
                $"Skippy_TroubleshootingLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            lock (this.gate)
            {
                this.writer?.Flush();
                File.Copy(this.LogFile, target, overwrite: true);
            }

            return target;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.queue.CompleteAdding();

        try
        {
            this.writerThread.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        lock (this.gate)
        {
            this.writer?.Dispose();
        }

        this.queue.Dispose();
    }
}
