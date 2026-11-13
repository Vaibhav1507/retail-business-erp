// Experiment runner. Two separate commands, so the test file can never influence thresholds.
//   node scripts/run.mjs --tune [--resolvers A,C1,C2] [--run <runId>]
//       verifies the manifest WITHOUT opening the test file, prepares anchors + tuning at L0/L1/L2,
//       selects tau/delta per resolver x level on tuning only, writes results/<runId>/thresholds.json + .sha256.
//   node scripts/run.mjs --test --run <runId>
//       refuses unless thresholds.json matches its recorded hash; reads the test file once and reports.
// C1/C2 are skipped with a recorded reason until model weights are approved, downloaded and pinned (stage 3).
import { execSync } from 'node:child_process'
import { arch, cpus, platform, release, totalmem } from 'node:os'
import { FileAccess, sha256, stableJson, verifyManifest } from './lib/io.mjs'
import { LEVELS, guard, prepare } from './lib/normalise.mjs'
import { buildIndexA, decide, scoreA, scoreC } from './lib/resolve.mjs'
import { DEBATABLE, classify, confusion, mcnemarExact, percentile, range, selectThresholds, tally } from './lib/evaluate.mjs'
import { LANGS } from './lib/validate.mjs'
import { checkModelFiles, loadEmbedder } from './lib/embed.mjs'
import { LEXICON_FILE, generateLexicon } from './build-lexicon.mjs'

const A_GRID = { tau: range(0.3, 0.9, 0.1), delta: range(0.05, 0.3, 0.05) }

function args(argv) {
  const out = { resolvers: ['A', 'C1', 'C2'] }
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--tune') out.mode = 'tune'
    else if (argv[i] === '--test') out.mode = 'test'
    else if (argv[i] === '--run') out.run = argv[++i]
    else if (argv[i] === '--resolvers') out.resolvers = argv[++i].split(',')
  }
  return out
}

// Loads everything a phase may use. In the tune phase the test file is forbidden at the file-access layer.
export function loadContext(fa, { phase }) {
  const cfg = fa.json('config.v1.json')
  if (phase === 'tune') fa.forbid(cfg.files.test)
  verifyManifest(fa, { skip: phase === 'tune' ? [cfg.files.test] : [] })
  const destinations = fa.json(cfg.files.destinations)
  const anchors = fa.json(cfg.files.anchors)
  const lexicon = fa.json(LEXICON_FILE)
  const regenerated = generateLexicon(new FileAccess({ root: fa.root, forbid: [cfg.files.tuning, cfg.files.test] }))
  if (JSON.stringify(regenerated) !== JSON.stringify(lexicon)) throw new Error(`${LEXICON_FILE} does not match its generator; aborting`)
  for (const [k, v] of Object.entries(lexicon.sourceHashes)) if (regenerated.sourceHashes[k] !== v) throw new Error(`lexicon source hash mismatch for ${k}`)
  const allowed = new Set(destinations.destinations.map((d) => d.hash))
  const anchorRows = destinations.destinations.flatMap((d) => (anchors.anchors[d.id] ?? []).map((text) => ({ id: d.id, text })))
  return { cfg, destinations, lexicon, allowed, anchorRows }
}

export function prepareSet(items, level, ctx) {
  return items.map((item) => {
    const g = guard(item.text, ctx.allowed)
    return { item, guard: g.ok ? 'ok' : g.reason, prepared: g.ok ? prepare(item.text, level, ctx.lexicon) : '' }
  })
}

// Returns a scorer per resolver x level: (preparedText) => Map scores. A is synchronous; C wraps an embedder.
export async function makeScorers(ctx, resolvers, root) {
  const scorers = {}
  const skipped = {}
  const modelMeta = {}
  for (const r of resolvers) {
    const spec = ctx.cfg.resolvers[r]
    if (!spec) throw new Error(`Unknown resolver ${r}`)
    if (spec.type === 'deterministic') {
      for (const level of LEVELS) {
        const index = buildIndexA(ctx.anchorRows.map((a) => ({ id: a.id, text: prepare(a.text, level, ctx.lexicon) })))
        scorers[`${r}/${level}`] = { resolver: r, level, grid: A_GRID, score: async (t) => scoreA(t, index) }
      }
      continue
    }
    const check = checkModelFiles(root, spec)
    if (!check.ok) { skipped[r] = check.reason; continue }
    const emb = await loadEmbedder(root, spec, ctx.cfg.runtime)
    modelMeta[r] = emb.meta
    for (const level of LEVELS) {
      const anchorVecs = []
      for (const a of ctx.anchorRows) anchorVecs.push({ id: a.id, vec: await emb.embed(prepare(a.text, level, ctx.lexicon)) })
      scorers[`${r}/${level}`] = { resolver: r, level, grid: { tau: ctx.cfg.decision.tauGrid, delta: ctx.cfg.decision.deltaGrid }, score: async (t) => scoreC(await emb.embed(t), anchorVecs) }
    }
  }
  return { scorers, skipped, modelMeta }
}

