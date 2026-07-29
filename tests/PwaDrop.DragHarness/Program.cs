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

            var extractionTask = extractor.ExtractAfterDropAsync(dataObject, payloadKind);
            if (extractionTask.IsCompleted)
            {
                throw new InvalidOperationException("Delayed extraction blocked the original Drop callback.");
            }

            var extraction = extractionTask.GetAwaiter().GetResult();
            if (extraction.Files.Count != 2 ||
                !File.ReadAllBytes(extraction.Files[0]).SequenceEqual(eml) ||
                !File.ReadAllBytes(extraction.Files[1]).SequenceEqual(pdf))
            {
                throw new InvalidDataException("The extracted test files did not match the delayed source data.");
            }

            if (!new PhysicalReplayResult(
                    unchecked((int)NativeMethods.DragDropSDrop),
                    DragDropEffects.Copy).Accepted ||
                new PhysicalReplayResult(
                    unchecked((int)NativeMethods.DragDropSDrop),
                    DragDropEffects.None).Accepted)
            {
                throw new InvalidOperationException("Physical replay result classification was invalid.");
            }

            Console.WriteLine("Deferred CF_HDROP self-test passed.");
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
