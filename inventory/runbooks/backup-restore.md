# Backup and Restore

## What is backed up

| Data | Backup | Location | Schedule |
| --- | --- | --- | --- |
| HomeBox database | `pg_dump` of `homebox` | `postgresql-backup-pvc` | Daily 02:00 |
| Home Assistant database | `pg_dump` of `homeassistant` | `postgresql-backup-pvc` | Daily 02:00 |
| HomeBox attachments | NAS snapshot / PVC copy | `homebox-data` on NFS | NAS policy |
| Grocy SQLite database | `VACUUM INTO` + gzip | `grocy-backup` on NFS | Daily 02:30 |
| Grocy config/uploads | `tar.gz` of `/config` without live DB | `grocy-backup` on NFS | Daily 02:30 |

Retention is 30 days for the database dumps and Grocy backups.

The PostgreSQL backup PVC is a node-pinned hostPath volume (currently on `pi-lab`). The backup Job intentionally has no `nodeSelector`; the PV node affinity places it on the correct node. Do not pin it to `ubuntu-desktop` or the Job will stay Pending.

After the fix is applied, delete any old Pending backup Job so the CronJob can schedule a new one:

```bash
kubectl get jobs -n data-storage
kubectl delete job -n data-storage <pending-job-name>
```

## Verify backups

```bash
kubectl get jobs -n data-storage
kubectl get jobs -n inventory
kubectl logs -n data-storage -l app=postgresql-backup --tail=50
kubectl logs -n inventory -l app=grocy-backup --tail=50
```

List files with a temporary pod:

```bash
kubectl run backup-inspect -n data-storage --rm -it --restart=Never \
  --image=busybox:1.36 \
  --overrides='{"spec":{"containers":[{"name":"backup-inspect","image":"busybox:1.36","command":["sh","-c","ls -lh /backups/postgresql"],"stdin":true,"tty":true,"volumeMounts":[{"name":"backups","mountPath":"/backups"}]}],"volumes":[{"name":"backups","persistentVolumeClaim":{"claimName":"postgresql-backup-pvc"}}]}}'
```

Use the same pattern with `grocy-backup` in the `inventory` namespace for Grocy.

## Restore HomeBox database

1. Scale down HomeBox:

   ```bash
   kubectl scale deployment -n inventory homebox --replicas=0
   ```

2. Restore the latest dump into the `homebox` database. Run a temporary PostgreSQL client pod:

   ```bash
   kubectl run pg-restore -n data-storage --rm -it --restart=Never \
     --image=docker.io/bitnami/postgresql@sha256:2faab3906ce920e5237c38b3a85db4c8dbfb3571f42eb4f935737872d803c5d0 \
     --overrides='{"spec":{"containers":[{"name":"pg-restore","image":"docker.io/bitnami/postgresql@sha256:2faab3906ce920e5237c38b3a85db4c8dbfb3571f42eb4f935737872d803c5d0","command":["bash"],"stdin":true,"tty":true,"env":[{"name":"PGPASSWORD","valueFrom":{"secretKeyRef":{"name":"postgresql-credentials","key":"postgres-password"}}}],"volumeMounts":[{"name":"backups","mountPath":"/backups"}]}],"volumes":[{"name":"backups","persistentVolumeClaim":{"claimName":"postgresql-backup-pvc"}}]}}'
   ```

3. In the pod:

   ```bash
   gunzip -c /backups/postgresql/homebox-YYYYMMDD-HHMMSS.sql.gz | \
     psql -h postgresql.data-storage.svc.cluster.local -U postgres -d homebox
   ```

4. Scale HomeBox back up:

   ```bash
   kubectl scale deployment -n inventory homebox --replicas=1
   ```

5. Verify login, items, and attachments.

`pg_dump` output normally includes `CREATE`/`COPY` statements. If the dump was made with `--clean`, it drops objects first. If the restore reports existing objects, restore into a fresh database and rename it instead.

## Restore HomeBox attachments

The attachments PVC is on NFS. Find its path from the PV:

```bash
kubectl get pv
kubectl describe pv <homebox-pv>
```

Restore from the NAS snapshot or copy the directory back to the same NFS path. Scale HomeBox down during the copy, then back up.

## Restore Grocy

1. Scale down Grocy:

   ```bash
   kubectl scale deployment -n inventory grocy --replicas=0
   ```

2. Mount both PVCs in a temporary pod:

   ```bash
   kubectl run grocy-restore -n inventory --rm -it --restart=Never \
     --image=lscr.io/linuxserver/grocy:v4.7.1-ls347 \
     --overrides='{"spec":{"containers":[{"name":"grocy-restore","image":"lscr.io/linuxserver/grocy:v4.7.1-ls347","command":["sh"],"stdin":true,"tty":true,"volumeMounts":[{"name":"config","mountPath":"/config"},{"name":"backups","mountPath":"/backups"}]}],"volumes":[{"name":"config","persistentVolumeClaim":{"claimName":"grocy-config"}},{"name":"backups","persistentVolumeClaim":{"claimName":"grocy-backup"}}]}}'
   ```

3. In the pod, choose the backup:

   ```bash
   ls -lh /backups
   ```

4. Restore the database:

   ```bash
   rm -f /config/data/grocy.db /config/data/grocy.db-wal /config/data/grocy.db-shm
   gunzip -c /backups/grocy-YYYYMMDD-HHMMSS.db.gz > /config/data/grocy.db
   chown 1000:1000 /config/data/grocy.db
   ```

5. Restore config/uploads if needed:

   ```bash
   tar -xzf /backups/grocy-config-YYYYMMDD-HHMMSS.tar.gz -C /config
   chown -R 1000:1000 /config
   ```

6. Scale Grocy back up:

   ```bash
   kubectl scale deployment -n inventory grocy --replicas=1
   ```

7. Verify login, products, and stock.

## Restore test cadence

Run a restore test into a temporary database or temporary directory every quarter. Do not restore over live data without a fresh backup.
