import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, cpSync, writeFileSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { FileAccess, ROOT, parseManifest, sha256, verifyManifest } from '../scripts/lib/io.mjs'
import { foldLatin, guard, l0, l1, l2, transliterateDevanagari } from '../scripts/lib/normalise.mjs'
import { buildIndexA, decide, damerauLevenshtein, scoreA } from '../scripts/lib/resolve.mjs'
import { classify, mcnemarExact, selectThresholds } from '../scripts/lib/evaluate.mjs'
import { validateData } from '../scripts/lib/validate.mjs'
import { LEXICON_FILE, generateLexicon } from '../scripts/build-lexicon.mjs'
import { runValidation } from '../scripts/validate.mjs'
import { parseDecision as parseLlmDecision, chatMessages as llmChatMessages } from '../scripts/run-llm-bakeoff.mjs'
import { gate, loadContext, prepareSet, test as runTest, tune } from '../scripts/run.mjs'

const FROZEN_TEST_SHA = '068754fcc42b8fc40817911cfafeb508215a5a4bf8913a6a7989dbef27ea5818'
const TEST_FILE = 'data/test.v1.frozen.jsonl'
const TUNING_FILE = 'data/tuning.v1.jsonl'

function sandbox() {
  const dir = mkdtempSync(join(tmpdir(), 'rbos-bakeoff-'))
  for (const p of ['MANIFEST.sha256', 'config.v1.json', 'destinations.v1.json', 'data', LEXICON_FILE]) cpSync(join(ROOT, p), join(dir, p), { recursive: true })
  return dir
}

const load = () => {
  const fa = new FileAccess()
  return { fa, destinations: fa.json('destinations.v1.json'), anchors: fa.json('data/anchors.v1.json'), tuning: fa.jsonl(TUNING_FILE), test: fa.jsonl(TEST_FILE), lexicon: fa.json(LEXICON_FILE) }
}

// ---------- Manifest integrity and frozen protection ----------
test('manifest pins the known frozen test hash and all entries verify', () => {
  const fa = new FileAccess()
  const m = parseManifest(fa.text('MANIFEST.sha256'))
  assert.equal(m.get(TEST_FILE), FROZEN_TEST_SHA)
  assert.equal(sha256(fa.bytes(TEST_FILE)), FROZEN_TEST_SHA)
  assert.equal(verifyManifest(fa).checked.length, m.size)
})

