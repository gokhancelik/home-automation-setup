# Tag Rollout

## Before buying in bulk

Complete `tags/pilot-test-plan.md` with five tags.

## Pilot rollout

1. Create one HomeBox bin, one HomeBox high-value item, and two Grocy products.
2. Generate the URLs using `tags/tag-scheme.md`.
3. Write five tags using `tags/writing-tags.md`.
4. Test each tag on both phones, on Wi-Fi and cellular.
5. Record every tag UID and URL in the master CSV.

Do not lock tags yet.

## First real zones

Order:

1. Garage bins (HomeBox).
2. Pantry and fridge shelves (Grocy).
3. High-value tools and electronics (HomeBox items).
4. Maintenance items with schedules (HomeBox items).

Create records first, write tags second, and record the CSV row immediately after each write.

## Bulk rollout

- Use labels from HomeBox for bins; put the NFC sticker under or beside the QR label.
- For metal shelves, use anti-metal stickers or mount the tag on a plastic card.
- For the freezer, test the tag through the door before committing.
- Keep one tag per bin/product. Do not tag individual cans or low-value units.
- Keep the master CSV outside Git.

## Maintenance

- If a record is recreated, rewrite its tag and update the CSV.
- Do not change the public domain after tags are locked.
- Review low-stock/expiry automations monthly.
- Re-run the pilot test after any ingress, DNS, or Cloudflare Access change.
