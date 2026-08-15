# Original Drag Continuation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Chromium delayed-file drags to the original destination without creating a second OLE drag.

**Architecture:** A transient relay primes `IDataObjectAsyncCapability`, unregisters itself, and nudges the real mouse-held drag onto the underlying destination. PWADrop retains the original COM object through release, forces `CF_HDROP` readiness on a worker, and ends the operation exactly once.

**Tech Stack:** .NET 10, Windows Forms, Win32 OLE drag/drop, `IDataObjectAsyncCapability`, xUnit plus the executable drag harness.

## Global Constraints

- Preserve the user's original mouse-held `DoDragDrop`; production must not start a second drag.
- Never leave the relay visible or registered after it yields to the destination.
- Every owned `StartOperation` receives exactly one `EndOperation`.
- Diagnostics contain no URLs, email content, or file names.
- All executable GUI tests use an external 10-second process-tree watchdog.

---

### Task 1: Restore async priming at relay entry

**Files:**
- Modify: `src/PwaDrop.App/Drag/OleRelayDropTarget.cs`
- Modify: `src/PwaDrop.App/Drag/RelayOverlayForm.cs`
- Test: `tests/PwaDrop.DragHarness/Program.cs`

**Interfaces:**
- Consumes: `VirtualFileExtractor.DetectPayload(IDataObject)`
- Produces: `OleRelayDropTarget(..., Func<IDataObject, bool> prime, ...)`

- [ ] **Step 1: Write a failing routing regression**

Create a harness assertion where `DragEnter` receives `AsyncFileDrop`, invokes `prime` once with the same object, returns `DROPEFFECT_NONE`, and never invokes the relay `Drop` callback.

- [ ] **Step 2: Run the watchdog self-test**

Run the harness through a `System.Diagnostics.Process` watchdog. Expected: failure because the current target has no prime callback.

- [ ] **Step 3: Implement prime routing**

Add the prime callback to `OleRelayDropTarget` and `RelayOverlayForm`. For `AsyncFileDrop`, call `prime`, clear relay payload state, and return `DROPEFFECT_NONE` so the relay never consumes the user's final drop.

- [ ] **Step 4: Re-run the watchdog self-test**

Expected: routing regression passes.

### Task 2: Yield OLE to the underlying destination

**Files:**
- Create: `src/PwaDrop.App/Drag/OriginalDragHandoff.cs`
- Modify: `src/PwaDrop.App/Drag/RelayOverlayForm.cs`
- Modify: `src/PwaDrop.App/Interop/NativeMethods.cs`
- Test: `tests/PwaDrop.DragHarness/Program.cs`

**Interfaces:**
- Produces: `OriginalDragHandoff.TryWakeUnderlyingTarget(Action hideAndSuspend, Func<bool> isLeftButtonDown, Action nudgeCursor) : bool`
- Produces: `RelayOverlayForm.YieldToUnderlyingTarget() : bool`

- [ ] **Step 1: Write failing handoff regressions**

Assert that handoff hides/suspends before nudging, nudges exactly once while the left button is down, and skips nudging after release.

- [ ] **Step 2: Verify failure**

Expected: compile failure because `OriginalDragHandoff` does not exist.

- [ ] **Step 3: Implement the handoff**

Implement the ordered handoff helper. `RelayOverlayForm.YieldToUnderlyingTarget` calls `HideRelay`, revokes its OLE registration, then emits a reversible one-pixel `SendInput` mouse move only when `GetAsyncKeyState(VK_LBUTTON)` is pressed.

- [ ] **Step 4: Verify green**

Expected: ordered handoff regressions pass and the overlay release regression remains green.

### Task 3: Retain and complete the original async operation

**Files:**
- Modify: `src/PwaDrop.App/Drag/VirtualFileExtractor.cs`
- Modify: `src/PwaDrop.App/Drag/DragSourceMonitor.cs`
- Modify: `src/PwaDrop.App/PwaDropApplicationContext.cs`
- Modify: `src/PwaDrop.App/Diagnostics/DiagnosticLog.cs`
- Test: `tests/PwaDrop.DragHarness/Program.cs`

**Interfaces:**
- Consumes: `VirtualFileExtractor.PrimeAsyncFileDrop(IDataObject)`
- Produces: `PrimedDragOperation.MaterializeAndCompleteAfterReleaseAsync(TimeSpan, CancellationToken) : Task<IReadOnlyList<string>>`
- Produces: a monitor release callback that resumes relay registration after lifecycle completion.

- [ ] **Step 1: Write failing lifecycle regressions**

Using `VirtualFileDataObject`, assert `StartOperation` occurs once, `CF_HDROP` remains unavailable before source-loop completion, release completion waits for the source-loop marker, returns both generated paths, and calls `EndOperation` once.

- [ ] **Step 2: Verify failure**

Expected: compile failure because the release-completion method does not exist.

- [ ] **Step 3: Implement lifecycle completion**

Retain the original data object and capability in `PrimedDragOperation`. After release, wait for the configured unwind delay, request `CF_HDROP`, and call `EndOperation(DROPEFFECT_COPY)` in `finally`; on failure call `EndOperation(DROPEFFECT_NONE)`. Make completion idempotent.

- [ ] **Step 4: Wire the application**

On relay prime, replace stale state, call `YieldToUnderlyingTarget`, and mark the monitor drag primed. On physical release, start bounded lifecycle completion, resume relay registration, update status, and log prime start/completion/failure.

- [ ] **Step 5: Verify green**

Expected: all core tests and watchdog harness self-tests pass with no replay invocation.

### Task 4: Remove replay production flow and validate live

**Files:**
- Delete: `src/PwaDrop.App/Drag/PhysicalFileReplay.cs` if no tests or production references remain
- Modify: `README.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/WINDOWS-VALIDATION.md`
- Modify: `ROADMAP.md`

**Interfaces:**
- Removes: the post-release physical replay production path.

- [ ] **Step 1: Remove replay-only code and update docs**

Describe original-drag priming and handoff; remove claims that PWADrop replays a synthetic physical drag.

- [ ] **Step 2: Run bounded automated verification**

Run `scripts/build.ps1`, core tests, and the drag harness self-test. The GUI harness remains under the 10-second watchdog.

- [ ] **Step 3: Run live validation**

Restart PWADrop and the automation harness. Drag to the local Edge fixture, then New Outlook to the fixture, then New Outlook to Google Drive. Require the destination highlight and received filename/UI result plus successful prime lifecycle diagnostics.

- [ ] **Step 4: Apply the fallback decision**

If controlled harness-to-Edge never receives the original object after verified handoff, stop relay changes and create a separate native x64 source-hook plan. Do not reintroduce synthetic replay.
