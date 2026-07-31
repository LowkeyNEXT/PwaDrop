# Security policy

PWADrop handles dragged files locally, so security and privacy regressions are release blockers.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could expose file contents, weaken Mark of the Web, escape the per-user cache, or cause arbitrary process interaction. Email **security@riddlenext.com** with the affected version, reproduction steps, and impact. You should receive an acknowledgement within three business days.

## Security boundaries

- PWADrop runs at the current user's integrity level and does not request elevation.
- It does not authenticate to Microsoft 365, read browser cookies, or send telemetry.
- It accepts only asynchronous `CF_HDROP` or Windows Shell virtual-file data objects from recognized Chromium, PWA, WebView2, or test-harness process trees. Ordinary synchronous path drags are passed through.
- Async Chromium files remain in the original source-to-target OLE operation; PWADrop does not read or copy their paths or contents.
- Legacy fallback temporary paths stay under the current user's local application data directory and receive `Zone.Identifier` when NTFS supports it.
- PWADrop does not attempt to bridge into elevated target applications.
- Redacted diagnostics contain only operation type, timing, HRESULT, and drop effect; they never include names, subjects, paths, URLs, or content.
