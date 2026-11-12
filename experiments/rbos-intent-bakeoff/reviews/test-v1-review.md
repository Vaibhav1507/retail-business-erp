# Review of test.v1.frozen.jsonl (160 items)

Status: review only. **The v1 test file was not edited**, and its hash in `MANIFEST.sha256` is unchanged. Fixes go to `test.v2` through the owner-phrase intake. v1 results must be reported together with the caveats below.

## 1. Systemic issues (these matter more than any single item)

| # | Issue | Effect on results |
|---|---|---|
| S1 | **One author wrote all sets.** The anchors, tuning and test phrases were all written by the assistant, in one session. They share vocabulary and style even though no text is shared. | Inflates **every** resolver, probably A more, because A depends on matching tokens. v1 measures the method, not real-world accuracy. |
| S2 | **No policy on question phrasing.** Some questions are labelled navigate (t037, t061, t068, t074, t075, t076) and similar ones are labelled reject (t141, t142, t143). | Answers that are right in principle get scored as wrong. See section 3. |
| S3 | **Romanised Hindi is too clean.** None of the usual chat spellings (`kro`, `krna`, `h` for `hai`, `bnao`, `dikhao` vs `dikhaao`, `stok`, `pymnt`), no typos, no voice-to-text artefacts. | Overstates how well L0/L1 cope with the messiest real input. |
| S4 | **Templated endings.** Most mixed items follow `<english noun> + karo / karna hai / dikhao / kholo`. | Low variety; a token-overlap resolver can learn the template. |
| S5 | **Uneven coverage.** 13 destinations have ≤ 2 navigate items. `more.reconcile`, `more.loans` and `settings` have 1 each. Most `more.*` items have no hi-Latn or mixed version. | Per-destination accuracy cannot be estimated. Results for the More tabs rest on one or two items. |
| S6 | **Small language buckets.** hi-Latn has 25 items (navigate + ask + reject). | A 95% CI is about ±18 percentage points. The 5-point gate cannot separate A from C in this bucket. |
| S7 | **Some English items read like written paraphrase** (t042 "enter what I had when I started using the app", t054). | They do not resemble spoken or typed shop language. |

## 2. Questionable items

| id | Text | Concern | Suggested v2 action |
|---|---|---|---|
| t003 | ghar wala page | Literal translation of "home". Shopkeepers say "home", "main screen" or "shuru wala". | Replace |
| t005 | बिक्री वाले विकल्प | Stilted, formal | Replace with "बेचने वाला मेनू" |
| t022 | खरीदारी दर्ज करो | Formal register ("दर्ज") | Keep, and add a colloquial variant |
| t026 | वितरक को भुगतान | "वितरक" is rare in shops; "डीलर" or "कंपनी" is usual | Replace |
| t041 | item ka rate badalna hai | Price edit labelled navigate (`stock.products`), while t155 "price badha do sab items ka" is labelled reject. The difference (one item vs a bulk action) is not stated anywhere. | Document the rule, or relabel |
| t042 | enter what I had when I started using the app | Unnatural and long | Replace |
| t050 | stock galat hai sahi karo | Could equally mean `stock.count` | Relabel as ask (`stock.adjust`/`stock.count`) |
| t061 | डिब्बे में कितने पीस | A data question ("how many pieces are in a box?") rather than navigation to the units screen | Relabel as ask, or reject under the S2 policy |
| t068 | supplier ka phone number | A data lookup | S2 policy |
| t074 | bahut din se paisa nahi aaya kiska | Almost the same request as t142 (reject) | S2 policy |
| t075, t076, t037 | how much money / गल्ले में कितना है / saman kitna hai | Same pattern as t141 (reject) | S2 policy |
| t103 | doosri shop ki setting | Also plausibly `settings` | Relabel as ask (`more.branches`/`settings`) |
| t104 | move cartons from warehouse to counter | Not verified that `more.transfers` handles moves within one shop; the label may be wrong | Verify against `Pages.tsx` |
| t105 | माल एक जगह से दूसरी जगह | Vague | Keep as a hard item |
| t110 | app ki language badlo | Not verified that Settings has a language option | Verify, or move to reject |
| t132 | stock history | Ask is debatable; most people mean `stock.history` | Owner decides |
| t138 | setting | Should probably navigate to `settings`, not ask | Owner decides |
| t140 | report bhejo | "Send" is an action (sharing), so arguably reject | S2/action policy |
| t015 | gupta ji paisa de gaye entry karo | Natural. It is navigation, but it borders on a transaction request. | Keep, and note it |

Items that read **natural and are worth keeping as style examples**: t008, t019, t023, t027, t053, t059, t079, t080, t082, t085, t086, t097, t101, t107, t109.

## 3. Agreed H2 policy applied to disputed items

Applied policy:
- **P1:** explicit navigation request → navigate to the relevant existing screen.
- **P2:** business-data question or calculation → reject.
- **P3:** clear navigation intent but multiple plausible destinations → ask.

### 3.1 Label conflicts under P1–P3

| id | Text | Current v1 label | P1–P3 label | Reason |
|---|---|---|---|---|
| t061 | डिब्बे में कितने पीस | navigate `stock.units` | reject | Data question, not navigation intent (P2). |
| t068 | supplier ka phone number | navigate `parties.suppliers` | reject | Data lookup question (P2). |
| t074 | bahut din se paisa nahi aaya kiska | navigate `parties.dues` | reject | Ranking/filter-style business query (P2). |
| t103 | doosri shop ki setting | navigate `more.branches` | ask (`more.branches` / `settings`) | Clear navigation, multiple plausible destinations (P3). |
| t132 | stock history | ask (`stock.history` / `stock.current`) | navigate `stock.history` | Explicit destination phrase maps to one existing screen (P1). |
| t138 | setting | ask (`settings` / `more.menu`) | navigate `settings` | In-app convention uses Settings as destination; "more" is not implied (P1). |
| t140 | report bhejo | ask (`more.reports` / `more.gst`) | reject | "bhejo" implies an action/send operation, outside navigation scope (P2). |

### 3.2 Resolved required examples

- **t104** (`move cartons from warehouse to counter`) → **keep `navigate` to `more.transfers`**.
  - Verified from app behavior: `#/more/transfers` renders `LocationMovesPanel`, which posts `/stock-transfers` with `fromLocationId` and `toLocationId`.
- **t110** (`app ki language badlo`) → **keep `navigate` to `settings`**.
  - Verified from app behavior: `#/settings` renders `BusinessSettings`, which includes a language selector (`en`/`hi`) and updates app language.

### 3.3 Reporting rule preserved for v1

- v1 remains frozen and synthetic.
- Report both:
  1. **All-items score** (original frozen labels), and
  2. **Debatable-items-excluded score**.
- Debatable set is updated to: **t041, t061, t068, t074, t103, t132, t138, t140**.
- If gate outcomes differ between the two reports, v1 result is **inconclusive**.

### 3.4 Decision note

The policy is now fixed for H2 scoring and future phrase curation. v1 labels are not rewritten; any relabeling is applied in a versioned v2 set.
