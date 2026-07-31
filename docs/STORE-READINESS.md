# Microsoft Store readiness

PWADrop uses the recommended packaged-desktop path: an x64 MSIX containing a medium-integrity WinForms application. A WinUI rewrite is not required for Store distribution. The package keeps the drag engine at `asInvoker`, declares only `runFullTrust`, and uses the package startup-task extension instead of the unpackaged `Run` key when package identity is present.

## Before the first Partner Center submission

1. Reserve the product name in Partner Center.
2. Copy the reserved package identity name, publisher subject, and publisher display name into the package command:

   ```powershell
   .\scripts\package-msix.ps1 `
     -Version 0.2.0.0 `
     -IdentityName "<Partner Center package identity name>" `
     -Publisher "<Partner Center publisher subject>" `
     -PublisherDisplayName "<Partner Center publisher display name>"
   ```

3. Replace the development package assets with final Store listing assets generated from the source-of-truth brand mark.
4. Publish a public privacy-policy URL and add it to the Store listing. The policy should describe the low-level mouse hook, local diagnostics, legacy compatibility cache, cleanup period, and absence of a network client.
5. In submission notes, justify `runFullTrust`: PWADrop is a medium-integrity desktop utility that uses Win32 OLE drag/drop, a same-user low-level mouse hook, and a notification-area icon. It never requests elevation or injects into another process.
6. Run the Windows App Certification Kit and complete `docs/WINDOWS-VALIDATION.md` on the exact package submitted.
7. Upload the MSIX to Partner Center, let the Store re-sign it, and test the resulting private-flight install before public rollout.

## Store-specific behavior already implemented

- `desktop:StartupTask` is declared but disabled by default.
- The user can enable startup from the PWADrop settings window; packaged builds use `StartupTask.RequestEnableAsync`, while portable builds use the current-user `Run` key.
- Package identity, publisher, version, and display publisher are build parameters instead of release-time source edits.
- The package targets Windows 11 22H2 or newer and does not declare network, broad file-system, elevation, or authentication capabilities.
- The app has keyboard-accessible navigation, toggles, window commands, and standard WinForms UI Automation roles.

## Microsoft references

- [Package and deploy Windows apps](https://learn.microsoft.com/windows/apps/package-and-deploy/)
- [Choose a distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)
- [App capability declarations](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations)
- [desktop:StartupTask manifest extension](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-desktop-startuptask)
- [Windows app accessibility checklist](https://learn.microsoft.com/windows/apps/design/accessibility/accessibility-checklist)
