# Handover: NFC Inventory with HomeBox + Grocy

> This is a handover document only. Move it to the new inventory project
> directory when starting that work. Do not add inventory code or
> configuration to the energy integration repository.

**Created:** 2026-10-29

## 1. Purpose and status

Build a Home Assistant-friendly inventory system for:

- **Household storage bins and durable items** -> HomeBox
- **Pantry and consumables** -> Grocy
- **NFC tags** -> quick lookup from iPhone and Android
- **Home Assistant** -> notifications, dashboards, and optional action tags

**Status:** planning complete; implementation not started.

**Important:** this is a separate project. Do **not** add inventory code or
configuration to `C:\Projects\energy-api-integrations`. The energy-price
integration is complete and released as `v0.27.0`.

Suggested project directory: `C:\Projects\home-inventory\` or
`/opt/home-inventory/`.

## Implementation addendum (2026-10-30)

This handover was implemented inside `home-automation-setup` rather than a
separate project, using the existing Flux/MicroK8s platform instead of Docker
Compose and Cloudflare Tunnel.

| Handover | Implemented |
|---|---|
| Docker Compose | Flux + MicroK8s manifests |
| Cloudflare Tunnel | nginx ingress + cert-manager |
| Portainer | GitOps |
| HomeBox + Grocy on PostgreSQL | HomeBox on PostgreSQL; Grocy on SQLite (upstream does not support PostgreSQL) |
| Suggested `C:\Projects\home-inventory` | `inventory/` docs and `clusters/microk8s/apps/inventory/` manifests |

See `DESIGN.md` for the full implementation decisions. The rest of this
document is the original handover and remains the requirements source.

## 2. Locked decisions

| Decision | Choice |
|---|---|
| Inventory systems | HomeBox + Grocy |
| HomeBox role | Bins, locations, durable items, photos, serials, receipts, maintenance |
| Grocy role | Pantry/consumables, stock, minimum stock, due dates, shopping list |
| HA install | HA Container/Core |
| HA version | 2026.10 |
| HACS | HACS custom repositories, because Marketplace starts in 2026.11 |
| Remote access | Cloudflare Tunnel |
| HA location | Different host from HomeBox/Grocy |
| Phones | iPhone + Android |
| Pantry scan behavior | Open Grocy product page; **no auto-consume** |
| Tag payloads | Direct backend URLs for lookup; HA tag URLs only for optional action tags later |
| Tag hardware | To be purchased: NTAG215/216 tags and NFC writer/app |

## 3. Architecture

### Responsibility split

- **HomeBox = "where/what"**
  - One location per physical bin.
  - Durable items live inside bins.
  - Photos, serials, receipts, manuals, warranty, maintenance.
  - Tag the bin, not every item.
  - Tag high-value tools individually if useful.

- **Grocy = "how many"**
  - Pantry, fridge, freezer, tins, household consumables.
  - Quantity units, minimum stock, due dates, shopping list.
  - Tag the product or shelf, not each unit.
  - HomeBox is not a stock ledger.

### Avoid duplication

- Create a HomeBox location for every physical bin.
- Create Grocy locations only for consumable storage bins/zones.
- Use the same location code in both systems, e.g. `PANTRY-BIN-02`.
- Cross-link:
  - HomeBox bin custom field -> Grocy location/product link.
  - Grocy product userfield/description -> HomeBox bin code.
- Keep a master CSV outside the repo:
  - `type, name, system, id, url, tag_uid, location, notes`

### Tag behavior

- Bin tag -> HomeBox location URL.
- High-value item tag -> HomeBox item URL.
- Pantry product/shelf tag -> Grocy `/product/<id>` URL.
- Optional action tag later -> `https://www.home-assistant.io/tag/<id>`.
- Phase 1 does **not** auto-consume stock. The user scans, reviews the
  Grocy page, then consumes/purchases manually.

## 4. Deployment on the separate Docker host

Create a Compose project with HomeBox, Grocy, and `cloudflared`.

