# Voice and text navigation assistant: research and recommendation (H2)

Status: Three-level voice navigation is implemented on `feature/voice-provider-boundary` (PR #24, draft). Level 1 uses 35 canonical deterministic destinations; Level 2 keeps local Shruti primary with an opt-in Groq-hosted Whisper Turbo fallback; Level 3 uses Gemini Flash primary with a separate Groq Qwen text fallback when deterministic navigation cannot resolve the command. The existing assistant UI is preserved. Product/catalogue extraction uses Gemini→Groq Qwen vision; invoice extraction now has an opt-in Groq Qwen vision fallback and manual-entry recovery. Hosted voice providers and the invoice Groq fallback are disabled by default pending explicit cost-tier configuration. CI and Android/device acceptance are still outstanding. Status: 2026-11-14.

## 1. Scope

- **In scope:** move the user to an existing RBOS screen from typed text, a predefined shortcut or a spoken command in Hindi, English, Romanised Hindi or Hinglish.
- **Out of scope:** general chatbot, business-data questions, and creating/posting transactions (X1 stays deferred). A navigation-only LLM intent layer is in scope as Level 3.
- **Hard constraints:** free-first; no paid provider usage, credits, trials or extra hosting without explicit owner approval. Shruti remains local-first. Hosted Whisper and LLM calls are optional, disabled by default and subject to verified account limits. Typed input and shortcuts always work without a microphone or model.

## 2. Evidence quality

| Source | What it supports | Independence |
|---|---|---|
| Hugging Face model APIs (licence, gating), read 2026-11-11 | Licences and access conditions | Primary, factual |
| GitHub API for sherpa-onnx, whisper.cpp, cactus | Licence and recent activity | Primary, factual |
| alphacephei.com/vosk/models | Vosk Hindi model size and WER | Vendor, self-reported |
| udgamlabs/Shruti model card, comparison table | Shruti and competitor WER, size, device latency | **Vendor, self-reported**. It is the only table that measures all candidates on the same sets. Udgam Labs built Shruti, so the table needs independent replication |
| Trelis/tara model card | Tara WER on Vistaar and CS-FLEURS | Vendor, self-reported |

No independent third-party benchmark covers Shruti, Koyal and Tara together. **Every WER figure below is a claim until it is reproduced on RBOS's own command set (section 7).** Web Speech API behaviour is from general platform knowledge. Check it against MDN and the Chrome documentation before relying on it.

## 3. Candidates

WER columns come from the Shruti card's comparison table (2026-10-18/19), unless marked otherwise. Lower is better. "n/c" means not comparable.

| Option | Licence / access | Size | Hindi WER (Monsoon / FLEURS / Lahaja) | Hinglish (MUCS) | English (Svarah / Libri) | Browser / mobile | Verdict |
|---|---|---|---|---|---|---|---|
| **Shruti** (udgamlabs, 55M, Cactus Whistle base) | Apache-2.0, not gated | 29.5 MB `.cact` (4-bit); ONNX variant published; 222 MB fp32 | 20.5 / 19.9 / 26.3 | **35.2** (best in table) | 22.5 / 6.0 | WASM in browser claimed; ~1 s per sentence, 80 MB peak on Galaxy F62 (vendor) | **Strongest lightweight Hinglish candidate. Unproven independently** |
| Shruti runtime: Cactus Needle engine | Apache-2.0 | small | same model | same | same | WASM | Reject as the *runtime* for now: vendor engine, package recently renamed (`cactus-needle` to `cactus`, Needle 2 on a legacy branch). Churn risk. Prefer the ONNX export |
| Shruti runtime: ONNX via onnxruntime-web or sherpa-onnx WASM | MIT / Apache-2.0 | ONNX size not verified | same | same | same | WASM, Web Worker | **Preferred runtime**. sherpa-onnx is actively maintained (pushed 2026-11-11) |
| AI4Bharat IndicConformer Hindi (ONNX port) | MIT; original repo gated (auto) | ~482 MB fp32 | 15.3 / 11.3 / **13.2** | 64.5 | Hindi only | Possible in WASM but heavy for low-end phones | Best Hindi-accent accuracy locally. Poor for Hinglish. Too large for first load on a shop phone |
| AI4Bharat IndicConformer 600M multilingual | MIT, gated (auto) | larger | not measured here | - | - | Server-class | Reject for the browser: size |
| Koyal-hi-120M (adalat-ai) | **CC-BY-4.0** (attribution), gated | ~481 MB | **11.6 / 9.6** / 18.7 | 57.5 | Hindi only | Heavy | Best Monsoon/FLEURS Hindi. Weak Hinglish, gated, needs attribution. Reject as primary |
| Trelis Tara | Apache-2.0 | Whisper large-v3, 1.55B params (GBs) | Vistaar mean 12.06 (vendor) | CS-FLEURS 8.37 (vendor) | 6.7 / 4.6 | Needs GPU or strong CPU; not practical in a browser | Best claimed accuracy, but **reject**: would need a paid or heavy server, which breaks zero cost and offline |
| Whisper large-v3 (whisper.cpp possible) | MIT | ~3 GB fp16 | 33.3 / 28.9 / 31.2 | 79.9 | **7.4 / 2.3** | Not on shop phones | Reject: size; poor Hindi in this table |
| Whisper small / tiny / base Hinglish fine-tunes | Apache-2.0 | 151-971 MB | 57-128 | 87 (Roman) | 20-34 | whisper.cpp WASM works, but slow | Reject: high Hindi WER, hallucinations on silence (500-1,676 words invented on 100 silent clips, per the table) |
| Vosk small Hindi (vosk-model-small-hi-0.22) | Apache-2.0 | 42 MB (vendor page) | vendor: 20.9 IITM, 24.7 MUCS | 61.1 (Shruti table, 139 MB variant) | 38.5 / 30.2 | vosk-browser WASM; Android/iOS | **Mature fallback.** Key advantage: **grammar-restricted recognition** (a closed phrase list), which suits navigation better than open dictation |
| Vosk big Hindi (0.22) | Apache-2.0 | 1.5 GB | vendor: 14.9 CV, 13.1 MUCS | - | - | Server only | Reject: size |
| Browser Web Speech API | Free to call | 0 | not benchmarked | unknown | good | Chrome/Edge; Safari partial; Firefox none | **Reject as the default.** Chrome sends audio to vendor servers (not private, not offline). Availability varies by browser. Could be an opt-in extra only, behind an explicit privacy notice |
| Qwen3-ASR-0.6B Hinglish fine-tunes (community) | Varies | ~0.6B | not verified | not verified | - | Heavy | Not credible yet: low downloads, no independent results |

## 4. Recommendation

1. **Selected for H2 implementation: text and shortcuts first, then Shruti's original `.cact` model through the Cactus Needle WebAssembly engine in a Web Worker.** The latest Shruti integration guide reports that the Hindi ONNX build does not match the original engine on its Hindi evaluation sets; therefore the original engine route is preferred over the ONNX export. Shruti and the engine are Apache-2.0. The vendor's device/browser measurements are not RBOS benchmarks and must be independently validated on the target phones.
2. **Best accuracy (local, Hindi only):** IndicConformer Hindi ONNX (MIT). Koyal is better on read Hindi, but it is gated, needs CC-BY attribution and is weaker on accents. Neither handles Hinglish. Tara claims the best accuracy overall but cannot run free on the device.
3. **Best lightweight and most robust for commands:** Vosk small Hindi with a **closed grammar** of RBOS phrases. Its open-dictation WER is worse, but a grammar limits output to valid commands, which can lower wrong navigation. This needs measuring against Shruti on the RBOS command set.

**Implementation decision:** keep Shruti's original-engine WASM provider as primary behind the shared resolver. Add Groq-hosted Whisper Turbo as an opt-in backup for Shruti failure, timeout, empty output or an unresolvable transcript; if hosted Whisper is disabled/unavailable, preserve the Shruti transcript for Level 3 or typed correction. Vosk grammar remains a possible local alternative if real RBOS testing justifies it.

## 5. Verified RBOS context

- **Routing** (`web/src/App.tsx` `parseRoute`): hash `#/<page>/<tab>`. Pages: `home`, `sell`, `buy`, `stock`, `parties`, `money`, `more`, `settings`; `#/print/<kind>/<uuid>` is a separate print path and is **not** a voice target.
- **Verified tabs** (`web/src/Pages.tsx`):
  - sell: `menu`, `bill`, `bills`, `collect`, `return`
  - buy: `receive`, `pay`, `history`, `return`
  - stock: `current`, `products`, `opening`, `adjust` (stale tabs from the inactive `Pages.tsx` StockPage export are excluded; purchase receiving uses canonical `buy.receive`)
  - parties: `customers`, `suppliers`, `purchases`, `dues`
  - money: `cash`, `expenses`, `owner`, `closing`
  - more: `reports`, `print`, `lock`, `audit`, `gst`, `staff`, `branches`, `transfers`, `orders`, `reconcile`, `loans`
- **Languages** (`web/src/i18n.ts`): `Lang = 'en' | 'hi'`, persisted in `localStorage['rbos.lang']`. There is no Hinglish or Romanised locale. Command synonyms must live in the resolver, not in UI strings.
- **Offline:** `web/public/sw.js` only has `install`/`activate` handlers and an **empty `fetch` handler, so there is no offline caching today.** G9 (offline operation) is deferred. A model could be cached on the device, but the app shell itself does not load offline yet. Voice "offline" therefore means "no network needed for recognition after the app has loaded".
- **Dependencies:** `web/package.json` has no speech or ML packages.
- **Unsaved work:** the billing form (`GenerateInvoice.tsx`), opening stock (`App.tsx` `OpeningStock`) and purchase receive hold unsaved state in React. A hash change unmounts them and loses that work.

## 6. Implemented three-level architecture

```
[Typed box] ----------------------------+
[Shortcut chips / keyboard] ------------+--> Normaliser --> Intent resolver --> Guard --> location.hash
[Mic button] -> SpeechProvider (Worker) -+        (pure)        (pure, table)    (unsaved/confirm)
```

- **STT provider boundary:** Shruti remains the local Web Worker primary. `speech-fallback.ts` converts the retained final PCM to bounded 16 kHz mono WAV and calls the authenticated server-side Whisper adapter only on eligible Shruti failure/quality conditions. Both outputs enter the same deterministic resolver. The Groq key never reaches the browser.
- **Model loading and caching:** Shruti downloads only when the user first opts into voice (never at app start), verifies SHA-256 and caches its ~29.5 MB model. Hosted Whisper uses the existing server-side Groq key and is disabled unless `Groq:WhisperEnabled=true`.
- **Web Worker:** Shruti inference and audio resampling to 16 kHz mono run off the main thread. Final PCM is retained only until Shruti succeeds or Whisper finishes, then cleared.
- **Microphone:** push-to-talk only (no always-listening, no wake word). Ask for permission on the first tap with an explanation. On denial, keep everything else working with a clear typed fallback. The microphone needs HTTPS, which Cloud Run provides.
- **Normaliser (deterministic):** Unicode NFC; lowercase; remove punctuation and filler words (`please`, `zara`, `karo`, `kholo`, `dikhao`, `jao`, `par`, `pe`, `mujhe`). Map Devanagari and Romanised variants to one canonical token through a lexicon, for example `बिल|bill|bil` to `bill`, `माल|maal|saman|सामान` to `stock`, `उधार|udhar|udhaar|baki|बाकी` to `dues/collect`, `खर्चा|kharcha|kharch|expense` to `expenses`, `वापसी|wapas|return` to `return`, `खरीद|kharid|purchase` to `buy`. Number words are ignored (navigation only).
- **Level 1 resolver:** a static table of required keyword/concept profiles built only from the 35 canonical destinations. Score candidates deterministically, preserve ambiguity and reject data/transaction requests before Level 3. Outcomes:
  - one clear winner above the threshold → navigate
  - two or more close scores (for example "return" means sell return or buy return) → show two or three choices to tap
  - nothing above the threshold → "Not understood", plus the typed box and shortcuts

  Never guess.
- **Guard:** if the current screen reports unsaved work (a small shared `dirty` flag), ask "Leave without saving?" before changing the hash. Voice never presses Save, Post or Pay.
- **Privacy:** audio stays in memory inside the Worker and is discarded after transcription. Nothing is sent to the API or logged. Only an anonymous on-device counter (intent, success or fail) is kept, and only if the owner opts in.
- **Accessibility:** the mic button needs a label, a visible listening state and `aria-live` results. The typed box is keyboard-first.

## 7. RBOS test plan (no results yet)

**Corpus**
- Build about 60 commands from section 5 routes × 4 forms (English, Devanagari Hindi, Romanised Hindi, Hinglish). For example: "bill banao", "नया बिल", "open stock", "udhaar wale customers", "kharcha likho", "supplier ko payment".
- Record with **written consent**: 8 or more speakers (mix of gender, age and region), each on a low-end Android phone and a laptop.
- Record two conditions: a quiet room, and a real shop or recorded shop noise (fan, traffic, customers). Store the files outside the repository and never commit them.
- Text-only set: the same commands typed, including common typos.

**Measure, for each provider and condition**
- Transcription WER and CER (after normalisation).
- Correct navigation rate.
- **Wrong-navigation rate** (navigated to the wrong screen).
- Ambiguity-prompt rate and not-understood rate.
- Latency from release to navigation, p50 and p95.
- Model download size, first-load time, peak memory (Performance panel or `performance.measureUserAgentSpecificMemory` where available).
- Behaviour with the network disabled after the model is cached.
- Words produced on silent and noise-only clips (hallucination).

**Acceptance criteria (proposed, owner to confirm)**
- Typed and shortcut resolver: ≥ 98% correct and 0% wrong navigation on the text set; unit tests over the full phrase table.
- Voice, quiet: ≥ 90% correct; ≤ 2% wrong navigation; p95 latency ≤ 2 s on the low-end phone.
- Voice, noisy: ≥ 80% correct; **≤ 3% wrong navigation**. Wrong navigation matters more than not-understood.
- Silence or noise only: 0 navigations.
- Model ≤ 60 MB download; peak memory ≤ 200 MB; recognition works with the network off after the first load.
- No audio or transcripts in network traces (Playwright check); unsaved-form guard covered by an e2e test.
- If no provider meets the voice criteria, ship text and shortcuts only and keep voice deferred.

## 8. Risks and open questions

- Every model accuracy figure is vendor-reported. The Shruti comparison is published by Shruti's own author.
- The size, operator support and WASM performance of the Shruti ONNX export are not verified.
- Low-end phones may struggle with the WASM threads or SIMD that some runtimes prefer.
- Gated models (IndicConformer original, Koyal) need account acceptance. Koyal needs attribution.
- Without app-shell caching (G9), "offline" is only partial.
- Speaker consent, retention and deletion for recordings need a written process.
- Shop noise is untested by every vendor.

## 9. Local AI intent understanding (additional track, 2026-11-11)

This track adds to sections 4–6; it replaces nothing. AI only ever *suggests* `{intentId, confidence}`. The deterministic guard maps `intentId` to a hash from the static allowlist in section 5. Free-text routes, generated URLs and actions are rejected. X1 stays separate and deferred.

### 9.1 Evidence (metadata read 2026-11-11 from Hugging Face and GitHub APIs; no weights downloaded)

| Model | Type | Licence / access | Smallest browser-ready file found | Hindi / Hinglish fit | Notes |
|---|---|---|---|---|---|
| multilingual-e5-small (intfloat; Xenova ONNX) | Sentence embedding, ~118M | MIT, not gated | int8 ONNX **113 MB** | Hindi in training languages (card); Romanised Hindi untested | Transformers.js-ready |
| paraphrase-multilingual-MiniLM-L12-v2 (Xenova ONNX) | Sentence embedding | Apache-2.0, not gated | int8 ONNX **113 MB** | Hindi listed; Romanised untested | Same size class |
| LaBSE | Embedding | Apache-2.0 | ONNX 1,794 MB | Strong cross-lingual (claim) | Reject: size |
| MuRIL base / IndicBERT v2 | Indic encoders (MuRIL claims transliterated-Hindi pretraining) | Apache-2.0 / MIT, not gated | No ONNX published; would need export plus fine-tuning | Best prior fit for Romanised Hindi (vendor claim) | Fine-tuning effort; candidate for a later trained classifier |
| EmbeddingGemma-300m | Embedding | Gemma Terms, **gated (manual)** | - | Multilingual (claim) | Reject for now: gated, plus custom licence terms |
| Qwen2.5-0.5B-Instruct (onnx-community) | LLM | Apache-2.0, not gated | q4f16 **461 MB**; int8 488 MB | Hindi quality at 0.5B unverified | Generative; needs constrained output |
| Qwen3-0.6B (onnx-community) | LLM | Apache-2.0, not gated | q4f16 **543 MB** | Unverified | Thinking mode must be disabled |
| Gemma 3 270M-it (onnx-community copy) | LLM | Gemma Terms (custom, use policy); original **gated (manual)** | q4f16 **260 MB** | Unverified | Licence obligations still apply to the community copy |
| Gemma 3 1B-it | LLM | Gemma Terms, gated | - | Unverified | Reject: gated, size |
| SmolLM2-360M-Instruct | LLM | Apache-2.0, not gated | int8 ONNX 348 MB | English-centric (card); Hindi likely weak | Reject for Hindi |

**Runtimes (all free, all local):**

| Runtime | Licence | Last pushed | Notes |
|---|---|---|---|
| Transformers.js | Apache-2.0 | 2026-11-11 | WASM and WebGPU |
| onnxruntime-web | MIT | 2026-11-11 | |
| WebLLM | Apache-2.0 | 2026-11-10 | **WebGPU only**; WebGPU is missing or unreliable on many low-end Android browsers (verify per device) |
| llama.cpp | MIT | 2026-11-11 | Native or WASM |
| LiteRT-LM | Apache-2.0 | 2026-11-11 | Native Android, not a web page |

**Cost:**
- None of these has an inference fee or usage quota.
- The real cost is a one-time download (113–550 MB), device storage, memory and battery.
- On a mobile data plan, a 500 MB download is a real cost to the shopkeeper.

### 9.2 Analysis

There are 40 verified destinations (see section 9.6).
- **An embedding approach is the credible option C:**
  1. Embed about 5–10 example phrases per intent at build time (stored as a JSON vector table, under 1 MB).
  2. Embed the user's normalised text at run time.
  3. Use cosine top-1 versus top-2 margin as the confidence score.
  4. The guard accepts only `intentId`s in the allowlist. Low margin leads to a choice prompt; low score leads to "not understood".
- **Romanised Hindi is the open risk.** General multilingual embedders were trained mostly on Devanagari Hindi. "udhaar wale customers" may embed poorly. A transliteration lexicon in front (section 6) may matter more than the model.
- **Small on-device LLM (0.5–0.6B):** this historical experiment was not selected. The current Level 3 implementation uses opt-in server-side Gemini Flash with Groq Qwen text fallback; it is separate from the on-device model candidates in this section.

### 9.3 A vs B vs C

| | A: typed text + deterministic resolver | B: STT (Shruti ONNX / Vosk grammar) + resolver | C: STT + local AI classifier (embedding) + deterministic guard |
|---|---|---|---|
| Extra download | 0 | ~30–140 MB (Vosk small-hi listed as 42 MB; Shruti 29.5 MB claimed) | B + ~113 MB int8 embedder (~150–250 MB total) |
| Runtime | Plain TS | WASM worker | WASM worker; WebGPU optional |
| Low-end Android | Yes | Likely (vendor: Shruti ~80 MB peak; unverified) | Uncertain: two models in memory at once |
| Paraphrase / unseen wording | Only what the lexicon covers | Same as A after transcription | Potentially better (unverified) |
| Recovering from STT errors | Edit distance only | Vosk grammar prevents most | Semantic similarity may help (unverified) |
| Wrong-navigation risk | Lowest, auditable | Low | Must be measured; margin threshold plus allowlist guard |
| Explainability | Full | Full after transcription | Score-based, partly opaque |
| Offline after load | Yes | Yes | Yes |
| Licence / access | n/a | Apache-2.0, not gated | MIT / Apache-2.0, not gated (avoid Gemma) |
| Maintenance | Phrase table | + model pinning | + embedder pinning, phrase-vector rebuild |
| Measured RBOS accuracy | **None yet** | **None yet** | **None yet** |

### 9.4 Recommendation

**Local AI deserves a small offline proof of concept, but only as a measured comparison, not a product commitment.**

- **Why:**
  - It is zero-cost and ungated: MIT/Apache embedders run on free runtimes.
  - It can run in WASM without WebGPU.
  - It may improve paraphrase coverage in Hinglish, which is the weakest part of A.
- **Why not yet in the product:**
  - It roughly doubles the download.
  - Its Romanised Hindi quality is unproven.
  - Approach A has not been measured either.
- **Gate:** adopt C only if, on the same corpus, it beats A or B on correct navigation by **≥ 5 percentage points**, with wrong navigation **≤ 2%** (quiet) or **≤ 3%** (noisy). It must also stay inside the latency (p95 ≤ 2 s) and memory (≤ 200 MB) limits on the low-end phone.
- **On-device generative LLM candidates:** none of the small browser models in this research were selected. This does not exclude the separate, constrained server-side navigation-only Level 3 implementation described in section 12.

### 9.5 Unverified claims and proposed experiments

**Unverified claims**
- Hindi or Romanised Hindi quality of every embedder and LLM above.
- Memory and latency of each file on a low-end Android browser.
- WebGPU availability on target phones.
- Shruti and Vosk figures (vendor-reported).

**Proposed experiments.** Run them offline, on a developer machine first. Each needs separate approval to download.
1. **Text-only bake-off:** A versus C (multilingual-e5-small int8, and MiniLM-L12 int8). Use the section 7 typed corpus plus about 100 unseen paraphrases from shop owners. Report correct, wrong, ambiguity and not-understood rates.
2. Repeat experiment 1 with and without the transliteration lexicon, to separate the model's contribution from the normaliser's.
3. Feed B's real transcripts (quiet and noisy) into A and C.
4. Measure on the low-end phone: first-load time, peak memory, p50/p95 latency, network-off behaviour.
5. Run Qwen2.5-0.5B with enum-constrained output **only if** experiment 1 shows embeddings under the gate.

### 9.6 Experiment 1 plan (Stage 2 implemented; model download still requires separate approval)

- **Location:** `experiments/rbos-intent-bakeoff/`, outside RBOS, with its own lockfile. It does not touch `web/package.json`.
- **Runtime:** Node 22 with `@huggingface/transformers@4.3.2` (Apache-2.0).
  - It pulls in `onnxruntime-node@1.30.0`, a pre-release `onnxruntime-web@1.31.0-dev`, `sharp` (unused), `@huggingface/jinja` and `@huggingface/tokenizers`.
  - Install with `npm ci`, then run with `env.allowRemoteModels=false`.
- **Model artefacts** (int8 ONNX, revisions pinned):

| Model | Repo and revision | Files and SHA-256 | Licence | Download |
|---|---|---|---|---|
| C1: multilingual-e5-small | `Xenova/multilingual-e5-small@761b726d` | `onnx/model_int8.onnx` 115,288 KB (`4d24e2bc…cb02c`), `tokenizer.json` 16,682 KB (`0b44a9d7…c4ebdf4c39`), `config.json`, `tokenizer_config.json`, `special_tokens_map.json` | Upstream `intfloat/…` is MIT | ≈ 132 MB |
| C2: paraphrase-multilingual-MiniLM-L12-v2 | `Xenova/paraphrase-multilingual-MiniLM-L12-v2@2c4055b1` | `onnx/model_int8.onnx` 115,288 KB (`d6ea442f…bcf4c8`), `tokenizer.json` 16,683 KB (`b60b6b43…fa441`), plus the same three small JSON files | Upstream `sentence-transformers/…` is Apache-2.0 | ≈ 132 MB |

  - Total ≈ 264 MB of models, plus the npm packages (size measured at install).
  - The revisions and hashes above are research notes only. `experiments/rbos-intent-bakeoff/config.v1.json` keeps placeholders until the model artefacts are downloaded and independently verified. Stage 2 code and deterministic Baseline A do not require model weights.
  - The two int8 ONNX files have the same byte size but different hashes. That is plausible because both are 12-layer, 384-dim models with the XLM-R vocabulary.
  - The Xenova repos do not declare their own licence, so they inherit the upstream terms. Confirm this before any redistribution.
- **Destinations:** 40 verified hashes, frozen in `destinations.v1.json`: `home`, `settings`, the More menu itself (`#/more`), and 37 page/tab pairs. The earlier count of 39 left out the More menu screen.
- **Data:** three disjoint, frozen files:
  - `data/anchors.v1.json`: 5 examples per destination (200), used as the reference phrases for A and C, and as the only source of the L2 lexicon.
  - `data/tuning.v1.jsonl` (54 items): used only to tune τ and δ.
  - `data/test.v1.frozen.jsonl`: 110 navigate, 30 ambiguous and 20 out-of-scope commands, each tagged with language (`en`, `hi-Deva`, `hi-Latn`, `mixed`). All are synthetic, so v1 is indicative only; see `reviews/test-v1-review.md`. Owner phrases go into a separate v2 set through `data/owner-phrases/`.
  - Each item's expected result is one of `navigate`, `ask{candidates}` or `reject`.
  - H2 label policy is fixed: P1 explicit navigation request → navigate; P2 business-data question/calculation → reject; P3 clear navigation intent with multiple plausible destinations → ask.
  - Policy resolutions recorded for disputed examples: `t104` stays `navigate more.transfers`; `t110` stays `navigate settings`; `t132` resolves to `navigate stock.history`; `t138` resolves to `navigate settings`.
  - Synthetic items are labelled. Owner-collected items never edit v1; they form a new versioned set.
- **Ablation:** each resolver runs on three preprocessing levels:
  - L0: Unicode NFC, lowercase, punctuation removed.
  - L1: L0 + rule-based transliteration and spelling folding.
  - L2: L1 + a lexicon generated only from the anchors (`specs/normalisation.v1.1.md`). A, C1 and C2 receive the identical prepared string (`specs/fairness.md`).
  - Comparing L0 and L2 shows how much the model adds versus the preprocessing.
- **Decision rule:**
  - Navigate only if top1 ≥ τ and top1 − top2 ≥ δ.
  - If top1 ≥ τ but the margin is below δ, ask the user (offering every candidate within δ).
  - Otherwise, not understood.
  - τ ∈ {0.70…0.90}, δ ∈ {0.02…0.12}, chosen on the dev set as the highest correct-navigation rate with wrong navigation ≤ 2%. Then frozen for the test set.
  - E5 inputs get the prefix `query: `. Embeddings are mean-pooled and L2-normalised.
- **Outputs:** `results/<runId>/`:
  - `summary.csv`: resolver × level × language; correct, wrong, ask-correct, ask-wrong, not-understood and out-of-scope-rejected rates, with 95% Wilson confidence intervals.
  - `per-item.jsonl` and `confusion.csv`.
  - `latency.csv`: p50/p95 and peak memory (RSS).
  - `environment.json`: hashes, versions, CPU, Node version.
- **Pass gate:** C must beat the best A configuration by ≥ 5 percentage points of correct navigation on the test set, with wrong navigation ≤ 2%, in **every** language bucket. Out-of-scope items must never navigate.
- **v1 interpretation:** v1 is exploratory (synthetic authored data), not a production accuracy benchmark. Publish both all-items and debatable-items-excluded reports. Romanised Hindi (`hi-Latn`) has a small sample in v1, so bucket-level conclusions are directional.

## 9.7 Approved Stage 3/4 execution (2026-11-13)

The owner approved the previously gated local model comparison. The implementation is isolated in the experiment folder and a dedicated workflow:

- Workflow: `.github/workflows/rbos-intent-model-bakeoff.yml`; runner script: `experiments/rbos-intent-bakeoff/scripts/run-model-bakeoff-ci.mjs`.
- Before inference, the runner resolves the current model metadata but accepts only the reviewed C1/C2 revision prefixes. It downloads the int8 ONNX file and tokenizer, verifies their SHA-256 values against the reviewed artefacts, and records the full resolved commit SHA plus hashes in `results/<runId>/model-pins.json`.
- The runner updates the model revision/hash fields and config manifest hash only inside the ephemeral CI workspace. It does not commit model weights or send text to a model provider. The two embedding models execute locally on the runner.
- The native ONNX Runtime package bundles its CPU runtime; the workflow sets `ONNXRUNTIME_NODE_INSTALL=skip` to avoid downloading the unused optional CUDA execution provider on Linux. This is not an inference-runtime skip.
- The adapter selects `dtype: int8` because the configured artefact is `onnx/model_int8.onnx`; `q8` would select the different `model_quantized.onnx` file.
- E5 uses its `query: ` prefix for both intent anchors and user queries because this experiment is symmetric semantic similarity. MiniLM has no task prefix.
- Stage 2 tests, validator and lexicon check run before any model download. The runner tunes thresholds on the 54-item tuning set, freezes them, then scores the 160-item v1 test set once for A/C1/C2 at L0/L1/L2.
- CI artifacts contain only small reports, predictions and model-pin metadata; they never contain model weights.

The v1 data is still synthetic and assistant-authored. A result can reject a weak model or justify further study, but cannot establish real shopkeeper accuracy. Even a passing text-only result is not permission to integrate the model in the app: real, consented owner phrases and low-end Android memory/latency checks remain Stage 5.

## 9.8 v1 semantic model result (2026-11-13)

The first complete Stage 3/4 workflow finished successfully: [run 43419876997](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/43418998859). Both approved ONNX int8 models were checksum-verified and run locally on the ephemeral CPU runner. Stage 2 tests, frozen-data validation and lexicon check also passed.

**Decision: do not integrate either candidate.** All model/preprocessing configurations failed the declared gate. E5 achieved only 2.7–4.5% correct navigation and was too conservative. MiniLM L0 achieved 14.5% correct navigation but had 11.3% wrong navigation; L2 reduced pooled wrong navigation to 2.5% but failed the per-language improvement and 2% safety limits. The deterministic baseline also performed poorly, so this does not make the current phrase resolver acceptable.

Full metrics and caveats: `experiments/rbos-intent-bakeoff/reviews/semantic-v1-results-20261113.md`. v1 is synthetic and assistant-authored, with only 25 hi-Latn examples. It is a method comparison and negative candidate-selection result, not production accuracy evidence. Inference timings are from a GitHub Linux CPU runner, not Android.

Next step is a separate consented v2 corpus of real shopkeeper phrases and ASR-corrupted transcripts. No phrase-specific fallback should be added to the production resolver. No model integration, deployment or change to the production navigation path is approved by this benchmark.

## 10. What happens after the bake-off

Do not integrate either embedder just because the workflow completes. First review the full and per-language metrics, wrong-navigation and reject leakage, ask precision, p50/p95 latency, and the pinned model files. If the gate fails or the result is inconclusive, keep the current navigation-only experience and improve the data/evaluation protocol instead of adding phrase-specific fallbacks. If a candidate passes, conduct a separate Stage 5 with consented shop-owner phrases and Android-device memory, latency and offline tests before making an app-integration decision.


## 11. H2 implementation status

- The feature is navigation-only. It never answers business-data questions, computes values, creates or posts transactions, or accepts arbitrary URLs. X1 remains deferred.
- Typed commands and quick actions do not depend on microphone permission, model downloads, or network speech services.
- Voice is opt-in and push-to-talk. Audio is captured in memory, resampled to 16 kHz mono and sent to the local Shruti Web Worker first. Only if Shruti fails, times out or returns no usable transcript is the short-lived PCM copy encoded as WAV and sent to the authenticated RBOS API for hosted Whisper fallback. PCM buffers are cleared after success, failure, cancellation or disposal. Audio, transcripts and provider payloads are never written to logs.
- The model is cached in browser Cache Storage after checksum verification. Model downloads happen only after the user selects Enable voice. The existing RBOS app shell is not fully offline; cached recognition is intended to work locally after the app has loaded.
- Navigation from billing, purchase receiving, and opening-stock routes is guarded with a confirmation before leaving. This currently uses route-level protection rather than field-level dirty-state tracking.
- CI status at 2026-11-14: latest PR workflow #290 passed frontend TypeScript, the full Vitest suite, production build, and dependency audit. The backend Release build, migration validation, tests, and container build were still running at the time of this update; do not mark the full pipeline green until they complete.
- Remaining manual acceptance: verify the same-origin Shruti runtime asset download and cache/reload; Android Chrome first-use microphone permission and push-to-talk; Hindi, English and Hinglish recognition; silence/noise rejection; and unsaved-form protection on desktop and a low-end Android device. No production deployment or cloud speech provider is enabled.


## 12. Speech provider boundary (2026-11-14)

The H2 assistant UI and interaction design remain unchanged. Speech runtime selection is now isolated in `web/src/speech-provider.ts`; `NavigationAssistant.tsx` no longer hardcodes the Shruti worker URL.

- Active provider: `shruti-local`, using the existing Shruti model and local Web Worker. The existing push-to-talk flow, partial transcript previews, PCM cleanup, model integrity checks and browser cache remain in place.
- Shruti remains the registered local worker primary. Hosted Whisper is implemented as a separate API adapter and is automatically invoked only after eligible Shruti failure or an unresolvable transcript. It remains disabled by default.
- The agreed Level 3 design is Gemini Flash primary. The repository's existing Groq integration is a Qwen vision fallback for catalogue photo/PDF extraction in `ProductFileReader`; it is not xAI Grok and is not currently used for voice intent. `InvoiceExtractionService` now uses the Gemini model chain followed by Groq Qwen vision as a cross-provider fallback. Voice intent uses a separate text-only Groq client; official Groq documentation confirms `qwen/qwen3.8-27b` accepts text input, returns text and supports JSON mode. Provider calls remain disabled by default until the owner enables them. These are intent-understanding providers, not STT providers. Use transcript-only transfer for Level 3, keep credentials server-side, and document provider limits, privacy and cost before enabling calls.
- This change does not add packages, API keys, backend endpoints, network speech calls or model downloads beyond the existing opt-in Shruti flow.
- `speech-provider.test.ts` verifies the default provider and worker selection. Existing assistant UI and resolver tests remain the regression baseline.


## 12.1 Project-wide AI fallback policy (2026-11-14)

The implementation now applies a consistent fallback rule to every AI workflow found in the API audit:

| Workflow | Primary | Fallback | Recovery |
|---|---|---|---|
| Product/catalogue photo/PDF | Gemini | Groq Qwen vision | Local OCR/text extraction where available; manual column mapping |
| Supplier invoice photo/PDF | Gemini model chain | Groq Qwen vision | Manual entry; extracted invoice remains an unsaved draft |
| Voice STT | Local Shruti | Groq Whisper Turbo | Typed command and deterministic resolver |
| Voice intent | Gemini Flash | Groq Qwen text generation | Deterministic suggestions or ask the user |

Qwen 3.8 27B is documented by Groq as accepting text and image inputs, returning text and supporting JSON object/schema output. The voice-intent integration therefore uses a dedicated text-only client rather than reusing the catalogue vision client. [Groq model documentation](https://console.groq.com/docs/model/qwen/qwen3.8-27b) and [text generation documentation](https://console.groq.com/docs/text-chat).

All new AI features must have an independent provider fallback or a documented deterministic/manual recovery path. Validate structured outputs, bound payloads/timeouts/retries/usage, keep credentials server-side, and do not log audio, full transcripts or provider payloads. If all providers fail, preserve user input and offer manual recovery.

Level 2 and Level 3 hosted providers are disabled by default in appsettings and require explicit environment flags. The API applies authenticated, CSRF-protected endpoints and a 100-request-per-user-per-UTC-day pilot cap. These in-memory counters are instance-local and supplement—not replace—provider organization limits.

## 13. Whisper vs Shruti evaluation (2026-11-14)

### Decision summary

**Keep Shruti as the primary STT provider and implement Whisper as its backup.** The published comparison does not support replacing Shruti for Hindi/Hinglish. Whisper remains useful as a recovery path when Shruti fails, times out or produces an unusable transcript. The selected backup is Groq-hosted Whisper Turbo, subject to explicit owner approval of the account's cost tier. It is disabled by default. The published Turbo Developer price is US$0.04 per audio hour; the public rate-limit table shows Developer-plan base limits, not guaranteed Free-tier limits. Free-tier quotas are account-specific and must be checked in the Groq console.

This is a research-based decision, not an RBOS audio benchmark. No owner-recorded audio corpus has been supplied yet, so no claim is made that either model has passed RBOS acceptance criteria.

### Evidence from the model comparison

Udgam Labs' published comparison ran Shruti and Whisper large-v3 on overlapping public test clips with the same normaliser/scorer. It is a vendor-authored comparison, not an independent third-party evaluation. Its reported WER (lower is better):

| Test set | Shruti | Whisper large-v3 | Better |
|---|---:|---:|---|
| Monsoon Hindi | 20.46 | 33.29 | Shruti |
| FLEURS Hindi | 20.14 | 28.88 | Shruti |
| Lahaja Hindi accents (first 300) | 22.87 | 31.19 | Shruti |
| MUCS Hinglish | 35.21 | 79.94 | Shruti |
| Svarah Indian English (first 300) | 21.35 | 7.39 | Whisper |
| LibriSpeech English (first 300) | 5.76 | 2.25 | Whisper |
| Words invented on 100 silent clips | 0 | 538 | Shruti |

Caveats: the Whisper comparison used a particular decoding configuration (beam 5, language set, without its temperature fallback); the author notes repetition loops and reports that filtering those clips does not reverse the Hindi result. These figures are not directly transferable to RBOS's short, noisy, command-style speech. They nevertheless do not support replacing Shruti for Hindi/Hinglish. Whisper is clearly stronger on the English subsets.

### Hosted Whisper via Groq: cost and operational fit

Groq currently documents two multilingual transcription model IDs: `whisper-large-v3` and `whisper-large-v3-turbo`. It recommends large-v3 when transcription is error-sensitive and turbo for speed/price performance. The published list price is **US$0.111 per audio hour** for large-v3 and **US$0.04 per audio hour** for turbo. Groq also documents request and audio-second rate limits; the actual limits are organization/account-specific and visible in its console. Therefore, this is **not a guaranteed zero-cost recurring service**. Any temporary free allowance or account quota must not be treated as a permanent $0 dependency.

For RBOS, a hosted Groq call means the recorded navigation command leaves the device and depends on network, provider availability, API credentials and account limits. The owner has confirmed that this is acceptable for ordinary navigation commands. Shruti remains local-first; hosted Whisper is called only after Shruti fails or returns no usable/unresolvable output. The adapter is disabled by default and requires `Groq:WhisperEnabled=true`; use the existing server-side `Groq:ApiKey`, never expose it in the browser, and verify the account's actual Free-tier limits before enabling. The API additionally limits each authenticated user to 100 fallback requests per UTC day.

### Which Whisper variant is relevant?

- **Groq `whisper-large-v3`:** choose only for a future controlled accuracy comparison, not as the default. It costs more per audio hour and Groq's documentation positions it for error-sensitive multilingual transcription.
- **Groq `whisper-large-v3-turbo`:** the practical hosted candidate if latency and lower cost matter more; it is still paid usage, not a guaranteed free fallback.
- **Self-hosted Whisper large-v3:** not a realistic browser/low-end Android replacement for H2. The published fp16 file is about 3.1 GB, compared with Shruti's 29.5 MB. Self-hosting also requires compute and operational hosting, so it does not meet the current zero-cost constraint.
- **Small Whisper checkpoints/fine-tunes:** the available comparison reports substantially weaker Hindi/Hinglish results for the listed small candidates; they are not a justified shortcut without RBOS-specific measurements.

### RBOS-specific test required before any provider decision

The published comparison cannot replace the test plan in section 7. The next meaningful evaluation is a paired audio test using the **same recordings** for Shruti and Whisper, with consent, on RBOS commands:

1. 8+ speakers with different accents; Hindi Devanagari, Romanised Hindi, Hinglish and Indian English.
2. Quiet and realistic shop-noise conditions; include silence/noise-only clips and close/far microphone placement.
3. Keep audio files outside GitHub. Use anonymised IDs; do not upload customer/shop transaction audio.
4. Compare normalised WER/CER, correct navigation, wrong navigation, ask/not-understood rate, silence hallucinations, release-to-navigation p50/p95, network dependency and total cost.
5. Apply the existing safety gate: wrong navigation <=2% quiet and <=3% noisy; silence/noise must never navigate. Prefer not-understood over a wrong screen.

**Evaluation status:** literature/vendor comparison complete; actual paired RBOS audio test not run because consented recordings are not yet available. The research supports Shruti as primary and Whisper as backup. Groq Whisper Turbo adapter and automatic failover are implemented but disabled by default pending verified account cost/limits and RBOS-specific accuracy measurements.


## 14. Keyword-first resolver and Bundeli/North Indian Hindi coverage (2026-11-14)

### Scope and evidence

The navigation resolver has been changed from whole-phrase anchor scoring to **required keyword/concept groups**. It now has 35 canonical assistant destinations. The route audit follows the active dispatcher in `App.tsx`: it imports `StockPage` from `Stock.tsx`, the Sell/Buy/Parties/Money/More pages from `Pages.tsx`, and `HomePage`/`BusinessSettings` from `Home.tsx`. The separate `StockPage` export in `Pages.tsx` is not active in the app; its import/count/history/alerts/units tabs are therefore not exposed as assistant destinations. The active `Stock.tsx` also has an alternate `#/stock/purchase` shortcut that renders the same receive workflow as `#/buy/receive`; the assistant uses the canonical `buy.receive` target to avoid duplicate destinations. The test checks parity between the 35 resolver targets and 35 keyword profiles; this is configuration parity, while the route audit itself is a code-backed review of the active imports and route handlers.

Research notes:
- Bundeli/Bundelkhandi is a Western Hindi variety with local variation. A Census-hosted Linguistic Survey description explicitly includes Jalaun, Hamirpur and Jhansi in the Bundeli-speaking area; an older district account identifies Bundeli as the prevailing Western Hindi variety in Jalaun, including Konch tehsil. This is useful geographic context, not a modern survey of an individual speaker's vocabulary.
- Bundeli has multiple local forms; a historical linguistic description records tendencies such as long final `a` shifting toward `o` and some loss of medial aspiration. Such patterns are treated only as optional romanised matching variants, never as a blanket rewrite of every word.
- Contemporary speech also mixes local Bundeli with standard Hindi and English product/business words. Do not assume one canonical "North Indian Hindi" vocabulary. Keep Hindi Devanagari, Romanised Hindi, Hinglish and locally reviewed words side by side.

### Implementation

- Removed sentence/phrase anchors from the production resolver. Each screen now has required groups of keywords; all groups must be evidenced before navigation.
- Added word aliases for common Hindi, Romanised Hindi, Hinglish, shop vocabulary and a conservative Bundeli spelling-fold layer (vowel length, aspiration, terminal a/o comparison).
- Added distinct contextual requirements for easy collisions: bill vs old bill, customer receipt vs supplier payment, customer return vs supplier return, purchase receive vs purchase history/return, stock view vs count/adjust/history/alerts, and customer dues vs collections.
- Kept ambiguous single words as clarification prompts. Data questions, transaction commands, numbers and arbitrary URLs remain rejected before routing.
- Suggestions use incomplete keyword matches but never execute navigation.
- The existing verified-route allowlist and unsaved-work confirmation remain the final execution safety boundary. Level 3 is implemented as Gemini Flash primary with a separate Groq Qwen text-generation fallback. It is called only when deterministic resolution is insufficient, returns strict structured intent, and is validated against the 35 canonical destination IDs. Both provider flags are disabled by default; no raw audio is sent to Level 3.

### Synthetic test protocol and caveats

A new synthetic regression suite exercises every allowlisted screen with multiple compact keyword inputs plus Hindi/Hinglish/Bundeli-style spellings, ASR-like variants, ambiguous terms and out-of-scope requests. The old v1 benchmark files remain frozen and unchanged. These tests measure the resolver **after transcription**; they do not fine-tune or change the Shruti acoustic model. Improving Shruti's actual speech recognition requires consented, recorded audio with accurate transcripts and an appropriate model-training or adapter process. Text-only synthetic inputs cannot train the ASR weights.

### Limits

Dialect variants are starting hypotheses informed by published descriptions, not verified Konch shopkeeper utterances. Add or change a local word only after checking it with speakers from the target area and keeping a held-out test set. An incorrect dialect alias can create false navigation, so a lower confidence result should ask rather than guess.
