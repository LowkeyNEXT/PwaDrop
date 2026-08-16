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
        if (args.Length != 2)
        {
            return false;
        }

        var renderMode = args[0].ToLowerInvariant();
        if (renderMode is not ("--render-settings" or "--render-settings-min" or
            "--render-settings-managed" or "--render-preferences"))
        {
            return false;
        }

        var managedStartup = renderMode == "--render-settings-managed";
        using var form = new SettingsForm(new AppSettings(StartWithWindows: managedStartup));
        form.SetStartupManaged(managedStartup);
        var pageName = renderMode == "--render-preferences" ? "Settings" : "Overview";
        var renderSize = renderMode == "--render-settings-min"
            ? new Size(760, 620)
            : new Size(920, 760);
        form.RenderTo(args[1], pageName, renderSize);
        return true;
    }
}