```yaml
services:
  homebox:
    image: ghcr.io/sysadminsmedia/homebox:latest
    restart: unless-stopped
    environment:
      HBOX_MODE: production
      HBOX_AUTH_API_KEY_PEPPER: ${HBOX_PEPPER}   # >=32 bytes
      HBOX_OPTIONS_ALLOW_ANALYTICS: "false"
      HBOX_OPTIONS_TRUST_PROXY: "true"
      HBOX_WEB_MAX_UPLOAD_SIZE: "25"
      HBOX_OPTIONS_ALLOW_REGISTRATION: "true"   # first boot only; set false after admin exists
      # HBOX_OPTIONS_HOSTNAME: ${HOMEBOX_HOSTNAME}  # optional; public hostname
    volumes:
      - homebox-data:/data

  grocy:
    image: lscr.io/linuxserver/grocy:latest
    restart: unless-stopped
    environment:
      PUID: ${PUID}
      PGID: ${PGID}
      TZ: ${TZ}
    volumes:
      - grocy-config:/config

  cloudflared:
    image: cloudflare/cloudflared:latest
    restart: unless-stopped
    command: tunnel --no-autoupdate run --token ${TUNNEL_TOKEN}
    depends_on:
      - homebox
      - grocy

volumes:
  homebox-data:
  grocy-config:
```

Notes:

- HomeBox listens on `7745`; Grocy listens on `80`.
- No app ports need to be published; `cloudflared` makes outbound connections.
- Put `HBOX_PEPPER`, `TUNNEL_TOKEN`, `TZ`, `PUID`, `PGID`, and optional
  `HOMEBOX_HOSTNAME` in `.env`; keep it private.
- HomeBox first boot: create the admin account, then set
  `HBOX_OPTIONS_ALLOW_REGISTRATION: "false"` and restart.
- Change Grocy's default `admin/admin` immediately.
- Create a Grocy API key for HA.
- Use the LinuxServer Grocy image because the old `grocy/grocy-docker`
  project is archived.

## 5. Cloudflare Tunnel

In Zero Trust -> Networks -> Tunnels, create a tunnel and add public
hostnames:

| Public hostname | Origin |
|---|---|
| `homebox.example.com` | `http://homebox:7745` |
| `grocy.example.com` | `http://grocy:80` |

- Cloudflare creates DNS and handles TLS.
- No router ports need to be opened.
- Disable caching for both hostnames.
- **Keep Cloudflare Access off initially.**
  - HomeBox/Grocy use their own login.
  - If Access is enabled later, add a bypass for API paths or HA's egress;
    otherwise the HA integrations will fail.
- Cloudflare's free upload limit is sufficient; HomeBox is capped at 25 MB.
- Test both URLs from cellular data, not just LAN.
- If `cloudflared` runs on the host instead of in Compose, use
  `http://localhost:3100` and `http://localhost:9283`; publishing
  localhost-only ports would then be required.

## 6. Home Assistant 2026.10 setup

1. If HACS is not installed, install it manually.
2. HACS -> Integrations -> Custom repositories:
   - `https://github.com/JeffreyDissmann/ha-homebox` -> Integration
   - `https://github.com/custom-components/grocy` -> Integration
3. Install both, restart HA.
4. Add integrations:
   - **HomeBox:** URL `https://homebox.example.com`, username/password.
   - **Grocy:** URL `https://grocy.example.com`, API key.
5. Enable the Grocy entities you need; they are disabled by default:
   - stock
   - missing products
   - expired products
   - expiring products
6. Leave HomeBox area-to-location sync off initially.
7. Because HA is on a different host, use the public HTTPS URLs for both
   integrations. No shared Docker network is needed.

### Integration expectations

- HomeBox integration provides inventory statistics, device linking, and
  maintenance sensors; it does **not** create per-item entities.
- Grocy integration provides stock/missing/expiring sensors and services
  such as:
  - `grocy.consume_product_from_stock`
  - `grocy.add_product_to_stock`
  - `grocy.add_missing_products_to_shopping_list`
  - `grocy.remove_product_in_shopping_list`

## 7. Data model

### HomeBox

- Hierarchy: `House > Room > Shelf > Bin`.
- Locations support photos, custom fields, attachments, notes, asset IDs.
- Use auto-increment asset IDs as human-readable backup codes.
- Suggested bin codes: `GARAGE-SHELF-A-BIN-01`, `PANTRY-BIN-02`.
- Print QR labels from the public URL; add an NFC sticker next to the QR.

### Grocy

- Create locations only for consumable storage bins/zones.
- Use the same codes as HomeBox.
- Product fields: name, quantity unit, default location, minimum stock,
  due days, product group, Grocycode.
- Product page URL: `https://grocy.example.com/product/<id>`.
- Grocy's stock-overview location filter does not appear to be
  URL-addressable; use product tags or an HA dashboard for bin-level stock
  views.

## 8. NFC tags and hardware

### Buy list for the pilot

