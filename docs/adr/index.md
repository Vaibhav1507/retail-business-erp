# ADR index

Implementation note: code now exists beyond Slice 4 (returns, period lock, reports, barcodes, GST profile, optional Gemini/MSG91 integrations). Gemini/MSG91 are optional, key-gated and off by default (see amendments to ADR-019 and ADR-020).

**Owner decision 2026-11-07:** ADR-001 to 021 are Accepted (008, 019 and 020 with amendments; 015 and 017 with review pending; 012, 013, 014, 016 CA reviewed). ADR-022, 023 and 024 remain Proposed. The status line in each ADR file is authoritative.

| ADR | Title | File | PO decision | CA review |
|---|---|---|---|---|
| 001 | Modular monolith | adr-001-006-platform.md | Yes | No |
| 002 | .NET 10 and ASP.NET Core | adr-001-006-platform.md | Yes | No |
| 003 | React, TypeScript and Vite | adr-001-006-platform.md | Yes | No |
| 004 | PostgreSQL | adr-001-006-platform.md | Yes | No |
| 005 | Shared schema with RLS | adr-001-006-platform.md | Yes | No |
| 006 | Tenant-context resolution | adr-001-006-platform.md | Yes | No |
| 007 | Application vs migration database roles | adr-007-012-domain.md | Yes | No |
| 008 | Electronics-lite initial vertical | adr-007-012-domain.md | Yes | No |
| 009 | Shared UI with metadata-driven fields | adr-007-012-domain.md | Yes | No |
| 010 | Product and stock separation | adr-007-012-domain.md | Yes | No |
| 011 | Movement-led inventory | adr-007-012-domain.md | Yes | No |
| 012 | Moving weighted-average valuation | adr-007-012-domain.md | Yes | **Yes** |
| 013 | Double-entry accounting | adr-013-018-finance.md | Yes | **Yes** |
| 014 | Immutable posted transactions | adr-013-018-finance.md | Yes | Yes |
| 015 | Idempotent posting | adr-013-018-finance.md | Yes | No |
| 016 | Controlled sequential document numbering | adr-013-018-finance.md | Yes | **Yes (+ official rules)** |
| 017 | Local document-storage abstraction | adr-013-018-finance.md | Yes | No |
| 018 | Database-backed jobs | adr-013-018-finance.md | Yes | No |
| 019 | Free-first OCR | adr-019-024-capabilities.md | Yes | No |
| 020 | Provider-neutral optional AI | adr-019-024-capabilities.md | Yes | No |
| 021 | Browser printing first | adr-019-024-capabilities.md | Yes | Invoice fields: Yes |
| 022 | Offline synchronisation deferred | adr-019-024-capabilities.md | Yes | No |
| 023 | Direct government integrations deferred | adr-019-024-capabilities.md | Yes | No |
| 024 | English/Hindi localisation foundation | adr-019-024-capabilities.md | Yes | No |
| 025 | Physical negative stock with provisional cost and later reconciliation | adr-025-026-flexible-sales-and-handwritten-capture.md | Owner review before production | **Yes before production** |
| 026 | Handwritten sales invoice capture as an unposted draft | adr-025-026-flexible-sales-and-handwritten-capture.md | Review source-evidence retention | Tax/accounting rules: Yes |
| 027 | Shared Document-to-Books lifecycle with separate sales/purchase posting | adr-027-document-to-books.md | Discovery direction only; implementation gates pending | Yes for purchase/cash-sale accounting changes |

Grouping into four files is deliberate for readability; each ADR keeps its own number and can be split out later without renumbering.
