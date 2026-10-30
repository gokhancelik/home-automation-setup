# Inventory Data

The master tag index is a spreadsheet or CSV kept **outside this repository**. It links each physical NFC tag to a system record.

Keep it outside Git because it reveals what is in the house and which items are high value. The repository ignores all CSV files in this directory except the example.

## Columns

| Column | Meaning |
| --- | --- |
| `type` | `bin`, `item`, `product`, or `action` |
| `name` | Human-readable name |
| `system` | `homebox`, `grocy`, or `home-assistant` |
| `id` | HomeBox or Grocy record ID |
| `url` | URL written to the tag |
| `tag_uid` | NFC tag UID (from NFC Tools) |
| `location` | Bin or shelf label |
| `notes` | Free text; replace date, warranty, exceptions |

Start from `master-inventory.example.csv`.

## Rules

- Do not put the CSV in Git, even in a private repository.
- Back it up with your normal documents.
- Record the tag UID before locking a tag.
- Changing a record ID means rewriting the tag. Prefer stable records.
- Keep HomeBox as the source of truth for bins and durable items; Grocy for pantry products.
