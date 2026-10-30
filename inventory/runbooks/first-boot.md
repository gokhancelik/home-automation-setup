# First Boot

## Prerequisites

- The inventory manifests are committed and Flux has reconciled.
- DNS records exist for `homebox.gcelik.dev` and `grocy.gcelik.dev`.
- The ingress controller and `letsencrypt-prod` ClusterIssuer are healthy.

Check:

```bash
flux get kustomizations
kubectl get pods -n inventory
kubectl get certificate -n inventory
kubectl get ingress -n inventory
```

## HomeBox

1. Open `https://homebox.gcelik.dev`.
2. Register the admin account. Registration is enabled for first boot.
3. Verify login and create a test location.
4. In `clusters/microk8s/apps/inventory/homebox/deployment.yaml`, set `HBOX_OPTIONS_ALLOW_REGISTRATION` to `"false"`.
5. Commit, push, and reconcile:

   ```bash
   flux reconcile kustomization flux-system --with-source
   ```

6. Confirm the HomeBox pod restarted and registration is closed.
7. Confirm `HBOX_OPTIONS_HOSTNAME` is `https://homebox.gcelik.dev` so generated labels use the public URL.

## Grocy

1. Open `https://grocy.gcelik.dev`.
2. Log in with the LinuxServer default: `admin` / `admin`.
3. Change the admin password immediately.
4. Create a Grocy API key: wrench menu → Manage API keys.
5. Create a few locations (`PANTRY`, `FRIDGE`, `FREEZER`) and one test product.
6. Confirm the product URL is `https://grocy.gcelik.dev/product/<id>`.

## TLS and DNS

If the certificate is not ready, check:

```bash
kubectl describe certificate -n inventory homebox-tls
kubectl describe certificate -n inventory grocy-tls
kubectl get challenges -A
```

Add A/AAAA (or CNAME) records for both hostnames at your DNS provider if they are missing. Use the same public IP as the existing `ha.gcelik.dev` / `frigate.gcelik.dev` records. DNS-01 issuance does not require the A record, but browsers do.

## Home Assistant

1. Install the integrations from `inventory/home-assistant/README.md`.
2. Add HomeBox and Grocy.
3. Verify the entity IDs used by the example automations and dashboard.
4. Paste the automations and dashboard, or recreate them in the UI.
5. Test each notification action.

## Done when

- [ ] HomeBox admin exists; registration is disabled.
- [ ] Grocy admin password changed; API key created.
- [ ] Both HTTPS URLs show a valid certificate.
- [ ] Home Assistant shows HomeBox and Grocy entities.
- [ ] A test bin, item, and product exist.
