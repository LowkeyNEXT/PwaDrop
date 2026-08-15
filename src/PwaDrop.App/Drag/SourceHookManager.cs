using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using PwaDrop.App.Diagnostics;
using PwaDrop.App.Interop;
using PwaDrop.Core;

namespace PwaDrop.App.Drag;

internal sealed class SourceHookManager : IDisposable
{
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const string EnableEventName = @"Local\PwaDrop.DragBridge.Enabled.v7";
    private static readonly TimeSpan EventDebounce = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan SafetyReconciliationInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FallbackReconciliationInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InjectionTimeout = TimeSpan.FromSeconds(10);

    private readonly string _hostPath;
    private readonly string _hookPath;
    private readonly DiagnosticLog _diagnostics;
    private readonly EventWaitHandle _enabledEvent;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly ConcurrentQueue<SourceProcessEvent> _processEvents = new();
    private readonly SemaphoreSlim _processSignal = new(0);
    private readonly ProcessEventWatcher _processWatcher = new();
    private readonly object _stateGate = new();
    private readonly HashSet<ProcessIdentity> _injected = [];
    private readonly Dictionary<ProcessIdentity, DateTimeOffset> _retryAfter = [];
    private Task? _worker;
    private bool _enabled;
    private bool _disposed;

    internal SourceHookManager(string runtimePath, DiagnosticLog diagnostics)
    {
        runtimePath = ResolveRuntimeGeneration(runtimePath);
        _hostPath = Path.Combine(runtimePath, "PwaDrop.HookHost.exe");
        _hookPath = Path.Combine(runtimePath, "PwaDrop.Hook.dll");
        _diagnostics = diagnostics;
        _processWatcher.SourceProcessChanged += OnSourceProcessChanged;
        var currentUser = WindowsIdentity.GetCurrent().User ??
            throw new InvalidOperationException("The current Windows user has no SID.");
        var eventSecurity = new EventWaitHandleSecurity();
        eventSecurity.AddAccessRule(new EventWaitHandleAccessRule(
            currentUser,
            EventWaitHandleRights.FullControl,
            AccessControlType.Allow));
        _enabledEvent = EventWaitHandleAcl.Create(
            initialState: false,
            mode: EventResetMode.ManualReset,
            name: EnableEventName,
            createdNew: out _,
            eventSecurity: eventSecurity);
        _enabledEvent.SetAccessControl(eventSecurity);
    }

    private static string ResolveRuntimeGeneration(string runtimeRoot)
    {
        var generationFile = Path.Combine(runtimeRoot, "current.txt");
        if (!File.Exists(generationFile))
        {
            return runtimeRoot;
        }

        var generation = File.ReadAllText(generationFile).Trim();
        if (string.IsNullOrWhiteSpace(generation) ||
            generation.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            generation.Contains(Path.DirectorySeparatorChar) ||
            generation.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidDataException("The PWADrop source-hook generation is invalid.");
        }

        return Path.Combine(runtimeRoot, generation);
    }

    internal bool IsRunning
    {
        get
        {
            lock (_stateGate)
            {
                return _enabled;
            }
        }
    }

    internal void Start()
    {
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_enabled)
            {
                return;
            }

