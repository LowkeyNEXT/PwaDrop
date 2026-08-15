# Enterprise deployment

PWADrop is an interactive, per-session desktop utility. Even when its files are installed machine-wide, it intentionally does not run as a service or as administrator. Each signed-in user gets one medium-integrity PWADrop tray process that can interact only with supported, same-user, non-elevated source applications in that Windows session.

> [!IMPORTANT]
> This technical guide does not grant commercial-use rights. PWADrop's repository license permits noncommercial purposes; a for-profit production deployment requires a separate written commercial license from the licensor.

## Choose an installation scope

| Scope | Default location | Startup registration | Administration | Intended use |
| --- | --- | --- | --- | --- |
| Current user | `%LOCALAPPDATA%\Programs\PWADrop` | `HKCU\...\Run` | Not required | Personal devices and user-targeted deployment |
| All users | `%ProgramFiles%\PWADrop` | `HKLM\...\Run` | Required for installation | Shared PCs and device-targeted enterprise deployment |
| MSIX | Windows package location | Packaged `StartupTask` | Depends on deployment channel | Microsoft Store or managed MSIX deployment |

An all-users installation writes one machine startup registration. Windows starts PWADrop separately with each interactive user's normal token. The startup toggle is shown as administrator-managed because an unprivileged user must not remove a machine policy.

Per-user installation works with PWADrop's same-user security model. On devices with App Control for Business, AppLocker, or a policy that blocks execution from user-writable locations, use the signed all-users package instead.

## Silent EXE deployment

Current user, with startup enabled:

```powershell
PWADrop-Setup-v0.1.0-alpha.7-win-x64.exe /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="startup" /LOG="%TEMP%\PWADrop-install.log"
```

All users, with startup enabled:

```powershell
PWADrop-Setup-v0.1.0-alpha.7-win-x64.exe /ALLUSERS /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="startup" /LOG="%ProgramData%\PWADrop-install.log"
```

For a clean installation without automatic startup, use `/TASKS=""`. During an upgrade, use `/MERGETASKS="!startup"` to explicitly turn an existing startup selection off. Exit code `0` means setup completed successfully; any nonzero code is a failure or cancellation.

The stable installer ID is `{C97D6EC5-BD37-4B82-B0EF-CC55E76EA141}`. In-place upgrades retain the previous install scope, path, and selected tasks unless deployment arguments override them. The uninstall command is registered under the standard 64-bit Windows uninstall key and can be used by Intune or Configuration Manager.

Recommended Intune Win32 settings for device deployment:

- Install behavior: **System**
- Install command: the `/ALLUSERS` command above
- Uninstall command: `"%ProgramFiles%\PWADrop\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`
- Detection: file `%ProgramFiles%\PWADrop\PwaDrop.exe`, with file-version comparison for upgrades
- Restart behavior: no specific action

For user-targeted deployment, choose **User**, use `/CURRENTUSER`, and detect `%LOCALAPPDATA%\Programs\PWADrop\PwaDrop.exe` in the user context.

## Signing and application control

Production releases should Authenticode-sign all executable content, not only the outer installer:

- `PwaDrop.exe`
- `PwaDrop.HookHost.exe`
- `PwaDrop.Hook.dll`
- the setup executable and its embedded uninstaller

`scripts/package-installer.ps1` accepts `-SigningCertificateThumbprint`, `-CertificateStore`, and `-TimestampUrl` and signs all of those components during packaging. Use a CA-trusted organization certificate or Azure Artifact Signing for public distribution. Internally managed environments can distribute trust for an enterprise certificate before deployment.

PWADrop performs narrowly scoped process injection. Endpoint security products may block that behavior even when installation succeeds. Do not create broad antivirus exclusions. Validate the signed build in audit mode first, then use publisher- and path-scoped App Control rules approved by the organization's security team. Elevated sources, protected processes, other user sessions, and unsupported executable families remain out of scope.

## MSIX and Microsoft Store

The repository also produces a medium-integrity, full-trust MSIX. MSIX is the preferred route for Microsoft Store distribution and can be deployed silently through Intune. Every sideloaded MSIX must be signed by a certificate trusted on the target device. Device provisioning can make the package available to multiple users, but PWADrop still runs separately inside each signed-in user's session.

Before selecting MSIX as the primary enterprise artifact, run the full drag validation matrix on the signed package. Some App Control and endpoint-security policies treat packaged and unpackaged process injection differently.

## Data, logs, and removal

Every user's settings, redacted diagnostics, and temporary drag cache remain under `%LOCALAPPDATA%\PwaDrop`, regardless of install scope. Uninstall removes application files and startup registration but intentionally preserves this per-user data. Enterprise offboarding may remove it under the organization's normal user-profile retention policy.

PWADrop has no network client, account credentials, or cross-user data store. See [SECURITY.md](../SECURITY.md) for the injection boundary and [WINDOWS-VALIDATION.md](WINDOWS-VALIDATION.md) for release gates.

## References

- [Microsoft: Choose a Windows app distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)
- [Microsoft: Deploy MSIX apps with Intune](https://learn.microsoft.com/windows/msix/desktop/managing-your-msix-deployment-intune)
- [Microsoft: Add and assign Win32 apps to Intune](https://learn.microsoft.com/intune/app-management/deployment/add-win32)
- [Microsoft: Code-signing options for Windows apps](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options)
- [Microsoft: App Control for Business](https://learn.microsoft.com/windows/security/application-security/application-control/app-control-for-business/)
- [Inno Setup: Setup command-line parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)
