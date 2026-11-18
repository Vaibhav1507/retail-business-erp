# Invoice Item-Tag Photo Capture — Product Requirements and Implementation Plan

**Status:** PR B implementation and GitHub Actions validation passed; review pending  
**Planning target:** Controlled pilot  
**Repository:** `Vaibhav1507/retail-business-erp`  
**Implementation branch:** `feat/invoice-item-tag-photo-capture`  
**Last reviewed:** 18 November 2026

## 1. Executive summary

Add a photo-upload option to the **Generate Invoice** workflow so a shopkeeper can photograph the product identification tag attached to each physical item being sold. RBOS should read the tag, identify the product, capture serial/IMEI and barcode identifiers, suggest a match in the shop's catalogue, and populate the billing form with the extracted information.

A customer may buy several products in one bill. Therefore, the user must be able to upload multiple photos in one invoice session. In the initial design, **each uploaded photo represents one physical item/tag capture**. Each image is processed and reviewed independently so that one failed or unclear photograph does not lose the other items.

This is not the same as uploading a supplier purchase invoice. The input is a product label/tag on an individual item, not a multi-line supplier invoice document.

The feature must also build a useful, tenant-owned product data set over time. After a user confirms extracted product-level information, RBOS can enrich the catalogue with brand, model, product specifications, barcode aliases, MRP and other useful identifiers. Unit-specific identifiers such as serial numbers and IMEIs must remain associated with the sold physical unit/transaction—not become shared catalogue attributes.

Use the central AI Gateway described in `docs/architecture/ai-gateway-pilot-implementation-plan.md`. Do not add a direct Gemini/Groq/xAI call from the browser or create a second provider abstraction.

## 2. Problem and desired outcome

### Current experience

The current `GenerateInvoice.tsx` form already supports:
- Manual product selection / product name entry.
- A barcode scanning action that looks up the product through `GET /products/by-barcode`.
- Manual serial-number entry.
- Quantity, selling price and discount entry.
- Invoice review and confirmation.

The sale API already accepts line-level serial values through the sale-line input, and the sale detail query returns those serials. Product catalogue support already includes fields such as brand, model, attributes/specifications, MRP, HSN, GST metadata and barcode aliases.

However, when the tag contains a long model description, serial/IMEI, barcode and specifications, the cashier has to type or scan individual values and manually find the catalogue product. This is slow and error-prone.

### Desired experience

The cashier should be able to:
1. Open Generate Invoice and choose **Add item by photo**.
2. Capture a tag with the phone camera or select a photo from the gallery.
3. Repeat this for every physical item the customer is buying.
4. Let RBOS process the images and prepare one review card per item.
5. Confirm or correct the product match, serial/IMEI, barcode and other extracted details.
6. Add the reviewed items to the invoice with the minimum possible typing.
7. Review the complete bill and confirm it using the existing sales workflow.

Manual entry and existing barcode scanning must continue to work. Photo capture is an additional path, not a replacement.

## 3. Product scope

### 3.1 In scope for the first release

- A camera/gallery upload action inside the product section of Generate Invoice.
- Multiple independent item-tag uploads per invoice.
- One image per physical item in the initial workflow.
- Independent upload, processing, success, review and failure state per image.
- Image validation and safe upload.
- Barcode extraction using deterministic barcode decoding where possible.
- OCR/vision extraction through the central AI Gateway for printed text and tag structure.
- Structured extraction of product identity, technical details and unit identifiers.
- Matching against the current tenant's product catalogue and barcode aliases.
- Displaying extracted details in editable review cards.
- Mapping confirmed data into invoice item rows.
- Mapping serial/IMEI values to the sale line using the current sales contract where compatible.
- Saving confirmed product-level catalogue enrichment and barcode aliases, with explicit user confirmation.
- Persisting a secure reference to the uploaded tag image and extraction record, with configurable retention.
- Usage, provider, latency and cost tracking through the central AI Gateway.

### 3.2 Out of scope for the first release

- Replacing the existing sales/invoice workflow.
- Uploading one supplier invoice and extracting its entire list of purchase lines (that is a separate existing workflow).
- Automatically posting a bill as soon as an image is read.
- Automatically choosing an unverified selling price, discount or tax treatment from a tag.
- Creating or changing catalogue products without user review.
- Automatically changing stock, purchase cost, inventory valuation or GST configuration from a tag.
- Full tracked-unit inventory, warranty claim, repair or after-sales workflows beyond the identifiers already supported by the current sales flow.
- Automatic lookup against arbitrary external websites for every uploaded item.
- Sending images directly from the browser to an AI provider.

## 4. End-to-end user journey

### Step 1 — Start an invoice

The cashier selects/enters the customer and invoice details as today. In the Products section, show two complementary actions:

- **Add item manually** — existing behaviour.
- **Add item by photo** — new behaviour.

Keep the current barcode scanner available. The new option should not make manual entry or barcode scanning harder to reach.

### Step 2 — Capture multiple item tags

