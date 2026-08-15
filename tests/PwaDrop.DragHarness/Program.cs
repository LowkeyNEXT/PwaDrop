using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using PwaDrop.App.Drag;
using PwaDrop.App.Interop;
using PwaDrop.Core;

namespace PwaDrop.DragHarness;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var result = NativeMethods.OleInitialize(IntPtr.Zero);
        if (result < 0)
        {
            throw new InvalidOperationException($"OLE initialization failed with 0x{result:X8}.");
        }

        try
        {
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                return RunSelfTest();
            }

            Application.Run(new HarnessForm(
                args.Contains("--automation", StringComparer.OrdinalIgnoreCase)));
            return 0;
        }
        finally
        {
            NativeMethods.OleUninitialize();
        }
    }

    private static int RunSelfTest()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "PwaDrop.SelfTest", Guid.NewGuid().ToString("N"));
        try
        {
            var eml = Encoding.UTF8.GetBytes("Subject: PwaDrop async self-test\r\n\r\nTest body.\r\n");
            var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% PwaDrop self-test\n%%EOF\n");
            using var dataObject = new VirtualFileDataObject(
                new VirtualTestFile("test-conversation.eml", eml),
                new VirtualTestFile("invoice.pdf", pdf));
            var extractor = new VirtualFileExtractor(new CacheManager(cacheRoot));
            var payloadKind = extractor.DetectPayload(dataObject);
            if (payloadKind != DragPayloadKind.AsyncFileDrop)
            {
                throw new InvalidOperationException($"Expected an async file drop, received {payloadKind}.");
            }

            try
            {
                _ = VirtualFileExtractor.ReadFileDropPaths(dataObject);
                throw new InvalidOperationException("The delayed source rendered before StartOperation.");
            }
            catch (COMException)
            {
                // Chromium-style delayed data is unavailable before priming.
            }

            using var primedDrag = extractor.PrimeAsyncFileDrop(dataObject);
            if (!primedDrag.OwnsOperation ||
                dataObject.InOperation(out var inOperation) != 0 ||
                !inOperation)
            {
                throw new InvalidOperationException("StartOperation did not prime the original data object.");
            }

            try
            {
                _ = VirtualFileExtractor.ReadFileDropPaths(dataObject);
                throw new InvalidOperationException("Priming rendered data before the original drag ended.");
            }
            catch (COMException)
            {
                // Chromium still refuses GetData while its source drag loop is active.
            }

            dataObject.FinishDragLoop();
            var targetPaths = primedDrag.MaterializeAndCompleteAfterReleaseAsync(
                    TimeSpan.Zero,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (targetPaths.Count != 2 ||
                !File.ReadAllBytes(targetPaths[0]).SequenceEqual(eml) ||
                !File.ReadAllBytes(targetPaths[1]).SequenceEqual(pdf))
            {
                throw new InvalidDataException("The target did not receive the primed source data byte-for-byte.");
            }

            _ = primedDrag.Complete();
            if (dataObject.InOperation(out inOperation) != 0 || inOperation)
            {
                throw new InvalidOperationException("EndOperation did not close the primed data operation.");
            }

            if (Directory.Exists(cacheRoot))
            {
                throw new InvalidOperationException("Priming unexpectedly created a PwaDrop cache session.");
            }

            VerifyRelayHidesAfterDropOpportunity(extractor);
            VerifyAsyncPayloadPrimesOriginalDrag(extractor);
            VerifyOriginalDragHandoffOrdering();
            VerifyRelayRegistrationCanBeSuspendedForReplay(extractor);
            VerifyPhysicalReplayWakesItsOwnDragLoop();
            VerifyPhysicalReplayRepeatsWakeUntilDragCompletes();
            VerifyPhysicalReplayCompletesWithoutExternalInput(extractor, cacheRoot);

            Console.WriteLine("PWADrop drag relay self-test passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, recursive: true);
                }
            }
            catch (IOException)
            {
                // CI cleanup will remove the temporary directory.
            }
        }
    }

    private static void VerifyAsyncPayloadPrimesOriginalDrag(VirtualFileExtractor extractor)
    {
        using var dataObject = new VirtualFileDataObject(
            new VirtualTestFile("test-conversation.eml", [1, 2, 3]));
        System.Runtime.InteropServices.ComTypes.IDataObject? primedObject = null;
        uint primedKeyState = 0;
        var dropCalled = false;
        var target = new OleRelayDropTarget(
            extractor,
            (candidate, keyState) =>
            {
                primedObject = candidate;
                primedKeyState = keyState;
                return true;
            },
            (_, _, payloadKind) =>
            {
                dropCalled = true;
                return true;
            },
            () => { },
            () => { });

        var effect = NativeMethods.DropEffectNone;
        _ = target.DragEnter(
            dataObject,
            NativeMethods.MkLButton,
            new NativeMethods.PointL(0, 0),
            ref effect);

        if (effect != NativeMethods.DropEffectNone ||
            !ReferenceEquals(primedObject, dataObject) ||
            (primedKeyState & NativeMethods.MkLButton) == 0)
        {
            throw new InvalidOperationException("The async payload did not prime the original drag.");
        }

        dataObject.FinishDragLoop();
        effect = NativeMethods.DropEffectCopy;
        _ = target.Drop(
            dataObject,
            0,
            new NativeMethods.PointL(0, 0),
            ref effect);

        if (dropCalled || effect != NativeMethods.DropEffectNone)
        {
            throw new InvalidOperationException("The primed async payload was consumed by the relay.");
        }
    }

    private static void VerifyRelayHidesAfterDropOpportunity(VirtualFileExtractor extractor)
    {
        using var overlay = new RelayOverlayForm(
            extractor,
            (_, _) => false,
            (_, _, _) => false,
            () => { },
            () => { });
        var primedDragReleased = false;
        using var monitor = new DragSourceMonitor(
            overlay,
            () => Array.Empty<IntPtr>(),
            () => primedDragReleased = true);

        _ = overlay.Handle;
        overlay.ShowRelay();
        monitor.MarkCurrentDragPrimed();

        var hookData = new NativeMethods.MsllHookStruct
        {
            Point = new NativeMethods.Point(0, 0)
        };
        var hookDataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.MsllHookStruct>());
        try
        {
            Marshal.StructureToPtr(hookData, hookDataPointer, false);
            var hookCallback = typeof(DragSourceMonitor).GetMethod(
                "HookCallback",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(DragSourceMonitor), "HookCallback");
            _ = hookCallback.Invoke(
                monitor,
                [0, new IntPtr(NativeMethods.WmLButtonUp), hookDataPointer]);

            if (!overlay.IsRelayVisible)
            {
                throw new InvalidOperationException("The relay overlay hid before OLE could deliver Drop.");
            }

            if (!primedDragReleased)
            {
                throw new InvalidOperationException("The monitor did not release the retained original drag.");
            }

            Thread.Sleep(350);
            Application.DoEvents();
            if (overlay.IsRelayVisible)
            {
                throw new InvalidOperationException("The relay overlay remained visible after the release failsafe.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(hookDataPointer);
        }
    }

    private static void VerifyOriginalDragHandoffOrdering()
    {
        var events = new List<string>();
        var handedOff = OriginalDragHandoff.TryWakeUnderlyingTarget(
            () => events.Add("suspend"),
            () =>
            {
                events.Add("button");
                return true;
            },
            () => events.Add("nudge"));

        if (!handedOff ||
            !events.SequenceEqual(["suspend", "button", "nudge"]))
        {
            throw new InvalidOperationException("The original drag was not yielded before its wake signal.");
        }

        events.Clear();
        handedOff = OriginalDragHandoff.TryWakeUnderlyingTarget(
            () => events.Add("suspend"),
            () =>
            {
                events.Add("button");
                return false;
            },
            () => events.Add("nudge"));

        if (handedOff ||
            !events.SequenceEqual(["suspend", "button"]))
        {
            throw new InvalidOperationException("The original drag was nudged after its mouse button was released.");
        }
    }

    private static void VerifyRelayRegistrationCanBeSuspendedForReplay(VirtualFileExtractor extractor)
    {
        const int dragDropAlreadyRegistered = unchecked((int)0x80040101);
        using var overlay = new RelayOverlayForm(
            extractor,
            (_, _) => false,
            (_, _, _) => false,
            () => { },
            () => { });
        var probeTarget = new OleRelayDropTarget(
            extractor,
            (_, _) => false,
            (_, _, _) => false,
            () => { },
            () => { });

        _ = overlay.Handle;
        var duplicateRegistration = NativeMethods.RegisterDragDrop(overlay.Handle, probeTarget);
        if (duplicateRegistration != dragDropAlreadyRegistered)
        {
            throw new InvalidOperationException(
                $"The relay target was not registered before replay suspension: 0x{duplicateRegistration:X8}.");
        }

        overlay.SuspendDropTarget();
        var replayRegistration = NativeMethods.RegisterDragDrop(overlay.Handle, probeTarget);
        if (replayRegistration != 0)
        {
            throw new InvalidOperationException(
                $"The relay target still intercepted replay registration: 0x{replayRegistration:X8}.");
        }

        _ = NativeMethods.RevokeDragDrop(overlay.Handle);
        overlay.ResumeDropTarget();
        var resumedRegistration = NativeMethods.RegisterDragDrop(overlay.Handle, probeTarget);
        if (resumedRegistration != dragDropAlreadyRegistered)
        {
            if (resumedRegistration == 0)
            {
                _ = NativeMethods.RevokeDragDrop(overlay.Handle);
            }

            throw new InvalidOperationException(
                $"The relay target did not resume after replay: 0x{resumedRegistration:X8}.");
        }
    }

    private static void VerifyPhysicalReplayWakesItsOwnDragLoop()
    {
        using var wakeObserved = new ManualResetEventSlim();
        var result = PhysicalFileReplay.RunWithWakeSignal(
            wakeObserved.Set,
            () =>
            {
                if (!wakeObserved.Wait(TimeSpan.FromSeconds(1)))
                {
                    throw new TimeoutException("The physical replay waited for external mouse input.");
                }

                return 17;
            },
            TimeSpan.FromMilliseconds(25));

        if (result != 17)
        {
            throw new InvalidOperationException("The physical replay wake changed the drag-loop result.");
        }
    }

    private static void VerifyPhysicalReplayRepeatsWakeUntilDragCompletes()
    {
        var wakeCount = 0;
        var result = PhysicalFileReplay.RunWithWakeSignal(
            () => Interlocked.Increment(ref wakeCount),
            () =>
            {
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                while (Volatile.Read(ref wakeCount) < 3 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(5);
                }

                return 23;
            },
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(10));

        if (result != 23 || wakeCount < 3)
        {
            throw new InvalidOperationException("The physical replay stopped waking before the drag could drop.");
        }
    }

    private static void VerifyPhysicalReplayCompletesWithoutExternalInput(
        VirtualFileExtractor extractor,
        string cacheRoot)
    {
        var replayFile = Path.Combine(cacheRoot, "physical-replay.txt");
        Directory.CreateDirectory(cacheRoot);
        File.WriteAllText(replayFile, "PWADrop physical replay self-test.");

        using var targetForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            TopMost = true,
            Opacity = 0.01,
            StartPosition = FormStartPosition.Manual,
            Bounds = SystemInformation.VirtualScreen
        };
        var target = new PhysicalFileDropProbeTarget();
        var registration = NativeMethods.RegisterDragDrop(targetForm.Handle, target);
        if (registration != 0)
        {
            throw new InvalidOperationException(
                $"The physical replay probe could not register: 0x{registration:X8}.");
        }

        targetForm.Show();
        NativeMethods.SetWindowPos(
            targetForm.Handle,
            new IntPtr(-1),
            targetForm.Left,
            targetForm.Top,
            targetForm.Width,
            targetForm.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        PhysicalReplayResult replay;
        try
        {
            replay = PhysicalFileReplay.Replay([replayFile]);
        }
        finally
        {
            _ = NativeMethods.RevokeDragDrop(targetForm.Handle);
            targetForm.Hide();
        }

        if (!replay.Accepted ||
            target.ReceivedFiles.Count != 1 ||
            !string.Equals(target.ReceivedFiles[0], replayFile, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The physical replay did not complete on its own: hresult=0x{replay.HResult:X8}, effect=0x{(uint)replay.Effect:X8}.");
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class PhysicalFileDropProbeTarget : IOleDropTarget
    {
        internal IReadOnlyList<string> ReceivedFiles { get; private set; } = [];

        public int DragEnter(
            System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
            uint keyState,
            NativeMethods.PointL point,
            ref uint effect)
        {
            effect = NativeMethods.DropEffectCopy;
            return 0;
        }

        public int DragOver(uint keyState, NativeMethods.PointL point, ref uint effect)
        {
            effect = NativeMethods.DropEffectCopy;
            return 0;
        }

        public int DragLeave() => 0;

        public int Drop(
            System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
            uint keyState,
            NativeMethods.PointL point,
            ref uint effect)
        {
            ReceivedFiles = VirtualFileExtractor.ReadFileDropPaths(dataObject);
            effect = NativeMethods.DropEffectCopy;
            return 0;
        }
    }
}
