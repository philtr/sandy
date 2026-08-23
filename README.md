# Sandy

Sandy is a deliberately small remote time-limit system for a family Windows gaming PC. A Rails control plane owns the authoritative deadline, while a per-user Windows agent keeps counting through short network outages and presents warnings and an expired-session overlay.

This is a visibility and consistency tool, not hardened parental-control software. A technically determined local user, elevated applications, and the Windows secure desktop are outside its threat model. `Ctrl`+`Alt`+`Delete` intentionally remains an escape hatch.

## Repository

- [`server/`](server/) — Rails 8.1 control plane, parent PWA, JSON API, and Action Cable endpoint.
- [`agent/`](agent/) — .NET 10 WPF agent and platform-independent timer/synchronization core.
- [`deploy/`](deploy/) — homelab Docker Compose deployment.
- [`docs/architecture.md`](docs/architecture.md) — boundaries, domain model, and design decisions.
- [`docs/protocol.md`](docs/protocol.md) — device HTTP/WebSocket synchronization contract.
- [`docs/deployment.md`](docs/deployment.md) — initial installation and upgrades.
- [`docs/backup-and-recovery.md`](docs/backup-and-recovery.md) — SQLite-volume backup and recovery.
- [`docs/windows-acceptance.md`](docs/windows-acceptance.md) — real-Windows release checklist.

## Development

The Rails app requires the Ruby version recorded in `server/.ruby-version` and SQLite:

```sh
cd server
bin/setup
bin/rails test
```

The Windows timer core can be developed and tested on macOS. Building or exercising WPF requires Windows:

```sh
dotnet restore agent/Sandy.slnx
dotnet test agent/Sandy.slnx -c Release
```

See [`docs/deployment.md`](docs/deployment.md) for production configuration. Never commit `server/config/master.key`, `deploy/.env`, a join code, or a device token.