The upload UI should support:
- Taking a photo using the device's rear camera.
- Selecting one or multiple existing images from the gallery/files.
- Adding more photos later in the same invoice.
- Removing a photo before it is processed, or removing its draft item after processing.
- Clear guidance to photograph the complete, readable tag with good lighting and minimal glare.

For the initial feature, one uploaded image represents one physical item. If a customer buys three appliances, the cashier uploads three item-tag photos. Do not interpret a multi-select upload as multiple pages of one document.

A future enhancement may support multiple photos of the same item (for example front and rear labels), but that is not needed for the pilot.

### Step 3 — Process uploads independently

Each image gets its own state:

- Uploading
- Queued
- Reading tag
- Matching product
- Ready for review
- Needs attention
- Failed (retry or manual entry available)
- Added to bill

Use bounded concurrency so a large multi-item upload does not overwhelm the API or AI provider. A failure for one image must not cancel successful results for other images.

### Step 4 — Show a review card for each physical item

A review card should show:
- Uploaded image thumbnail and a way to view it larger.
- Proposed product/catalogue match and match reason.
- Brand, product name, model/part number and key specifications.
- Detected barcode(s), including barcode type when known.
- Serial number / IMEI / service-tag identifiers.
- MRP and any other readable label values.
- Confidence or a simple **Check this value** indicator for uncertain fields.
- Warnings for unreadable, conflicting or duplicate identifiers.
- Editable values and a retry/retake option.

Do not show a large technical OCR dump to the cashier by default. Provide a secondary details view for the full extracted information.

### Step 5 — Add reviewed items to the invoice

When the cashier selects **Add to bill**:
- The selected product is mapped to the invoice.
- The serial/IMEI is mapped to the correct physical unit / sale line.
- The barcode and other reusable product attributes are retained as proposed catalogue enrichment.
- Price, discount, quantity and tax values are displayed as normal editable invoice fields.
- The original image remains associated with its capture record.

The cashier can still edit any field or remove the item before final invoice confirmation.

### Step 6 — Final invoice confirmation

The existing review-and-confirm step remains authoritative. No sale is posted when a photo is uploaded, extracted or merely added to a draft.

The final `POST /sales` operation must remain the only point that posts the sale, updates stock/ledger projections, and creates the invoice according to existing sales rules. Attach confirmed item-capture records to the final sale line as part of a validated, tenant-scoped operation.

## 5. What should be extracted?

Use a structured, extensible result rather than hard-coding only serial number and barcode.

### 5.1 Product-level fields (reusable catalogue data)

| Field | Examples | Intended use |
|---|---|---|
| Brand / manufacturer | LG, Samsung, Voltas | Catalogue matching and enrichment |
| Product description | 1.5 Ton 5 Star Inverter AC | Product display name / match |
| Product category | Television, Refrigerator, Washing Machine | Catalogue category suggestion |
| Model / model code | Exact printed model identifier | High-value product matching |
| Manufacturer part number / SKU | MPN or printed item code | Identifier/search metadata |
| Barcode value | EAN/UPC/GTIN or other code | Barcode alias and future scanning |
| Barcode symbology | EAN-13, UPC-A, QR, Code 128, etc. | Correct interpretation |
| Variant attributes | Colour, size, capacity, storage, screen size | Product variant matching |
| Specifications | Wattage, voltage, dimensions, capacity, connectivity | Catalogue specs/attributes |
| MRP | Printed maximum retail price | Reference value only |
| HSN code | If clearly printed | Candidate tax metadata; review required |
| Warranty statement | e.g. 1 year product / 10 years compressor | Product/warranty information |
| Manufacturing/importer details | Manufacturer or importer name | Useful reference metadata |
| Other visible text | Any additional meaningful printed field | Extensible metadata |

### 5.2 Unit-level fields (specific to the physical item)

| Field | Examples | Intended use |
|---|---|---|
| Serial number | Appliance serial / service tag | Identify the individual unit |
| IMEI 1 / IMEI 2 | Mobile phone identifiers | Identify individual handset/SIM slots |
| Device identifier | Service tag or device ID | Unit-level traceability |
| Batch / lot number | Where present | Future batch tracking |
| Manufacturing date | Month/year or full date | Warranty/age context where useful |
| Expiry date | Where relevant to a future vertical | Future controlled scope |
| Warranty serial/reference | Printed warranty reference | Future after-sales workflow |

**Data separation is mandatory:**
- Model, brand, specifications and product barcode usually identify the product or product variant.
- Serial number, IMEI and service tag identify one physical unit.
- Never save a unique serial number as the product's model, SKU or shared catalogue barcode.
- If a tag contains multiple different identifier types, preserve the type labels. Do not flatten IMEI 1, IMEI 2, serial and barcode into an unlabelled string list in the new capture record.

### 5.3 Extraction evidence

For each extracted field, retain where practical:
- Normalised value.
- Original text as read.
- Extraction method (barcode decoder, OCR or vision model).
- Confidence / validation state.
- Whether the value was user-confirmed or user-edited.

