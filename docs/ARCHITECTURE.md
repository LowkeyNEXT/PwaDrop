# Architecture

PwaDrop is a tray application with three deliberately small layers:

- `PwaDrop.Core` owns filename safety, settings, and cache lifecycle without Windows dependencies.
- `PwaDrop.App` owns source detection, OLE interop, virtual-file materialization, replay, tray UI, and settings.
- `PwaDrop.DragHarness` produces Chromium-style delayed `CF_HDROP` and provides a target that accepts only `FileDrop` paths.

## Relay lifecycle

1. A low-level, same-user mouse hook records a candidate drag only when the source window belongs to `olk.exe` or its WebView2 descendants.
2. Once the Windows drag threshold is crossed and the cursor leaves Outlook, a non-activating transparent window becomes the OLE target.
3. The target accepts either asynchronous `CF_HDROP` or `FileGroupDescriptorW` plus indexed `FileContents`; ordinary synchronous file drags are ignored.
4. On drop, PwaDrop calls `StartOperation`, requests the delayed `CF_HDROP`, and lets Chromium finish its authenticated download. The returned paths are copied in 1 MiB chunks before `EndOperation` allows the source to remove them. Legacy descriptor streams follow the same private-copy path.
5. Unsafe path components, reserved DOS names, collisions, and excessive filename lengths are normalized before any file is created.
6. The transparent window disappears and PwaDrop starts a new OLE operation containing physical `FileDrop` paths. Because the physical mouse button is already released, the replay source completes after the destination's drag-enter negotiation.
7. The session directory is queued for deletion after 15 minutes. Locked files are retried and abandoned sessions older than 24 hours are purged at startup.

The relay never synthesizes mouse input and does not inject a DLL into Outlook. If automatic replay proves unreliable in the Windows validation matrix, it must not be hidden behind retries or input simulation; the fallback design is a narrowly scoped source-side `IDataObject` proxy reviewed as a separate change.

## COM ownership

- The app runs in an STA and explicitly initializes OLE.
- Every `STGMEDIUM` returned by the source is released with `ReleaseStgMedium`.
- Virtual streams and delayed physical sources are copied sequentially and never buffered as complete files.
- The test harness implements `IDataObjectAsyncCapability`, refuses early data requests, and allocates a movable `DROPFILES` block only after `StartOperation`.
- No COM pointer is persisted after the original drop callback returns.

## Security invariants

- PwaDrop runs at `asInvoker`; it cannot bridge into elevated targets.
- Source recognition is based on the top-level process tree, not page text or filenames.
- Only the current user's local application-data directory is used.
- Partial files use a `.partial` suffix and are atomically renamed after complete writes.
- Web-origin files receive `Zone.Identifier` with `ZoneId=3` when NTFS supports it.
- Logs and notifications must never contain email subjects, file names, URLs, or content.
- The project has no network client and no telemetry dependency.

## Current technical risk

The public-API overlay/replay design needs interactive Windows validation because OLE target behavior varies between applications. In particular, Edge/Chrome upload zones must receive the replayed `DragEnter` and `Drop` sequence after the original mouse release. The included harness makes this deterministic and observable before testing against New Outlook or a real ticketing system.

Primary platform references:

- [Transferring Shell objects with drag-and-drop](https://learn.microsoft.com/en-us/windows/win32/shell/dragdrop)
- [Shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)
- [IDataObjectAsyncCapability](https://learn.microsoft.com/en-us/windows/win32/api/shldisp/nn-shldisp-idataobjectasynccapability)
- [New Outlook architecture](https://learn.microsoft.com/en-us/microsoft-365-apps/outlook/overview-new-outlook-windows)