- **50x NTAG215 25 mm stickers** - main bin/product tags.
- **10x on-metal NTAG215 tags** - metal shelving/toolboxes.
- **10x waterproof NTAG215 tags** - garage/outdoor bins.
- **NFC Tools** app on Android/iOS for writing/testing.
- Optional: **ACR122U** desktop writer for bulk encoding.
- Optional: label printer later. For the pilot, print QR labels on regular
  paper and tape them next to the NFC sticker.

### Tag scheme

| Tag on | Payload |
|---|---|
| Bin/location | Exact HomeBox location URL |
| High-value durable item | HomeBox item URL |
| Pantry product/shelf | `https://grocy.example.com/product/<id>` |
| Action tag (later) | HA tag URL |

### Writing and testing

- Use HomeBox's generated label/QR URL; don't hand-build location URLs.
- Write direct URLs with NFC Tools or a desktop writer.
- The HA Companion app writes HA tag URLs, not arbitrary backend URLs.
- Test 5 tags on both phones before writing the rest.
- iPhone background NFC requires iPhone XS/XR/11 or newer; older iPhones
  use Control Center.
- Add QR + human-readable code as backup.
- Don't lock tags until the public domain is final.
- UID is cloneable; never treat a tag as authentication.
- Keep the master CSV outside the repo.

## 9. Pilot

Start with 5 bins and 10 pantry products.

Acceptance criteria:

- Both phones open the correct HomeBox/Grocy page over Cloudflare HTTPS.
- QR backup works.
- HA shows HomeBox/Grocy entities.
- Grocy consume/purchase works from the product page.
- Low-stock or expiring notification fires.
- Both Docker volumes restore successfully.
- Tag URLs remain valid after a container restart.

## 10. Automations and dashboard

- Daily/weekly low-stock notification.
- Expired/expiring notification from Grocy binary sensors.
- HomeBox maintenance-due notification.
- Optional: `grocy.add_missing_products_to_shopping_list` when missing
  products appear.
- HA dashboard:
  - HomeBox total items, locations, value, maintenance due.
  - Grocy stock, missing, expiring.
  - Links to HomeBox and Grocy.
  - Avoid iframe cards if the apps block embedding.
- No auto-consume automation in phase 1.

## 11. Operations and security

- Back up:
  - `homebox-data` volume
  - `grocy-config` volume
  - `.env` and Cloudflare Tunnel token/config
  - Compose and proxy configuration
- Test restore before scaling.
- Update deliberately: `docker compose pull && docker compose up -d` after
  a backup.
- Keep app ports private; use Cloudflare Tunnel only.
- Use strong app passwords; disable HomeBox registration after the first
  account.
- Keep the HA Companion app updated.
- Tags are convenience, not authorization; don't map sensitive actions to
  them.

## 12. Open items / next actions

1. Confirm exact public hostnames, e.g. `homebox.<domain>` and
   `grocy.<domain>`.
2. Confirm whether Cloudflare Access is currently on or planned.
3. Confirm Docker host OS and volume paths.
4. Confirm whether HACS is already installed on HA 2026.10.
5. Buy/test the NFC tag starter pack.
6. Deploy Compose + Cloudflare Tunnel.
7. Install HACS integrations and connect HA.
8. Create pilot bins/products and write 5 test tags.
9. Validate both phones, QR fallback, notifications, and backup restore.
10. Scale with CSV import and bulk tag writing.

## 13. References

- HA NFC/QR tags:
  https://companion.home-assistant.io/docs/integrations/universal-links/
- HA tag integration:
  https://www.home-assistant.io/integrations/tag/
- HomeBox docs: https://homebox.software/
- HomeBox install: https://homebox.software/en/quick-start/install/
- HomeBox HACS integration:
  https://github.com/JeffreyDissmann/ha-homebox
- Grocy: https://grocy.info/
- Grocy LinuxServer image:
  https://docs.linuxserver.io/images/docker-grocy/
- Grocy HACS integration:
  https://github.com/custom-components/grocy
- Cloudflare Tunnel:
  https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/
- Optional ESPHome PN532:
  https://esphome.io/components/binary_sensor/pn532/

## 14. Do not

- Do not add inventory code to the energy integration repo.
- Do not use HA ingress URLs on NFC tags.
- Do not expose HomeBox/Grocy ports publicly.
- Do not enable Cloudflare Access without an API bypass for HA.
- Do not auto-consume stock in phase 1.
- Do not lock NFC tags before the public domain is final.
- Do not treat NFC UID as authentication.
- Do not use HomeBox for stock quantities.
- Do not duplicate the full location tree in both systems.
- Do not use `localhost` from inside the HA container.