This provenance is useful for reviewing errors and improving extraction. Do not claim a model's confidence is calibrated unless validated with a representative tag-image test set.

## 6. Barcode and OCR/vision strategy

Use a hybrid extraction pipeline. Do not rely on a vision LLM alone for every field.

1. Validate the image format, size and file signature.
2. Run a deterministic barcode decoder against the image.
3. Run OCR and/or a vision-capable model through the central AI Gateway to read printed text and understand which label value belongs to which field.
4. If OCR/vision detects a printed barcode string that the decoder did not decode, mark it as a candidate and validate it with existing barcode rules before saving it.
5. Normalise identifiers and preserve their original form for review.
6. Match the extracted values against the tenant's product catalogue.
7. Return a structured result and warnings; never create a catalogue product or sale automatically.

The repository already has `BarcodeRules.Normalize`, which validates common GTIN check digits and normalises UPC-A to EAN-13, plus tenant-scoped barcode lookup through `BarcodeService`. Reuse these rules rather than inventing a second barcode normalisation implementation.

A valid barcode is a strong match signal, but the UI should still show the matched product and let the cashier confirm. QR codes may contain URLs, serial numbers or other payloads; classify and validate the payload rather than treating every QR code as a product barcode.

## 7. Product matching rules

Match in this order, and display the reason:

1. **Exact local barcode match:** decoded/validated barcode matches a barcode alias already owned by this tenant.
2. **Exact manufacturer model match:** normalised brand + model/part number matches one active tenant catalogue product.
3. **Exact SKU / manufacturer code match:** a known code maps to one product.
4. **Strong attribute match:** brand + model + key variant attributes match.
5. **Possible match:** fuzzy text/specification similarity suggests one or more products; require explicit selection.
6. **No match:** offer manual catalogue search or a reviewable new-product draft.

Never auto-select a weak/ambiguous match. Do not create duplicate products simply because OCR punctuation, whitespace or model-code formatting differs.

Where a known local barcode is found, the existing `GET /products/by-barcode` route can return the product identity and serial-tracking status. The existing `GET /products/suggest-for-barcode` workflow may provide catalogue suggestions / external lookup results; any external information must be treated as a suggestion with provenance, not as a verified fact.

### New product handling

If no match exists:
- Pre-fill a **draft product suggestion** with extracted product-level fields.
- Ask the cashier to choose a category and confirm the name/model.
- Create the product only through the normal catalogue application service after user confirmation.
- Do not create a separate generic product for every photographed tag.
- Keep the unit serial/IMEI on the capture / sale item, not on the new product master record.

## 8. Mapping to invoice fields

| Extracted value | Invoice treatment |
|---|---|
| Product / matched product ID | Populate the invoice product selection |
| Product description | Populate the item name when no local product is matched; allow editing |
| Serial / IMEI | Populate the serial field for the correct sale line, subject to product tracking policy |
| Barcode | Use for product matching; offer to save as a barcode alias after validation and confirmation |
| Brand / model / specs | Catalogue enrichment after user confirmation |
| MRP | Show as reference; do not assume it is the selling price |
| Selling price | Use a trusted existing tenant price if one exists; otherwise require cashier input/confirmation |
| Quantity | One per physical-item photo initially; cashier may change it only where tracking rules allow |
| Discount | Never infer from the tag; user enters/confirms |
| HSN / GST information | Candidate metadata only; existing business tax and invoice rules remain authoritative |

Important pricing/tax guardrails:
- MRP is not the same as the actual selling price. Never silently bill at MRP.
- A tag value must not override an existing selling price or an approved price list without confirmation.
- Do not calculate tax from a loosely read label. Existing GST/non-GST invoice choice and current tax behaviour remain unchanged.
- GST billing remains optional. This feature must support both GST and non-GST bills.
- Do not introduce IGST presentation or alter tax scope as part of this image-upload feature.
- Any extracted HSN/GST value must be reviewed and validated under the existing tax workflow before use.

## 9. Multiple uploads and line-item behaviour

A single invoice may contain many item captures. The implementation must retain a stable capture ID per uploaded image.

### Initial behaviour

- One successful image capture creates one physical-item review record.
- The cashier adds each reviewed capture independently.
- Each record keeps its own image, extracted fields, edits, product match and unit identifiers.
- A failed or rejected capture does not block other items.
- The cashier can remove a capture or retake its photo without clearing the rest of the invoice.

### Repeated units of the same product

For example, a customer buys two identical refrigerators with different serial numbers:
- Two photos are uploaded.
- Both match the same catalogue product.
- Each unit has its own serial and photo/capture reference.
- Invoice quantity may be consolidated to two only when product, selling price, discount and tax treatment are the same and both serials remain associated with the resulting line.
- If values differ, preserve separate bill lines.

Do not deduplicate solely because the model or barcode is the same. Two units of the same model may have different serial numbers and are two physical items.

### Duplicate and conflict handling

