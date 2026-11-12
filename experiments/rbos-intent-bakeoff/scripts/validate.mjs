// Stage 2 validator: manifest hashes, schema, coverage, route allowlist, duplicates and leakage.
// Usage: node scripts/validate.mjs        (non-zero exit on any error)
import { FileAccess, sha256, verifyManifest } from './lib/io.mjs'
import { validateData } from './lib/validate.mjs'
import { LEXICON_FILE, generateLexicon } from './build-lexicon.mjs'

export function runValidation(fa) {
  const { checked } = verifyManifest(fa)
  const cfg = fa.json('config.v1.json')
  const destinations = fa.json(cfg.files.destinations)
  const anchors = fa.json(cfg.files.anchors)
  const tuning = fa.jsonl(cfg.files.tuning)
  const test = fa.jsonl(cfg.files.test)
  const result = validateData({ destinations, anchors, tuning, test })

  // Lexicon must be generated from the manifest-pinned sources and match the generator exactly.
  if (!fa.exists(LEXICON_FILE)) result.errors.push(`${LEXICON_FILE} missing; run node scripts/build-lexicon.mjs`)
  else {
    const lex = fa.json(LEXICON_FILE)
    const expected = generateLexicon(new FileAccess({ root: fa.root, forbid: [cfg.files.tuning, cfg.files.test] }))
    if (JSON.stringify(lex) !== JSON.stringify(expected)) result.errors.push(`${LEXICON_FILE} differs from generator output (hand-edited or stale)`)
    result.stats.lexiconSha256 = sha256(fa.bytes(LEXICON_FILE))
  }
  result.ok = result.errors.length === 0
  return { ...result, manifestChecked: checked }
}

function main() {
  const r = runValidation(new FileAccess())
  console.log(`Manifest verified: ${r.manifestChecked.join(', ')}`)
  console.log(`Stats: ${JSON.stringify(r.stats)}`)
  for (const w of r.warnings) console.log(`WARN  ${w}`)
  for (const e of r.errors) console.log(`ERROR ${e}`)
  console.log(r.ok ? 'VALIDATION PASSED' : `VALIDATION FAILED (${r.errors.length} errors)`)
  process.exit(r.ok ? 0 : 1)
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('scripts/validate.mjs')) main()