export async function tune(fa, { resolvers, runId }) {
  const ctx = loadContext(fa, { phase: 'tune' })
  const tuning = fa.jsonl(ctx.cfg.files.tuning)
  const { scorers, skipped, modelMeta } = await makeScorers(ctx, resolvers, fa.root)
  const thresholds = {}
  for (const [key, s] of Object.entries(scorers)) {
    const scored = []
    for (const p of prepareSet(tuning, s.level, ctx)) scored.push({ ...p, scores: p.guard === 'ok' ? await s.score(p.prepared) : new Map() })
    const sel = selectThresholds(scored, s.grid.tau, s.grid.delta, ctx.cfg.gate.maxWrongNavigate)
    thresholds[key] = { tau: sel.tau, delta: sel.delta, feasible: sel.feasible, feasibleCount: sel.feasibleCount, gridSize: sel.gridSize, tuning: { correctNavigate: sel.correctNavigate, wrongNavigateRate: sel.wrongNavigateRate, rejectNavigated: sel.rejectNavigated } }
  }
  const opened = [...new Set(fa.log)].sort()
  if (opened.includes(ctx.cfg.files.test)) throw new Error('test file was opened during tuning; aborting')
  const doc = { runId, createdAt: new Date().toISOString(), frozenBeforeTest: true, filesOpened: opened, lexiconSha256: sha256(fa.bytes(LEXICON_FILE)), skippedResolvers: skipped, modelMeta, thresholds }
  const content = stableJson(doc)
  fa.write(`results/${runId}/thresholds.json`, content)
  fa.write(`results/${runId}/thresholds.json.sha256`, `${sha256(content)}  thresholds.json\n`)
  return doc
}

function environment(fa, modelMeta) {
  let tfVersion = null
  try { tfVersion = JSON.parse(fa.text('node_modules/@huggingface/transformers/package.json')).version } catch { /* not installed */ }
  let lock = null
  try { lock = sha256(fa.bytes('package-lock.json')) } catch { /* no lockfile */ }
  return { node: process.version, platform: platform(), release: release(), arch: arch(), cpu: cpus()[0]?.model, cpuCount: cpus().length, totalMemMb: Math.round(totalmem() / 2 ** 20), transformers: tfVersion, packageLockSha256: lock, models: modelMeta, gitless: true, timezone: Intl.DateTimeFormat().resolvedOptions().timeZone, npm: (() => { try { return execSync('npm --version', { encoding: 'utf8' }).trim() } catch { return null } })() }
}