- Warn or block when a serial/IMEI is already recorded for another unit or appears in a previous sale, according to existing product tracking and duplicate-identifier rules.
- Warn when two uploads in the same draft produce the same serial/IMEI.
- If multiple barcodes are read, show each with its detected type and ask the user which one should be saved as the product barcode alias.
- A duplicate barcode linked to a different product must not be silently reassigned.
- Do not reject an entire bill because one item's label is unreadable; allow manual entry for that item.

## 10. Proposed technical design

### 10.1 Component boundaries

- **Frontend:** add a focused invoice item-photo capture component used by `GenerateInvoice.tsx`. Keep image state and item review isolated from the broader invoice form.
- **API:** authenticated endpoints for upload, processing status, result retrieval, user correction/confirmation and attachment to the final sale.
- **Application service:** an item-tag extraction and matching coordinator that calls the central AI Gateway and existing catalogue/barcode services.
- **AI Gateway:** chooses a compatible vision/OCR provider, enforces rate limits and budgets, and records usage. Feature code must not know API keys or provider SDKs.
- **Persistence:** tenant-scoped capture metadata, extraction result, image/document reference, processing status and eventual sale-line link.
- **Catalogue:** use existing product attributes/specifications, MRP, brand/model and barcode alias capabilities; only persist confirmed reusable values.
- **Sales:** map confirmed unit identifiers to the appropriate sale line using the current sales command; preserve the existing idempotent posting path.

### 10.2 Request lifecycle

Suggested logical lifecycle:

```text
Invoice screen
   |
   | capture/select one or many images
   v
Authenticated upload API
   |
   | validate file and tenant ownership
   v
Persist temporary item-capture record + secure image reference
   |
   v
Queue / bounded processing
   |
   +--> deterministic barcode decoder
   +--> OCR / AI Gateway vision extraction
   +--> field validation and normalisation
   +--> local catalogue/barcode matching
   |
   v
Structured extraction result + warnings
   |
   v
Cashier review / correction
   |
   +--> Add to invoice draft
   +--> Retake / retry
   +--> Remove / ignore
   |
   v
Existing invoice review and confirmation
   |
   v
POST /sales (idempotent)
   |
   +--> link capture evidence to sale line
   +--> persist confirmed product metadata / barcode aliases
   +--> existing sales posting and invoice generation
```

Processing can be asynchronous so the cashier can continue scanning the next item. The UI should poll or receive status updates; implementation should follow existing job/hosting patterns rather than introducing an unnecessary queue service for the pilot.

### 10.3 API contract (illustrative)

Exact endpoint names and DTOs must follow repository conventions. A possible shape:

- `POST /sales/item-captures` — create an upload/capture record for the authenticated tenant and current invoice draft.
- `POST /sales/item-captures/{captureId}/image` — upload the image securely.
- `POST /sales/item-captures/{captureId}/process` — enqueue or start extraction.
- `GET /sales/item-captures/{captureId}` — retrieve status, extracted fields, match candidates and warnings.
- `PUT /sales/item-captures/{captureId}/review` — save cashier corrections and confirmed product match.
- `POST /sales/item-captures/{captureId}/retry` — retry extraction where safe.
- Sale request line(s) include the reviewed capture IDs / unit identifiers needed to attach evidence to the posted sale line.

The backend must verify that every capture belongs to the current tenant, authenticated user/session and active draft context. A client must never be able to attach another tenant's capture ID to a sale.

These routes are proposals, not claims that endpoints already exist.

### 10.4 Persistence and file storage

Before adding tables, inspect the current migration chain and reuse suitable existing document-storage primitives if present. The current architecture catalogue already describes a tenant-owned stored-document concept, and supplier invoice extraction currently stores its original invoice file separately. Do not duplicate an existing storage abstraction without a reason.

The logical data that must be persisted includes:
- Capture ID, tenant ID, creator, draft/session ID, location context and timestamps.
- Secure storage object/document reference, MIME type, file size and content hash.
- Processing status, retry count and normalised error category.
- Structured extracted fields, source text, field provenance and confidence/review state.
- Selected catalogue product ID and any user corrections.
- Final sale ID / sale-line reference after posting.
- Retention/deletion state.

Store image bytes in the approved private document/object-storage mechanism where available. If the current platform requires database-backed storage for the pilot, follow its existing document pattern and document the size/retention implications. Never store base64 image data in browser localStorage, source control or application logs.

The current invoice screen holds draft bills in browser localStorage. The implementation must not place image bytes in that localStorage payload. Persist captures server-side and keep only safe capture IDs/status references in a held-bill record; when a held bill is resumed, rehydrate the capture state. Or, if the capture cannot be resumed safely, show that clearly and allow the user to re-upload rather than silently losing an image.

### 10.5 Product-level vs unit-level persistence

