# Backup and Recovery

All production SQLite databases live in the named Docker volume `sandy_storage`. A consistent cold backup briefly stops the application and archives the entire volume, including SQLite WAL/SHM files.

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

Copy backups off the Docker host, encrypt them at rest, and retain more than one generation. The archive contains account and device-token digests plus family history, so treat it as private even though raw device tokens are not stored.

Regularly verify an archive with `tar -tzf <archive>` and conduct a restore drill on a separate Docker volume/host.

## Restore

Restoration replaces current state. Preserve a copy of the current volume first and confirm the exact archive name before continuing.

```sh
docker compose --env-file deploy/.env -f deploy/compose.yml stop app
docker run --rm \
  --mount type=volume,src=sandy_storage,dst=/target \
  --mount type=bind,src="$PWD/deploy/backups",dst=/backup,readonly \
  alpine:3.22 sh -c 'find /target -mindepth 1 -maxdepth 1 -exec rm -rf -- {} + && tar -C /target -xzf /backup/REPLACE_WITH_BACKUP.tar.gz && chown -R 1000:1000 /target'
docker compose --env-file deploy/.env -f deploy/compose.yml start app
docker compose --env-file deploy/.env -f deploy/compose.yml ps
```

Then sign in, inspect the audit history, and verify the Windows agent reconnects and receives the authoritative state. Restoring an older backup can lower the server's `state_version`; enrolled agents should resolve this by treating the freshly authenticated startup/reconnect fetch as the new authority, but may need their local cache cleared if the restored server predates enrollment.

## Lost secrets

- Losing `SECRET_KEY_BASE` invalidates all browser sessions. Restore it from the password manager where possible; replacing it forces both parents to sign in again.
- Losing `SETUP_TOKEN` after setup is harmless because setup is disabled. It is needed only for a deliberate administrative reset.
- A lost device token is replaced by revoking the device and enrolling it again with the current join code.
- A lost shared parent password requires an authenticated recovery mechanism or an administrative Rails console reset; document the actual recovery command in the release runbook once the account model is finalized.
