using PwaDrop.App.Interop;

namespace PwaDrop.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\PwaDrop.Singleton", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var oleResult = NativeMethods.OleInitialize(IntPtr.Zero);
        if (oleResult < 0)
        {
            throw new InvalidOperationException($"OLE initialization failed with 0x{oleResult:X8}.");
        }

        try
        {
            using var context = new PwaDropApplicationContext();
            Application.Run(context);
        }
        finally
        {
            NativeMethods.OleUninitialize();
        }
    }
}