            ValidateRuntime();
            try
            {
                _processWatcher.Start();
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                _diagnostics.ProcessWatcherUnavailable(exception);
            }
            _enabledEvent.Set();
            _enabled = true;
            _worker ??= Task.Run(() => RunAsync(_disposeCancellation.Token));
            _processSignal.Release();
        }
    }

    internal void Stop()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _enabled = false;
            _enabledEvent.Reset();
            _processWatcher.Stop();
        }
    }

    public void Dispose()
    {
        Task? worker;
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _enabled = false;
            _enabledEvent.Reset();
            _processWatcher.Stop();
            _disposeCancellation.Cancel();
            worker = _worker;
        }

        try
        {
            _ = worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception)
            when (exception.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            // Cancellation is the expected shutdown path.
        }

        _enabledEvent.Dispose();
        _processWatcher.SourceProcessChanged -= OnSourceProcessChanged;
        _processWatcher.Dispose();
        _processSignal.Dispose();
        _disposeCancellation.Dispose();
    }

    private void ValidateRuntime()
    {
        if (!File.Exists(_hostPath) || !File.Exists(_hookPath))
        {
            throw new FileNotFoundException("The PWADrop source-hook runtime is incomplete.");
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsRunning)
            {
                await InjectNewSourcesAsync(cancellationToken).ConfigureAwait(false);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var signaled = await _processSignal.WaitAsync(
                    _processWatcher.IsRunning
                        ? SafetyReconciliationInterval
                        : FallbackReconciliationInterval,
                    cancellationToken).ConfigureAwait(false);
                if (!signaled)
                {
                    if (IsRunning)
                    {
                        await InjectNewSourcesAsync(cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                await Task.Delay(EventDebounce, cancellationToken).ConfigureAwait(false);
                var started = new HashSet<uint>();
                var reconcileAll = DrainProcessEvents(started);
                if (IsRunning && (reconcileAll || started.Count > 0))
                {
                    await InjectNewSourcesAsync(
                        cancellationToken,
                        reconcileAll ? null : started).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Disposal cancels scanning and any bounded helper process.
        }
    }

    private void OnSourceProcessChanged(SourceProcessEvent processEvent)
    {
        _processEvents.Enqueue(processEvent);
        _processSignal.Release();
    }

    private bool DrainProcessEvents(HashSet<uint> started)
    {
        var reconcileAll = false;
        do
        {
            if (!_processEvents.TryDequeue(out var processEvent))
            {
                continue;
            }

            if (processEvent.Started)
            {
                if (processEvent.ProcessId == 0)
                {
                    reconcileAll = true;
                }
                else
                {
                    started.Add(processEvent.ProcessId);
                }
            }
            else
            {
                started.Remove(processEvent.ProcessId);
                _injected.RemoveWhere(identity => identity.ProcessId == processEvent.ProcessId);
                foreach (var identity in _retryAfter.Keys
                    .Where(identity => identity.ProcessId == processEvent.ProcessId)
                    .ToArray())
                {
                    _retryAfter.Remove(identity);
                }
            }
        }
        while (_processSignal.Wait(0));

        return reconcileAll;
    }

    private async Task InjectNewSourcesAsync(
        CancellationToken cancellationToken,
        IReadOnlySet<uint>? requestedProcessIds = null)
    {
        var now = DateTimeOffset.UtcNow;
        var observed = new HashSet<ProcessIdentity>();
        var candidates = new List<(Process Process, ProcessIdentity Identity)>();
        var processTree = ProcessClassifier.SnapshotProcessTree();
        var processIds = requestedProcessIds is null
            ? processTree.Values
                .Where(process => Path.GetFileNameWithoutExtension(
                    process.ExecutableName).Equals(
                        "msedgewebview2",
                        StringComparison.OrdinalIgnoreCase))
                .Select(process => process.ProcessId)
                .Concat(ProcessClassifier.SnapshotTopLevelWindowProcessIds())
                .ToHashSet()
            : requestedProcessIds;
        foreach (var processId in processIds)
        {
            if (!ProcessClassifier.IsRootSupportedSourceProcess(processId, processTree))
            {
                continue;
            }

            Process? process = null;
            try
            {
                process = Process.GetProcessById((int)processId);
                if (!ProcessSecurity.CanInject(process))
                {
                    process.Dispose();
                    continue;
                }

                var identity = new ProcessIdentity(process.Id, process.StartTime.ToUniversalTime());
                observed.Add(identity);
                if (_injected.Contains(identity) ||
                    (_retryAfter.TryGetValue(identity, out var retryAt) && retryAt > now))
                {
                    process.Dispose();
                    continue;
                }

                _retryAfter[identity] = now + RetryDelay;
                candidates.Add((process, identity));
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                UnauthorizedAccessException or
                ArgumentException)
            {
                process?.Dispose();
            }
        }

        if (requestedProcessIds is null)
        {
            _injected.RemoveWhere(identity => !observed.Contains(identity));
            foreach (var identity in _retryAfter.Keys
                .Where(identity => !observed.Contains(identity))
                .ToArray())
            {
                _retryAfter.Remove(identity);
            }
        }

        foreach (var batch in candidates.Chunk(4))
        {
            var results = await Task.WhenAll(batch.Select(async candidate =>
                (
                    candidate.Identity,
                    Success: await InjectAsync(candidate.Process, candidate.Identity, cancellationToken)
                        .ConfigureAwait(false)
                ))).ConfigureAwait(false);

            foreach (var result in results)
            {
                if (result.Success)
                {
                    _injected.Add(result.Identity);
                    _retryAfter.Remove(result.Identity);
                }
            }
        }
    }

    private async Task<bool> InjectAsync(
        Process sourceProcess,
        ProcessIdentity identity,
        CancellationToken cancellationToken)
    {
        using (sourceProcess)
        {
            if (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var started = Stopwatch.GetTimestamp();
            Process? host = null;
            try
            {
                host = Process.Start(new ProcessStartInfo
                {
                    FileName = _hostPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ArgumentList =
                    {
                        identity.ProcessId.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        identity.Started.Ticks.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        _hookPath
                    }
                });
                if (host is null)
                {
                    _diagnostics.HookInjectionFailed(
                        EFail,
                        Stopwatch.GetElapsedTime(started));
                    return false;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(InjectionTimeout);
                await host.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                if (host.ExitCode == 0)
                {
                    _diagnostics.HookInjectionCompleted(
                        Stopwatch.GetElapsedTime(started));
                    return true;
                }

                _diagnostics.HookInjectionFailed(
                    HResultForExitCode(host.ExitCode),
                    host.ExitCode,
                    Stopwatch.GetElapsedTime(started));
                return false;
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(host);
                if (!cancellationToken.IsCancellationRequested)
                {
                    _diagnostics.HookInjectionFailed(
                        unchecked((int)0x800705B4),
                        Stopwatch.GetElapsedTime(started));
                }

                return false;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                UnauthorizedAccessException)
            {
                _diagnostics.HookInjectionFailed(
                    exception.HResult,
                    Stopwatch.GetElapsedTime(started));
                return false;
            }
            finally
            {
                host?.Dispose();
            }
        }
    }

    private static int HResultForExitCode(int exitCode) =>
        exitCode switch
        {
            2 => EInvalidArg,
            3 => EAccessDenied,
            6 => unchecked((int)0x800705B4),
            _ => EFail
        };

    private static void KillProcessTree(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException)
        {
            // The helper may have exited between the state check and termination.
        }
    }

    private readonly record struct ProcessIdentity(int ProcessId, DateTime Started);
}
