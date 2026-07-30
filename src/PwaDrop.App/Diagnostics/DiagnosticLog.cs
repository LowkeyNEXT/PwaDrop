using PwaDrop.App.Drag;

namespace PwaDrop.App.Diagnostics;

internal sealed class DiagnosticLog
{
    private readonly object _gate = new();

    internal DiagnosticLog(string path)
    {
        Path = path;
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

    internal void PrimeCompleted(string reason, int endResult, TimeSpan elapsed) =>
        Write($"prime_completed reason={reason} hresult=0x{endResult:X8} elapsed_ms={elapsed.TotalMilliseconds:F0}");

    internal void PrimeFailed(int errorCode) =>
        Write($"prime_failed hresult=0x{errorCode:X8}");

    internal void UnsupportedPayload() => Write("unsupported_payload");

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
}
