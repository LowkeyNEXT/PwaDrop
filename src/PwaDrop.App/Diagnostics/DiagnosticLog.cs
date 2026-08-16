using PwaDrop.App.Drag;

namespace PwaDrop.App.Diagnostics;

internal sealed class DiagnosticLog
{
    private const long MaximumBytes = 1024 * 1024;
    private const int RetainedBytes = 512 * 1024;
    private readonly object _gate = new();

    internal DiagnosticLog(string path)
    {
        Path = path;
        TryPrune();
    }

    internal string Path { get; }

    internal void ExtractionStarted(DragPayloadKind payloadKind) =>
        Write($"extraction_started payload={payloadKind}");

    internal void ExtractionCompleted(DragPayloadKind payloadKind, int fileCount, TimeSpan elapsed) =>
        Write($"extraction_completed payload={payloadKind} files={fileCount} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void ExtractionFailed(DragPayloadKind payloadKind, int errorCode, TimeSpan elapsed) =>
        Write($"extraction_failed payload={payloadKind} hresult=0x{errorCode:X8} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void ReplayCompleted(PhysicalReplayResult replay, TimeSpan elapsed) =>
        Write($"replay_completed hresult=0x{replay.HResult:X8} effect=0x{(uint)replay.Effect:X8} accepted={replay.Accepted} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void ReplayFailed(int errorCode, TimeSpan elapsed) =>
        Write($"replay_failed hresult=0x{errorCode:X8} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void PrimeStarted(bool ownsOperation) =>
        Write($"prime_started owns_operation={ownsOperation}");

    internal void PrimeCompleted(string reason, int endResult, int fileCount, TimeSpan elapsed) =>
        Write($"prime_completed reason={reason} hresult=0x{endResult:X8} files={fileCount} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void PrimeFailed(int errorCode, TimeSpan elapsed) =>
        Write($"prime_failed hresult=0x{errorCode:X8} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void HookInjectionCompleted(TimeSpan elapsed) =>
        Write($"hook_injection_completed elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void HookInjectionFailed(int errorCode, TimeSpan elapsed) =>
        Write($"hook_injection_failed hresult=0x{errorCode:X8} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void HookInjectionFailed(int errorCode, int nativeExitCode, TimeSpan elapsed) =>
        Write($"hook_injection_failed hresult=0x{errorCode:X8} native_exit={nativeExitCode} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void ProcessWatcherUnavailable(Exception exception) =>
        Write($"process_watcher_unavailable hresult=0x{exception.HResult:X8} type={exception.GetType().Name} message={SingleLine(exception.Message)} fallback_seconds=5");

    internal void UnsupportedPayload() => Write("unsupported_payload");

    private static string SingleLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    private void Write(string eventData)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.AppendAllText(
                    Path,
                    $"{DateTimeOffset.UtcNow:O}\t{eventData}{Environment.NewLine}");
                PruneIfNeeded();
            }
        }
        catch (IOException)
        {
            // Diagnostics must never interrupt a drag.
        }
        catch (UnauthorizedAccessException)
        {
            // Managed devices may restrict local application data.
        }
    }

    private void TryPrune()
    {
        try
        {
            lock (_gate)
            {
                PruneIfNeeded();
            }
        }
        catch (IOException)
        {
            // Logging is best effort and must never prevent PWADrop from starting.
        }
        catch (UnauthorizedAccessException)
        {
            // Managed devices may restrict local application data.
        }
    }

    private void PruneIfNeeded()
    {
        var file = new FileInfo(Path);
        if (!file.Exists || file.Length <= MaximumBytes)
        {
            return;
        }

        var bytesToRead = (int)Math.Min(file.Length, RetainedBytes);
        var tail = new byte[bytesToRead];
        using (var source = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            source.Seek(-bytesToRead, SeekOrigin.End);
            source.ReadExactly(tail);
        }

        var firstLineBreak = Array.IndexOf(tail, (byte)'\n');
        var start = firstLineBreak >= 0 ? firstLineBreak + 1 : 0;
        var temporaryPath = Path + ".prune";
        using (var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            destination.Write(tail, start, tail.Length - start);
        }

        File.Move(temporaryPath, Path, overwrite: true);
    }
}
