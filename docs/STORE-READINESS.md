# Microsoft Store readiness

PWADrop uses the packaged-desktop path: an x64 MSIX containing a medium-integrity WinForms application. A WinUI rewrite is not required for Store distribution. The package keeps the drag engine at `asInvoker`, declares only `runFullTrust`, and uses the package startup-task extension instead of the unpackaged `Run` key when package identity is present.

## Before the first Partner Center submission

1. The `PWADrop` product name is reserved in Partner Center under Store ID `9NLG3ZRF0MM3`.
2. Build the Store upload package with the reserved identity already configured:

   ```powershell
   .\scripts\package-msix.ps1
   ```

   This produces `artifacts\store\<version>\PWADrop_<version>_x64.msixupload`. Override `-Version` for later submissions; the first component must be nonzero and the fourth must remain `0` for Store packages.

   The build uses the Windows SDK directly. Visual Studio and its Store-association sign-in flow are not required because the exact Partner Center identity is already configured.

3. Confirm the package assets and Store listing screenshots represent the final release UI.
4. Publish a public privacy-policy URL and add it to the Store listing. The policy should describe event-driven source discovery, same-user source injection, local diagnostics, cache cleanup, and the absence of a network client.
5. In submission notes, justify `runFullTrust`: PWADrop is a medium-integrity desktop utility that uses Win32 OLE drag/drop, a guarded same-user native source hook, and a notification-area icon. It never requests elevation and refuses elevated, cross-user, unsigned, or unknown source processes.
6. Run the Windows App Certification Kit and complete `docs/WINDOWS-VALIDATION.md` on the exact package submitted.
7. Upload the `.msixupload` to Partner Center, let the Store sign it, and test the resulting private-flight install before public rollout.

## Store-specific behavior already implemented

- `desktop:StartupTask` is declared but disabled by default.
- The user can enable startup from the PWADrop settings window; packaged builds use `StartupTask.RequestEnableAsync`, while portable builds use the current-user `Run` key.
- Package identity, publisher, version, and display publisher are build parameters instead of release-time source edits.
- The checked-in defaults use the exact Partner Center identity: `RiddleNEXT.PWADrop`, `CN=8548EF9A-500E-4856-8CEB-6C3736E43012`, and `RiddleNEXT`.
- The package targets Windows 11 22H2 or newer and does not declare network, broad file-system, elevation, or authentication capabilities.
- The app has keyboard-accessible navigation, toggles, window commands, and standard WinForms UI Automation roles.
- The signed MSIX still requires the complete source/destination validation matrix because enterprise App Control can treat packaged process injection differently from an unpackaged install.

## Microsoft references

- [Package and deploy Windows apps](https://learn.microsoft.com/windows/apps/package-and-deploy/)
- [Choose a distribution path](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)
- [App capability declarations](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations)
- [desktop:StartupTask manifest extension](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-desktop-startuptask)
- [Windows app accessibility checklist](https://learn.microsoft.com/windows/apps/design/accessibility/accessibility-checklist)
