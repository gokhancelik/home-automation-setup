# Pilot Test Plan

Run this before buying or writing tags in bulk.

## Scope

Five tags:

1. One HomeBox bin in the garage.
2. One HomeBox high-value item.
3. One Grocy pantry product.
4. One Grocy fridge product.
5. One tag in a difficult location (metal shelf, freezer door, or basement).

## Setup

- [ ] HomeBox and Grocy are reachable over HTTPS.
- [ ] Admin accounts exist and default passwords are changed.
- [ ] Both test phones have NFC enabled.
- [ ] The master CSV is open for recording UIDs.

## Tests per tag

- [ ] Tap on phone A with Wi-Fi: correct page opens.
- [ ] Tap on phone A with cellular data only: correct page opens.
- [ ] Tap on phone B with Wi-Fi: correct page opens.
- [ ] Tap on phone B with cellular data only: correct page opens.
- [ ] QR fallback works.
- [ ] Tag reads through the intended mounting (bin lid, shelf, freezer door).
- [ ] UID and URL are recorded.

## Pass criteria

- 5/5 tags open the correct page on both phones.
- No tag requires an app, login prompt, or manual URL entry.
- No tag points to an internal IP or ingress node port.

## If a test fails

- Metal: use an anti-metal NFC sticker or move the tag away from the metal.
- Range: try a larger tag (NTAG216) or a different mounting position.
- Wrong page: fix the URL and rewrite before locking.
- Slow load: check ingress/TLS and the app's response time before rolling out.
