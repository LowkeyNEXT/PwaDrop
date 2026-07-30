using System.Runtime.InteropServices;
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

            Application.Run(new HarnessForm());
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
            var targetPaths = VirtualFileExtractor.ReadFileDropPaths(dataObject);
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

            Console.WriteLine("Primed original CF_HDROP self-test passed.");
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
}
