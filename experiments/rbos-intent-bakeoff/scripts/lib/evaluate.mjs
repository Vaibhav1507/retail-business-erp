// Outcome classification, threshold selection and summaries. Shared by every resolver.
import { decide } from './resolve.mjs'

export const DEBATABLE = ['t041', 't061', 't068', 't074', 't103', 't132', 't138', 't140']

// Classifies one decision against the item label (policy P1-P3).
// Navigating on an ask item is counted as a wrong navigation, because P3 requires asking.
export function classify(item, decision) {
  const { kind, ids } = decision
  if (item.expect === 'navigate') {
    if (kind === 'navigate') return ids[0] === item.target ? 'correct_navigate' : 'wrong_navigate'
    if (kind === 'ask') return ids.includes(item.target) ? 'ask_containing_target' : 'ask_without_target'
    return 'not_understood'
  }
  if (item.expect === 'ask') {
    if (kind === 'navigate') return 'wrong_navigate'
    if (kind === 'ask') return ids.every((id) => item.candidates.includes(id)) ? 'correct_ask' : 'ask_with_stray'
    return 'not_understood'
  }
  if (item.expect === 'reject') {
    if (kind === 'navigate') return 'reject_navigated'
    if (kind === 'ask') return 'reject_asked'
    return 'correct_reject'
  }
  throw new Error(`Unknown expect ${item.expect} on ${item.id}`)
}

export function tally(rows) {
  const n = rows.length
  const count = (f) => rows.filter(f).length
  const nav = rows.filter((r) => r.expect === 'navigate')
  const ask = rows.filter((r) => r.expect === 'ask')
  const rej = rows.filter((r) => r.expect === 'reject')
  const outcomes = {}
  for (const r of rows) outcomes[r.outcome] = (outcomes[r.outcome] ?? 0) + 1
  const asked = count((r) => r.kind === 'ask')
  const rate = (a, b) => (b ? a / b : null)
  return {
    items: n,
    navigateItems: nav.length,
    askItems: ask.length,
    rejectItems: rej.length,
    correctNavigate: count((r) => r.outcome === 'correct_navigate'),
    correctNavigateRate: rate(count((r) => r.outcome === 'correct_navigate'), nav.length),
    wrongNavigate: count((r) => r.outcome === 'wrong_navigate' || r.outcome === 'reject_navigated'),
    wrongNavigateRate: rate(count((r) => r.outcome === 'wrong_navigate' || r.outcome === 'reject_navigated'), n),
    correctAsk: count((r) => r.outcome === 'correct_ask'),
    correctAskRate: rate(count((r) => r.outcome === 'correct_ask'), ask.length),
    askPrecision: rate(count((r) => r.kind === 'ask' && r.expect === 'ask'), asked),
    askRate: rate(asked, n),
    notUnderstoodRate: rate(count((r) => r.kind === 'not_understood'), n),
    correctReject: count((r) => r.outcome === 'correct_reject'),
    rejectNavigated: count((r) => r.outcome === 'reject_navigated'),
    guardHits: count((r) => r.guard !== 'ok'),
    outcomes,
  }
}

export function range(from, to, step) {
  const out = []
  for (let v = from; v <= to + 1e-9; v += step) out.push(Math.round(v * 1000) / 1000)
  return out
}

// Selection on TUNING only (config decision.selection). scored: [{item, scores:Map, guard}].
export function selectThresholds(scored, tauGrid, deltaGrid, maxWrong) {
  const candidates = []
  for (const tau of tauGrid) for (const delta of deltaGrid) {
    const rows = scored.map(({ item, scores, guard }) => {
      const d = guard === 'ok' ? decide(scores, tau, delta) : { kind: 'not_understood', ids: [] }
      return { expect: item.expect, kind: d.kind, guard, outcome: classify(item, d) }
    })
    const t = tally(rows)
    candidates.push({ tau, delta, feasible: t.wrongNavigateRate <= maxWrong + 1e-12 && t.rejectNavigated === 0, correctNavigate: t.correctNavigate, wrongNavigateRate: t.wrongNavigateRate, rejectNavigated: t.rejectNavigated })
  }
  const byPreference = (a, b) => b.correctNavigate - a.correctNavigate || b.delta - a.delta || b.tau - a.tau
  const feasible = candidates.filter((c) => c.feasible).sort(byPreference)
  if (feasible.length) return { ...feasible[0], gridSize: candidates.length, feasibleCount: feasible.length }
  // No feasible point: fall back to the safest point and flag it. The flag is carried into the report.
  const safest = [...candidates].sort((a, b) => a.rejectNavigated - b.rejectNavigated || a.wrongNavigateRate - b.wrongNavigateRate || byPreference(a, b))[0]
  return { ...safest, gridSize: candidates.length, feasibleCount: 0 }
}

export function confusion(rows) {
  const m = {}
  for (const r of rows) {
    const truth = r.expect === 'navigate' ? r.target : r.expect === 'ask' ? 'ASK' : 'REJECT'
    const pred = r.kind === 'navigate' ? r.ids[0] : r.kind === 'ask' ? 'ASK' : 'NOT_UNDERSTOOD'
    m[truth] ??= {}
    m[truth][pred] = (m[truth][pred] ?? 0) + 1
  }
  return m
}

// Exact two-sided McNemar test on paired correctness (b = only X right, c = only Y right).
export function mcnemarExact(b, c) {
  const n = b + c
  if (n === 0) return 1
  const k = Math.min(b, c)
  let p = 0
  let coef = 1
  for (let i = 0; i <= k; i++) {
    if (i > 0) coef = (coef * (n - i + 1)) / i
    p += coef
  }
  return Math.min(1, (2 * p) / 2 ** n)
}

export function percentile(values, q) {
  if (!values.length) return null
  const s = [...values].sort((a, b) => a - b)
  return s[Math.min(s.length - 1, Math.ceil(q * s.length) - 1)]
}
