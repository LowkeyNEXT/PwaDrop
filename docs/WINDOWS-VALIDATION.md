# Windows validation matrix

Record results in a pull request before marking an installer as beta. Never test first against a production ticket containing sensitive data.

## Environment

- Windows 11 24H2 x64, fully patched
- Current stable New Outlook and WebView2 Runtime
- Current stable Edge and Chrome
- At least 5 GB free in `%LOCALAPPDATA%`
- PwaDrop built in `Release` configuration at medium integrity

## Gate 1: deterministic harness

1. Start PwaDrop and confirm the tray status is **Bridge active**.
2. Start `PwaDrop.DragHarness`.
3. Drag the delayed `CF_HDROP` source card onto the physical target card.
4. Confirm exactly `test-conversation.eml` and `invoice.pdf` appear with non-zero sizes.
5. Confirm the harness remains responsive and the drop completes as one continuous mouse gesture.
6. Repeat 20 times, including after moving the windows across monitors with different scaling.
7. Cancel five drags with Escape and confirm no target drop and no stale overlay.
8. Pause PwaDrop and confirm the harness target rejects the virtual source.

Pass criterion: 20/20 original-drag handoffs, 5/5 cancellations, no extra click, no cursor jump, no stuck transparent window, and no PwaDrop cache session.

## Gate 2: New Outlook

Test each row with one email, multiple selected emails, one attachment, and multiple attachments:

| Destination | Expected result |
| --- | --- |
| File Explorer | Existing Outlook behavior remains intact |
| Edge harness drop page | Normal browser `File` objects |
| Chrome harness drop page | Normal browser `File` objects |
| ServiceNow non-production ticket | Files attach once with correct names and sizes |
| New Outlook folder/compose surface | Internal drag behavior remains intact |

Also test duplicate names, Unicode, shared mailbox items, an attachment over 10 MB, offline mode, insufficient disk space, and a destination running as administrator.

## Gate 3: privacy and cleanup

- Confirm async Outlook and harness drags do not create a session under `%LOCALAPPDATA%\PwaDrop\Cache`.
- Exercise the legacy descriptor fallback separately and confirm cached files receive `Zone.Identifier` on NTFS.
- Confirm legacy `.partial` files are removed after a failed transfer and completed sessions expire.
- Search diagnostic output for the test subjects, filenames, URLs, and content; none may appear.
- Confirm each async handoff records `prime_started` followed by `prime_completed` without extraction or replay events.

## Browser fixture

Open `tests/browser-drop-target/index.html` in Edge or Chrome. Its drop handler logs `event.dataTransfer.files` names, sizes, and types and contains no PwaDrop-specific integration.
