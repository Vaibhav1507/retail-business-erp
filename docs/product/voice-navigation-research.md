# Voice and text navigation assistant: research and recommendation (H2)

Status: research only. Nothing is implemented, installed or downloaded. Date of evidence: 2026-11-11.

## 1. Scope

- **In scope:** move the user to an existing RBOS screen from typed text, a predefined shortcut or a spoken command in Hindi, English, Romanised Hindi or Hinglish.
- **Out of scope:** chatbot, LLM, questions about business data, creating or posting anything (X1 stays deferred).
- **Hard constraints:** zero cost (no paid API, credits, trial or extra paid hosting); runs on the device; typed input and shortcuts always work, even with no microphone and no model.

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

1. **Best overall (provisional): text and shortcuts first, then Shruti through its ONNX export in a Web Worker (onnxruntime-web or sherpa-onnx WASM).** Shruti is the only candidate under 50 MB that claims usable Hinglish and runs in the browser under a permissive licence. This confirms the *model* preference only conditionally. It challenges the *runtime*: do not take a dependency on the Cactus Needle engine while its packaging is changing.
2. **Best accuracy (local, Hindi only):** IndicConformer Hindi ONNX (MIT). Koyal is better on read Hindi, but it is gated, needs CC-BY attribution and is weaker on accents. Neither handles Hinglish. Tara claims the best accuracy overall but cannot run free on the device.
3. **Best lightweight and most robust for commands:** Vosk small Hindi with a **closed grammar** of RBOS phrases. Its open-dictation WER is worse, but a grammar limits output to valid commands, which can lower wrong navigation. This needs measuring against Shruti on the RBOS command set.

**Decision rule:** keep both Shruti-ONNX and Vosk-grammar behind the same adapter. Pick by the section 7 results, not by the WER tables above.

## 5. Verified RBOS context

- **Routing** (`web/src/App.tsx` `parseRoute`): hash `#/<page>/<tab>`. Pages: `home`, `sell`, `buy`, `stock`, `parties`, `money`, `more`, `settings`; `#/print/<kind>/<uuid>` is a separate print path and is **not** a voice target.
- **Verified tabs** (`web/src/Pages.tsx`):
  - sell: `menu`, `bill`, `bills`, `collect`, `return`
  - buy: `receive`, `pay`, `history`, `return`
  - stock: `current`, `products`, `opening`, `import`, `adjust`, `count`, `history`, `alerts`, `units`
  - parties: `customers`, `suppliers`, `purchases`, `dues`
  - money: `cash`, `expenses`, `owner`, `closing`
  - more: `reports`, `print`, `lock`, `audit`, `gst`, `staff`, `branches`, `transfers`, `orders`, `reconcile`, `loans`
- **Languages** (`web/src/i18n.ts`): `Lang = 'en' | 'hi'`, persisted in `localStorage['rbos.lang']`. There is no Hinglish or Romanised locale. Command synonyms must live in the resolver, not in UI strings.
- **Offline:** `web/public/sw.js` only has `install`/`activate` handlers and an **empty `fetch` handler, so there is no offline caching today.** G9 (offline operation) is deferred. A model could be cached on the device, but the app shell itself does not load offline yet. Voice "offline" therefore means "no network needed for recognition after the app has loaded".
- **Dependencies:** `web/package.json` has no speech or ML packages.
- **Unsaved work:** the billing form (`GenerateInvoice.tsx`), opening stock (`App.tsx` `OpeningStock`) and purchase receive hold unsaved state in React. A hash change unmounts them and loses that work.

## 6. Proposed architecture (design only)

```
[Typed box] ----------------------------+
[Shortcut chips / keyboard] ------------+--> Normaliser --> Intent resolver --> Guard --> location.hash
[Mic button] -> SpeechProvider (Worker) -+        (pure)        (pure, table)    (unsaved/confirm)
```

- **`SpeechProvider` interface:** `isAvailable()`, `load(onProgress)`, `start()`, `stop() -> {text, confidence?}`, `dispose()`. Implementations: `NoSpeech` (default), `ShrutiOnnx`, `VoskGrammar`, optional `WebSpeech` (opt-in, cloud warning). The resolver never knows which provider produced the text.
- **Model loading and caching:** download only when the user turns voice on (never at app start). Show the size before downloading. Store the model in Cache Storage or IndexedDB with a version and SHA-256 hash. Serve it from RBOS's own origin or a pinned copy, not a third-party CDN at run time. Respect `navigator.storage.estimate()`, and ask for `persist()`.
- **Web Worker:** inference and audio resampling to 16 kHz mono run off the main thread. Post-processing returns text only. Terminate the worker when idle.
- **Microphone:** push-to-talk only (no always-listening, no wake word). Ask for permission on the first tap with an explanation. On denial, keep everything else working with a clear typed fallback. The microphone needs HTTPS, which Cloud Run provides.
- **Normaliser (deterministic):** Unicode NFC; lowercase; remove punctuation and filler words (`please`, `zara`, `karo`, `kholo`, `dikhao`, `jao`, `par`, `pe`, `mujhe`). Map Devanagari and Romanised variants to one canonical token through a lexicon, for example `बिल|bill|bil` to `bill`, `माल|maal|saman|सामान` to `stock`, `उधार|udhar|udhaar|baki|बाकी` to `dues/collect`, `खर्चा|kharcha|kharch|expense` to `expenses`, `वापसी|wapas|return` to `return`, `खरीद|kharid|purchase` to `buy`. Number words are ignored (navigation only).
- **Intent resolver:** a static table of `{intent, hash, phrases[]}` built only from the verified routes in section 5. Score each candidate on token overlap plus a bounded edit distance. Outcomes:
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
- **Small LLM (0.5–0.6B):** keep only as a last-resort experiment. Use it only with grammar- or enum-constrained output, and only if embeddings fail on the corpus. It is too large for the first-load target in section 7 (≤ 60 MB voice model).

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
- **Generative LLMs:** don't build with them now. Gemma: avoid while gated and under custom terms. SmolLM: weak in Hindi.

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

### 9.6 Experiment 1 plan (awaiting owner approval; nothing downloaded)

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
  - The revisions, hashes and package version above come from the earlier metadata check. `experiments/rbos-intent-bakeoff/config.v1.json` keeps placeholders until they are re-verified against the actual files at stages 2–3.
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

## 10. Smallest next step

Approve a **text-only slice**: the normaliser, intent table and resolver as pure TypeScript with unit tests over the verified routes, plus a typed "Go to…" box. It has no model, no package and no microphone. It delivers value on its own, and it becomes the resolver that every speech provider will later feed. In parallel, the owner collects the consented recording corpus for the provider bake-off.
