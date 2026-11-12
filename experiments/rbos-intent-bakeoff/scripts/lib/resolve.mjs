// Shared decision rule (config.v1.json decision.rule) and Baseline A (specs/baseline-A.md).
import { tokens } from './normalise.mjs'

export const MAX_ASK = 3

// scores: Map<destinationId, number>. Same rule for A, C1 and C2.
export function decide(scores, tau, delta) {
  const ranked = [...scores].sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1))
  if (!ranked.length) return { kind: 'not_understood', ids: [], top1: 0, top2: 0 }
  const top1 = ranked[0][1]
  const top2 = ranked[1]?.[1] ?? 0
  const eps = 1e-9
  if (top1 + eps < tau) return { kind: 'not_understood', ids: [], top1, top2 }
  if (top1 - top2 + eps >= delta) return { kind: 'navigate', ids: [ranked[0][0]], top1, top2 }
  const ids = ranked.filter(([, s]) => top1 - s < delta - eps).slice(0, MAX_ASK).map(([id]) => id)
  return { kind: 'ask', ids, top1, top2 }
}

// Restricted Damerau-Levenshtein (optimal string alignment) distance.
export function damerauLevenshtein(a, b) {
  const A = [...a]; const B = [...b]
  const d = Array.from({ length: A.length + 1 }, (_, i) => Array.from({ length: B.length + 1 }, (_, j) => (i === 0 ? j : j === 0 ? i : 0)))
  for (let i = 1; i <= A.length; i++) {
    for (let j = 1; j <= B.length; j++) {
      const cost = A[i - 1] === B[j - 1] ? 0 : 1
      d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + cost)
      if (i > 1 && j > 1 && A[i - 1] === B[j - 2] && A[i - 2] === B[j - 1]) d[i][j] = Math.min(d[i][j], d[i - 2][j - 2] + 1)
    }
  }
  return d[A.length][B.length]
}

export function tokenMatches(q, a) {
  if (q === a) return true
  const lq = [...q].length; const la = [...a].length
  return lq >= 5 && la >= 5 && Math.abs(lq - la) <= 1 && damerauLevenshtein(q, a) <= 1
}

// Phrase index from prepared anchors only: [{id, tokens:Set}]. Label text is never added.
export function buildIndexA(preparedAnchors) {
  return preparedAnchors.map(({ id, text }) => ({ id, tokens: [...new Set(tokens(text))] })).filter((e) => e.tokens.length)
}

export function scoreA(preparedQuery, index) {
  const q = [...new Set(tokens(preparedQuery))]
  const scores = new Map()
  for (const { id, tokens: at } of index) {
    const hit = at.filter((a) => q.some((t) => tokenMatches(t, a))).length
    const s = hit / at.length
    if (!scores.has(id) || s > scores.get(id)) scores.set(id, s)
  }
  return scores
}

// Cosine scores for C: max over a destination's anchor vectors (vectors are L2-normalised).
export function scoreC(queryVec, anchorVecs) {
  const scores = new Map()
  for (const { id, vec } of anchorVecs) {
    let dot = 0
    for (let i = 0; i < vec.length; i++) dot += vec[i] * queryVec[i]
    if (!scores.has(id) || dot > scores.get(id)) scores.set(id, dot)
  }
  return scores
}
