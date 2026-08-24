# Homelab Deployment

## Prerequisites

- Docker Engine with Compose v2.
- A DNS name and HTTPS reverse proxy capable of forwarding WebSocket upgrade headers.
- Permission to pull the server image from GHCR. Make the package public or authenticate Docker on the host.
- A password manager for the Rails master key and setup token.

## First installation

1. Copy `deploy/.env.example` to `deploy/.env` and set `SANDY_IMAGE` to `ghcr.io/<owner>/<repository>-server:latest`.
2. Generate independent secrets with `openssl rand -hex 64` for `SECRET_KEY_BASE` and `openssl rand -hex 32` for `SETUP_TOKEN`.
3. Set `APP_HOST` to the public hostname and `APP_ORIGIN` to its externally reachable HTTPS origin, without a trailing path.
4. Start the container from the repository root:

   ```sh
   docker compose --env-file deploy/.env -f deploy/compose.yml pull
   docker compose --env-file deploy/.env -f deploy/compose.yml up -d
   docker compose --env-file deploy/.env -f deploy/compose.yml ps
   ```

5. Configure the reverse proxy to send the public origin to `127.0.0.1:${SANDY_PORT}`. Preserve `Host`, `X-Forwarded-Proto`, and WebSocket upgrade headers.
6. Confirm `https://<host>/up` returns success, then open the setup page and supply `SETUP_TOKEN`. Create the family, shared parent account, two parent profiles, and initial join code. Setup is disabled after this succeeds.
7. Install the Windows release, enter the public server URL and family join code, and verify the device appears online.

Keep `WEB_CONCURRENCY=1` for the initial deployment. SQLite and Solid Cable can coordinate multiple processes, but one family does not benefit enough to justify the extra concurrency and operational testing.

## Upgrade

Server images are immutable. Back up first, then pull and recreate the app:

```sh
docker compose --env-file deploy/.env -f deploy/compose.yml pull app
docker compose --env-file deploy/.env -f deploy/compose.yml up -d app
docker compose --env-file deploy/.env -f deploy/compose.yml ps
```

The Rails image entrypoint prepares databases before boot. Review release notes for exceptional migration instructions. Do not run two app versions against the same SQLite volume during an upgrade.

Server images are released from `server-vX.Y.Z` Git tags or a manual workflow run with an `X.Y.Z` version. Each release publishes the exact `X.Y.Z` image tag, the moving `latest` tag, and the moving `vX-latest` tag for consumers that want updates within one major version. Default-branch builds publish only an immutable `sha-*` tag.

Windows releases are published from `agent-v*` Git tags. Set the installed agent's `SANDY_UPDATE_URL` to the public repository URL, such as `https://github.com/OWNER/REPOSITORY`. The agent checks that source at startup and every six hours, downloads quietly, and applies at a safe boundary. Stable artifacts should be Authenticode-signed; unsigned development builds can trigger SmartScreen.

For the launcher/revocation migration, publish and allow uptake of the recovery-capable Windows agent before operators begin relying on revoked-token tombstone responses. Old rows whose token digest was already cleared cannot be identified retroactively; those agents use the generic unauthorized, current-join-code recovery flow. Existing installations retain credentials and cached timer state, begin with an empty manual pin grid, and keep app editing locked until a parent grants a lease.

## Operations

- `GET /up` is a liveness/boot check. Also alert on the parent dashboard being unreachable and inspect device heartbeat age for end-to-end health.
- View logs with `docker compose --env-file deploy/.env -f deploy/compose.yml logs --tail=200 app`.
- Use **Add PC** to generate a fresh family join code after unexpected disclosure. Existing devices remain enrolled.
- Revoke a lost or replaced PC's device credential from the parent UI.
- Use **Settings → Revoked PC behavior** to choose whether revoked PCs remain denied or receive a release state. The release state uses the schema-1 active timer shape supported by agent 1.1.0.
- Use the header settings gear to archive revoked PCs that should no longer appear on the dashboard.
- After upgrading a server that already has time grants, backfill the unified activity feed once. The task is idempotent and safe to rerun:

  ```sh
  docker compose --env-file deploy/.env -f deploy/compose.yml exec app bin/rails sandy:backfill_time_grant_events
  ```

- Back up `sandy_storage` regularly according to [`backup-and-recovery.md`](backup-and-recovery.md).

The container port is bound to loopback by default. If the reverse proxy runs in Docker, attach both services to a private Docker network and proxy to `app:80` instead of publishing the port broadly.
