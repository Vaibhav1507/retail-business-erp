// Generates lexicon.v1.json from destinations.v1.json and data/anchors.v1.json ONLY.
// Usage: node scripts/build-lexicon.mjs [--check]
//   --check  regenerate in memory and fail if the committed lexicon differs (no write).
import { FileAccess, readManifest, sha256, stableJson } from './lib/io.mjs'
import { buildLexicon } from './lib/lexicon.mjs'

export const LEXICON_FILE = 'lexicon.v1.json'
export const LEXICON_SOURCES = { destinations: 'destinations.v1.json', anchors: 'data/anchors.v1.json' }

export function generateLexicon(fa) {
  const manifest = readManifest(fa)
  const sourceHashes = {}
  const data = {}
  for (const [key, path] of Object.entries(LEXICON_SOURCES)) {
    const bytes = fa.bytes(path)
    const actual = sha256(bytes)
    const expected = manifest.get(path)
    if (!expected) throw new Error(`${path} is not in the manifest`)
    if (actual !== expected) throw new Error(`Hash mismatch for ${path}: expected ${expected}, got ${actual}`)
    sourceHashes[key] = actual
    data[key] = JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, ''))
  }
  return buildLexicon({ ...data, sourceHashes })
}

async function main() {
  // Only the manifest and the two source files may be opened.
  const fa = new FileAccess({ forbid: ['data/tuning.v1.jsonl', 'data/test.v1.frozen.jsonl'] })
  const lexicon = generateLexicon(fa)
  const content = stableJson(lexicon)
  const opened = [...new Set(fa.log)].sort()
  if (process.argv.includes('--check')) {
    const current = fa.exists(LEXICON_FILE) ? fa.text(LEXICON_FILE) : null
    if (current !== content) { console.error(`${LEXICON_FILE} is stale or hand-edited`); process.exit(1) }
    console.log(`${LEXICON_FILE} matches the generator. sha256=${sha256(content)}`)
    return
  }
  fa.write(LEXICON_FILE, content)
  console.log(`Wrote ${LEXICON_FILE} sha256=${sha256(content)}`)
  console.log(`Inputs opened: ${opened.join(', ')}`)
  console.log(`Stats: ${JSON.stringify(lexicon.stats)}`)
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('scripts/build-lexicon.mjs')) main()
