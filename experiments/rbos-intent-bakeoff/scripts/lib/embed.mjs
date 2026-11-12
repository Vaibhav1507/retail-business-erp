// Local-only embedding for C1/C2. Never downloads: allowRemoteModels=false, models read from ./models/<repo>.
// Weights are NOT present until stage 3 is approved; callers get a clear error instead of a network fetch.
import { createHash } from 'node:crypto'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

export function modelDir(root, repo) { return join(root, 'models', ...repo.split('/')) }

export function checkModelFiles(root, resolver) {
  const dir = modelDir(root, resolver.repo)
  const model = join(dir, resolver.file)
  const tokenizer = join(dir, 'tokenizer.json')
  const missing = [model, tokenizer].filter((p) => !existsSync(p))
  if (missing.length) return { ok: false, reason: `model files not present (${missing.join(', ')}). Weight download is a separate approval (stage 3).` }
  if (/^TO_/.test(resolver.revision) || /^TO_/.test(resolver.sha256) || /^TO_/.test(resolver.tokenizerSha256)) return { ok: false, reason: 'model revision/hashes are not pinned in config.v1.json' }
  const h = (p) => createHash('sha256').update(readFileSync(p)).digest('hex')
  const actual = { sha256: h(model), tokenizerSha256: h(tokenizer) }
  if (actual.sha256 !== resolver.sha256 || actual.tokenizerSha256 !== resolver.tokenizerSha256) return { ok: false, reason: `model hash mismatch: ${JSON.stringify(actual)}` }
  return { ok: true, dir, ...actual }
}

export async function loadEmbedder(root, resolver, runtime) {
  const check = checkModelFiles(root, resolver)
  if (!check.ok) throw new Error(`${resolver.repo}: ${check.reason}`)
  const tf = await import('@huggingface/transformers')
  tf.env.allowRemoteModels = false
  tf.env.allowLocalModels = true
  tf.env.localModelPath = join(root, 'models')
  const extractor = await tf.pipeline('feature-extraction', resolver.repo, { dtype: 'q8', local_files_only: true })
  const prefix = resolver.queryPrefix ?? ''
  return {
    meta: { repo: resolver.repo, revision: resolver.revision, sha256: check.sha256, tokenizerSha256: check.tokenizerSha256, transformersVersion: tf.env.version },
    async embed(text) {
      const out = await extractor(prefix + text, { pooling: runtime.pooling, normalize: runtime.normalize })
      return Array.from(out.data)
    },
  }
}
