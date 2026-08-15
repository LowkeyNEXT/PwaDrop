# Architecture

PWADrop is a tray application with three deliberately small layers:

- `PwaDrop.Core` owns filename safety, settings, and cache lifecycle without Windows dependencies.
- `PwaDrop.AsyncDrag` provides a managed contract test for the asynchronous-operation lifetime rule.
- `PwaDrop.App` owns allowlisted process discovery, hook deployment, tray UI, and settings.
- The native `PwaDrop.HookHost` performs a policy-checked `LoadLibraryW` injection without changing another thread's context.
- The native `PwaDrop.Hook` replaces the `DoDragDrop` import-address-table entry in `msedge.dll`, `chrome.dll`, or an allowlisted Electron executable.
- `PwaDrop.DragHarness` produces Chromium-style delayed `CF_HDROP` and provides a target that accepts only `FileDrop` paths.

## Source-hook lifecycle

1. A permission-free WinEvent hook reports top-level window creation. PWADrop coalesces each burst for 250 ms, captures the process tree once, and reconciles eligible roots. One 30-second safety reconciliation covers silent background starts; a 5-second fallback is used only if event registration fails.
2. Source policy recognizes root processes for Edge, Chrome, Brave, Chromium, Opera, Vivaldi, Comet, Slack, Missive, and Superhuman. WebView2 is narrower: its root must descend from New Outlook or New Teams.
3. Before injection, both the tray and native helper independently verify the PID creation time, x64 architecture, current session and user, medium integrity, canonical hook path, and root-process relationship. The helper also requires Windows-trusted Authenticode for production targets.
4. The short-lived native helper starts `LoadLibraryW` on a new remote thread. It never suspends a Chromium thread or calls `SetThreadContext`.
5. The helper passes a cryptographically random nonce through target-owned memory to the DLL's exported bootstrap function. Fresh nonce-qualified events acknowledge success or failure; stale or pre-existing events are rejected.
6. `DllMain` performs no hook work and creates no threads. The exported bootstrap changes only the selected module's writable `ole32!DoDragDrop` IAT slot. Both binaries opt into Control Flow Guard and CET compatibility.
7. Ordinary and non-asynchronous data objects pass straight through.
8. For an asynchronous object, the hook owns `StartOperation`, triggers source materialization, obtains and validates `CF_HDROP`, and pairs the operation with exactly one `EndOperation`.
9. The original drop source, cursor, allowed effects, and destination remain unchanged. A narrow `IDataObject` wrapper serves the retained physical file drop, delegates compatible formats, and suppresses only `chromium/x-renderer-taint`.
10. A current-user-only named event makes installed hooks pass through immediately whenever the tray setting is paused.

The abandoned relay/replay implementation remains temporarily in the tree for comparison, but the application no longer starts its mouse monitor or overlay. It cannot cover the desktop or intercept the user's clicks.

## COM ownership

- The app runs in an STA and explicitly initializes OLE.
- The hook queries `IDataObjectAsyncCapability` and wraps only successfully materialized file drags.
- The wrapper deep-copies the `CF_HDROP` storage, delegates other formats, and correctly filters the renderer-taint format from `QueryGetData`, `GetData`, and `EnumFormatEtc`.
- The hook never changes allowed effects or the result returned by the original `DoDragDrop`.
- The test harness implements `IDataObjectAsyncCapability` and refuses early data requests.
- `StartOperation` is paired with exactly one `EndOperation` on both success and failure paths.

## Security invariants

- PWADrop runs at `asInvoker`; it cannot bridge into elevated targets.
- Injection is limited to explicitly recognized, trusted-signed roots in the current user/session at non-elevated integrity and x64 architecture. WebView2 additionally requires a New Outlook or New Teams ancestor.
- The hook does not inspect page text, filenames, paths, URLs, or file contents.
- Logs and notifications must never contain email subjects, file names, URLs, or content.
- Diagnostics are limited to operation type, elapsed time, HRESULT, and drop effect.
- The project has no network client and no telemetry dependency.

## Current technical risk

Source injection may be rejected by Chromium code-integrity policy, endpoint protection, Smart App Control, or enterprise policy. The native probe verifies injection, IAT replacement, taint filtering, and exact `StartOperation`/`EndOperation` ordering without involving a real application. Production validation must still be completed for every named source family. Unknown applications and unrelated WebView2 processes must remain untouched.

Primary platform references:

- [Transferring Shell objects with drag-and-drop](https://learn.microsoft.com/en-us/windows/win32/shell/dragdrop)
- [Shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)
- [IDataObjectAsyncCapability](https://learn.microsoft.com/en-us/windows/win32/api/shldisp/nn-shldisp-idataobjectasynccapability)
- [New Outlook architecture](https://learn.microsoft.com/en-us/microsoft-365-apps/outlook/overview-new-outlook-windows)
