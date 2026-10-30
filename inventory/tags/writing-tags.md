# Writing NFC Tags

Use NTAG215 or NTAG216 stickers/cards. They are widely compatible and hold a URL comfortably.

## Tools

- Android: NFC Tools
- iPhone: NFC Tools or Shortcuts

## Procedure

1. Create the record in HomeBox or Grocy.
2. Generate the URL using `tag-scheme.md`.
3. Open NFC Tools and choose **Write** → **URL/URI**.
4. Paste the URL.
5. Tap the tag and confirm the write.
6. Immediately test the tag with both phones.
7. Record the tag UID and URL in the master CSV.
8. Only after the pilot passes, optionally lock the tag.

## Test checklist

- [ ] Opens without an app.
- [ ] Opens on Wi-Fi and on cellular data.
- [ ] Lands on the correct HomeBox location/item or Grocy product.
- [ ] QR fallback from the HomeBox label works.
- [ ] Tag UID is recorded.

## Locking

Locking is permanent. Do not lock a tag until:

- The domain (`gcelik.dev`) and hostnames are final.
- The HomeBox/Grocy record will not be recreated.
- The tag has passed the pilot test on both phones.
