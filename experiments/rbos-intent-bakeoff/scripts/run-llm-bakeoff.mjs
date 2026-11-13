import { createHash } from 'node:crypto'
import { createWriteStream, mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { Readable, Transform } from 'node:stream'
import { pipeline } from 'node:stream/promises'
import { ROOT, FileAccess, sha256 } from './lib/io.mjs'
import { loadContext } from './run.mjs'
import { classify, tally, percentile } from './lib/evaluate.mjs'
import { chatMessages, parseDecision } from './lib/llm-router.mjs'

const MODEL = { repo: 'onnx-community/Qwen2.5-1.5B-Instruct', revision: '9f9c976052bfb08a20793f0c3013563dc095c966', file: 'onnx/model_q4f16.onnx', sha256: '19dec9f63488016185ba997d5e4492b5ac5b4f7ef1abb45243a91de958838dcd' }
const FILES = ['added_tokens.json','config.json','generation_config.json','merges.txt','quantize_config.json','special_tokens_map.json','tokenizer.json','tokenizer_config.json','vocab.json',MODEL.file]

async function download(file, destination) {
  const url = 'https://huggingface.co/' + MODEL.repo + '/resolve/' + MODEL.revision + '/' + file
  const response = await fetch(url, { redirect: 'follow' })
  if (!response.ok || !response.body) throw new Error(file + ': HTTP ' + response.status)
  mkdirSync(dirname(destination), { recursive: true })
  const digest = createHash('sha256')
  const verifier = new Transform({ transform(chunk, _encoding, callback) { digest.update(chunk); callback(null, chunk) } })
  await pipeline(Readable.fromWeb(response.body), verifier, createWriteStream(destination))
  const actual = digest.digest('hex')
  if (file === MODEL.file && actual !== MODEL.sha256) throw new Error('Qwen ONNX SHA-256 mismatch: ' + actual)
  return { file, sha256: actual }
}

function contentOf(result) {
  const item = Array.isArray(result) ? result[0] : result
  const generated = item && item.generated_text
  if (typeof generated === 'string') return generated
  if (Array.isArray(generated)) return generated.at(-1)?.content ?? ''
  return ''
}


const SMOKE_CASES = ['t003', 't037', 't111', 't141', 't156']

async function main() {
  const runId = process.env.RBOS_LLM_RUN_ID || 'qwen-' + (process.env.GITHUB_RUN_ID || 'local') + '-' + (process.env.GITHUB_RUN_ATTEMPT || '1')
  const modelDir = join(ROOT, 'models', ...MODEL.repo.split('/'))
  const files = []
  for (const file of FILES) files.push(await download(file, join(modelDir, ...file.split('/'))))
  const { pipeline: makePipeline, env } = await import('@huggingface/transformers')
  env.allowRemoteModels = false
  env.allowLocalModels = true
  env.localModelPath = join(ROOT, 'models')
  const modelLoadStarted = process.hrtime.bigint()
  const generator = await makePipeline('text-generation', MODEL.repo, { dtype: 'q4f16', local_files_only: true })
  const modelLoadMs = Number(process.hrtime.bigint() - modelLoadStarted) / 1e6

  const fa = new FileAccess()
  const ctx = loadContext(fa, { phase: 'test' })
  const allItems = fa.jsonl(ctx.cfg.files.test)
  const smoke = process.argv.includes('--smoke')
  const testItems = smoke ? SMOKE_CASES.map((id) => allItems.find((item) => item.id === id)).filter(Boolean) : allItems
  if (smoke && testItems.length !== SMOKE_CASES.length) throw new Error('Smoke fixture IDs are missing from the frozen test set')
  const allowed = new Set(ctx.destinations.destinations.map((d) => d.id))
  const rows = []
  for (const item of testItems) {
    const started = process.hrtime.bigint()
    let decision, raw = '', failure = null
    try {
      const result = await generator(chatMessages(item.text, ctx.destinations.destinations), { max_new_tokens: smoke ? 48 : 72, do_sample: false })
      raw = contentOf(result)
      decision = parseDecision(raw, allowed)
    } catch (error) {
      failure = error instanceof Error ? error.message : String(error)
      decision = { kind: 'not_understood', ids: [], reason: 'inference_error' }
    }
    const ms = Number(process.hrtime.bigint() - started) / 1e6
    rows.push({ id:item.id, lang:item.lang, expect:item.expect, target:item.target??null, candidates:item.candidates??null, kind:decision.kind, ids:decision.ids, outcome:classify(item,decision), reason:decision.reason, ms, debatable:['t041','t061','t068','t074','t103','t132','t138','t140'].includes(item.id), raw, failure })
    console.log(item.id + ': ' + decision.kind + ' ' + decision.ids.join(',') + ' -> ' + rows.at(-1).outcome + ' (' + ms.toFixed(0) + 'ms)')
  }

  const byScope = {}
  for (const [scope, keep] of Object.entries({ all: () => true, excludingDebatable: (r) => !r.debatable })) {
    const scoped = rows.filter(keep)
    byScope[scope] = {
      overall: tally(scoped),
      byLang: Object.fromEntries(['en','hi-Deva','hi-Latn','mixed'].map((lang) => [lang,tally(scoped.filter((r) => r.lang === lang))])),
      latencyMs: { p50:percentile(scoped.map((r)=>r.ms),.5), p95:percentile(scoped.map((r)=>r.ms),.95) }
    }
  }
  const smokeExpectations = {
    t003: { kind: 'navigate', ids: ['home'] },
    t037: { kind: 'navigate', ids: ['stock.current'] },
    t111: { kind: 'ask', ids: ['sell.return', 'buy.return'] },
    t141: { kind: 'not_understood', ids: [] },
    t156: { kind: 'not_understood', ids: [] }
  }
  const smokeFailures = smoke ? rows.filter((row) => {
    const expected = smokeExpectations[row.id]
    return !expected || row.kind !== expected.kind ||
      JSON.stringify(row.ids) !== JSON.stringify(expected.ids)
  }).map((row) => ({ id: row.id, actual: { kind: row.kind, ids: row.ids }, expected: smokeExpectations[row.id] })) : []
  const smokeGate = smoke ? { passed: smokeFailures.length === 0, failures: smokeFailures } : null

  const report = {
    runId, model:MODEL, promptVersion:'qwen-router-system-fewshot-v3', mode: smoke ? 'smoke' : 'full', modelLoadMs,
    dataset:{version:'v1 frozen synthetic',items:rows.length,totalItems:allItems.length,sha256:sha256(fa.bytes(ctx.cfg.files.test)),testReadCount:fa.log.filter((p)=>p===ctx.cfg.files.test).length},
    modelFiles:files, scope:byScope, smokeGate,
    failures:rows.filter((r)=>r.failure).length, rows,
    caveat:'Synthetic assistant-authored text-only test set. Not real shopkeeper accuracy, ASR accuracy, Android latency, or production evidence.'
  }
  const out=join(ROOT,'results',runId)
  mkdirSync(out,{recursive:true})
  writeFileSync(join(out,'report.json'),JSON.stringify(report,null,2)+'\n')
  const lines=['# '+runId,'','Mode: '+report.mode,'Model: '+MODEL.repo+' ('+MODEL.revision+')','Model load: '+modelLoadMs.toFixed(0)+' ms','Dataset: '+rows.length+' of '+allItems.length+' frozen synthetic utterances; read '+report.dataset.testReadCount+' time(s).','','| Scope | Correct nav | Wrong nav | Correct ask | Reject navigated | Not understood | p50 ms | p95 ms |','|---|---:|---:|---:|---:|---:|---:|---:|',...Object.entries(byScope).map(([k,v])=>'| '+k+' | '+(v.overall.correctNavigateRate*100).toFixed(1)+'% | '+(v.overall.wrongNavigateRate*100).toFixed(1)+'% | '+(v.overall.correctAskRate*100).toFixed(1)+'% | '+v.overall.rejectNavigated+' | '+(v.overall.notUnderstoodRate*100).toFixed(1)+'% | '+v.latencyMs.p50.toFixed(0)+' | '+v.latencyMs.p95.toFixed(0)+' |'),'','## Language safety','','| Language | Correct nav | Wrong nav | Reject navigated |','|---|---:|---:|---:|',...Object.entries(byScope.all.byLang).map(([k,v])=>'| '+k+' | '+(v.correctNavigateRate===null?'n/a':(v.correctNavigateRate*100).toFixed(1)+'%')+' | '+(v.wrongNavigateRate===null?'n/a':(v.wrongNavigateRate*100).toFixed(1)+'%')+' | '+v.rejectNavigated+' |'),'','Smoke acceptance: both navigation controls correct, the ambiguous return control asks for both return destinations, and both reject controls return NOT_UNDERSTOOD.','',report.caveat,'']
  writeFileSync(join(out,'summary.md'),lines.join('\n'))
  console.log(lines.join('\n'))
  if (smoke && (rows.some((r) => r.failure || !r.raw.trim()) || !smokeGate.passed)) { throw new Error('Smoke acceptance failed: ' + JSON.stringify(smokeFailures)) }
}
main().catch((error)=>{console.error(error instanceof Error?error.stack||error.message:String(error));process.exitCode=1})
