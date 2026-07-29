# Security policy

PwaDrop handles email messages and attachments locally, so security and privacy regressions are release blockers.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could expose file contents, weaken Mark of the Web, escape the per-user cache, or cause arbitrary process interaction. Email **security@riddlenext.com** with the affected version, reproduction steps, and impact. You should receive an acknowledgement within three business days.

## Security boundaries

- PwaDrop runs at the current user's integrity level and does not request elevation.
- It does not authenticate to Microsoft 365, read browser cookies, or send telemetry.
- It accepts only asynchronous `CF_HDROP` or Windows Shell virtual-file data objects from the recognized New Outlook process tree. Ordinary synchronous path drags are passed through.
- Temporary paths are generated under the current user's local application data directory.
- Web-origin files receive a `Zone.Identifier` marker when the filesystem supports alternate data streams.
- PwaDrop does not attempt to bridge into elevated target applications.
