# Architecture

PWADrop is a tray application with three deliberately small layers:

- `PwaDrop.Core` owns filename safety, settings, and cache lifecycle without Windows dependencies.
- `PwaDrop.App` owns source detection, OLE interop, original-drag priming, legacy virtual-file materialization, tray UI, and settings.
- `PwaDrop.DragHarness` produces Chromium-style delayed `CF_HDROP` and provides a target that accepts only `FileDrop` paths.

## Relay lifecycle

1. A low-level, same-user mouse hook records a candidate drag only when the source window belongs to a recognized Chromium, installed-PWA, WebView2, or test-harness process.
2. Once the Windows drag threshold is crossed and the cursor leaves the source app, a non-activating transparent window becomes the OLE target.
3. If the data object advertises asynchronous `CF_HDROP`, PWADrop calls `StartOperation` during `DragEnter`, marks that gesture as primed, and immediately hides its window.
4. The overlay remains suppressed for the rest of the gesture, allowing Windows OLE to deliver the same original `IDataObject` to the underlying browser or application.
5. When the user releases the mouse, the destination's ordinary `GetData(CF_HDROP)` call can trigger Chromium's authenticated download because the async operation is already active.
6. PWADrop retains the async capability briefly and calls `EndOperation` after the destination has had time to retrieve the data.
7. Legacy `FileGroupDescriptorW`/`FileContents` sources still use sanitized cache materialization and physical replay as a compatibility fallback.

The primer never synthesizes mouse input, starts a replacement drag, or injects a DLL into a source process. If OLE does not preserve the primed state while retargeting on supported Windows builds, any future source-side `DoDragDrop` hook requires a separate security review; input simulation is not acceptable.

## COM ownership

- The app runs in an STA and explicitly initializes OLE.
- Every `STGMEDIUM` returned by the source is released with `ReleaseStgMedium`.
- Async Chromium data remains owned by the original source and destination; PWADrop does not request or copy it.
- Legacy virtual streams are copied sequentially and never buffered as complete files.
- The test harness implements `IDataObjectAsyncCapability`, refuses early data requests, and allocates a movable `DROPFILES` block only after `StartOperation`.
- The primed async-capability pointer is retained for at most two minutes and completed idempotently after release or shutdown.

## Security invariants

- PWADrop runs at `asInvoker`; it cannot bridge into elevated targets.
- Source recognition is based on the top-level process tree, not page text or filenames.
- Only legacy fallback materialization writes to the current user's local application-data directory.
- Legacy partial files use a `.partial` suffix and are atomically renamed after complete writes.
- Legacy cached web-origin files receive `Zone.Identifier` with `ZoneId=3` when NTFS supports it.
- Logs and notifications must never contain email subjects, file names, URLs, or content.
- Diagnostics are limited to payload kind, file count, elapsed time, HRESULT, and drop effect.
- The project has no network client and no telemetry dependency.

## Current technical risk

The public-API priming design needs interactive Windows validation because PWADrop briefly participates as an OLE target and then retargets the original drag. The included harness deterministically verifies the `StartOperation` state transition and target-side `CF_HDROP` rendering before testing against production source apps or a real ticketing system.

Primary platform references:

- [Transferring Shell objects with drag-and-drop](https://learn.microsoft.com/en-us/windows/win32/shell/dragdrop)
- [Shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)
- [IDataObjectAsyncCapability](https://learn.microsoft.com/en-us/windows/win32/api/shldisp/nn-shldisp-idataobjectasynccapability)
- [New Outlook architecture](https://learn.microsoft.com/en-us/microsoft-365-apps/outlook/overview-new-outlook-windows)
