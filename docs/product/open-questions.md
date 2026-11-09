# Open questions

★ = blocks the next engineering task (Slice 1). Others block later slices.

| ID | Question | Blocks | Owner | Notes |
|---|---|---|---|---|
| Q-1 | May NuGet MCP, Context7 and Microsoft Learn connectors be used for public docs, or stay off entirely? | — (default: off) | Product owner | Treated as off. Not blocking: public docs can be read through an ordinary browser. |
| Q-2 | Do user-level NuGet sources include a company feed? | — | Product owner | **RESOLVED BY POLICY; IMPLEMENTATION PENDING.** Project-level `NuGet.Config` with `<clear/>` and only `https://api.nuget.org/v3/index.json`, used explicitly for restore, created before any project creation, package lookup or restore in Slice 1; effective sources reported before restore. User-level sources are deliberately not inspected. See `decisions/product-owner-decisions.md`. |
| Q-3 | Shop GST registration type (regular / composition / unregistered)? | Slice 5 (invoice type) | Product owner via observation | Tax invoice vs bill of supply. Not assumed. |
| Q-4 | How routine are credit sales and how many credit customers? | Slice 5 UX | Observation V1/V4 | Count only, no names. |
| Q-5 | Are serials/IMEIs recorded today, for which categories? | Phase 1B priority | Observation V1 | |
| Q-6 | Billing device and printer model/width? | Slice 5 print | Observation V1 | |
| Q-7 | Is a CA available to review accounting and tax policy? | Slice 3 | Product owner | See `decisions/professional-review-decisions.md`. |
| Q-8 | Freight on purchases: capitalise or expense? | Slice 4 | CA | Proposal: capitalise direct freight. |
| Q-9 | Slice 1: post opening stock financially (minimal journal) or quantity-only? | — | Product owner | **RESOLVED** by PD-12 (DECIDED): financially posted costed opening stock with a minimal Inventory / Opening Balance Equity journal. |
| Q-10 ★ | PostgreSQL via Docker or native install? | Slice 1 | Product owner | PD-06 (OPEN): Docker only if personally controlled/permitted, not company-connected, no company registry, licence acceptable; otherwise native personal install. |
| Q-11 | Price entry default: tax-inclusive or tax-exclusive? | Slice 5 | Observation V1 | A-18. |
| Q-12 | Exact statutory invoice-numbering rules (series, length, FY reset)? | Slice 5 | Official source + CA | `architecture/document-numbering.md`. |
| Q-13 | Rounding policy for invoice totals and tax lines? | Slice 5 | CA | PR-05. |
| Q-14 | Should returns accept items without a source invoice? | Slice 5 | Product owner + CA | 1A proposal: allowed only for Owner, with reason, at current WAC. |
