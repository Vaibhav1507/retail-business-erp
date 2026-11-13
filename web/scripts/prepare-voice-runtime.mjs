import { createHash } from 'node:crypto'
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const target = resolve(root, 'public/voice-runtime')
const revision = '68a8d77e88e58ed1201cc20a9c4adc5d9750723f'
const assets = [
  { name: 'needle.js', url: `https://raw.githubusercontent.com/udgamlabs/shruti/${revision}/examples/browser/needle/needle.js`, sha256: 'a675c66f506a1b6f922d852c0cc0b82a843ce9d0fab6a80a408106f516df126f' },
  { name: 'needle.wasm', url: `https://raw.githubusercontent.com/udgamlabs/shruti/${revision}/examples/browser/needle/needle.wasm`, sha256: 'a6f87da3e7a9e83c13982979f115cf0afa4b1a63641e58f31aa87dec8afd13d2' },
  { name: 'shruti.cact', url: 'https://huggingface.co/udgamlabs/Shruti/resolve/main/shruti.cact?download=true', sha256: 'b189e954aaed4a9fd41acbad3ac05e486381e96325b22a4c695b9ea5153d7065' },
]
await mkdir(target, { recursive: true })
for (const asset of assets) {
  const path = resolve(target, asset.name)
  try {
    const existing = await readFile(path)
    if (createHash('sha256').update(existing).digest('hex') === asset.sha256) continue
  } catch { /* download missing or stale runtime asset */ }
  const response = await fetch(asset.url, { redirect: 'follow' })
  if (!response.ok) throw new Error(`Could not download ${asset.name}: HTTP ${response.status}`)
  const bytes = Buffer.from(await response.arrayBuffer())
  const actual = createHash('sha256').update(bytes).digest('hex')
  if (actual !== asset.sha256) throw new Error(`Checksum mismatch for ${asset.name}: ${actual}`)
  await writeFile(path, bytes)
  console.log(`Verified ${asset.name} (${bytes.length} bytes)`)
}