- Reuse tenant product fields for confirmed product-level values (brand, model, attributes/specs, MRP, HSN/GST metadata where valid).
- Reuse `catalog.product_barcode` and the existing barcode service for confirmed, validated product barcode aliases.
- Reuse the current sale-line serial representation where it is sufficient and compatible with product tracking rules.
- Keep unique serial/IMEI values unit-specific. Do not add them to `catalog.product.specs` or store them as generic catalogue attributes.
- Preserve a link from each sale-line unit identifier to its tag capture record.
- If the current serial array cannot represent typed identifiers or trace an identifier through stock receipt, sale, return and transfer, extend the product-tracking design deliberately. Align with `docs/architecture/product-tracking-extensibility.md`; do not create a parallel serial inventory model inside this feature.

### 10.6 Image and extraction validation

Initial limits should be configurable and aligned with the existing file-processing infrastructure. At minimum:
- Allow only supported raster image types for the first release (JPEG, PNG, WebP; HEIC support only after explicit compatibility testing).
- Validate MIME type against file signatures; do not trust the browser-supplied content type or extension.
- Enforce maximum bytes, pixel dimensions and image count per invoice.
- Reject empty, corrupt, unsupported or oversized images with a useful message.
- Correct orientation and optionally resize/compress oversized images without making small tag text unreadable.
- Remove unnecessary EXIF/GPS metadata before external processing/storage where appropriate.
- Use short-lived upload permissions or authenticated backend upload; images must be private.
- Protect against duplicate upload/retry creating duplicate capture records.
- Do not put image bytes, full OCR content or secrets in logs.

The first pilot should choose conservative limits after testing actual mobile-camera images and AI provider payload constraints.

## 11. Integration with the central AI Gateway

The item-tag feature is a consumer of the gateway, not a gateway-specific implementation.

The feature should submit a task such as `ProductTagExtraction` with:
- Image content reference or securely streamed image bytes.
- Expected schema / supported field definitions.
- Language/label context if needed.
- Strict instruction to use null/unknown for unreadable fields and never invent a value.
- Request/correlation ID and tenant context supplied by trusted backend code.

The gateway selects an image-capable model/provider that is enabled for the data classification and budget. The extraction coordinator validates the structured output after the gateway returns it.

Required safeguards:
- Never use a text-only model for image extraction.
- Do not send the same photo to multiple providers by default. Use the configured fallback only after a retryable failure or invalid response and within the budget.
- Treat image text as untrusted data; ignore any instructions printed on a tag.
- Keep model/provider/model-version and token/image usage where available.
- Enforce per-shop and global AI budget before a paid fallback.
- If all vision providers fail or the budget is exhausted, retain the upload and allow manual entry.
- Do not block manual invoicing when AI is unavailable.

## 12. Security, privacy and audit

- Authenticate every upload, processing, status, review and attachment endpoint.
- Resolve tenant and user context from the authenticated server-side identity, not from arbitrary client-supplied IDs.
- Enforce tenant isolation for capture records and image retrieval.
- Check authorization for the selected location and sales operation.
- Use private storage and short-lived access mechanisms; never public image URLs.
- Restrict access to the image and extracted identifiers to authorised business users.
- Avoid retaining customer/personal content that is not needed for item identification.
- Audit creation, review/edit, removal, catalogue enrichment and final sale attachment.
- Do not log serial numbers, IMEIs, full OCR payloads or image content in application logs.
- Define a configurable retention policy for raw tag photos. Retain confirmed structured data according to catalogue/transaction needs, but do not keep raw images indefinitely by default.
- Ensure deletion/retention rules do not remove evidence still required by a posted sale or an applicable legal/business record policy.

## 13. Failure and recovery behaviour

| Failure | Expected behaviour |
|---|---|
| Blurry / glare-heavy photo | Mark Needs attention; ask user to retake or enter manually |
| Barcode not decoded | Continue OCR/vision; show any text-read candidate as unverified |
| OCR cannot read serial | Leave serial empty; user can type it |
| Product match ambiguous | Show candidates; require cashier selection |
| Product not found | Offer manual catalogue search or draft product creation |
| Duplicate serial/IMEI | Show warning/block according to tracking policy; do not silently accept |
| Barcode already belongs to another product | Do not remap; ask user to investigate |
| AI provider rate limited / unavailable | Retry with gateway policy, then approved fallback; preserve upload |
| Gateway budget exhausted | Stop paid AI attempts and offer manual recovery |
| One photo fails in a multi-upload | Keep other successful captures; let cashier retry/remove failed one |
| Browser closes / bill held | Rehydrate server capture references where possible; never rely on localStorage image bytes |
| Sale posting fails | Keep the invoice draft and captures available; use existing idempotency rules on retry |
| User removes an item | Detach/mark capture unused; apply the configured temporary-image cleanup policy |

## 14. Pilot implementation phases

### Phase 0 — Confirm data and storage fit

- Re-check current `GenerateInvoice.tsx`, sales API contract, product/barcode API, migrations, serial validation and document-storage capabilities.
- Decide how to link a capture to a server-side invoice draft context and then to a posted sale line.
- Confirm raw image retention, upload-size and per-invoice image-count settings.
- Confirm provider data-handling eligibility for real pilot-shop photos.
- Add test fixtures using synthetic product tags (never real customer/pilot data in source control).

