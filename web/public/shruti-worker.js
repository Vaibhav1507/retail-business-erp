/* Local-only Shruti speech worker. Based on Udgam Labs' Apache-2.0 browser example. */
'use strict'
importScripts('/voice-runtime/needle.js')
const CACHE_NAME = 'rbos-shruti-hi-v1'
const MODEL_URL = '/voice-runtime/shruti.cact'
const MODEL_SHA256 = 'b189e954aaed4a9fd41acbad3ac05e486381e96325b22a4c695b9ea5153d7065'
const MODEL_BYTES = 29546527
const MAGIC = [0x84, 0x2a, 0xe1, 0x05]
const SR = 16000
// Keep this vocabulary small and specific. These are screen concepts, not user/customer names.
const KEYWORDS = [
 'customer', 'grahak', 'ग्राहक',
 'supplier', 'सप्लायर',
 'purchase', 'kharid', 'खरीद',
 'stock', 'स्टॉक', 'maal', 'माल',
 'bill', 'बिल', 'invoice',
 'expense', 'kharcha', 'खर्चा',
 'payment', 'भुगतान', 'पेमेंट',
 'collection', 'vasooli', 'वसूली',
 'inventory', 'supplier payment'
]
let M = null
let loaded = false
const encoder = new TextEncoder()
const send = message => self.postMessage(message)
function isModel(bytes) { return bytes?.length > 1024 && MAGIC.every((v, i) => bytes[i] === v) }
async function sha256Hex(bytes) {
 const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))
 return Array.from(digest, v => v.toString(16).padStart(2, '0')).join('')
}
async function openCache() { try { return await caches.open(CACHE_NAME) } catch { return null } }
async function getModel() {
 const cache = await openCache()
 if (cache) {
  const hit = await cache.match(MODEL_URL)
  if (hit) {
   const bytes = new Uint8Array(await hit.arrayBuffer())
   if (isModel(bytes) && await sha256Hex(bytes) === MODEL_SHA256) return { bytes, source: 'cache', saved: true }
   await cache.delete(MODEL_URL)
  }
 }
 const response = await fetch(MODEL_URL, { mode: 'cors', credentials: 'omit' })
 if (!response.ok || !response.body) throw new Error(`Speech model download failed (HTTP ${response.status}).`)
 const reader = response.body.getReader(), chunks = []
 let loadedBytes = 0, last = 0
 for (;;) {
  const part = await reader.read()
  if (part.done) break
  chunks.push(part.value); loadedBytes += part.value.length
  if (Date.now() - last > 100) { last = Date.now(); send({ type: 'progress', loaded: loadedBytes, total: Math.max(MODEL_BYTES, loadedBytes) }) }
 }
 const bytes = new Uint8Array(loadedBytes); let offset = 0
 for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length }
 if (!isModel(bytes) || await sha256Hex(bytes) !== MODEL_SHA256) throw new Error('Speech model failed its SHA-256 integrity check.')
 let saved = false
 if (cache) { try { await cache.put(MODEL_URL, new Response(bytes, { headers: { 'content-type': 'application/octet-stream' } })); saved = true } catch { /* storage quota: current session still works */ } }
 return { bytes, source: 'network', saved }
}
async function startEngine() {
 const response = await fetch('/voice-runtime/needle.wasm')
 if (!response.ok) throw new Error(`Speech engine download failed (HTTP ${response.status}).`)
 return createNeedle({ wasmBinary: await response.arrayBuffer(), print: () => {}, printErr: () => {} })
}
function lastError() { try { return M.UTF8ToString(M._needle_last_error()) || 'Speech engine error.' } catch { return 'Speech engine error.' } }
function alloc(size) { const pointer = M._malloc(size); if (!pointer) throw new Error('Not enough memory to run speech recognition.'); return pointer }
function copyIn(bytes) { const pointer = alloc(Math.max(4, bytes.length)); M.HEAPU8.set(bytes, pointer); return pointer }
async function init() {
 try {
  const [model, engine] = await Promise.all([getModel(), startEngine()])
  M = engine
  const pointer = copyIn(model.bytes)
  const result = M._needle_load(pointer, BigInt(model.bytes.length)); M._free(pointer)
  if (result < 0 || !(M._needle_models() & 2)) throw new Error(lastError())
  loaded = true
  send({ type: 'ready', source: model.source, saved: model.saved })
 } catch (error) { send({ type: 'error', stage: 'init', message: String(error?.message || error) }) }
}
function transcribe(message) {
 const pcm = message.pcm
 const seconds = pcm.length / SR
 let audioPointer = 0, languagePointer = 0, keywordsPointer = 0, outputPointer = 0
 try {
  audioPointer = copyIn(new Uint8Array(pcm.buffer, pcm.byteOffset, pcm.byteLength))
  languagePointer = message.language ? copyIn(encoder.encode(message.language + '\0')) : 0
  keywordsPointer = copyIn(encoder.encode(KEYWORDS.join('\n') + '\0'))
  const capacity = Math.max(1 << 16, Math.ceil(seconds) * 4096 + (1 << 14))
  outputPointer = alloc(capacity)
  const started = performance.now()
  // Needle's WASM API accepts newline-separated keyword hints and optional word timestamps.
  const length = M._needle_transcribe(audioPointer, pcm.length, languagePointer, keywordsPointer, 0, outputPointer, capacity)
  if (length < 0) throw new Error(lastError())
  const result = JSON.parse(M.UTF8ToString(outputPointer))
  send({
   type: message.partial ? 'partial-result' : 'result', id: message.id,
   captureId: message.captureId, text: result.text || '', language: result.language || null,
   ms: performance.now() - started
  })
 } finally {
  pcm.fill(0)
  if (audioPointer) { M.HEAPU8.fill(0, audioPointer, audioPointer + pcm.byteLength); M._free(audioPointer) }
  if (languagePointer) { M.HEAPU8.fill(0, languagePointer, languagePointer + encoder.encode(message.language + '\0').length); M._free(languagePointer) }
  if (keywordsPointer) { M.HEAPU8.fill(0, keywordsPointer, keywordsPointer + encoder.encode(KEYWORDS.join('\n') + '\0').length); M._free(keywordsPointer) }
  if (outputPointer) M._free(outputPointer)
 }
}
self.onmessage = event => {
 const message = event.data || {}
 if (message.type === 'init') { if (!loaded) void init(); else send({ type: 'ready', source: 'memory', saved: true }); return }
 if (message.type === 'transcribe') {
  try { if (!loaded) throw new Error('Load the speech model first.'); transcribe(message) }
  catch (error) { send({ type: 'error', id: message.id, stage: 'transcribe', partial: !!message.partial, captureId: message.captureId, message: String(error?.message || error) }) }
 }
}
