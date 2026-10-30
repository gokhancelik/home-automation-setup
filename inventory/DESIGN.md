# Inventory Design

This document records how the handover was adapted to the existing Flux/MicroK8s platform. The original requirements are in `HANDOVER.md`.

## Architecture

```text
Phone NFC tap
    │
    ├── bin / durable item ──► HomeBox (https://homebox.gcelik.dev)
    │                              │
    │                              ├── PostgreSQL `homebox` (data-storage)
    │                              └── attachments PVC (nfs-storage)
    │
    └── pantry product ───────► Grocy (https://grocy.gcelik.dev)
                                   ├── SQLite /config/data/grocy.db (local SSD)
                                   └── daily backups (nfs-storage)

Home Assistant (Docker on pi-lab)
    ├── HomeBox integration ──► maintenance due, totals
    └── Grocy integration ────► low stock, expiring products
```

## Decisions

| Decision | Choice | Reason |
| --- | --- | --- |
| Deployment model | Flux + MicroK8s, not Docker Compose | Matches the existing platform and backup/secret conventions |
| Project location | This repo (`inventory/` + `clusters/microk8s/apps/inventory/`) | Keeps infrastructure and inventory operations together |
| Exposure | nginx ingress + cert-manager, `homebox.gcelik.dev` / `grocy.gcelik.dev` | Existing public HTTPS pattern; no new tunnel needed |
| HomeBox database | Existing PostgreSQL in `data-storage` | No second database server; uses existing backups |
| Grocy database | SQLite on local SSD, backed up daily to NFS | Grocy does not support PostgreSQL |
| Grocy storage class | `local-retain` (hostPath `/mnt/ssd-data`, Retain) | Avoids SQLite over NFS while keeping the data after PVC deletion |
| HomeBox images | `ghcr.io/sysadminsmedia/homebox:0.27.1` | Pinned; PostgreSQL and S3 support |
| Grocy image | `lscr.io/linuxserver/grocy:v4.7.1-ls347` | Pinned LinuxServer build |
| Node placement | `ubuntu-desktop` | NFS, SSD, and PostgreSQL all live there |
| Tag payload | Public HTTPS URL | Works on both phones without an app; no deep-link dependency |
| Auto-consume | Not enabled | Stock changes stay explicit; avoids false consumption |
| Cloudflare Access | Not enabled initially | Would block the phone's HTTP request; add later with a bypass rule if needed |

## Data ownership

| Data | System | Location | Backup |
| --- | --- | --- | --- |
| Bins, locations, durable items, maintenance | HomeBox | PostgreSQL `homebox` | Daily `pg_dump` to `postgresql-backup-pvc` |
| Photos and attachments | HomeBox | PVC `homebox-data` (NFS) | NAS snapshots |
| Pantry products, stock, expiry | Grocy | SQLite `/config/data/grocy.db` (SSD) | Daily SQLite backup to `grocy-backup` (NFS) |
| Grocy uploads and config | Grocy | PVC `grocy-config` (SSD) | Daily file archive to `grocy-backup` |
| Master tag index | User | Outside Git (spreadsheet) | User's normal document backup |

## HomeBox

- Namespace: `inventory`
- Service: `homebox:7745`
- PVC: `homebox-data`, 20Gi, `nfs-storage`, mounted at `/data`
- Database: `homebox` database and `homebox` role, created by the `homebox-db-init` Job in `data-storage`
- Secrets:
  - `homebox-credentials` in `inventory`: `auth-api-key-pepper`, `db-password`
  - `homebox-db-credentials` in `data-storage`: `password` (same value as `db-password`)
- First boot leaves registration enabled. After the admin account exists, set `HBOX_OPTIONS_ALLOW_REGISTRATION` to `"false"` and reconcile.

The API key pepper must remain stable. Changing it invalidates existing API keys.

Rotating the HomeBox database password requires re-sealing both `homebox-credentials` and `homebox-db-credentials`, then deleting and recreating the `homebox-db-init` Job so the role password is updated.

## Grocy

- Namespace: `inventory`
- Service: `grocy:80`
- Config PVC: `grocy-config`, 5Gi, `local-retain`, mounted at `/config`
- Backup PVC: `grocy-backup`, 5Gi, `nfs-storage`, mounted at `/backups`
- `GROCY_MODE=production`; `GROCY_BASE_URL=https://grocy.gcelik.dev/`
- The backup CronJob uses the Grocy image's PHP/PDO SQLite support to run `VACUUM INTO`, then archives `/config` without the live database.
- The LinuxServer image creates the initial admin account as `admin` / `admin`. Change it immediately.

## Home Assistant

Home Assistant runs in Docker on `pi-lab` (192.168.2.59) and is not managed by this repository. Install integrations through HACS:

- HomeBox: `JeffreyDissmann/ha-homebox`
- Grocy: `iamkarlson/grocy` (maintained fork; `custom-components/grocy` is unmaintained)

Requirements:

- Home Assistant 2026.8.1 or newer for the Grocy fork.
- HomeBox integration uses the HomeBox username/password.
- Grocy integration uses a Grocy API key created in the Grocy UI.

The example automations use persistent notifications so they are safe to paste before choosing a mobile notification service.

## Tag scheme

| Tag type | Payload | Written on |
| --- | --- | --- |
| Bin | HomeBox location URL | The bin itself |
| High-value item | HomeBox item URL | The item or its case |
| Pantry product | `https://grocy.gcelik.dev/product/<id>` | The product's shelf or bin |
| Action (optional) | Home Assistant tag URL | A command card or label |

Details are in `tags/tag-scheme.md`.

## Security

- No raw secrets in Git. All secret material is a `SealedSecret`.
- No direct public ports; traffic enters through the existing nginx ingress.
- HomeBox registration is disabled after first boot.
- Grocy admin password is changed immediately.
- The master CSV stays outside Git because it links physical tags to household contents.
- If Cloudflare Access is added later, create a bypass policy for `/api/*` and any tag-reachable URL, or tags will stop working.

## Non-goals

- Barcode scanning workflow (Grocy's built-in barcode field is available later).
- Automatic stock consumption from NFC taps.
- Replacing Home Assistant automations with HomeBox/Grocy logic.
- Full location tree duplication between HomeBox and Grocy.
- Storing inventory data in the energy repository.

## Deviations from the handover

| Handover | Implemented |
| --- | --- |
| Docker Compose on `ubuntu-server` | Flux/MicroK8s on the existing cluster |
| Cloudflare Tunnel | nginx ingress + cert-manager |
| Portainer stacks | GitOps manifests |
| Grocy on PostgreSQL | Grocy on SQLite, documented exception |
| Pushover | Home Assistant persistent notifications; user can switch to mobile notifications |
