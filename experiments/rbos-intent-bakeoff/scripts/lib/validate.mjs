// Data validation (README "Leakage controls", fairness.md). Pure over already-loaded data.
import { l0, l1, tokens } from './normalise.mjs'

export const EXPECTED_COUNTS = { anchorsPerDestination: 5, destinations: 40, anchors: 200, tuning: 54, test: 160, testExpect: { navigate: 110, ask: 30, reject: 20 } }
export const LANGS = ['en', 'hi-Deva', 'hi-Latn', 'mixed']
export const NEAR_DUP_JACCARD = 0.8

export function jaccard(a, b) {
  const A = new Set(tokens(a)); const B = new Set(tokens(b))
  if (!A.size && !B.size) return 1
  let inter = 0
  for (const t of A) if (B.has(t)) inter++
  return inter / (A.size + B.size - inter)
}

function checkItems(name, items, ids, prefix, errors) {
  const seen = new Set()
  for (const it of items) {
    const where = `${name}:${it.id}`
    if (!new RegExp(`^${prefix}\\d{3}$`).test(it.id ?? '')) errors.push(`${where} bad id`)
    if (seen.has(it.id)) errors.push(`${where} duplicate id`)
    seen.add(it.id)
    if (typeof it.text !== 'string') errors.push(`${where} text is not a string`)
    else if (!it.text.trim() && it.expect !== 'reject') errors.push(`${where} empty text on a non-reject item`)
    if (!LANGS.includes(it.lang)) errors.push(`${where} unknown lang ${it.lang}`)
    if (it.expect === 'navigate') {
      if (!ids.has(it.target)) errors.push(`${where} target ${it.target} is not a verified destination`)
    } else if (it.expect === 'ask') {
      const c = it.candidates
      if (!Array.isArray(c) || c.length < 2) errors.push(`${where} ask needs >= 2 candidates`)
      else for (const id of c) if (!ids.has(id)) errors.push(`${where} candidate ${id} is not a verified destination`)
    } else if (it.expect === 'reject') {
      if (it.target || it.candidates) errors.push(`${where} reject must not carry a destination`)
    } else errors.push(`${where} unknown expect ${it.expect}`)
  }
}

// sets: {anchors:[{set,id,text}], tuning:[items], test:[items]}
export function validateData({ destinations, anchors, tuning, test }) {
  const errors = []
  const warnings = []
  const dests = destinations.destinations
  const ids = new Set(dests.map((d) => d.id))
  if (ids.size !== dests.length) errors.push('duplicate destination id')
  if (dests.length !== EXPECTED_COUNTS.destinations) errors.push(`expected ${EXPECTED_COUNTS.destinations} destinations, found ${dests.length}`)
  for (const d of dests) {
    if (!/^#\/[a-z]+(\/[a-z-]+)?$/.test(d.hash)) errors.push(`destination ${d.id} has unsafe hash ${d.hash}`)
  }
  if (new Set(dests.map((d) => d.hash)).size !== dests.length) errors.push('duplicate destination hash')

  const anchorRows = []
  for (const [id, list] of Object.entries(anchors.anchors)) {
    if (!ids.has(id)) errors.push(`anchors: ${id} is not a verified destination`)
    if (list.length !== EXPECTED_COUNTS.anchorsPerDestination) errors.push(`anchors: ${id} has ${list.length} phrases`)
    list.forEach((text, i) => anchorRows.push({ set: 'anchors', id: `${id}#${i + 1}`, dest: id, text }))
  }
  for (const id of ids) if (!anchors.anchors[id]) errors.push(`anchors: no phrases for ${id}`)
  if (anchorRows.length !== EXPECTED_COUNTS.anchors) errors.push(`expected ${EXPECTED_COUNTS.anchors} anchors, found ${anchorRows.length}`)

  checkItems('tuning', tuning, ids, 'd', errors)
  checkItems('test', test, ids, 't', errors)
  if (tuning.length !== EXPECTED_COUNTS.tuning) errors.push(`expected ${EXPECTED_COUNTS.tuning} tuning items, found ${tuning.length}`)
  if (test.length !== EXPECTED_COUNTS.test) errors.push(`expected ${EXPECTED_COUNTS.test} test items, found ${test.length}`)
  for (const [k, v] of Object.entries(EXPECTED_COUNTS.testExpect)) {
    const n = test.filter((t) => t.expect === k).length
    if (n !== v) errors.push(`expected ${v} test ${k} items, found ${n}`)
  }

  const all = [...anchorRows, ...tuning.map((t) => ({ set: 'tuning', id: t.id, text: t.text })), ...test.map((t) => ({ set: 'test', id: t.id, text: t.text }))]
  for (const r of all) { r.k0 = l0(r.text); r.k1 = l1(r.text) }

  const groups = (key) => {
    const m = new Map()
    for (const r of all) { if (!m.has(r[key])) m.set(r[key], []); m.get(r[key]).push(r) }
    return [...m.values()].filter((g) => g.length > 1)
  }
  const exactDuplicates = groups('k0').map((g) => g.map((r) => `${r.set}:${r.id}`))
  const foldedDuplicates = groups('k1').filter((g) => new Set(g.map((r) => r.k0)).size > 1).map((g) => g.map((r) => `${r.set}:${r.id}`))
  for (const g of [...groups('k0'), ...groups('k1')]) {
    const sets = new Set(g.map((r) => r.set))
    const label = g.map((r) => `${r.set}:${r.id}`).join(', ')
    if (sets.size > 1) errors.push(`leakage across sets after normalisation: ${label}`)
    else if (g[0].set !== 'anchors' || new Set(g.map((r) => r.dest)).size > 1) errors.push(`duplicate within ${g[0].set}: ${label}`)
    else warnings.push(`repeated anchor wording within one destination: ${label}`)
  }

  const nearDuplicates = []
  for (let i = 0; i < all.length; i++) for (let j = i + 1; j < all.length; j++) {
    const a = all[i]; const b = all[j]
    if (a.k1 === b.k1) continue
    const s = jaccard(a.k1, b.k1)
    if (s >= NEAR_DUP_JACCARD) nearDuplicates.push({ a: `${a.set}:${a.id}`, b: `${b.set}:${b.id}`, jaccard: Math.round(s * 1000) / 1000, crossSet: a.set !== b.set })
  }
  for (const nd of nearDuplicates) (nd.crossSet ? errors : warnings).push(`near-duplicate (token Jaccard ${nd.jaccard} at L1): ${nd.a} ~ ${nd.b}`)

  const byLang = Object.fromEntries(LANGS.map((l) => [l, test.filter((t) => t.lang === l).length]))
  return { ok: errors.length === 0, errors, warnings, stats: { destinations: dests.length, anchors: anchorRows.length, tuning: tuning.length, test: test.length, testByLang: byLang, exactDuplicates: exactDuplicates.length, foldedDuplicates: foldedDuplicates.length, nearDuplicates: nearDuplicates.length } }
}
