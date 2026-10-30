# Home Assistant Integration

Home Assistant runs in Docker on `pi-lab` (192.168.2.59). Its configuration is not managed by this repository; the files here are copy-ready examples.

## HACS integrations

Install through HACS → Integrations → Custom repositories:

| Integration | Repository | Type |
| --- | --- | --- |
| HomeBox | `JeffreyDissmann/ha-homebox` | Integration |
| Grocy | `iamkarlson/grocy` | Integration |

Do not install `custom-components/grocy`; it is unmaintained.

The Grocy fork requires Home Assistant 2026.8.1 or newer.

## HomeBox

1. Settings → Devices & Services → Add Integration → HomeBox.
2. URL: `https://homebox.gcelik.dev`
3. Username/password: the HomeBox account created during first boot.
4. Verify entities such as total items, total locations, total value, and maintenance due.

Useful entities (names may vary):

- `sensor.homebox_total_items`
- `sensor.homebox_total_locations`
- `sensor.homebox_total_value`
- `sensor.homebox_maintenance_due_today`

## Grocy

1. In Grocy, open the wrench menu → Manage API keys → create a key.
2. Settings → Devices & Services → Add Integration → Grocy.
3. URL: `https://grocy.gcelik.dev`
4. API key: the key from step 1.
5. Verify stock, missing, expired, and expiring entities.

Useful entities (names may vary):

- `sensor.grocy_stock`
- `binary_sensor.grocy_missing_products`
- `binary_sensor.grocy_expired_products`
- `binary_sensor.grocy_expiring_products`

## Automations

Copy the YAML from `automations/` into an automation YAML file or recreate the automations in the UI. The examples use `persistent_notification.create` so they work without a mobile notification service. Replace the action with `notify.mobile_app_<phone>` when ready.

Verify the entity IDs in **Developer Tools → States** before enabling.

## Dashboard

Copy `dashboard/inventory-view.yaml` into a new Lovelace view, or use it as a source of cards. Adjust entity IDs to match your installation.