test('a one-byte change to the frozen test file aborts verification', () => {
  const dir = sandbox()
  try {
    writeFileSync(join(dir, TEST_FILE), readFileSync(join(dir, TEST_FILE), 'utf8') + ' ')
    assert.throws(() => verifyManifest(new FileAccess({ root: dir })), /Hash mismatch for data\/test\.v1\.frozen\.jsonl/)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('malformed or duplicate manifest lines are rejected', () => {
  assert.throws(() => parseManifest('abc  x'), /Malformed/)
  const h = 'a'.repeat(64)
  assert.throws(() => parseManifest(`${h}  x\n${h}  x`), /Duplicate/)
})

// ---------- Data validation and leakage ----------
test('full validator passes on the committed data', () => {
  const r = runValidation(new FileAccess())
  assert.deepEqual(r.errors, [])
  assert.equal(r.stats.test, 160)
  assert.deepEqual(r.stats.testByLang, { en: 59, 'hi-Deva': 44, 'hi-Latn': 25, mixed: 32 })
})

test('no text is shared between anchors, tuning and test after L0 or L1', () => {
  const { anchors, tuning, test: t } = load()
  const anchorKeys = new Set(Object.values(anchors.anchors).flat().flatMap((s) => [l0(s), l1(s)]))
  const tuningKeys = new Set(tuning.flatMap((s) => [l0(s.text), l1(s.text)]))
  for (const item of t) {
    if (!item.text.trim()) continue
    assert.ok(!anchorKeys.has(l1(item.text)) && !anchorKeys.has(l0(item.text)), `${item.id} leaks into anchors`)
    assert.ok(!tuningKeys.has(l1(item.text)) && !tuningKeys.has(l0(item.text)), `${item.id} leaks into tuning`)
  }
})

test('validator detects injected cross-set leakage, folded duplicates and unknown routes', () => {
  const { destinations, anchors, tuning, test: t } = load()
  const leak = structuredClone(t)
  leak[0].text = tuning[0].text.toUpperCase() + '!'
  assert.ok(validateData({ destinations, anchors, tuning, test: leak }).errors.some((e) => /leakage/.test(e)))
  const folded = structuredClone(t)
  folded[0].text = 'zzqaal qwertee'; folded[1].text = 'jjkal qvertii'
  assert.ok(validateData({ destinations, anchors, tuning, test: folded }).errors.some((e) => /duplicate within test/.test(e)))
  const badRoute = structuredClone(t)
  const nav = badRoute.find((x) => x.expect === 'navigate')
  nav.target = 'admin.secret'
  assert.ok(validateData({ destinations, anchors, tuning, test: badRoute }).errors.some((e) => /not a verified destination/.test(e)))
})

// ---------- Lexicon source restrictions ----------
test('lexicon is generated only from destinations + anchors and matches the committed file', () => {
  const fa = new FileAccess({ forbid: [TUNING_FILE, TEST_FILE] })
  const lex = generateLexicon(fa)
  assert.deepEqual([...new Set(fa.log)].sort(), ['MANIFEST.sha256', 'data/anchors.v1.json', 'destinations.v1.json'])
  assert.deepEqual(JSON.parse(readFileSync(join(ROOT, LEXICON_FILE), 'utf8')), lex)
  const m = parseManifest(fa.text('MANIFEST.sha256'))
  assert.equal(lex.sourceHashes.anchors, m.get('data/anchors.v1.json'))
  assert.equal(lex.sourceHashes.destinations, m.get('destinations.v1.json'))
})

test('lexicon generation aborts when anchors differ from the manifest', () => {
  const dir = sandbox()
  try {
    const p = join(dir, 'data/anchors.v1.json')
    writeFileSync(p, readFileSync(p, 'utf8').replace('go home', 'go home now'))
    assert.throws(() => generateLexicon(new FileAccess({ root: dir })), /Hash mismatch/)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('shared tokens such as "return" carry no screen-group tag; unique tokens do', () => {
  const { lexicon } = load()
  assert.deepEqual(lexicon.tokens['return'].tags, [])
  for (const [k, v] of Object.entries(lexicon.tokens)) assert.ok(v.tags.length <= 1, `${k} has more than one tag`)
})

// ---------- Normalisation ----------
test('L0 strips punctuation, lowercases and keeps Devanagari marks', () => {
  assert.equal(l0('  Open   STOCK-history!! '), 'open stock history')
  assert.equal(l0('स्टॉक देखो।'), 'स्टॉक देखो')
})

test('L1 folds udhaar / udhar / उधार to the same key', () => {
  assert.equal(l1('udhaar'), 'udhar')
  assert.equal(l1('udhar'), 'udhar')
  assert.equal(l1('उधार'), 'udhar')
})

test('Devanagari: final schwa dropped, anusvara -> n, nukta -> base', () => {
  assert.equal(transliterateDevanagari('कमल'), 'kamal')
  assert.equal(transliterateDevanagari('हिंदी'), 'hindii')
  assert.equal(transliterateDevanagari('ज़रूरी'), transliterateDevanagari('जरूरी'))
  assert.equal(foldLatin('wapas'), 'vapas')
  assert.equal(foldLatin('phone'), 'fone')
  assert.equal(foldLatin('kharcha'), 'kharcha')
})

test('L2 needs a lexicon and appends sorted tags', () => {
  const { lexicon } = load()
  assert.throws(() => l2('stock', null))
  const out = l2('opening stock', lexicon)
  assert.ok(out.startsWith('opening stok'))
  assert.match(out, /@stock$/)
})

test('normalisation is deterministic', () => {
  const { lexicon, tuning } = load()
  const run = () => tuning.map((t) => [l0(t.text), l1(t.text), l2(t.text, lexicon)].join('|')).join('\n')
  assert.equal(sha256(run()), sha256(run()))
})

// ---------- Guard / allowlist ----------
test('guard rejects empty, URLs, digits and unverified routes; allows verified routes', () => {
  const allowed = new Set(['#/stock/history', '#/home'])
  assert.equal(guard('', allowed).ok, false)
  assert.equal(guard('open https://evil.example', allowed).ok, false)
  assert.equal(guard('javascript:alert(1)', allowed).ok, false)
  assert.equal(guard('go to #/admin/secret', allowed).reason, 'unknown-route')
  assert.equal(guard('bill 500', allowed).reason, 'digits')
  assert.equal(guard('go to #/home', allowed).ok, true)
  assert.equal(guard('open stock history', allowed).ok, true)
})

// ---------- Decision rule and Baseline A (P1/P2/P3) ----------
test('decide: navigate, ask (max 3), not_understood', () => {
  assert.deepEqual(decide(new Map([['a', 0.9], ['b', 0.5]]), 0.6, 0.1).kind, 'navigate')
  const ask = decide(new Map([['a', 0.8], ['b', 0.79], ['c', 0.78], ['d', 0.77], ['e', 0.1]]), 0.6, 0.1)
  assert.equal(ask.kind, 'ask'); assert.equal(ask.ids.length, 3)
  assert.equal(decide(new Map([['a', 0.2]]), 0.6, 0.1).kind, 'not_understood')
  assert.equal(decide(new Map(), 0.6, 0.1).kind, 'not_understood')
})

test('Damerau-Levenshtein and fuzzy threshold', () => {
  assert.equal(damerauLevenshtein('stokc', 'stock'), 1)
  assert.equal(damerauLevenshtein('abc', 'abc'), 0)
})

function aResolver(level) {
  const fa = new FileAccess({ forbid: [TEST_FILE] })
  const ctx = loadContext(fa, { phase: 'tune' })
  const index = buildIndexA(ctx.anchorRows.map((a) => ({ id: a.id, text: l2Prep(a.text, level, ctx) })))
  return { ctx, run: (text, tau = 0.5, delta = 0.1) => {
    const g = guard(text, ctx.allowed)
    return g.ok ? decide(scoreA(l2Prep(text, level, ctx), index), tau, delta) : { kind: 'not_understood', ids: [] }
  } }
}
const l2Prep = (t, level, ctx) => prepareSet([{ text: t }], level, ctx)[0].prepared

test('P1: explicit anchor wording navigates to the verified destination', () => {
  const { run, ctx } = aResolver('L1')
  const d = run('opening stock')
  assert.equal(d.kind, 'navigate'); assert.equal(d.ids[0], 'stock.opening')
  assert.ok(ctx.destinations.destinations.some((x) => x.id === d.ids[0]))
})

test('P3: bare "return" asks between sales and purchase return, never navigates', () => {
  const { run } = aResolver('L1')
  const d = run('return')
  assert.equal(d.kind, 'ask')
  assert.ok(d.ids.includes('sell.return') && d.ids.includes('buy.return'))
})

test('P2 / unknown: gibberish and digit-bearing data questions are not navigated', () => {
  const { run } = aResolver('L2')
  assert.equal(run('xyzzy plugh').kind, 'not_understood')
  assert.equal(run('what was sales on 12 march').kind, 'not_understood')
})

test('semantic gate enforces the wrong-navigation ceiling in every language bucket', () => {
  const langs = ['en', 'hi-Deva', 'hi-Latn', 'mixed']
  const baselineLang = Object.fromEntries(langs.map((lang) => [lang, { correctNavigateRate: 0.5, wrongNavigateRate: 0 }]))
  const candidateLang = Object.fromEntries(langs.map((lang) => [lang, {
    correctNavigateRate: 0.56,
    wrongNavigateRate: lang === 'hi-Latn' ? 0.03 : 0.01,
  }]))
  const summary = {
    'A/L0': { byLang: baselineLang, overall: { wrongNavigateRate: 0, rejectNavigated: 0 } },
    'C1/L0': { byLang: candidateLang, overall: { wrongNavigateRate: 0.015, rejectNavigated: 0 } },
  }
  const result = gate(summary, [], { gate: { minAdvantagePp: 5, maxWrongNavigate: 0.02, rejectItemsNavigated: 0 } })
  assert.ok(Math.abs(result.byCandidate['C1/L0'].perLang['hi-Latn'].advantagePp - 6) < 1e-9)
  assert.equal(result.byCandidate['C1/L0'].perLang['hi-Latn'].wrongNavigateOk, false)
  assert.equal(result.byCandidate['C1/L0'].pass, false)
})

test('classify follows P1-P3 policy', () => {
  assert.equal(classify({ expect: 'navigate', target: 'home' }, { kind: 'navigate', ids: ['home'] }), 'correct_navigate')
  assert.equal(classify({ expect: 'navigate', target: 'home' }, { kind: 'navigate', ids: ['settings'] }), 'wrong_navigate')
  assert.equal(classify({ expect: 'ask', candidates: ['sell.return', 'buy.return'] }, { kind: 'navigate', ids: ['sell.return'] }), 'wrong_navigate')
  assert.equal(classify({ expect: 'ask', candidates: ['sell.return', 'buy.return'] }, { kind: 'ask', ids: ['sell.return', 'buy.return'] }), 'correct_ask')
  assert.equal(classify({ expect: 'reject' }, { kind: 'navigate', ids: ['home'] }), 'reject_navigated')
  assert.equal(classify({ expect: 'reject' }, { kind: 'not_understood', ids: [] }), 'correct_reject')
})

// ---------- Threshold selection uses tuning only ----------
test('selectThresholds never picks a point that navigates a reject item when a safe point exists', () => {
  const scored = [
    { item: { expect: 'navigate', target: 'a' }, guard: 'ok', scores: new Map([['a', 0.9], ['b', 0.2]]) },
    { item: { expect: 'reject' }, guard: 'ok', scores: new Map([['a', 0.6], ['b', 0.1]]) },
  ]
  const s = selectThresholds(scored, [0.5, 0.7], [0.1], 0.02)
  assert.equal(s.tau, 0.7); assert.equal(s.rejectNavigated, 0); assert.equal(s.feasible, true)
})

test('tune phase cannot open the test file and records the files it opened', async () => {
  const dir = sandbox()
  try {
    const fa = new FileAccess({ root: dir })
    const doc = await tune(fa, { resolvers: ['A', 'C1', 'C2'], runId: 'unit' })
    assert.ok(!doc.filesOpened.includes(TEST_FILE))
    assert.ok(!fa.log.includes(TEST_FILE))
    assert.equal(Object.keys(doc.thresholds).length, 3)
    assert.ok(doc.skippedResolvers.C1 && doc.skippedResolvers.C2, 'C resolvers must be skipped without weights')
    const blocked = new FileAccess({ root: dir, forbid: [TEST_FILE] })
    assert.throws(() => blocked.text(TEST_FILE), /forbidden/)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('test phase refuses without frozen thresholds or with tampered thresholds', async () => {
  const dir = sandbox()
  try {
    await assert.rejects(runTest(new FileAccess({ root: dir }), { runId: 'none' }), /No frozen thresholds/)
    await tune(new FileAccess({ root: dir }), { resolvers: ['A'], runId: 'tamper' })
    const p = join(dir, 'results/tamper/thresholds.json')
    writeFileSync(p, readFileSync(p, 'utf8').replace('"tau": ', '"tau": 0.1, "x": '))
    await assert.rejects(runTest(new FileAccess({ root: dir }), { runId: 'tamper' }), /recorded hash/)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('test phase verifies and scores the frozen test file from one cached read', async () => {
  const dir = sandbox()
  try {
    await tune(new FileAccess({ root: dir }), { resolvers: ['A'], runId: 'single-read' })
    const fa = new FileAccess({ root: dir })
    const report = await runTest(fa, { runId: 'single-read' })
    assert.equal(report.testReadCount, 1)
    assert.equal(fa.log.filter((p) => p === TEST_FILE).length, 1)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('repeatability: two tuning runs give identical thresholds', async () => {
  const dir = sandbox()
  try {
    const a = await tune(new FileAccess({ root: dir }), { resolvers: ['A'], runId: 'r1' })
    const b = await tune(new FileAccess({ root: dir }), { resolvers: ['A'], runId: 'r2' })
    assert.deepEqual(a.thresholds, b.thresholds)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('mcnemarExact sanity', () => {
  assert.equal(mcnemarExact(0, 0), 1)
  assert.ok(Math.abs(mcnemarExact(0, 5) - 0.0625) < 1e-12)
  assert.equal(mcnemarExact(3, 3), 1)
})


// ---------- Qwen classifier protocol and prompt contract ----------
test('Qwen parser accepts only exact allowlisted single-line decisions', () => {
  const allowed = new Set(['home', 'stock.current', 'sell.return', 'buy.return'])
  assert.deepEqual(parseLlmDecision('NAVIGATE|stock.current', allowed), {
    kind: 'navigate', ids: ['stock.current'], reason: 'model_navigate'
  })
  assert.deepEqual(parseLlmDecision('ASK|sell.return,buy.return', allowed), {
    kind: 'ask', ids: ['sell.return', 'buy.return'], reason: 'model_ask'
  })
  assert.equal(parseLlmDecision('NOT_UNDERSTOOD', allowed).kind, 'not_understood')
  assert.equal(parseLlmDecision('NAVIGATE|1', allowed).reason, 'invalid_model_protocol')
  assert.equal(parseLlmDecision('NAVIGATE|admin.secret', allowed).kind, 'not_understood')
  assert.equal(parseLlmDecision('ASK|sell.return', allowed).kind, 'not_understood')
  assert.equal(parseLlmDecision('{"decision":"navigate","destination_id":1}', allowed).kind, 'not_understood')
  assert.equal(parseLlmDecision('NAVIGATE|home\\nextra text', allowed).kind, 'not_understood')
})

test('Qwen policy is a system message and user utterance remains separate', () => {
  const messages = llmChatMessages('stock position check karo', [
    { id: 'stock.current', label: 'Check stock' },
    { id: 'sell.return', label: 'Customer return' }
  ])
  assert.equal(messages.length, 2)
  assert.equal(messages[0].role, 'system')
  assert.match(messages[0].content, /how much stock do I have => NAVIGATE\\|stock.current/)
  assert.match(messages[0].content, /NOT_UNDERSTOOD for greetings/)
  assert.match(messages[0].content, /stock.current/)
  assert.equal(messages[1].role, 'user')
  assert.equal(messages[1].content, 'stock position check karo')
})
