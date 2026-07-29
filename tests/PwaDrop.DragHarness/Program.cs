using PwaDrop.App.Interop;

namespace PwaDrop.DragHarness;

internal static class Program
{
    [STAThread]
    private static void Main()
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
            Application.Run(new HarnessForm());
        }
        finally
        {
            NativeMethods.OleUninitialize();
        }
    }
}