export async function test(fa, { runId }) {
  const thrPath = `results/${runId}/thresholds.json`
  if (!fa.exists(thrPath) || !fa.exists(`${thrPath}.sha256`)) throw new Error(`No frozen thresholds for run ${runId}; run --tune first`)
  const thrText = fa.text(thrPath)
  const recorded = fa.text(`${thrPath}.sha256`).split(/\s+/)[0]
  if (sha256(thrText) !== recorded) throw new Error('thresholds.json does not match its recorded hash; refusing to test')
  const frozen = JSON.parse(thrText)
  if (fa.exists(`results/${runId}/report.json`)) throw new Error(`run ${runId} already has a test report; the test set is scored once per run`)
  const ctx = loadContext(fa, { phase: 'test' })
  if (sha256(fa.bytes(LEXICON_FILE)) !== frozen.lexiconSha256) throw new Error('lexicon changed since thresholds were frozen')
  const testItems = fa.jsonl(ctx.cfg.files.test)
  const resolvers = [...new Set(Object.keys(frozen.thresholds).map((k) => k.split('/')[0]))]
  const { scorers, skipped, modelMeta } = await makeScorers(ctx, resolvers, fa.root)

  const predictions = []
  const prepared = {}
  for (const [key, s] of Object.entries(scorers)) {
    const thr = frozen.thresholds[key]
    if (!thr) throw new Error(`no frozen threshold for ${key}`)
    const set = prepareSet(testItems, s.level, ctx)
    prepared[s.level] ??= sha256(set.map((p) => p.prepared).join('\n'))
    for (const p of set) {
      const t0 = process.hrtime.bigint()
      const scores = p.guard === 'ok' ? await s.score(p.prepared) : new Map()
      const d = p.guard === 'ok' ? decide(scores, thr.tau, thr.delta) : { kind: 'not_understood', ids: [], top1: 0, top2: 0 }
      const ms = Number(process.hrtime.bigint() - t0) / 1e6
      // Allowlist: an id is only ever turned into a route by lookup in the verified inventory.
      const route = d.kind === 'navigate' ? ctx.destinations.destinations.find((x) => x.id === d.ids[0])?.hash : null
      if (d.kind === 'navigate' && !route) throw new Error(`resolver produced unverified destination ${d.ids[0]}`)
      predictions.push({ key, resolver: s.resolver, level: s.level, id: p.item.id, lang: p.item.lang, expect: p.item.expect, target: p.item.target ?? null, candidates: p.item.candidates ?? null, guard: p.guard, prepared: p.prepared, kind: d.kind, ids: d.ids, route, top1: d.top1, top2: d.top2, outcome: classify(p.item, d), ms, debatable: DEBATABLE.includes(p.item.id) })
    }
  }

  const report = buildReport(predictions, ctx.cfg, frozen)
  report.skippedResolvers = { ...frozen.skippedResolvers, ...skipped }
  report.preparedTestSha256 = prepared
  report.filesOpened = [...new Set(fa.log)].sort()
  report.testReadCount = fa.log.filter((p) => p === ctx.cfg.files.test).length
  fa.write(`results/${runId}/predictions.jsonl`, predictions.map((p) => JSON.stringify(p)).join('\n') + '\n')
  fa.write(`results/${runId}/report.json`, stableJson(report))
  fa.write(`results/${runId}/environment.json`, stableJson({ ...environment(fa, modelMeta), lexiconStats: ctx.lexicon.stats }))
  fa.write(`results/${runId}/summary.md`, renderSummary(report))
  return report
}

export function buildReport(predictions, cfg, frozen) {
  const keys = [...new Set(predictions.map((p) => p.key))]
  const scopes = { all: () => true, excludingDebatable: (p) => !p.debatable }
  const out = { runId: frozen.runId, thresholds: frozen.thresholds, scopes: {} }
  for (const [scope, keep] of Object.entries(scopes)) {
    const rows = predictions.filter(keep)
    const summary = {}
    for (const key of keys) {
      const mine = rows.filter((p) => p.key === key)
      const ms = mine.map((p) => p.ms)
      summary[key] = { overall: tally(mine), byLang: Object.fromEntries(LANGS.map((l) => [l, tally(mine.filter((p) => p.lang === l))])), latencyMs: { p50: percentile(ms, 0.5), p95: percentile(ms, 0.95) }, confusion: confusion(mine) }
    }
    out.scopes[scope] = { summary, gate: gate(summary, rows, cfg) }
  }
  const verdicts = Object.values(out.scopes).map((s) => JSON.stringify(Object.fromEntries(Object.entries(s.gate.byCandidate).map(([k, v]) => [k, v.pass]))))
  out.conclusion = verdicts[0] === verdicts[1] ? 'consistent across all-items and debatable-excluded scopes' : 'INCONCLUSIVE: gate outcome differs between all-items and debatable-excluded scopes'
  out.caveat = 'v1 phrases are synthetic and exploratory; small language buckets (hi-Latn ~25 items) cannot resolve a 5-point difference.'
  return out
}

function gate(summary, rows, cfg) {
  const g = cfg.gate
  const byCandidate = {}
  for (const level of LEVELS) {
    const aKey = `A/${level}`
    if (!summary[aKey]) continue
    for (const c of ['C1', 'C2']) {
      const cKey = `${c}/${level}`
      if (!summary[cKey]) continue
      const perLang = {}
      let pass = true
      for (const l of LANGS) {
        const a = summary[aKey].byLang[l]; const cc = summary[cKey].byLang[l]
        const adv = cc.correctNavigateRate === null || a.correctNavigateRate === null ? null : (cc.correctNavigateRate - a.correctNavigateRate) * 100
        const wrongNavigateOk = cc.wrongNavigateRate !== null && cc.wrongNavigateRate <= g.maxWrongNavigate
        const ok = adv !== null && adv >= g.minAdvantagePp && wrongNavigateOk
        perLang[l] = { advantagePp: adv, wrongNavigateRate: cc.wrongNavigateRate, wrongNavigateOk, ok }
        pass &&= ok
      }
      const o = summary[cKey].overall
      const wrongOk = o.wrongNavigateRate <= g.maxWrongNavigate
      const rejectOk = o.rejectNavigated <= g.rejectItemsNavigated
      const aIds = new Map(rows.filter((p) => p.key === aKey).map((p) => [p.id, p.outcome === 'correct_navigate']))
      let b = 0; let cOnly = 0
      for (const p of rows.filter((x) => x.key === cKey && x.expect === 'navigate')) {
        const cr = p.outcome === 'correct_navigate'; const ar = aIds.get(p.id)
        if (cr && !ar) b++
        if (!cr && ar) cOnly++
      }
      byCandidate[cKey] = { vs: aKey, pass: pass && wrongOk && rejectOk, perLang, wrongNavigateOk: wrongOk, rejectOk, mcnemar: { cOnlyCorrect: b, aOnlyCorrect: cOnly, pExact: mcnemarExact(b, cOnly) } }
    }
  }
  return { rule: g, byCandidate, evaluated: Object.keys(byCandidate).length > 0 }
}

