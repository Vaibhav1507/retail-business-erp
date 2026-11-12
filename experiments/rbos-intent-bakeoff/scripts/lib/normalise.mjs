// Shared input preparation for A, C1 and C2 (specs/normalisation.v1.1.md).
// Pure functions only: no file access, no word lists. The lexicon used by L2 is passed in.

export const LEVELS = ['L0', 'L1', 'L2']

// ---------- Guard G (section 1) ----------
export function guard(raw, allowedHashes) {
  const text = String(raw ?? '').normalize('NFC')
  if (!text.trim()) return { ok: false, reason: 'empty' }
  const lower = text.toLowerCase()
  if (lower.includes('://') || lower.includes('javascript:') || lower.includes('data:')) return { ok: false, reason: 'url-or-scheme' }
  for (const m of text.matchAll(/#\/([^\s]*)/g)) {
    if (!allowedHashes.has('#/' + m[1].replace(/[^\p{L}\p{N}/._-]+$/u, ''))) return { ok: false, reason: 'unknown-route' }
  }
  if (/\p{Nd}+/u.test(text)) return { ok: false, reason: 'digits' }
  return { ok: true }
}

// ---------- L0 (section 2) ----------
export function l0(raw) {
  return String(raw ?? '')
    .normalize('NFC')
    .toLowerCase()
    .replace(/[^\p{L}\p{M}\p{Nd}\s]/gu, ' ')
    .replace(/\s+/gu, ' ')
    .trim()
}

// ---------- L1 (section 3) ----------
const DEVA_CONSONANTS = {
  'क': 'k', 'ख': 'kh', 'ग': 'g', 'घ': 'gh', 'ङ': 'n',
  'च': 'ch', 'छ': 'chh', 'ज': 'j', 'झ': 'jh', 'ञ': 'n',
  'ट': 't', 'ठ': 'th', 'ड': 'd', 'ढ': 'dh', 'ण': 'n',
  'त': 't', 'थ': 'th', 'द': 'd', 'ध': 'dh', 'न': 'n',
  'प': 'p', 'फ': 'ph', 'ब': 'b', 'भ': 'bh', 'म': 'm',
  'य': 'y', 'र': 'r', 'ल': 'l', 'ळ': 'l', 'व': 'v',
  'श': 'sh', 'ष': 'sh', 'स': 's', 'ह': 'h',
}
// Precomposed nukta letters (U+0958-U+095F) map to their base consonant.
const DEVA_NUKTA_PRECOMPOSED = { 'क़': 'क', 'ख़': 'ख', 'ग़': 'ग', 'ज़': 'ज', 'ड़': 'ड', 'ढ़': 'ढ', 'फ़': 'फ', 'य़': 'य' }
const DEVA_VOWELS = { 'अ': 'a', 'आ': 'aa', 'इ': 'i', 'ई': 'ii', 'उ': 'u', 'ऊ': 'uu', 'ऋ': 'ri', 'ए': 'e', 'ऐ': 'ai', 'ओ': 'o', 'औ': 'au', 'ऑ': 'o', 'ऍ': 'e' }
const DEVA_MATRAS = { 'ा': 'aa', 'ि': 'i', 'ी': 'ii', 'ु': 'u', 'ू': 'uu', 'ृ': 'ri', 'े': 'e', 'ै': 'ai', 'ो': 'o', 'ौ': 'au', 'ॉ': 'o', 'ॅ': 'e' }
const VIRAMA = '्'
const NUKTA = '़'
const DEVA_NASAL = { 'ं': 'n', 'ँ': 'n' }
const VISARGA = 'ः'

export const hasDevanagari = (s) => /[\u0900-\u097F]/u.test(s)

// Character-level ISO-15919 simplified to ASCII; the inherent schwa is dropped only at the end of a word.
export function transliterateDevanagari(word) {
  const chars = [...word.normalize('NFC')].map((c) => DEVA_NUKTA_PRECOMPOSED[c] ?? c).filter((c) => c !== NUKTA)
  const out = []
  let pendingSchwa = false
  const flush = () => { if (pendingSchwa) { out.push('a'); pendingSchwa = false } }
  for (const c of chars) {
    if (DEVA_CONSONANTS[c]) { flush(); out.push(DEVA_CONSONANTS[c]); pendingSchwa = true; continue }
    if (c === VIRAMA) { pendingSchwa = false; continue }
    if (DEVA_MATRAS[c]) { pendingSchwa = false; out.push(DEVA_MATRAS[c]); continue }
    if (DEVA_VOWELS[c]) { flush(); out.push(DEVA_VOWELS[c]); continue }
    if (DEVA_NASAL[c]) { flush(); out.push(DEVA_NASAL[c]); continue }
    if (c === VISARGA) { flush(); out.push('h'); continue }
    flush()
    out.push(c)
  }
  // pendingSchwa still set here means the word ends in a consonant with an inherent schwa: drop it.
  return out.join('')
}

// Latin key folding, applied to every Latin token (original or transliterated).
export function foldLatin(token) {
  if (!/^[a-z]+$/.test(token)) return token
  let t = token
    .replace(/ph/g, 'f')
    .replace(/ck/g, 'k')
    .replace(/q/g, 'k')
    .replace(/w/g, 'v')
    .replace(/z/g, 'j')
    .replace(/e{2,}/g, 'i')
    .replace(/o{2,}/g, 'u')
    .replace(/([a-z])\1+/g, '$1')
  if (t.length > 1 && /[aeiou]h$/.test(t)) t = t.slice(0, -1)
  return t
}

export function l1Token(token) {
  return foldLatin(hasDevanagari(token) ? transliterateDevanagari(token) : token)
}

export const tokens = (s) => (s ? s.split(' ').filter(Boolean) : [])

export function l1(raw) {
  return tokens(l0(raw)).map(l1Token).filter(Boolean).join(' ')
}

// ---------- L2 (section 4) ----------
export function l2(raw, lexicon) {
  if (!lexicon?.tokens) throw new Error('L2 requires a generated lexicon')
  const keys = tokens(l1(raw))
  const tags = new Set()
  const mapped = keys.map((k) => {
    const entry = Object.hasOwn(lexicon.tokens, k) ? lexicon.tokens[k] : undefined
    for (const tag of entry?.tags ?? []) tags.add(tag)
    return entry?.classRep ?? k
  })
  return [...mapped, ...[...tags].sort()].join(' ')
}

export function prepare(raw, level, lexicon) {
  if (level === 'L0') return l0(raw)
  if (level === 'L1') return l1(raw)
  if (level === 'L2') return l2(raw, lexicon)
  throw new Error(`Unknown level ${level}`)
}
