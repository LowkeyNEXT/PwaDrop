using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace PwaDrop.App.Interop;

internal static class ProcessSecurity
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    internal static bool CanInject(Process process)
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            if (process.Id == Environment.ProcessId ||
                process.SessionId != currentProcess.SessionId ||
                !Environment.Is64BitOperatingSystem ||
                !Environment.Is64BitProcess)
            {
                return false;
            }

            using var processHandle = OpenProcess(
                ProcessQueryLimitedInformation,
                inheritHandle: false,
                (uint)process.Id);
            if (processHandle.IsInvalid ||
                !IsWow64Process(processHandle, out var isWow64) ||
                isWow64 ||
                !OpenProcessToken(processHandle, TokenQuery, out var token))
            {
                return false;
            }

            using (token)
            {
                return GetTokenUser(token) == WindowsIdentity.GetCurrent().User?.Value &&
                       !IsElevated(token);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? GetTokenUser(SafeAccessTokenHandle token)
    {
        _ = GetTokenInformation(
            token,
            TokenInformationClass.TokenUser,
            IntPtr.Zero,
            0,
            out var requiredLength);
        if (requiredLength <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(requiredLength);
        try
        {
            if (!GetTokenInformation(
                    token,
                    TokenInformationClass.TokenUser,
                    buffer,
                    requiredLength,
                    out _))
            {
                return null;
            }

            var sidPointer = Marshal.ReadIntPtr(buffer);
            return new SecurityIdentifier(sidPointer).Value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool IsElevated(SafeAccessTokenHandle token)
    {
        var size = Marshal.SizeOf<TokenElevation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(
                    token,
                    TokenInformationClass.TokenElevation,
                    buffer,
                    size,
                    out _))
            {
                return true;
            }

            return Marshal.PtrToStructure<TokenElevation>(buffer).TokenIsElevated != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        SafeProcessHandle processHandle,
        uint desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(
        SafeProcessHandle processHandle,
        [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle tokenHandle,
        TokenInformationClass tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    private enum TokenInformationClass
    {
        TokenUser = 1,
        TokenElevation = 20
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct TokenElevation
    {
        internal readonly int TokenIsElevated;
    }
}
