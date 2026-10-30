# Acceptance Tests

## Platform

- [ ] `flux get kustomizations` shows all inventory resources ready.
- [ ] `kubectl get pods -n inventory` shows HomeBox and Grocy running.
- [ ] Both pods are on `ubuntu-desktop`.
- [ ] `kubectl get certificate -n inventory` shows both certificates ready.
- [ ] `https://homebox.gcelik.dev` and `https://grocy.gcelik.dev` load with valid TLS.

## HomeBox

- [ ] Admin registration completed.
- [ ] Registration is disabled after first boot.
- [ ] Can create a location, item, and label.
- [ ] Generated label URL uses `https://homebox.gcelik.dev`.
- [ ] Data survives a pod restart.
- [ ] Attachments survive a pod restart.

## Grocy

- [ ] Default admin password changed.
- [ ] API key created.
- [ ] Can create a product, stock entry, and expiry date.
- [ ] Product URL uses `https://grocy.gcelik.dev/product/<id>`.
- [ ] Data survives a pod restart.

## Home Assistant

- [ ] HomeBox integration connected.
- [ ] Grocy integration connected.
- [ ] Low-stock, expiry, and maintenance automations trigger.
- [ ] Dashboard view renders.
- [ ] Notifications arrive (or persistent notifications appear).

## NFC

- [ ] Five pilot tags pass on both phones, on Wi-Fi and cellular.
- [ ] QR fallback works.
- [ ] Tags work in their mounted locations.
- [ ] Master CSV records UID, URL, and location.

## Backup and restore

- [ ] PostgreSQL backup Job completed for `homeassistant` and `homebox`.
- [ ] Grocy backup Job completed.
- [ ] Backup files are non-empty.
- [ ] A test restore of the HomeBox database succeeds.
- [ ] A test restore of the Grocy database succeeds.
- [ ] Restore procedure is documented and reproducible.

## Security

- [ ] No raw secrets in Git.
- [ ] No direct public NodePort access to HomeBox or Grocy.
- [ ] HomeBox registration disabled.
- [ ] Grocy admin password changed.
- [ ] Master CSV is outside Git.
- [ ] Cloudflare Access, if enabled later, has a bypass for tag-reachable URLs.

## Operations

- [ ] Runbooks reviewed by the household.
- [ ] Backup retention confirmed at 30 days.
- [ ] A failed backup produces a visible failed Job.
- [ ] Flux reconciles after a deliberate drift (test with a harmless annotation change).
