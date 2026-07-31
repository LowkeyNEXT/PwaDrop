using PwaDrop.App.Interop;
using PwaDrop.App.Ui;
using PwaDrop.Core;

namespace PwaDrop.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (TryRenderSettings(args))
        {
            return;
        }

        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\PwaDrop.Singleton", out var createdNew);
        if (!createdNew)
        {
            return;
        }

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

    private static bool TryRenderSettings(string[] args)
    {
        if (args.Length != 2 || !args[0].Equals("--render-settings", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var previewRoot = Path.Combine(Path.GetTempPath(), "PWADrop.UiPreview");
        using var form = new SettingsForm(
            new AppSettings(),
            Path.Combine(previewRoot, "Cache"),
            Path.Combine(previewRoot, "diagnostics.log"));
        form.RenderTo(args[1]);
        return true;
    }
}