const pct = (v) => (v === null || v === undefined ? 'n/a' : (v * 100).toFixed(1) + '%')

export function renderSummary(report) {
  const lines = [`# Run ${report.runId}`, '', `> ${report.caveat}`, '', `Conclusion: ${report.conclusion}`, '']
  if (Object.keys(report.skippedResolvers ?? {}).length) lines.push('Skipped resolvers:', ...Object.entries(report.skippedResolvers).map(([k, v]) => `- ${k}: ${v}`), '')
  for (const [scope, s] of Object.entries(report.scopes)) {
    lines.push(`## Scope: ${scope}`, '', '| resolver/level | tau | delta | correct-nav | wrong-nav | correct-ask | ask-precision | reject-navigated | not-understood | p50 ms | p95 ms |', '|---|---|---|---|---|---|---|---|---|---|---|')
    for (const [key, v] of Object.entries(s.summary)) {
      const t = report.thresholds[key]; const o = v.overall
      lines.push(`| ${key} | ${t.tau} | ${t.delta} | ${pct(o.correctNavigateRate)} | ${pct(o.wrongNavigateRate)} | ${pct(o.correctAskRate)} | ${pct(o.askPrecision)} | ${o.rejectNavigated} | ${pct(o.notUnderstoodRate)} | ${v.latencyMs.p50?.toFixed(2)} | ${v.latencyMs.p95?.toFixed(2)} |`)
    }
    lines.push('', '| resolver/level | ' + LANGS.join(' | ') + ' |', '|---|' + LANGS.map(() => '---').join('|') + '|')
    for (const [key, v] of Object.entries(s.summary)) lines.push(`| ${key} | ` + LANGS.map((l) => `${pct(v.byLang[l].correctNavigateRate)} (n=${v.byLang[l].navigateItems})`).join(' | ') + ' |')
    lines.push('', s.gate.evaluated ? 'Gate: ' + Object.entries(s.gate.byCandidate).map(([k, v]) => `${k} ${v.pass ? 'PASS' : 'FAIL'} (McNemar p=${v.mcnemar.pExact.toFixed(3)})`).join('; ') : 'Gate: not evaluated (no C resolver results).', '')
  }
  return lines.join('\n') + '\n'
}

async function main() {
  const a = args(process.argv.slice(2))
  const fa = new FileAccess()
  if (a.mode === 'tune') {
    const runId = a.run ?? new Date().toISOString().replace(/[:.]/g, '-')
    const doc = await tune(fa, { resolvers: a.resolvers, runId })
    console.log(`Thresholds frozen: results/${runId}/thresholds.json`)
    for (const [k, v] of Object.entries(doc.thresholds)) console.log(`  ${k}: tau=${v.tau} delta=${v.delta} feasible=${v.feasible} tuningCorrectNav=${v.tuning.correctNavigate}`)
    for (const [k, v] of Object.entries(doc.skippedResolvers)) console.log(`  skipped ${k}: ${v}`)
    console.log(`Files opened: ${doc.filesOpened.join(', ')}`)
  } else if (a.mode === 'test') {
    if (!a.run) throw new Error('--test requires --run <runId>')
    const r = await test(fa, { runId: a.run })
    console.log(`Report written to results/${a.run}/ (test file read ${r.testReadCount} time(s)). ${r.conclusion}`)
  } else {
    console.log('Usage: node scripts/run.mjs --tune [--resolvers A,C1,C2] [--run id] | --test --run id')
    process.exit(2)
  }
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('scripts/run.mjs')) main().catch((e) => { console.error(e.message); process.exit(1) })
