import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

export const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')
export const MANIFEST = 'MANIFEST.sha256'

export const sha256 = (data) => createHash('sha256').update(data).digest('hex')
export const stableJson = (value) => JSON.stringify(value, null, 2) + '\n'

// Every data read goes through FileAccess so that each phase can (a) forbid files and (b) log what it opened.
export class FileAccess {
  constructor({ root = ROOT, forbid = [] } = {}) {
    this.root = resolve(root)
    this.forbidden = new Set()
    this.log = []
    for (const p of forbid) this.forbid(p)
  }
  rel(p) { return relative(this.root, isAbsolute(p) ? p : resolve(this.root, p)).split(sep).join('/') }
  abs(p) { return resolve(this.root, p) }
  forbid(p) { this.forbidden.add(this.rel(p)) }
  exists(p) { return existsSync(this.abs(p)) }
  bytes(p) {
    const r = this.rel(p)
    if (this.forbidden.has(r)) throw new Error(`Access to ${r} is forbidden in this phase`)
    this.log.push(r)
    return readFileSync(this.abs(p))
  }
  text(p) { return this.bytes(p).toString('utf8').replace(/^\uFEFF/, '') }
  json(p) { return JSON.parse(this.text(p)) }
  jsonl(p) {
    return this.text(p).split(/\r?\n/).map((line, i) => [line, i]).filter(([line]) => line.trim()).map(([line, i]) => {
      try { return JSON.parse(line) } catch { throw new Error(`${this.rel(p)} line ${i + 1}: invalid JSON`) }
    })
  }
  write(p, content) {
    const a = this.abs(p)
    mkdirSync(dirname(a), { recursive: true })
    writeFileSync(a, content)
  }
}

export function parseManifest(text) {
  const entries = new Map()
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trimEnd()
    if (!line) continue
    const m = /^([0-9a-f]{64}) {2}(\S.*)$/.exec(line)
    if (!m) throw new Error(`Malformed manifest line: ${line}`)
    if (entries.has(m[2])) throw new Error(`Duplicate manifest entry: ${m[2]}`)
    entries.set(m[2], m[1])
  }
  return entries
}

export const readManifest = (fa) => parseManifest(fa.text(MANIFEST))

// Verifies every manifest entry. `skip` lists paths that must not be opened in this phase (the test set during tuning).
export function verifyManifest(fa, { skip = [] } = {}) {
  const manifest = readManifest(fa)
  const checked = []
  const skipped = []
  for (const [path, expected] of manifest) {
    if (skip.includes(path)) { skipped.push(path); continue }
    if (!fa.exists(path)) throw new Error(`Manifest file missing: ${path}`)
    const actual = sha256(fa.bytes(path))
    if (actual !== expected) throw new Error(`Hash mismatch for ${path}: expected ${expected}, got ${actual}. Frozen files must not change; create a new version instead.`)
    checked.push(path)
  }
  return { manifest, checked, skipped }
}
