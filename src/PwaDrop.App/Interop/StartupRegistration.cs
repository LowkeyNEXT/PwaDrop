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

    internal readonly record struct State(bool Enabled, bool Managed);

    internal static async Task<State> GetStateAsync()
    {
        if (IsPackaged())
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            return startupTask.State switch
            {
                StartupTaskState.Enabled => new State(true, false),
                StartupTaskState.EnabledByPolicy => new State(true, true),
                StartupTaskState.DisabledByPolicy => new State(false, true),
                _ => new State(false, false)
            };
        }

        if (RegistrationExists(Registry.LocalMachine))
        {
            return new State(true, true);
        }

        return new State(RegistrationMatches(Registry.CurrentUser), false);
    }

    internal static async Task<bool> SetEnabledAsync(bool enabled)
    {
        if (!IsPackaged())
        {
            SetUnpackagedStartup(enabled);
            return (await GetStateAsync()).Enabled;
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
        if (RegistrationExists(Registry.LocalMachine))
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true) ??
                        Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
        if (enabled)
        {
            key.SetValue(RegistryValueName, StartupCommand);
        }
        else
        {
            key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
            key.DeleteValue("PwaDrop", throwOnMissingValue: false);
        }
    }

    private static string StartupCommand => $"\"{Application.ExecutablePath}\" --startup";

    private static bool RegistrationMatches(RegistryKey root)
    {
        var value = ReadRegistration(root);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var executable = Application.ExecutablePath;
        return value.Equals(StartupCommand, StringComparison.OrdinalIgnoreCase) ||
               value.Equals($"\"{executable}\"", StringComparison.OrdinalIgnoreCase) ||
               value.Equals(executable, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RegistrationExists(RegistryKey root) =>
        !string.IsNullOrWhiteSpace(ReadRegistration(root));

    private static string? ReadRegistration(RegistryKey root)
    {
        using var key = root.OpenSubKey(RegistryPath, writable: false);
        return key?.GetValue(RegistryValueName) as string;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
