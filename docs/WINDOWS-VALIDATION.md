# Windows validation matrix

Record results in a pull request before marking an installer as beta. Never test first against a production ticket containing sensitive data.

## Environment

- Windows 11 24H2 x64, fully patched
- Current stable New Outlook, New Teams, and WebView2 Runtime
- Current stable Edge, Chrome, Brave, Opera, Vivaldi, Slack, and any other source claimed for the release
- At least 5 GB free in `%LOCALAPPDATA%`
- PWADrop built in `Release` configuration at medium integrity

## Gate 1: deterministic native hook probe

1. Run `scripts\build-native-hook.ps1 -Configuration Release`.
2. Run `scripts\test-native-hook.ps1`.
3. Confirm the native host and probe both exit zero within the seven-second watchdog.
4. Read `native\build-msvc-current.txt`, inspect the DLL under the matching `native\build-msvc-generations` directory with `dumpbin /headers`, and confirm x64, Control Flow Guard, and CET compatibility.
5. Run the probe repeatedly and confirm there is no application crash event.

Pass criterion: injection succeeds without `SetThreadContext`, the IAT hook is installed, and `StartOperation`/`EndOperation` are each observed exactly once.

## Gate 2: installation lifecycle

1. Run `scripts\package-installer.ps1` and confirm the output is one setup executable plus its checksum file.
2. Run `scripts\test-installer.ps1 -SkipLaunch` on unsigned CI artifacts to verify current-user install, startup selection, upgrade, and uninstall without administrator access. This is packaging validation, not a release-signing substitute.
3. On a clean VM, interactively test the default current-user location and the all-users location. Confirm Installed Apps, Start menu, optional desktop shortcut, startup selection, repair/upgrade, and uninstall behavior.
4. Confirm the runtime install contains no `.pdb` files and that its `Hook\current.txt` points to a complete generation directory.
5. For a signed candidate, run `scripts\test-installer.ps1` without `-SkipLaunch`, then verify Authenticode on `PwaDrop.exe`, `PwaDrop.HookHost.exe`, `PwaDrop.Hook.dll`, setup, and the installed uninstaller.
6. With two non-administrator test accounts, install for all users, sign into each account, and confirm exactly one non-elevated tray process and independent per-user settings/data.

Pass criterion: both scopes install and update cleanly, startup matches the selected or managed policy, no elevated runtime remains, and uninstall removes program files and registration without deleting per-user data.

## Gate 3: desktop .NET targets

1. Run `PwaDrop.DragHarness` without PWADrop and confirm its baseline behavior.
2. Run `PwaDrop.WpfDropTarget` and confirm ordinary physical drops still work.
3. Confirm PWADrop does not inject into either desktop test target.

Pass criterion: both targets receive ordinary physical file paths and remain responsive; neither target needs a PWADrop SDK, extension, or custom data format.

## Gate 4: production source apps

Test each row with one email, multiple selected emails, one attachment, and multiple attachments:

| Destination | Expected result |
| --- | --- |
| File Explorer | Existing Outlook behavior remains intact |
| Edge harness drop page | Normal browser `File` objects |
| Chrome harness drop page | Normal browser `File` objects |
| ServiceNow non-production ticket | Files attach once with correct names and sizes |
| New Outlook folder/compose surface | Internal drag behavior remains intact |

Also test duplicate names, Unicode, shared mailbox items, an attachment over 10 MB, offline mode, insufficient disk space, and a destination running as administrator.

Repeat the browser rows for Edge, Chrome, Brave, Opera, Vivaldi, and Comet when installed. Repeat the app rows for New Teams, Slack, Missive, and Superhuman when claimed. Test both a single item and multi-select, `%`/`#` characters, long paths, cross-origin web frames, and Office documents that normally open a preview.

## Gate 5: privacy and process safety

- Confirm the hook is loaded only into allowlisted, same-user, same-session, non-elevated source processes.
- Pause PWADrop and confirm already-installed hooks immediately pass ordinary and asynchronous drags through.
- Exit PWADrop and confirm already-installed hooks remain passive.
- Search diagnostic output for the test subjects, filenames, URLs, and content; none may appear.
- Confirm each successful injection records `hook_injection_completed` without a process name, path, or payload detail.
- Confirm renderer children, unrelated WebView2 hosts, games, anti-cheat processes, unsigned lookalike executables, elevated processes, and x86 processes are rejected.
- Leave PWADrop idle across at least two 30-second safety reconciliations and record CPU usage. A release must not reintroduce the former one-second full-process polling loop.

## Browser fixture

Open `tests/browser-drop-target/index.html` in Edge or Chrome. Its drop handler logs `event.dataTransfer.files` names, sizes, and types and contains no PWADrop-specific integration.
