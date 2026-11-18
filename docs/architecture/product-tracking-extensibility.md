# Product tracking extensibility

Status: **Proposed.** Phase 1A supports only non-tracked products.

## 1. Tracking modes

| Mode | Unit of identity | Typical use | Phase |
|---|---|---|---|
| none | variant only | most stock | 1A |
| serial | one identifier per unit | appliances, TVs | 1B |
| imei | serial with IMEI format (1–2 per device) | phones | 1B |
| batch | group identifier | consumables | 1B+ |
| batch_expiry | batch + expiry date | perishable; pharmacy only after regulatory review | Later |
| warranty reference | attached to a tracked unit at sale | electronics | 1B |

## 2. Where policy lives

- `product_variant.tracking_mode` (column exists in 1A, constrained to `none`).
- Tracking policy is a Catalogue concern; enforcement is an Inventory concern at posting.
- Vertical templates set the **default** mode for categories; the variant value is authoritative.
- Changing tracking mode on a variant with stock is blocked (requires zero stock or a migration document).

## 3. Future behaviour (design only)

| Workflow | Behaviour when tracked |
|---|---|
| Receipt | Posting requires one identifier per base unit (serial/IMEI) or a batch id per line split; duplicates within tenant rejected |
| Sale | Posting requires selecting specific identifiers that are currently in stock at the location |
| Return | Customer return must reference an identifier previously sold to that sale line; supplier return references received identifiers |
| Movement history | Each movement line carries `tracking_ref` rows (one per identifier) so history is traceable per unit |
| Stock count | Count by identifier; variance lists missing/unexpected identifiers |
| Transfer | Identifiers move with the transfer |

## 4. Tracking vs valuation

They are separate concepts:

- **Tracking** answers "which physical unit".
- **Valuation** answers "what cost".

Proposed default: tracked electronics remain valued at **WAC per variant**. Specific identification of cost per serial is a separate, future policy decision (PR-01) and is not implied by tracking.

## 5. One inventory engine

Tracked and non-tracked stock use the same movement table, the same projections and the same posting pipeline. Tracking adds a child table of identifiers per movement line plus a projection of identifier status; it never forks the engine.

## 6. Phase 1A extension points (only these)

- `tracking_mode` column with CHECK = 'none'.
- Movement line has a stable `line_id` that future tracking rows can reference.
No other tracking tables are created in 1A.


## PR B boundary — tag evidence is not inventory unit tracking

PR B adds `sales.item_tag_capture` as a photo/extraction evidence record associated with a product and, after posting, a sale line. It can preserve serial and IMEI values from an individual product tag. It does **not** change `product_variant.tracking_mode`, populate `inventory.serial_unit`, make serial tracking mandatory, or change stock movement/WAC rules. Those remain separate tracking-policy work and must be implemented with their own receipt, sale, return and stock-availability lifecycle.
