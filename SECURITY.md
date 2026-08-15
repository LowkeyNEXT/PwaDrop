# Security policy

PWADrop handles dragged files locally, so security and privacy regressions are release blockers.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could expose file contents, weaken Mark of the Web, escape the per-user cache, or cause arbitrary process interaction. Email **security@riddlenext.com** with the affected version, reproduction steps, and impact. You should receive an acknowledgement within three business days.

## Security boundaries

- PWADrop runs at the current user's integrity level and does not request elevation.
- It does not authenticate to Microsoft 365, read browser cookies, or send telemetry.
- It injects only into an explicit x64 Chromium-browser or Electron root, or a WebView2 root descended from New Outlook/New Teams, in the current user's session and at non-elevated integrity.
- Except for the deterministic local probe, every target executable must have a Windows-trusted Authenticode signature and belong to an explicit source family. Ordinary Chromium shells, games, anti-cheat processes, overlays, and unsigned lookalikes are never candidates.
- The native helper repeats Authenticode trust, PID creation-time, user, session, integrity, architecture, root-process, ancestor, and canonical hook-path checks immediately before injection.
- Hook bootstrap uses a fresh cryptographic nonce passed through target-owned memory and acknowledged by nonce-qualified events; `DllMain` does no hook work and starts no threads.
- The hook modifies only the `DoDragDrop` import slot in `msedge.dll`, `chrome.dll`, or the allowlisted source executable; it does not suspend threads or change thread context.
- Its source hook only acts on data objects that expose asynchronous operation capability. Ordinary synchronous path drags are passed through unchanged.
- Async Chromium files remain in the original source-to-target OLE operation. PWADrop validates the materialized paths and retains only the Windows `CF_HDROP` handle needed by the destination; it does not open or copy file contents.
- PWADrop does not attempt to bridge into elevated target applications.
- Redacted diagnostics contain only operation type, timing, HRESULT, and drop effect; they never include names, subjects, paths, URLs, or content.
