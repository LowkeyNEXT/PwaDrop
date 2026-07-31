# Contributing

PWADrop welcomes focused bug reports and pull requests. The project is clean-room: do not submit code, assets, strings, or reverse-engineered details taken from commercial drag-and-drop utilities.

## Development setup

Install Visual Studio 2026 or the current Visual Studio release with the .NET desktop and Windows 11 SDK components, plus the .NET 10 SDK.

```powershell
git clone <your-fork>
cd PwaDrop
.\scripts\build.ps1
```

To exercise the bridge without an external account, start PWADrop and `PwaDrop.DragHarness`, then drag the virtual source card to the physical target card.

## Pull requests

- Add tests for platform-neutral behavior.
- Run the harness for OLE changes and record the Windows build and browser version.
- Preserve internal source-app drag behavior.
- Never log filenames, email metadata, URLs, or file contents.
- Keep commits scoped and use imperative commit subjects.
