# Vertical templates and shared UI

Status: **Proposed.**

## 1. What is shared vs configured

| Shared (never per vertical) | Configurable by template |
|---|---|
| Application shell, navigation principles, onboarding pattern | Product attributes and labels |
| Product/variant model | Required vs optional fields |
| Purchase, sales, party workflows | Default categories, default units |
| Inventory movement model, accounting engine | Tracking mode defaults |
| Tax-preparation architecture | Validation rules (from a fixed set) |
| Reports, security, audit | Suggested terminology, feature visibility, onboarding defaults, report defaults |

No separate apps, frontends, inventory engines, accounting engines or navigation per vertical.

## 2. Deliberate limits (not a low-code platform)

Excluded: scripting languages, expression languages, runtime workflow definitions, user-authored validation code, dynamic database schemas per vertical.

### Attribute types (closed set)

`text`, `number` (with unit), `enum` (fixed option list), `boolean`, `date`.

### Validation rules (closed set)

`required` (levels: now / before-posting / optional), `maxLength`, `min`, `max`, `pattern` (from a curated list only, e.g. `gstin`, `hsn`), `allowedValues`.

### Tracking modes

`none` (1A), `serial`, `imei`, `batch`, `batch_expiry` (later). Warranty duration is an attribute; unit-level warranty reference is later tracking.

### Storage

Attribute values stored as typed rows (`variant_attribute_value` with value columns per type) rather than per-vertical columns. Core fields (name, SKU, unit, price, tax class, tracking mode) are real columns shared by all verticals.

## 3. Electronics-lite template v1

| Attribute | Type | Required | Notes |
|---|---|---|---|
| Category | core | now | Defaults: TV, Fan, Kitchen appliance, Mobile accessory, Cable, Remote, Charger, Lighting, Other |
| Brand | text | optional | Empty allowed for generic |
| Model | text | optional | |
| Screen size / capacity | number+unit | optional | inch, litre, mAh, W |
| Colour | text | optional | |
| Purchase unit / sales unit | core | now | piece default; box with conversion |
| Supplier reference | supplier product ref | optional | Slice 4 |
| Internal SKU | core | auto | `INT-` for generic or when no code |
| Barcode aliases | core | optional | Slice 2 |
| Purchase price / selling price | core | before posting | Unknown cost never 0 |
| Tax classification | core | before posting tax invoice | HSN entered by user, not invented |
| Warranty duration (months) | number | optional | informational in 1A |

## 4. Template versioning

1. Templates are platform-owned, versioned, immutable once published.
2. At onboarding the tenant receives a **copy**; it becomes tenant-owned (`tenant_vertical_config` records source template + version).
3. Platform updates never overwrite tenant configuration.
4. Later, an update may be **offered** as a reviewable migration: list of added/changed/removed fields and validation effects; the owner accepts per item.
5. Making a field required never invalidates existing records: it applies at next edit/post, with a checklist of affected records.
6. Template migrations are audited and reversible where practical (previous configuration version retained).

## 5. Frontend behaviour

The product form renders core fields plus the tenant's attribute definitions in order. The server validates with the same definitions; the client validation is convenience only.
