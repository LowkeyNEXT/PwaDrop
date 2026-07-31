using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace PwaDrop.App.Interop;

internal static class StartupRegistration
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "PWADrop";
    private const string StartupTaskId = "PWADropStartup";
    private const int AppModelErrorNoPackage = 15700;

    internal static async Task<bool> SetEnabledAsync(bool enabled)
    {
        if (!IsPackaged())
        {
            SetUnpackagedStartup(enabled);
            return enabled;
        }

        var startupTask = await StartupTask.GetAsync(StartupTaskId);
        if (!enabled)
        {
            startupTask.Disable();
            return false;
        }

        var state = await startupTask.RequestEnableAsync();
        return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    internal static bool IsPackaged()
    {
        var length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        return result != AppModelErrorNoPackage;
    }

    private static void SetUnpackagedStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true) ??
                        Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
        if (enabled)
        {
            key.SetValue(RegistryValueName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
            key.DeleteValue("PwaDrop", throwOnMissingValue: false);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
