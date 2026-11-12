// Anchor-only lexicon generation (specs/normalisation.v1.1.md section 4). Deterministic, no model.
import { l0, l1Token, tokens } from './normalise.mjs'

export const LEXICON_VERSION = 'v1'
export const prefixOf = (id) => id.split('.')[0]

export function buildLexicon({ destinations, anchors, sourceHashes }) {
  const ids = destinations.destinations.map((d) => d.id)
  const anchorMap = anchors.anchors

  // Step 1-2: fold every anchor and record which destinations contain each key.
  const keyDest = new Map()
  const prepared = []
  const foldSources = new Map()
  for (const id of ids) {
    for (const phrase of anchorMap[id] ?? []) {
      const raw = tokens(l0(phrase))
      const keys = raw.map(l1Token)
      raw.forEach((r, i) => {
        if (!foldSources.has(keys[i])) foldSources.set(keys[i], new Set())
        foldSources.get(keys[i]).add(r)
      })
      prepared.push({ id, keys })
      for (const k of keys) {
        if (!keyDest.has(k)) keyDest.set(k, new Set())
        keyDest.get(k).add(id)
      }
    }
  }

  // Step 3: tag only when every containing destination shares one page prefix.
  const tagsFor = (k) => {
    const prefixes = new Set([...keyDest.get(k)].map(prefixOf))
    return prefixes.size === 1 ? ['@' + [...prefixes][0]] : []
  }

  // Step 4: slot-filler synonyms within the same destination, supported by at least 2 destinations.
  const pairSupport = new Map()
  for (const id of ids) {
    const mine = prepared.filter((p) => p.id === id)
    const slots = new Map()
    for (const { keys } of mine) {
      keys.forEach((k, i) => {
        const template = keys.length + '|' + keys.map((x, j) => (j === i ? '_' : x)).join(' ')
        if (!slots.has(template)) slots.set(template, new Set())
        slots.get(template).add(k)
      })
    }
    for (const fillers of slots.values()) {
      const list = [...fillers].sort()
      for (let a = 0; a < list.length; a++) for (let b = a + 1; b < list.length; b++) {
        const pair = list[a] + '\u0000' + list[b]
        if (!pairSupport.has(pair)) pairSupport.set(pair, new Set())
        pairSupport.get(pair).add(id)
      }
    }
  }
  const parent = new Map()
  const find = (x) => { while (parent.has(x) && parent.get(x) !== x) x = parent.get(x); return x }
  const union = (a, b) => {
    const ra = find(a); const rb = find(b)
    if (ra === rb) return
    const [lo, hi] = [ra, rb].sort()
    parent.set(lo, lo); parent.set(hi, lo)
  }
  const synonymPairs = []
  for (const [pair, dests] of [...pairSupport].sort(([a], [b]) => (a < b ? -1 : 1))) {
    if (dests.size >= 2) {
      const [a, b] = pair.split('\u0000')
      synonymPairs.push({ a, b, support: [...dests].sort() })
      union(a, b)
    }
  }

  const tokensOut = {}
  for (const k of [...keyDest.keys()].sort()) tokensOut[k] = { tags: tagsFor(k), classRep: find(k) }

  const collisions = [...foldSources].filter(([, s]) => s.size > 1).map(([key, s]) => ({ key, sources: [...s].sort() })).sort((a, b) => (a.key < b.key ? -1 : 1))

  return {
    version: LEXICON_VERSION,
    generator: 'scripts/build-lexicon.mjs',
    sourceHashes,
    stats: {
      anchors: prepared.length,
      keys: keyDest.size,
      taggedKeys: Object.values(tokensOut).filter((t) => t.tags.length).length,
      synonymPairs: synonymPairs.length,
      synonymClasses: new Set(Object.values(tokensOut).filter((t, i, all) => all.some((o) => o !== t && o.classRep === t.classRep)).map((t) => t.classRep)).size,
      foldCollisions: collisions.length,
    },
    synonymPairs,
    foldCollisions: collisions,
    tokens: tokensOut,
  }
}
