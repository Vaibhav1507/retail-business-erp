import { createHash } from 'node:crypto'
import { createWriteStream, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { Readable, Transform } from 'node:stream'
import { pipeline } from 'node:stream/promises'
import { ROOT, FileAccess, sha256 } from './lib/io.mjs'
import { test as scoreTest, tune } from './run.mjs'

const MODELS = {
  C1: {
    repo: 'Xenova/multilingual-e5-small',
    expectedRevisionPrefix: '761b726',
    modelSha256: '4d24e2bc01a447951524466ef533e52944bf48509e6552810bcee1a2711cb02c',
    tokenizerSha256: '0b44a9d7b51c3c62626640cda0e2c2f70fdacdc25bbbd68038369d14ebdf4c39',
    files: [
      'config.json',
      'tokenizer.json',
      'tokenizer_config.json',
      'special_tokens_map.json',
      'sentencepiece.bpe.model',
      'onnx/model_int8.onnx',
    ],
  },
  C2: {
    repo: 'Xenova/paraphrase-multilingual-MiniLM-L12-v2',
    expectedRevisionPrefix: '2c4055b',
    modelSha256: 'd6ea442ff6a891daefed7c83b2f596fc5dc66bf697e4d006236f64f34bbcf4c8',
    tokenizerSha256: 'b60b6b43406a48bf3638526314f3d232d97058bc93472ff2de930d43686fa441',
    files: [
      'config.json',
      'tokenizer.json',
      'tokenizer_config.json',
      'special_tokens_map.json',
      'unigram.json',
      'onnx/model_int8.onnx',
    ],
  },
}

async function getPinnedRevision(repo, prefix) {
  const response = await fetch('https://huggingface.co/api/models/' + repo, { redirect: 'follow' })
  if (!response.ok) throw new Error('Model metadata request failed for ' + repo + ': HTTP ' + response.status)
  const metadata = await response.json()
  if (!/^[a-f0-9]{40}$/.test(metadata.sha || '') || !metadata.sha.startsWith(prefix)) {
    throw new Error(repo + ' revision moved unexpectedly: expected prefix ' + prefix + ', received ' + (metadata.sha || 'no SHA'))
  }
  return metadata.sha
}

async function downloadVerified(repo, revision, file, expectedSha, destination) {
  const url = 'https://huggingface.co/' + repo + '/resolve/' + revision + '/' + file
  const response = await fetch(url, { redirect: 'follow' })
  if (!response.ok || !response.body) throw new Error(url + ' -> HTTP ' + response.status)

  mkdirSync(dirname(destination), { recursive: true })
  const digest = createHash('sha256')
  let bytes = 0
  const verifier = new Transform({
    transform(chunk, _encoding, callback) {
      digest.update(chunk)
      bytes += chunk.length
      callback(null, chunk)
    },
  })
  await pipeline(Readable.fromWeb(response.body), verifier, createWriteStream(destination))
  const actualSha = digest.digest('hex')
  if (expectedSha && actualSha !== expectedSha) {
    throw new Error(repo + '/' + file + ' SHA-256 mismatch: expected ' + expectedSha + ', received ' + actualSha)
  }
  return { sha256: actualSha, bytes }
}

async function prepareModels(config) {
  const pins = {}
  for (const key of ['C1', 'C2']) {
    const spec = config.resolvers[key]
    const approved = MODELS[key]
    if (spec.repo !== approved.repo || spec.file !== 'onnx/model_int8.onnx') {
      throw new Error(key + ' configuration does not match the reviewed model artefact')
    }

    const revision = await getPinnedRevision(approved.repo, approved.expectedRevisionPrefix)
    const folder = join(ROOT, 'models', ...approved.repo.split('/'))
    const downloaded = {}
    for (const file of approved.files) {
      const destination = join(folder, ...file.split('/'))
      const expected = file === 'onnx/model_int8.onnx'
        ? approved.modelSha256
        : file === 'tokenizer.json'
          ? approved.tokenizerSha256
          : null
      downloaded[file] = await downloadVerified(approved.repo, revision, file, expected, destination)
    }

    spec.revision = revision
    spec.sha256 = approved.modelSha256
    spec.tokenizerSha256 = approved.tokenizerSha256
    pins[key] = {
      repo: approved.repo,
      revision,
      licence: spec.upstreamLicence,
      verifiedFiles: downloaded,
    }
  }
  return pins
}

function writeEphemeralPinnedConfig(config) {
  config.status = 'stage 3 models pinned and checksum-verified in the ephemeral CI workspace; stage 4 benchmark runs below'
  config.runtime.package = '@huggingface/transformers@4.3.2'
  const configPath = join(ROOT, 'config.v1.json')
  const configText = JSON.stringify(config, null, 2) + '\n'
  writeFileSync(configPath, configText)

  const manifestPath = join(ROOT, 'MANIFEST.sha256')
  const lines = readFileSync(manifestPath, 'utf8').trimEnd().split(/\r?\n/)
  const configHash = sha256(configText)
  let replaced = false
  const nextLines = lines.map((line) => {
    if (line.endsWith('  config.v1.json')) {
      replaced = true
      return configHash + '  config.v1.json'
    }
    return line
  })
  if (!replaced) throw new Error('MANIFEST.sha256 does not pin config.v1.json')
  writeFileSync(manifestPath, nextLines.join('\n') + '\n')
}

async function main() {
  const runId = process.env.RBOS_BAKEOFF_RUN_ID || 'semantic-' + (process.env.GITHUB_RUN_ID || 'local') + '-' + (process.env.GITHUB_RUN_ATTEMPT || '1')
  const configPath = join(ROOT, 'config.v1.json')
  const config = JSON.parse(readFileSync(configPath, 'utf8'))

  const pins = await prepareModels(config)
  writeEphemeralPinnedConfig(config)

  // Tuning must not be able to read the frozen test file. Use a fresh FileAccess
  // instance for the test phase after thresholds and their hash have been written.
  const tuningAccess = new FileAccess()
  const frozen = await tune(tuningAccess, { resolvers: ['A', 'C1', 'C2'], runId })

  const resultFolder = join(ROOT, 'results', runId)
  const pinsPath = join(resultFolder, 'model-pins.json')
  const pinReport = {
    runId,
    pinnedAt: new Date().toISOString(),
    package: '@huggingface/transformers@4.3.2',
    models: pins,
    frozenThresholds: frozen.thresholds,
    testReadCount: null,
    modelWeightsCommitted: false,
    modelWeightsUploadedAsArtifacts: false,
  }
  writeFileSync(pinsPath, JSON.stringify(pinReport, null, 2) + '\n')

  const testAccess = new FileAccess()
  const report = await scoreTest(testAccess, { runId })
  pinReport.testReadCount = report.testReadCount
  writeFileSync(pinsPath, JSON.stringify(pinReport, null, 2) + '\n')

  const summary = readFileSync(join(resultFolder, 'summary.md'), 'utf8')
  console.log(summary)
  console.log('\nRun ' + runId + ' complete. Test set read ' + report.testReadCount + ' time(s).')
  console.log('Only small result files are eligible for CI artifacts; model weights remain in the ephemeral runner workspace.')
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack || error.message : String(error))
  process.exitCode = 1
})
