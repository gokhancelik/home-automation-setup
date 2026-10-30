# Inventory (HomeBox + Grocy)

NFC-first household inventory. HomeBox holds bins, locations, durable items, and maintenance schedules; Grocy holds pantry products, stock, and expiry dates.

## URLs

| App | URL | Purpose |
| --- | --- | --- |
| HomeBox | https://homebox.gcelik.dev | Bins, locations, durable items, maintenance |
| Grocy | https://grocy.gcelik.dev | Pantry products, stock, expiry |
| Home Assistant | https://ha.gcelik.dev | Automations and dashboard (HA runs in Docker on `pi-lab`) |

## Deployment

- Manifests: `clusters/microk8s/apps/inventory/`
- Namespace: `inventory`
- Images: HomeBox `0.27.1`, Grocy `4.7.1` (LinuxServer)
- HomeBox data: PostgreSQL database `homebox` in `data-storage` plus attachments PVC `homebox-data` (NFS)
- Grocy data: SQLite at `/config/data/grocy.db` on the local SSD with daily backups to NFS
- Both workloads are pinned to `ubuntu-desktop`

Grocy is the only SQLite exception. Upstream Grocy does not support PostgreSQL.

## First time

1. Commit and push the manifests, then let Flux reconcile. Do not use `kubectl apply`.
2. Follow `runbooks/first-boot.md` to create the HomeBox admin, change the Grocy admin password, and connect Home Assistant.
3. Follow `runbooks/tag-rollout.md` for the pilot tags.

## Layout

```text
inventory/
├── README.md
├── DESIGN.md
├── HANDOVER.md              # original requirements + implementation addendum
├── data/                    # master CSV template and data-handling notes
├── tags/                    # tag scheme, writing instructions, pilot plan
├── home-assistant/          # HA integration notes, automations, dashboard
└── runbooks/                # first boot, backup/restore, tag rollout, acceptance
```

## Status

- [x] Manifests and documentation scaffolded
- [ ] DNS records for `homebox.gcelik.dev` and `grocy.gcelik.dev`
- [ ] First boot and admin accounts
- [ ] Home Assistant integrations and dashboard
- [ ] Pilot tags and acceptance tests

See `HANDOVER.md` for the original requirements and `DESIGN.md` for the implementation decisions.
