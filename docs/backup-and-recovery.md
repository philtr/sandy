# Backup and Recovery

All production SQLite data is in the Docker volume `sandy_storage`. A cold
backup stops the app briefly and archives the full volume, including SQLite
WAL/SHM files.

## Backup

From the repository root:

```sh
mkdir -p deploy/backups
docker compose --env-file deploy/.env -f deploy/compose.yml stop app
docker run --rm \
  --mount type=volume,src=sandy_storage,dst=/source,readonly \
  --mount type=bind,src="$PWD/deploy/backups",dst=/backup \
  alpine:3.22 tar -C /source -czf /backup/sandy-storage-$(date -u +%Y%m%dT%H%M%SZ).tar.gz .
docker compose --env-file deploy/.env -f deploy/compose.yml start app
```

Copy backups off the Docker host. Encrypt them at rest and keep more than one
generation. Archives contain account and device-token digests and family
history. Treat them as private. Raw device tokens are not stored.

Verify archives with `tar -tzf <archive>`. Run restore drills on a separate
Docker volume or host.

## Restore

Restoring replaces the current state. Save the current volume first. Confirm
the exact archive name before you continue.

```sh
docker compose --env-file deploy/.env -f deploy/compose.yml stop app
docker run --rm \
  --mount type=volume,src=sandy_storage,dst=/target \
  --mount type=bind,src="$PWD/deploy/backups",dst=/backup,readonly \
  alpine:3.22 sh -c 'find /target -mindepth 1 -maxdepth 1 -exec rm -rf -- {} + && tar -C /target -xzf /backup/REPLACE_WITH_BACKUP.tar.gz && chown -R 1000:1000 /target'
docker compose --env-file deploy/.env -f deploy/compose.yml start app
docker compose --env-file deploy/.env -f deploy/compose.yml ps
```

Then sign in, inspect the audit history, and verify that the Windows agent
reconnects and receives current state. An older backup can lower
`state_version`. Agents should accept the authenticated startup or reconnect
response as the new authority. Clear the local cache if the backup predates
the device enrollment.

## Lost secrets

- Losing `SECRET_KEY_BASE` invalidates all browser sessions. Restore it from the password manager where possible; replacing it forces both parents to sign in again.
- Losing `SETUP_TOKEN` after setup is harmless because setup is disabled. It is needed only for a deliberate administrative reset.
- A lost device token is replaced by unenrolling the device and enrolling it again with the current join code.
- A lost shared parent password requires an authenticated recovery mechanism or an administrative Rails console reset; document the actual recovery command in the release runbook once the account model is finalized.
