# Sandy Windows agent

The agent is a per-user .NET 10 WPF application. `Sandy.Core` contains the
platform-neutral protocol, timer, persistence, and synchronization logic;
`Sandy.Agent` owns WPF, DPAPI, registry startup, the launcher desktop and AppBars,
Win32 keyboard hooks, monitor overlays, taskbar recovery, and Velopack integration.

While time is available, Sandy displays its launcher on every monitor and uses a
Sandy-owned bottom AppBar instead of Explorer's visible taskbar. A child guardian
process restores Explorer taskbars if the UI process exits or stops renewing its
lease. Explorer remains the shell and no persistent shell/auto-hide setting is
changed.

Pins are intentionally manual. With a parent-issued 30-minute edit lease and a
current online connection, **More** can enumerate Start shortcuts or AppsFolder
on demand, select or drop `.lnk`, `.url`, and `.exe` files, or accept a validated
absolute URI. Sandy performs no startup application scan, Steam parsing, catalog
watching, or application allowlisting. Up to 15 pins are stored atomically in
`%LocalAppData%\Sandy\launcher-pins.json`; icons extracted from explicitly chosen
files are kept in the adjacent `launcher-icons` directory so stale targets can
still render recognizably.

## Build and test

Use a .NET 10 SDK. The WPF host must be exercised on Windows; the core tests run
on any supported SDK platform.

### macOS with Docker

From the repository root, run the core tests with the .NET 10 SDK container:

```bash
docker run --rm \
  -v "$PWD/agent:/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test Sandy.slnx -c Release
```

Cross-compile the WPF application:

```bash
docker run --rm \
  -v "$PWD/agent:/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build src/Sandy.Agent/Sandy.Agent.csproj -c Release
```

Produce a self-contained Windows x64 build under `agent/artifacts/agent`:

```bash
docker run --rm \
  -v "$PWD/agent:/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish src/Sandy.Agent/Sandy.Agent.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -o artifacts/agent
```

These commands verify timer tests, C# and XAML compilation, and Windows publish
output. macOS cannot launch the WPF host or exercise DPAPI, registry startup,
keyboard hooks, overlays, monitor handling, or Velopack installation. Run the
Windows acceptance checklist before publishing a stable agent release.

### Windows with the .NET SDK

From the `agent` directory:

```powershell
dotnet restore .\Sandy.slnx
dotnet test .\Sandy.slnx -c Release
dotnet publish .\src\Sandy.Agent\Sandy.Agent.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\agent
```

Package a release with Velopack 1.2.0:

```powershell
vpk pack --packId Sandy.Agent --packVersion 1.2.3 --packDir .\artifacts\agent --mainExe Sandy.Agent.exe
```

Updates use `https://github.com/philtr/sandy` by default. Set the per-user or
machine `SANDY_UPDATE_URL` environment variable only to override the public
GitHub repository URL. Updates download in the background, auto-apply on the
next launch, or apply after the timer has been expired for 60 seconds and the
server confirms it is still expired. The agent checks at startup and every 15
minutes thereafter.

## Wire contract

- Enrollment: `POST /api/v1/enrollments` with `join_code`, `device_name`,
  `agent_version`, and `platform`; response includes `device_id`, `device_token`,
  and `timer_state`.
- State: `GET /api/v1/state` with `Authorization: Bearer <device token>`.
- Presence: `POST /api/v1/heartbeats` with `agent_version` and
  `overlay_active`; response is a complete timer snapshot.
- Events: `POST /api/v1/events` with an `events` array. Each event has a stable
  UUID `event_id`, `event_type`, `occurred_at`, and optional `metadata`.
- Realtime: connect to `wss://<server>/cable`, pass the device token in the
  `Authorization` header, and subscribe to `DeviceChannel`. The client accepts
  either a direct timer snapshot message or `{ "timer_state": { ... } }`.

Device credentials are DPAPI-protected for the current Windows user. Cached
snapshots are atomically replaced under `%LocalAppData%\Sandy`. A missing or
invalid cache produces an unknown state, which the WPF host treats as expired
until an authenticated server response arrives.

The keyboard hook is intentionally non-hardened. While time is available, a
Windows-key tap invokes Sandy Home and Windows-key chords are consumed; Alt-Tab
and ordinary non-Windows-key shortcuts remain available. While expired, the
existing Alt-Tab, Alt-Esc, Alt-F4, Ctrl-Esc, and Ctrl-Shift-Esc restrictions also
apply. Ctrl-Alt-Delete remains handled by Windows and is the deliberate manual
escape hatch.

Unenrolled credentials receive the explicit `device_revoked` protocol response.
Sandy clears only the matching enrollment/cache, restores Explorer taskbars, and
shows current-code re-enrollment. A generic unauthorized response and all network
failures remain fail-closed.
