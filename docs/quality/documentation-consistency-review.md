> **Current-state addendum — 2026-11-14:** The Phase 0 consistency checks below remain historical and are not a current code audit. PR #24 merged the three-level voice navigation implementation; PR #22 closed without merge; PR #25 merged the quick-action wrapping CSS fix; duplicate PR #26 closed without merge. For the current implementation/provider/PR snapshot, see `docs/current-implementation-status.md`. For the scope of the latest documentation reconciliation and remaining domain-document review debt, see `docs/quality/documentation-freshness-review.md`.

> **Historical review:** The checks below describe the Phase 0 documentation baseline and are not a current implementation audit. See `docs/quality/documentation-freshness-review.md` for the latest targeted review against the merged repository.

# Documentation consistency review

Review date: Phase 0 finalisation. Method: read-through plus text searches across `projects/retail-business-os/**/*.md` for "Accepted", "allow with warning", "gapless", "installed", "passed" and zero-cost wording.

## Results

| Check | Result |
|---|---|
| Phase 1A scope consistent | ✔ Single source `product/scope-phase-1a-1b-future.md`; roadmap slices map to it |
| Phase 1B and future scope consistent | ✔ OCR, GSTR-2B, serial/IMEI, transfers, counts, offline, printers, loans in 1B; filing/e-invoice/e-way bill in Future |
| Electronics-lite is first vertical | ✔ Scope, templates, ADR-008, validation plan |
| Shared UI consistently described | ✔ Vertical templates, ADR-009 |
| Multitenancy preserved | ✔ Shared schema + RLS everywhere; single-shop validation does not change architecture |
| Tenant A / Tenant B usage | ✔ A = synthetic electronics, B = synthetic adversarial (roadmap, security test plan, testing strategy) |
| Negative stock blocked in 1A | ✔ Scope, inventory invariants, valuation policy, PD-10. ⚠ Discovery report §inventory still mentions an "Allow with warning" option — **superseded** |
| Unknown cost never silently zero | ✔ Stock setup, unknown-opening-cost, worked example 10, inventory I-13 |
| WAC terminology | ✔ Q/V/W notation, "moving weighted average" throughout |
| Financial report definitions | ✔ Gross / operating / PBT / net defined once; other files reference it |
| No unconditional gapless numbering | ✔ Numbering doc, ADR-016, PD-09. ⚠ Discovery report (ADR-016 title, D-10, POS description) says "gapless" — **superseded** |
| GST profile not assumed | ✔ "Not yet known" profile; illustrative 18% always labelled unverified |
| Company separation in guardrails and dependency docs | ✔ |
| No AI posting authority | ✔ Guardrails, ADR-020, threat T30 |
| All ADRs Proposed | ✔ at Phase 0. **Superseded 2026-11-07:** ADR-001 to 021 accepted by owner; see docs/adr/index.md |
| No implementation implied | ✔ |
| No tests claimed passed | ✔ |
| No dependency described as installed | ✔ (The discovery report records pre-existing *machine tools* such as the .NET SDK, which is environment fact, not project dependency) |
| No database described as running | ✔ |

## Conflicts and resolution

The historical `00-discovery-report.md` contains two positions refined by the product-owner review:
1. Negative stock "Allow with warning" option → replaced by **Block in 1A**.
2. "Gapless" invoice numbering → replaced by **controlled, unique, sequential, no reuse; no unconditional gapless promise**.

Resolution: the report is kept unchanged as a historical record; the README states that later documents refine it. Where they differ, the finalised documents prevail.

## Addendum: final-readiness corrections

| Check | Result |
|---|---|
| Original Markdown files valid UTF-8 | 44 of 44 valid, no BOM, no mojibake patterns found. The mojibake in the review bundle came from the bundling/reading step (Windows PowerShell 5.1 `Get-Content` without `-Encoding UTF8`), not the originals. No file was rewritten for encoding |
| UUID wording | `database-strategy.md` and `threat-model.md` corrected: UUIDv7 is time-ordered, not unguessable; identifiers are never an authorization boundary |
| Tenant bootstrap, safe pre-tenant lookup, audit separation | New `architecture/tenant-bootstrap-and-audit.md`; `tenant-isolation-rls.md` points to it and the unsafe `user_id` function is withdrawn |
| Database terminology | `database-strategy.md`: shared database, module schemas, shared tenant tables, `tenant_id` + RLS; not schema-per-tenant |
| Cost correction, P-25, examples 12-13 | Labelled illustrative, not implementation-ready, deferred pending CA review |
| Rebuild vs independent verification | Distinguished in `transaction-and-projection-model.md`; verification deferred |
| Slice 1 has no GST and no tax-invoice posting | Stated in `roadmap.md` |
| Decision register | Updated. PD-06 open, PD-09 implementation deferred to Slice 5, PD-17 no real data. All PR-xx remain open; WAC is not professionally approved |
| Not re-read in this pass | `scope-phase-1a-1b-future.md`, `assumptions-register.md`, `open-questions.md` and the ADR files may still carry older wording (e.g. PD-12/Q-9 "pending", Q-10 status). ADRs remain Proposed because the register records proposed approvals, not recorded acceptance |

## Minor follow-ups (non-blocking)

- Hindi labels in `financial-report-definitions.md` are drafts requiring native-speaker review.
- Deferred documents are listed in the README; none were created as placeholders.

## Decision-closure addendum

Product-owner register updated to the DECIDED status model; Q-2 resolved by policy (implementation pending); Q-9 resolved by PD-12; PD-18 decided; PD-06 open. All 24 ADRs remain Proposed; all professional-review decisions remain Open; Slice 1 remains Proposed and unauthorized. A text scan found no stale live blockers outside the unchanged historical report (remaining matches are false positives). No implementation, package, database or test result is claimed.
