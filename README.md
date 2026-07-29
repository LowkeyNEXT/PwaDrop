<p align="center">
  <img src="assets/brand/pwadrop-hero.png" alt="PwaDrop carries files across an application bridge" width="100%" />
</p>

# PwaDrop

**Drag from New Outlook. Drop anywhere.**

PwaDrop is an open-source Windows utility that turns New Outlook's asynchronous virtual files into normal file drops. It is designed for the missing workflow where an email or attachment can be dragged to File Explorer, but not directly into a browser upload target, a ServiceNow ticket, or another Windows application.

> [!IMPORTANT]
> This repository contains a cross-compiling MVP and a purpose-built Windows drag harness. It has not yet been exercised against a live New Outlook session from this macOS development environment. Treat `0.1.0` as an alpha until the [Windows validation matrix](docs/WINDOWS-VALIDATION.md) passes.

## MVP behavior

- Watches only drags that originate from the installed New Outlook process tree.
- Detects standard Shell virtual files (`FileGroupDescriptorW` and `FileContents`).
- Streams selected messages and attachments into a private per-user cache.
- Replays the drop as normal physical file paths at the original destination.
- Preserves `.eml` messages and original attachment names.
- Marks web-origin files with Mark of the Web and removes temporary sessions after a grace period.
- Uses no Graph permissions, Outlook add-in, browser extension, mailbox login, or telemetry.

## How it works

```mermaid
sequenceDiagram
    participant O as New Outlook
    participant R as PwaDrop relay
    participant C as Private cache
    participant T as Browser or Windows target

    O->>R: OLE virtual-file drag
    R->>O: Request FileDescriptor/FileContents
    O-->>R: Authenticated IStream data
    R->>C: Stream to random temporary paths
    R->>T: Replay as CF_HDROP files
    T-->>R: Copy accepted or rejected
    R->>C: Delayed cleanup
```

New Outlook is a WebView2-based native application. Chromium represents download-on-drop items as virtual files, while many targets accept only real paths in `CF_HDROP`. PwaDrop bridges those documented Windows formats locally. See the [architecture notes](docs/ARCHITECTURE.md).

## Build and run

Requirements:

- Windows 11 22H2 or newer, x64
- .NET 10 SDK
- Current Visual Studio with .NET desktop tools and the Windows 11 SDK

```powershell
.\scripts\build.ps1
dotnet run --project .\src\PwaDrop.App\PwaDrop.App.csproj
```

PwaDrop starts in the notification area. Double-click its icon to open settings.

### Test without Outlook

In a second terminal:

```powershell
dotnet run --project .\tests\PwaDrop.DragHarness\PwaDrop.DragHarness.csproj
```

Drag the harness's **DRAG FROM HERE** card onto **DROP HERE**. The source intentionally provides only virtual files and the target intentionally accepts only physical paths, so a successful two-file result exercises the complete relay.

For a browser destination, open [`tests/browser-drop-target/index.html`](tests/browser-drop-target/index.html) in Edge or Chrome and drag the harness source onto its drop zone.

### Build an MSIX

```powershell
.\scripts\create-dev-certificate.ps1
.\scripts\package-msix.ps1 `
  -CertificatePath .\artifacts\PwaDrop-Development.pfx `
  -CertificatePassword pwadrop-dev
```

Development certificates and packages are ignored by git. Release packages must use a trusted code-signing certificate whose subject matches the manifest publisher.

## Privacy and compatibility

PwaDrop never calls Microsoft Graph or downloads using copied browser credentials. New Outlook remains responsible for producing the selected data through its existing authenticated drag object. Diagnostic notifications contain only HRESULT-style error codes.

The MVP supports the installed New Outlook app on Windows 11 x64. Outlook on the web, Teams, Gmail, Slack, ARM64, elevated targets, and full enterprise policy support are not yet claimed. See the [roadmap](ROADMAP.md).

## Open-source policy

PwaDrop is an independent clean-room implementation based on public Windows Shell and Chromium behavior. Do not contribute code, branding, assets, or reverse-engineered implementation details from Magic Dragin or any other commercial product.

Licensed under the [MIT License](LICENSE).