**Exit:** Repository-specific design and migration proposal are agreed; no duplicate storage or serial-tracking model is introduced.

### Phase 1 — Upload and review vertical slice

- Add camera/gallery upload to Generate Invoice.
- Add authenticated upload/capture endpoint and private persistence.
- Add independent per-image processing states.
- Implement deterministic barcode decode and one vision extraction provider through the AI Gateway.
- Add structured result schema, validation and editable review card.
- Keep manual entry and current barcode scanner working.
- Add retry, retake, remove and manual fallback.

**Exit:** One image can be securely uploaded, extracted, reviewed and added to an invoice draft without posting a sale.

### Phase 2 — Multiple items and catalogue enrichment

- Support multiple item captures in one invoice.
- Add exact barcode/model matching against local catalogue.
- Add possible-match/manual selection flow.
- Add confirmed barcode alias and catalogue attribute enrichment.
- Preserve separate unit serial/IMEI capture and duplicate checks.
- Ensure repeated units of the same product keep every physical identifier and image association.

**Exit:** A multi-item bill can be prepared from multiple tag photos with correct per-unit identifier mapping.

### Phase 3 — Sale attachment and pilot hardening

- Attach confirmed capture records to the final sale lines safely and transactionally.
- Reconcile capture IDs and serial/IMEI count with line quantities and tracking policy.
- Add held-bill resume behaviour for server-side capture references.
- Add audit, usage/cost metrics, retention cleanup and operational visibility.
- Add integration, security, failure, mobile and end-to-end tests.
- Verify that invoice printing and WhatsApp invoice delivery continue to work as before.

**Exit:** The cashier can complete a sale with photo-captured items, and the posted sale retains correct identifiers and evidence references.

## 15. Acceptance criteria

### User experience
- [ ] Photo upload is available inside Generate Invoice.
- [ ] Camera capture and gallery selection work on supported mobile browsers.
- [ ] More than one photo can be added to the same invoice.
- [ ] Every photo has an independent status and review card.
- [ ] A failed photo does not discard other uploads or the bill.
- [ ] User can retake, retry, edit, remove or manually enter an item.
- [ ] Manual item entry and existing barcode scanner remain functional.
- [ ] Uploaded photos do not automatically post a sale.

### Extraction and matching
- [ ] Structured output supports product-level and unit-level fields separately.
- [ ] Barcode decoder output is preferred over an LLM's guessed barcode string.
- [ ] GTIN values use existing normalisation/check-digit rules.
- [ ] Exact local barcode and model matches are ranked above fuzzy matches.
- [ ] Ambiguous matches require user choice.
- [ ] Missing or unreadable values remain null/empty rather than guessed.
- [ ] Field corrections are retained and can feed confirmed catalogue enrichment.
- [ ] Serial/IMEI values are not stored as product-level catalogue metadata.

### Invoice and catalogue integrity
- [ ] Product match is mapped to the correct sale line.
- [ ] Per-unit serial/IMEI values remain associated with the corresponding item.
- [ ] Same-model multi-unit uploads preserve distinct serials and capture references.
- [ ] MRP does not silently become selling price.
- [ ] Discount and tax are not guessed from tag images.
- [ ] GST and non-GST billing continue to work unchanged.
- [ ] Product/barcode enrichment occurs only after confirmation.
- [ ] Sale posting uses the existing idempotent sales workflow.

### Security and operations
- [ ] All upload and capture endpoints are authenticated and tenant-scoped.
- [ ] Cross-tenant image/capture access is rejected.
- [ ] File size, type, signature and dimensions are validated.
- [ ] Images are private and not exposed in logs or browser localStorage.
- [ ] Gateway usage and cost are recorded by feature/tenant.
- [ ] Provider outage or budget exhaustion falls back to manual entry.
- [ ] Retention and cleanup are tested.
- [ ] Audit trail records user corrections and final sale attachment.

## 16. Test scenarios required

1. One clear tag image with a readable barcode and serial.
2. One tag with serial readable but barcode not decodable.
3. One tag with barcode readable but serial blurred.
4. A tag containing model, MRP, HSN and warranty text.
5. A mobile photo rotated by EXIF orientation.
6. A very large, corrupt, unsupported or spoofed-MIME image.
7. Three different item photos in one bill.
8. Two identical product models with different serial numbers.
9. Two uploads of the same photo / duplicate serial within one draft.
10. A serial already present in prior sales.
11. A barcode that is already mapped to another tenant product.
12. Ambiguous brand/model match and no-match case.
13. One extraction failure while other uploads succeed.
14. Provider 429, timeout, malformed structured output and all-provider outage.
15. AI budget exhausted; manual billing remains available.
16. Held bill / browser refresh / resumed bill with uploaded captures.
17. Sale post retry using the same idempotency key.
18. Posted invoice, print output and existing WhatsApp invoice delivery remain correct.
19. Tenant A cannot view, edit, attach or download Tenant B's capture.
20. User removes a captured item before posting; no incorrect sale-line attachment remains.

