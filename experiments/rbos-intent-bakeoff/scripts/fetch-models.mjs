// Stage 3: download pinned model files into ./models/<repo>/ and record hashes.
// Usage: node scripts/fetch-models.mjs --pin   resolve current commit SHA, download, print record (one-time)
//        node scripts/fetch-models.mjs          download missing files at the revision pinned in config and verify hashes
// Only files listed in FILES are fetched. Inference never downloads (allowRemoteModels=false).
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { ROOT } from './lib/io.mjs'

export const FILES = ['config.json', 'tokenizer.json', 'tokenizer_config.json', 'onnx/model_int8.onnx']
const sha = (p) => createHash('sha256').update(readFileSync(p)).digest('hex')

async function fetchFile(repo, revision, file, dest) {
  const url = `https://huggingface.co/${repo}/resolve/${revision}/${file}`
  const res = await fetch(url)
  if (!res.ok) throw new Error(`${url} -> HTTP ${res.status}`)
  mkdirSync(dirname(dest), { recursive: true })
  writeFileSync(dest, Buffer.from(await res.arrayBuffer()))
}

async function main() {
  const pin = process.argv.includes('--pin')
  const cfg = JSON.parse(readFileSync(join(ROOT, 'config.v1.json'), 'utf8'))
  const record = {}
  let failed = false
  for (const key of ['C1', 'C2']) {
    const r = cfg.resolvers[key]
    let revision = r.revision
    if (pin) {
      const meta = await (await fetch(`https://huggingface.co/api/models/${r.repo}`)).json()
      revision = meta.sha
    }
    if (!/^[0-9a-f]{40}$/.test(revision)) throw new Error(`${key}: revision not pinned to a 40-char commit SHA`)
    const files = {}
    for (const f of FILES) {
      const dest = join(ROOT, 'models', ...r.repo.split('/'), ...f.split('/'))
      if (!existsSync(dest)) await fetchFile(r.repo, revision, f, dest)
      files[f] = { sha256: sha(dest), bytes: statSync(dest).size }
      const expected = r.files?.[f]?.sha256
      if (!pin && expected && expected !== files[f].sha256) { console.error(`HASH MISMATCH ${key} ${f}`); failed = true }
      if (!pin && !expected) { console.error(`${key} ${f} has no recorded hash`); failed = true }
    }
    record[key] = { repo: r.repo, revision, files }
  }
  console.log(JSON.stringify(record, null, 2))
  if (failed) process.exit(1)
  if (!pin) console.log('All model files verified against config.v1.json')
}

main().catch((e) => { console.error(e.message); process.exit(1) })
