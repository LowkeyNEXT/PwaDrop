# Roadmap

## 0.1 — MVP

- New Outlook process detection
- Original-drag priming for Chromium asynchronous `CF_HDROP`
- Safe temporary cache and replay fallback for legacy virtual-file descriptors
- Tray/settings experience and branded MSIX setup
- Deterministic source/target harness

## 0.2 — Compatibility beta

- Complete Windows/New Outlook/Edge/Chrome/ServiceNow validation
- Progress UI for slow files and explicit cancellation
- Redacted diagnostic bundle
- Signed AppInstaller update feed
- Shared mailbox and multi-select regression coverage

## Later

- Outlook on the web and installed browser PWAs
- ARM64 builds
- Teams, OneDrive, SharePoint, Gmail, and Slack source adapters
- Enterprise policy surface for startup, cache lifetime, and diagnostics
- Optional browser extension for richer browser-only feedback, never as a requirement

`.msg` conversion and Microsoft Graph conversation expansion are intentionally out of scope unless a concrete destination rejects standards-based `.eml` files.