## 17. Decisions and guardrails

1. **One photo = one physical item** in the first release. Multi-page supplier invoices are a separate workflow.
2. Multiple uploads per invoice are a core requirement, not a future enhancement.
3. Each capture is independently processed, reviewable and recoverable.
4. The cashier must confirm extracted values and product match before adding them to the bill.
5. No automatic sale posting or automatic price/tax selection from a tag image.
6. Product attributes and barcodes can enrich the tenant catalogue only after confirmation.
7. Serial/IMEI/service-tag values remain unit-level identifiers.
8. Use the central AI Gateway for provider selection, credential rotation, rate limits, fallback and budget control.
9. Reuse current barcode rules, product catalogue, sales serial fields and posting path where they fit.
10. Keep existing manual billing and barcode scanning fully supported.
11. Preserve optional GST/non-GST billing behaviour and do not expand tax scope here.
12. This document specifies a feature; no code, database migration, provider key, storage setting or production configuration has been changed by creating it.

## 18. Repository touchpoints inspected

The following current files inform this design. They are implementation references, not a final change list:

- `GenerateInvoice.tsx` — current invoice UI, manual serial entry, barcode scan, invoice draft and held-bill behaviour.
- `src/Rbos.Api/SalesEndpoints.cs` — authenticated sales routes and current sale request.
- `src/Rbos.Api/Application/SalesPosting.cs` — sales service, sale detail and sale-line serial persistence/read path.
- `src/Rbos.Api/BarcodeEndpoints.cs` — product barcode lookup, barcode aliases and serial-tracking routes.
- `src/Rbos.Api/Application/BarcodeService.cs` — barcode normalisation, GTIN checks, tenant-scoped lookup and serial-tracking rules.
- `src/Rbos.Api/CatalogueEndpoints.cs` — product catalogue fields, barcode suggestions and product import.
- `src/Rbos.Api/Application/InvoiceExtractionService.cs` — existing supplier purchase-invoice photo/PDF extraction; this is not the same as per-item tag capture.
- `docs/architecture/product-tracking-extensibility.md` — intended direction for tracked physical units, identifiers and warranty references.
- `docs/architecture/ai-gateway-pilot-implementation-plan.md` — central provider-neutral AI integration and cost/rate-limit controls.

Before implementation, reconcile any differences between historical design documents and the current migration/code state. The implementation must follow current tests and migrations as source of truth.

## 19. Related documents

- Central AI Gateway: `docs/architecture/ai-gateway-pilot-implementation-plan.md`
- Product tracking extensibility: `docs/architecture/product-tracking-extensibility.md`
- Domain model: `docs/architecture/domain-model-erd.md`
- Current implementation status: `docs/current-implementation-status.md`



## 20. PR B implementation reconciliation — 18 November 2026

This section records the implementation on `feat/invoice-item-tag-photo-capture` against the original requirements above. The implementation follows the post-PR-A code on `main`; PR #30 supplied this plan and PR #31 supplied adjacent authorisation/offline constraints. This is a code-status record, not a claim that builds or tests have passed.

### 20.1 What existed before PR B

- `GenerateInvoice.tsx` already supported manual product entry, a live barcode scanner and manual serial/IMEI text.
- Catalogue barcodes and serial-unit records already had tenant-scoped persistence and barcode normalisation.
- Supplier invoice documents already had a separate upload/extraction workflow and PostgreSQL binary storage. That workflow is not reused for an item tag.
- PR A's Central AI Gateway already supported image understanding, structured output, credential eligibility, cost budgeting and usage ledger accounting.
- PR #40/#41's encrypted IndexedDB sales outbox already supported offline JSON sale drafts and idempotent server posting. It did not persist image bytes.

No invoice item-tag photo capture, per-photo review state, or sale-line evidence association was present before PR B.

### 20.2 Requirement-to-code reconciliation

