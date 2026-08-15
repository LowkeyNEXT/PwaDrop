# Roadmap

## 0.1 — MVP

- Chromium, installed-PWA, and WebView2 source detection
- Guarded browser, New Outlook/New Teams WebView2, and named Electron root injection with CFG/CET-compatible `DoDragDrop` IAT interception
- Event-driven source discovery with a low-frequency safety reconciliation
- Asynchronous `CF_HDROP` activation using `IDataObjectAsyncCapability`
- Tray/settings experience and branded MSIX setup
- Deterministic source/target harness

## 0.2 — Compatibility beta

- Complete Windows/Edge/Chrome/WebView2/Electron/ServiceNow validation matrix
- Progress UI for slow files and explicit cancellation
- Redacted diagnostic bundle
- Signed AppInstaller update feed
- Shared mailbox and multi-select regression coverage

## Later

- Additional source-specific compatibility adapters based on verified executable identities
- ARM64 builds
- Additional WebView2/Electron clients beyond the current Teams, Slack, Missive, and Superhuman families
- Enterprise policy surface for startup, cache lifetime, and diagnostics
- Optional browser extension for richer browser-only feedback, never as a requirement

`.msg` conversion and Microsoft Graph conversation expansion are intentionally out of scope unless a concrete destination rejects standards-based `.eml` files.
