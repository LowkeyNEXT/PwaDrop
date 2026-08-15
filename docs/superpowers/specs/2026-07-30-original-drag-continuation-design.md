# Original Drag Continuation Design

## Goal

Make Chromium `DownloadURL` drags from New Outlook and browser-based apps work with existing Windows and browser drop targets without requiring those targets to implement `IDataObjectAsyncCapability`.

Success means the destination receives the original mouse-held OLE drag as ordinary files. PWADrop must not synthesize a second drag after the user releases the mouse, must not leave an input-blocking overlay visible, and must clean up every async operation.

## Evidence

- PWADrop can call `StartOperation`, return from `IDropTarget::Drop`, retrieve `CF_HDROP`, and materialize both harness files.
- A later `DoDragDrop` with physical file paths is accepted by a permissive WinForms target but declined by Edge with `DROPEFFECT_NONE`.
- Chromium solutions that work generically preserve or wrap the source process's original `DoDragDrop`.
- The previous priming implementation started the original async operation but hid the relay without forcing OLE to discover the underlying destination. The destination therefore did not consistently receive `DragEnter`.

## Approaches

### 1. Continue the original drag through a transient relay (selected)

PWADrop briefly presents an OLE relay target, calls `StartOperation` on the original data object, removes the relay from hit testing, and nudges the pointer while the real left button is still held. That movement makes OLE enter the underlying destination with the original data object and authentic key state.

This is the smallest approach, needs no process injection, and reuses the existing monitor, relay, and async-capability code.

### 2. Hook `DoDragDrop` inside Chromium source processes (fallback)

Inject a native x64 component into supported source processes and wrap the source data object/drop source. This most closely matches proven commercial implementations, but adds native detouring, process injection, code-signing, security-review, and endpoint-protection concerns.

Use this only if the transient relay cannot pass the original drag reliably in real New Outlook and Edge.

### 3. Materialize and replay after release (rejected)

The existing experimental implementation captures the delayed object, creates files, and starts a new OLE drag. Browser targets reject that second drag because it is no longer the user's original mouse-held operation. More replay timing changes will not address that architectural mismatch.

## Components and data flow

1. `DragSourceMonitor` detects a supported source drag crossing into another top-level window.
2. `RelayOverlayForm` appears only long enough for OLE to call the relay's `DragEnter`.
3. `OleRelayDropTarget` detects `AsyncFileDrop` and asks the application to prime it.
4. `VirtualFileExtractor.PrimeAsyncFileDrop` calls `StartOperation` once and retains the original `IDataObject` and async-capability interfaces.
5. The relay becomes invisible and temporarily unregisters as an OLE target.
6. A one-pixel reversible pointer nudge occurs while `MK_LBUTTON` is still down, causing OLE to enter the actual underlying destination with the original object.
7. On physical mouse release, PWADrop waits for the source drag loop to unwind, requests `CF_HDROP` on a background thread to keep the download alive until the paths exist, then calls `EndOperation`.
8. A bounded timeout ends abandoned operations with `DROPEFFECT_NONE`. Shutdown completes all retained operations.

The physical-file replay path is removed from production once the original-drag path passes live validation.

## Error handling and safety

- Priming is idempotent for a drag already in operation.
- Every owned `StartOperation` has exactly one `EndOperation`.
- The relay hides synchronously and has a short release failsafe so it cannot block clicks.
- Pointer nudging is skipped if the left button is no longer held.
- Operation completion has a bounded timeout and emits redacted diagnostics only.
- No email contents, URLs, or file names are written to diagnostics.

## Testing

- Unit/integration regression: an async harness data object is primed exactly once and remains in operation until completion.
- OLE handoff regression: after relay priming, an underlying target receives `DragEnter` on the same original data object while `MK_LBUTTON` is set.
- Overlay regression: the relay is no longer visible or registered when the underlying target is entered and cannot block later clicks.
- Lifecycle regression: release triggers data readiness and exactly one successful `EndOperation`; timeout/shutdown complete with failure.
- Live watchdog tests: harness-to-Edge, then New Outlook-to-local Edge fixture, then New Outlook-to-Google Drive. Every GUI test runs under an external process-tree timeout.

## Fallback decision

If the original object cannot be handed from the transient relay to Edge in the controlled harness despite an actual mouse-held drag and verified pointer movement, stop modifying the relay design and implement the native source-process hook as a separate signed component.