| Requirement | PR B implementation |
|---|---|
| Camera and gallery from Generate Invoice | Camera-oriented file input (`capture=environment`) and multi-select gallery input inside `ItemTagCapture`. |
| Independent photo per physical item | Each selected file is uploaded as an independent capture with its own idempotency key. The cashier confirms and adds one physical item at a time. |
| Validation and preview | JPEG, PNG and WebP only; 8 MiB limit; server verifies file signature and size; the browser shows a preview and upload/retry/error state. |
| Deterministic barcode read | Existing ZXing dependency decodes EAN-13, EAN-8, UPC-A, UPC-E and Code 128 locally. QR/Data Matrix content is not assumed to be a product barcode. Known local barcodes are resolved through the existing tenant-scoped lookup API. |
| Vision/OCR extraction | Structured extraction is requested through PR A's `AiGateway` using the `InvoiceItemTagExtraction` feature. No direct provider call or new AI SDK is added. |
| Catalogue suggestion | Exact local barcode, unique brand/model, then unique exact product-name match may suggest a product. Suggestions require cashier confirmation. Ambiguous matches are not auto-selected. |
| Human review | The review card lets the cashier select a catalogue product (or explicitly create one online) and review/edit product name, brand, model, barcode, serial, IMEI 1/2, MRP and HSN. MRP is reference-only. |
| Sale-line association | Optional `captureIds` are accepted on sale lines. Sale posting locks and validates every capture and attaches it to the matching product line in the same transaction as the sale. Existing sale requests without capture IDs remain valid. |
| Evidence retrieval | Authenticated metadata and image routes return tenant-owned data only. Image responses are marked private/no-store and nosniff. Sale detail and immutable invoice snapshot retain capture IDs, not image bytes. |
| Remove/replace | Unattached captures can be removed; retaking is a new independent capture. Attached evidence cannot be deleted through the cashier API. |
| Orphan handling | Unattached captures expire after 30 days and are purged opportunistically on a subsequent upload. Attached evidence is excluded from that purge. |
| Multi-tenant isolation | Composite tenant keys/foreign keys, RLS ENABLE+FORCE, active-membership policy, server-side tenant resolution and role-filtered routes. |
| Offline | Photo capture/upload is disabled while offline. The offline sales payload contains capture IDs only when the photo was already uploaded and reviewed online. No image bytes are placed in localStorage or the offline outbox. |

### 20.3 Data flow and contracts

1. The browser validates the chosen file, creates a local preview, and attempts local retail-barcode decoding.
2. `POST /api/sales/item-tag-captures` accepts the raw image body, its MIME type and a required `Idempotency-Key`. The server calculates SHA-256 and persists the private image before AI processing.
3. The Central AI Gateway may extract tag fields. The API still returns a reviewable capture when AI is unavailable; the user can enter the values manually.
4. The cashier confirms a catalogue product and the extracted identifiers through `PUT /api/sales/item-tag-captures/{id}/review`.
5. The normal `POST /api/sales` includes the capture ID in the relevant line's optional `captureIds` array. The sale transaction validates ownership, review state and product association, then attaches the capture atomically.
6. `GET /api/sales/item-tag-captures/{id}` returns metadata; `GET /api/sales/item-tag-captures/{id}/image` streams the protected image; `DELETE` is limited to unattached captures.

### 20.4 Database and storage decision

Migration `049_invoice_item_tag_captures.sql` adds `sales.item_tag_capture`. Image bytes use PostgreSQL `bytea`, consistent with the existing supplier-invoice file-storage pattern. This avoids a new paid service, bucket, SDK or secret. The row records MIME type, byte count, SHA-256, extraction JSON, reviewed product/unit identifiers, creator/reviewer and optional sale-line association. Tenant RLS and composite foreign keys are applied in the migration. The table is separate from `procurement.invoice_file`.

### 20.5 PR B validation added

- `tests/Rbos.UnitTests/SaleLineCaptureRulesTests.cs`: optional-reference compatibility, duplicate/empty IDs, unit quantity and count rules.
- `tests/Rbos.IntegrationTests/ItemTagCaptureTests.cs`: upload, idempotency conflict/replay, MIME/signature/size validation, private image retrieval, tenant isolation, review/delete, sale attachment and unreviewed-capture rejection.
- `web/src/ItemTagCapture.test.tsx`: camera/gallery controls, image upload, review/add, unsupported image feedback and offline-disabled controls.
- `ApiAuthorizationTests` continues to inventory all registered API routes against the fail-closed access registry.

GitHub Actions run [#566](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44330825072) passed on the PR B head: .NET Release build, migrations 001–049, 126 unit tests, 160 integration tests, frontend TypeScript, frontend tests, frontend production build, dependency audit and deployment-container build without pushing an image. One pre-existing CA1416 platform-compatibility warning remains in `PdfPageReader.cs`. These results are CI evidence; no local execution is claimed.

### 20.6 Explicit limitations / decisions still open

- No offline photo byte queue, deferred upload, or automatic photo retry after reconnect is implemented. This is intentionally not claimed; offline billing without photos remains available.
- Upload progress is an indeterminate busy state, not a percentage.
- AI is optional and remains unavailable by default because the Central AI Gateway and provider switches are disabled in checked-in configuration. Enabling it requires an explicitly verified eligible image-capable credential; paid providers remain disabled by default. Manual review is the recovery path.
- Local barcode decoding intentionally excludes QR and Data Matrix payloads from automatic product-barcode mapping.
- Extraction confidence is not presented as a calibrated score. Warranty, specifications and other printed text are visible in a read-only “More information” section; they are not yet editable catalogue fields.
- Unattached-image cleanup is opportunistic rather than a scheduled job. Attached evidence has no automated retention/deletion policy in this change; define that policy before production-scale retention.
- The feature records evidence and unit identifiers but does not enable inventory serial tracking or alter stock valuation/tracking policy.
