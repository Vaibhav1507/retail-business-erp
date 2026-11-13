import { useEffect, useRef, useState } from 'react'
import { getLanguage } from './i18n'
import { resampleAudio } from './audio-utils'
import { resolveNavigation, suggestNavigation, type NavigationTarget } from './navigation-resolver'
import './navigation-assistant.css'

const copy = {
 en: {
  title:'Ask your business assistant', placeholder:'Type a command or hold the mic…', go:'Go', shortcuts:'Quick actions', suggestions:'Suggestions',
  unclear:'I could not identify that screen. Try a shortcut or a clearer command.',
  reject:'I can only navigate screens. I cannot answer business questions or perform transactions.',
  choose:'Which screen do you mean?', open:'Open assistant', close:'Close assistant',
  loading:'Preparing speech model', holdToTalk:'Hold to talk', releaseToSend:'Release to send', recording:'Listening… release to send',
  transcribing:'Transcribing on this device…', opening:'Opening', micDenied:'Microphone access was denied. Allow microphone access for this site in your browser or Android settings, then try again. You can still type.',
  micMissing:'No microphone was found.', secure:'Voice requires HTTPS and microphone support. Typed navigation still works.',
  tooShort:'I did not hear enough speech. Please try again.', tooLong:'Please keep commands under 20 seconds.',
  failed:'Voice setup failed. Check the details below and retry. Typed navigation still works.',
  retry:'Retry speech setup', voiceNote:'Voice is processed on this device. Audio is discarded after transcription.'
 },
 hi: {
  title:'बिज़नेस असिस्टेंट', placeholder:'कमांड लिखें या माइक दबाकर रखें…', go:'जाएँ', shortcuts:'जल्दी खोलें', suggestions:'संभावित विकल्प',
  unclear:'स्क्रीन समझ नहीं आई। शॉर्टकट चुनें या स्पष्ट कमांड बोलें।',
  reject:'मैं केवल स्क्रीन खोल सकता हूँ। कारोबार के सवालों या लेन-देन में मदद नहीं कर सकता।',
  choose:'आप कौन-सी स्क्रीन खोलना चाहते हैं?', open:'असिस्टेंट खोलें', close:'असिस्टेंट बंद करें',
  loading:'स्पीच मॉडल तैयार हो रहा है', holdToTalk:'बोलने के लिए दबाकर रखें', releaseToSend:'भेजने के लिए छोड़ें', recording:'सुन रहा है… भेजने के लिए छोड़ें',
  transcribing:'इस डिवाइस पर आवाज़ समझी जा रही है…', opening:'खोल रहे हैं',
  micDenied:'माइक्रोफोन की अनुमति नहीं मिली। ब्राउज़र या Android सेटिंग में इस वेबसाइट के लिए माइक्रोफोन अनुमति दें और फिर कोशिश करें। आप टाइप कर सकते हैं।',
  micMissing:'माइक्रोफोन नहीं मिला।', secure:'वॉइस के लिए HTTPS और माइक्रोफोन सपोर्ट चाहिए। टाइप से नेविगेशन जारी रखें।',
  tooShort:'आवाज़ स्पष्ट नहीं मिली। फिर कोशिश करें।', tooLong:'कृपया 20 सेकंड से छोटा कमांड बोलें।',
  failed:'वॉइस सेटअप विफल हुआ। नीचे दिया विवरण देखें और फिर कोशिश करें। आप टाइप कर सकते हैं।',
  retry:'स्पीच सेटअप फिर से आज़माएँ', voiceNote:'आवाज़ इसी डिवाइस पर प्रोसेस होती है और ट्रांसक्रिप्शन के बाद हटा दी जाती है।'
 }
}
const quick: NavigationTarget[] = [
 {id:'sell.bill',hash:'#/sell/bill',label:'New bill'}, {id:'buy.receive',hash:'#/buy/receive',label:'Add purchase'},
 {id:'stock.current',hash:'#/stock/current',label:'Check stock'}, {id:'sell.collect',hash:'#/sell/collect',label:'Collect money'},
 {id:'money.expenses',hash:'#/money/expenses',label:'Record expense'}, {id:'parties.customers',hash:'#/parties/customers',label:'Customers'}
]
const SAMPLE_RATE = 16000
const PARTIAL_INTERVAL_MS = 2200
const MAX_HISTORY_MESSAGES = 30
const labelsHi: Record<string,string> = {
 'sell.bill':'नया बिल','buy.receive':'खरीद जोड़ें','stock.current':'स्टॉक देखें','sell.collect':'पैसे वसूलें','money.expenses':'खर्च दर्ज करें','parties.customers':'ग्राहक',
 'sell.return':'ग्राहक वापसी','buy.return':'सप्लायर को वापसी','sell.bills':'पुराने बिल','buy.history':'खरीद इतिहास','stock.history':'स्टॉक बदलाव इतिहास','parties.purchases':'ग्राहक खरीद इतिहास','more.audit':'ऑडिट इतिहास',
 'buy.pay':'सप्लायर को भुगतान','parties.dues':'ग्राहक बकाया','stock.adjust':'स्टॉक ठीक करें','stock.count':'स्टॉक गिनती','more.orders':'खरीद ऑर्डर','settings':'बिज़नेस सेटिंग',
 'home':'होम','sell.menu':'बिक्री','stock.products':'सामान','stock.opening':'शुरुआती स्टॉक','stock.import':'प्रोडक्ट इम्पोर्ट','stock.alerts':'कम स्टॉक अलर्ट','stock.units':'यूनिट','parties.suppliers':'सप्लायर',
 'money.cash':'नकद और बैंक','money.owner':'मालिक का पैसा','money.closing':'दिन बंद करें','more.menu':'और विकल्प','more.reports':'रिपोर्ट','more.print':'प्रिंट','more.lock':'पुरानी तारीखें लॉक','more.gst':'GST','more.staff':'स्टाफ','more.branches':'शाखाएँ','more.transfers':'स्टॉक ट्रांसफर','more.reconcile':'बैंक मिलान','more.loans':'लोन'
}
function targetLabel(target: NavigationTarget, language: 'en'|'hi') { return language === 'hi' ? labelsHi[target.id] || target.label : target.label }
type WorkerMessage = {type:string; stage?:string; loaded?:number; total?:number; text?:string; message?:string; language?:string|null; ms?:number; partial?:boolean; captureId?:number}
type ChatMessage = {id:number; role:'user'|'assistant'; content:string}

export function NavigationAssistant() {
 const [open,setOpen] = useState(false)
 const [query,setQuery] = useState('')
 const [message,setMessage] = useState('')
 const [choices,setChoices] = useState<NavigationTarget[]>([])
 const [history,setHistory] = useState<ChatMessage[]>([])
 const [voiceLoading,setVoiceLoading] = useState(false)
 const [voiceProgress,setVoiceProgress] = useState(0)
 const [recording,setRecording] = useState(false)
 const [transcribing,setTranscribing] = useState(false)
 const [voiceError,setVoiceError] = useState('')
 const panelOpenRef = useRef(false)
 const cancelledFinalRef = useRef(false)
 const workerRef = useRef<Worker|null>(null)
 const streamRef = useRef<MediaStream|null>(null)
 const contextRef = useRef<AudioContext|null>(null)
 const nodeRef = useRef<AudioWorkletNode|null>(null)
 const blocksRef = useRef<Float32Array[]>([])
 const flushResolveRef = useRef<(() => void)|null>(null)
 const pendingPcmRef = useRef<Float32Array|null>(null)
 const startedRef = useRef(0)
 const lastPartialAtRef = useRef(0)
 const holdingRef = useRef(false)
 const recordingRef = useRef(false)
 const transcribingRef = useRef(false)
 const workerLoadingRef = useRef(false)
 const workerReadyRef = useRef(false)
 const partialInFlightRef = useRef(false)
 const captureIdRef = useRef(0)
 const pointerIdRef = useRef<number|null>(null)
 const messageIdRef = useRef(0)
 const historyEndRef = useRef<HTMLDivElement|null>(null)
 const lang = getLanguage(), text = copy[lang]

 useEffect(() => {
  historyEndRef.current?.scrollIntoView?.({ block:'end' })
 }, [history])

 function addHistory(role: ChatMessage['role'], content: string) {
  setHistory(current => [...current, { id:++messageIdRef.current, role, content }].slice(-MAX_HISTORY_MESSAGES))
 }

 function go(target: NavigationTarget, closePanel = true) {
  setChoices([]); setMessage(''); setQuery('')
  if (closePanel) { panelOpenRef.current = false; setOpen(false) }
  window.location.hash = target.hash
 }

 function handleCommand(value: string) {
  const command = value.trim()
  if (!command) return
  addHistory('user', command)
  setQuery(''); setChoices([]); setMessage('')
  const result = resolveNavigation(command)
  if (result.kind === 'navigate') {
   addHistory('assistant', `${text.opening} ${targetLabel(result.target, lang)}.`)
    window.location.hash = result.target.hash
   return
  }
  if (result.kind === 'ask') {
   setChoices(result.targets); setMessage(text.choose); addHistory('assistant', text.choose)
   return
  }
  if (result.kind === 'not-understood') {
   const suggestions = suggestNavigation(command)
   if (suggestions.length > 0) {
    setChoices(suggestions); setMessage(text.choose); addHistory('assistant', text.choose)
    return
   }
  }
  const feedback = result.kind === 'reject' ? text.reject : text.unclear
  setMessage(feedback); addHistory('assistant', feedback)
 }

 function submit(e: React.FormEvent) { e.preventDefault(); handleCommand(query) }

 function chooseSuggestion(target: NavigationTarget) {
  const command = query.trim()
  if (command) addHistory('user', command)
  addHistory('assistant', `${text.opening} ${targetLabel(target, lang)}.`)
  setQuery(''); setChoices([]); setMessage('')
  window.location.hash = target.hash
 }

 function dispatchFinal(pcm: Float32Array) {
  const worker = workerRef.current
  if (!worker || !workerReadyRef.current) {
   pendingPcmRef.current?.fill(0)
   pendingPcmRef.current = pcm
   transcribingRef.current = true
   setTranscribing(true)
   return
  }
  transcribingRef.current = true
  setTranscribing(true)
  worker.postMessage({ type:'transcribe', id:++messageIdRef.current, pcm }, [pcm.buffer])
 }

 function requestPartialTranscript() {
  const worker = workerRef.current
  if (!worker || !workerReadyRef.current || !recordingRef.current || partialInFlightRef.current) return
  const now = Date.now()
  if (now - lastPartialAtRef.current < PARTIAL_INTERVAL_MS) return
  const blocks = blocksRef.current
  const total = blocks.reduce((sum, block) => sum + block.length, 0)
  const sourceRate = contextRef.current?.sampleRate || SAMPLE_RATE
  if (total < sourceRate * 1.6) return
  const joined = new Float32Array(total)
  let offset = 0
  for (const block of blocks) { joined.set(block, offset); offset += block.length }
  const pcm = resampleAudio(joined, sourceRate, SAMPLE_RATE)
  joined.fill(0)
  if (pcm.length < SAMPLE_RATE * 1.2) { pcm.fill(0); return }
  partialInFlightRef.current = true
  lastPartialAtRef.current = now
  worker.postMessage({ type:'transcribe', id:++messageIdRef.current, partial:true, captureId:captureIdRef.current, pcm }, [pcm.buffer])
 }

 function abortCapture() {
  holdingRef.current = false
  recordingRef.current = false
  setRecording(false)
  nodeRef.current?.disconnect()
  nodeRef.current = null
  streamRef.current?.getTracks().forEach(track => track.stop())
  streamRef.current = null
  const context = contextRef.current
  contextRef.current = null
  if (context && context.state !== 'closed') void context.close().catch(() => undefined)
  for (const block of blocksRef.current) block.fill(0)
  blocksRef.current = []
  flushResolveRef.current?.()
  flushResolveRef.current = null
 }

 function failWorker(detail: string) {
  workerRef.current?.terminate()
  workerRef.current = null
  workerLoadingRef.current = false
  workerReadyRef.current = false
  partialInFlightRef.current = false
  setVoiceLoading(false); setVoiceError(detail); setTranscribing(false)
  transcribingRef.current = false
  pendingPcmRef.current?.fill(0); pendingPcmRef.current = null
  abortCapture()
 }

 function ensureWorker() {
  if (workerRef.current || workerLoadingRef.current || workerReadyRef.current) return
  workerLoadingRef.current = true
  setVoiceLoading(true); setVoiceError(''); setVoiceProgress(0)
  try {
   const worker = new Worker('/shruti-worker.js')
   workerRef.current = worker
   worker.onmessage = (event: MessageEvent<WorkerMessage>) => {
    const data = event.data
    if (data.type === 'progress') {
     setVoiceProgress(data.total ? Math.min(100, Math.round((data.loaded || 0) / data.total * 100)) : 0)
     return
    }
    if (data.type === 'ready') {
     workerLoadingRef.current = false
     workerReadyRef.current = true
     setVoiceLoading(false); setVoiceProgress(100); setVoiceError('')
     const pending = pendingPcmRef.current
     if (pending) { pendingPcmRef.current = null; dispatchFinal(pending) }
     if (recordingRef.current) requestPartialTranscript()
     return
    }
    if (data.type === 'partial-result') {
     partialInFlightRef.current = false
     if (data.captureId !== captureIdRef.current || !recordingRef.current) return
     const transcript = (data.text || '').trim()
     if (transcript) {
      setQuery(transcript)
      const suggestions = suggestNavigation(transcript)
      setChoices(suggestions)
      setMessage(suggestions.length ? text.suggestions : '')
     }
     return
    }
    if (data.type === 'result') {
     transcribingRef.current = false
     setTranscribing(false)
     if (cancelledFinalRef.current || !panelOpenRef.current) {
      cancelledFinalRef.current = false
      return
     }
     setQuery(''); setChoices([]); setMessage('')
     const transcript = (data.text || '').trim()
     if (!transcript) { setVoiceError(text.tooShort); return }
     setVoiceError('')
     handleCommand(transcript)
     return
    }
    if (data.type === 'error') {
     if (data.stage !== 'transcribe') { failWorker(data.message || text.failed); return }
     if (data.partial) { partialInFlightRef.current = false; return }
     transcribingRef.current = false
     setTranscribing(false)
     setVoiceError(data.message || text.failed)
    }
   }
   worker.onerror = event => failWorker(event.message || text.failed)
   worker.postMessage({ type:'init' })
  } catch (error) {
   workerLoadingRef.current = false
   failWorker(error instanceof Error ? error.message : text.failed)
  }
 }

 async function beginCapture(stream: MediaStream, context: AudioContext) {
  try {
   await context.resume()
   if (!holdingRef.current) {
    stream.getTracks().forEach(track => track.stop())
    if (streamRef.current === stream) streamRef.current = null
    if (contextRef.current === context) contextRef.current = null
    if (context.state !== 'closed') await context.close()
    return
   }
   await context.audioWorklet.addModule('/capture-worklet.js')
   if (!holdingRef.current) {
    stream.getTracks().forEach(track => track.stop())
    if (streamRef.current === stream) streamRef.current = null
    if (contextRef.current === context) contextRef.current = null
    if (context.state !== 'closed') await context.close()
    return
   }
   const node = new AudioWorkletNode(context, 'rbos-capture')
   const blocks: Float32Array[] = []
   node.port.onmessage = event => {
    if (event.data?.type === 'flushed') {
     flushResolveRef.current?.()
     flushResolveRef.current = null
    } else if (event.data instanceof Float32Array) {
     blocks.push(event.data)
     requestPartialTranscript()
    }
   }
   const source = context.createMediaStreamSource(stream)
   const mute = context.createGain(); mute.gain.value = 0
   source.connect(node); node.connect(mute); mute.connect(context.destination)
   contextRef.current = context; nodeRef.current = node; blocksRef.current = blocks
   captureIdRef.current += 1; lastPartialAtRef.current = 0; partialInFlightRef.current = false
   startedRef.current = Date.now(); recordingRef.current = true; setRecording(true); setVoiceError('')
  } catch (error) {
   stream.getTracks().forEach(track => track.stop())
   await context.close().catch(() => undefined)
   if (contextRef.current === context) contextRef.current = null
   if (streamRef.current === stream) streamRef.current = null
   holdingRef.current = false
   const detail = error instanceof Error ? error.message : String(error)
   setVoiceError(`Could not start microphone capture: ${detail}`)
  }
 }

 function beginHoldInteraction() {
  if (holdingRef.current || recordingRef.current || transcribingRef.current) return
  holdingRef.current = true
  cancelledFinalRef.current = false
  setVoiceError('')
  setQuery(''); setChoices([]); setMessage('')
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
   setVoiceError(text.secure); holdingRef.current = false; return
  }
  // Create/resume audio processing directly in the pointer gesture. Some mobile browsers
  // will reject AudioContext.resume() when it is deferred until getUserMedia resolves.
  let context: AudioContext
  try { context = new AudioContext({ sampleRate:SAMPLE_RATE }) }
  catch {
   try { context = new AudioContext() }
   catch (error) { setVoiceError(error instanceof Error ? error.message : text.failed); holdingRef.current = false; return }
  }
  contextRef.current = context
  const contextActivation = context.resume().catch(() => undefined)
  // The first press-and-hold itself requests permission. There is no separate setup tap.
  void navigator.mediaDevices.getUserMedia({ audio:{ channelCount:1, echoCancellation:true, noiseSuppression:true, autoGainControl:true } })
   .then(async stream => {
    if (!holdingRef.current) {
     stream.getTracks().forEach(track => track.stop())
     if (contextRef.current === context) contextRef.current = null
     await context.close().catch(() => undefined)
     return
    }
    streamRef.current = stream
    ensureWorker()
    if (!holdingRef.current) return
    await contextActivation
    await beginCapture(stream, context)
   })
   .catch(async error => {
    if (contextRef.current === context) contextRef.current = null
    await context.close().catch(() => undefined)
    const name = (error as DOMException)?.name
    const detail = error instanceof Error ? error.message : ''
    setVoiceError(name === 'NotAllowedError' || name === 'PermissionDeniedError' ? text.micDenied
     : name === 'NotFoundError' || name === 'DevicesNotFoundError' ? text.micMissing : detail || text.failed)
    holdingRef.current = false
   })
 }

 function pressTalk(event: React.PointerEvent<HTMLButtonElement>) {
  event.preventDefault()
  if (transcribingRef.current || holdingRef.current || recordingRef.current) return
  pointerIdRef.current = event.pointerId
  try { event.currentTarget.setPointerCapture(event.pointerId) } catch { /* Pointer capture is not available in every test/browser. */ }
  beginHoldInteraction()
 }

 function releaseTalk(event?: React.PointerEvent<HTMLButtonElement>) {
  if (event && pointerIdRef.current !== null && event.pointerId !== pointerIdRef.current) return
  pointerIdRef.current = null
  if (!holdingRef.current) return
  holdingRef.current = false
  if (recordingRef.current) void stopRecording()
  else {
   streamRef.current?.getTracks().forEach(track => track.stop())
   streamRef.current = null
   const context = contextRef.current
   contextRef.current = null
   if (context && context.state !== 'closed') void context.close().catch(() => undefined)
  }
 }

 async function stopRecording() {
  if (!recordingRef.current) return
  const elapsed = (Date.now() - startedRef.current) / 1000
  const node = nodeRef.current
  if (node) {
   let timeout = 0
   await Promise.race([
    new Promise<void>(resolve => { flushResolveRef.current = resolve; node.port.postMessage('stop') }),
    new Promise<void>(resolve => { timeout = window.setTimeout(resolve, 1200) })
   ])
   window.clearTimeout(timeout)
  }
  flushResolveRef.current = null
  const context = contextRef.current, blocks = blocksRef.current
  node?.disconnect()
  streamRef.current?.getTracks().forEach(track => track.stop())
  const rate = context?.sampleRate || SAMPLE_RATE
  if (context && context.state !== 'closed') await context.close().catch(() => undefined)
  nodeRef.current = null; streamRef.current = null; contextRef.current = null
  recordingRef.current = false; setRecording(false)
  blocksRef.current = []
  if (elapsed < 0.4) {
   for (const block of blocks) block.fill(0)
   setVoiceError(text.tooShort); return
  }
  if (elapsed > 20) {
   for (const block of blocks) block.fill(0)
   setVoiceError(text.tooLong); return
  }
  const total = blocks.reduce((sum, block) => sum + block.length, 0)
  const joined = new Float32Array(total)
  let offset = 0
  for (const block of blocks) { joined.set(block, offset); offset += block.length; block.fill(0) }
  const pcm = resampleAudio(joined, rate, SAMPLE_RATE)
  joined.fill(0)
  if (!pcm.length) { setVoiceError(text.tooShort); return }
  // Keep the latest partial transcript and its suggestions visible until
  // the final, full-utterance transcript is returned by the worker.
  dispatchFinal(pcm)
 }

 function closeAssistant() {
  panelOpenRef.current = false
  if (holdingRef.current || recordingRef.current) abortCapture()
  else if (transcribingRef.current) cancelledFinalRef.current = true
  setOpen(false)
 }

 useEffect(() => () => {
  holdingRef.current = false
  pendingPcmRef.current?.fill(0)
  pendingPcmRef.current = null
  workerRef.current?.terminate()
  streamRef.current?.getTracks().forEach(track => track.stop())
  nodeRef.current?.disconnect()
  void contextRef.current?.close()
  for (const block of blocksRef.current) block.fill(0)
 }, [])

 return <>
  <button className="assistant-launcher" type="button" aria-label={open ? text.close : text.open} aria-expanded={open} aria-controls="rbos-assistant-panel" onClick={() => {
   if (open) closeAssistant()
   else { panelOpenRef.current = true; setOpen(true) }
  }}>
   <span aria-hidden="true">✦</span><span>{open ? text.close : 'Assistant'}</span>
  </button>
  {open && <section id="rbos-assistant-panel" className="navigation-assistant" aria-label="Navigation assistant">
   <header className="assistant-heading"><div><strong>{text.title}</strong><p>Find any screen by typing or speaking</p></div><button type="button" className="assistant-close" aria-label={text.close} onClick={closeAssistant}>×</button></header>
   {history.length > 0 && <div className="navigation-history" aria-label="Conversation history">
    {history.map(item => <div key={item.id} className={`navigation-message ${item.role}`}><span>{item.content}</span></div>)}
    <div ref={historyEndRef} />
   </div>}
   {history.length === 0 && !query.trim() && <div className="navigation-shortcuts" aria-label={text.shortcuts}>
    {quick.map(target => <button type="button" key={target.id} onClick={() => go(target)}>{targetLabel(target, lang)}</button>)}
   </div>}
   <form onSubmit={submit} className="navigation-assistant-form">
    <input id="rbos-go-to" aria-label={text.title} value={query} onChange={event => {
     const value = event.target.value
     setQuery(value); setMessage('')
     setChoices(suggestNavigation(value))
    }} placeholder={text.placeholder} autoComplete="off" readOnly={recording || transcribing} />
    <button type="button" className={recording ? 'assistant-mic is-recording' : 'assistant-mic'} aria-label={recording ? text.releaseToSend : text.holdToTalk} title={recording ? text.releaseToSend : text.holdToTalk}
     disabled={transcribing}
     onPointerDown={pressTalk} onPointerUp={releaseTalk} onPointerCancel={releaseTalk} onLostPointerCapture={releaseTalk}
     onKeyDown={event => {
      if ((event.key === ' ' || event.key === 'Enter') && !event.repeat) { event.preventDefault(); beginHoldInteraction() }
     }}
     onKeyUp={event => {
      if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); releaseTalk() }
     }}
     onContextMenu={event => event.preventDefault()}>
     {recording ? <span className="voice-wave" aria-label="Recording audio">{Array.from({ length:24 }, (_, i) => <i key={i} style={{ '--wave-index':i } as React.CSSProperties} />)}</span> : '🎙'}
    </button>
    <button type="submit" className="assistant-go" disabled={recording || transcribing}>{text.go}</button>
   </form>
   {voiceLoading && <div className="voice-progress"><progress max="100" value={voiceProgress} aria-label={text.loading}/><span>{text.loading} {voiceProgress}%</span></div>}
   {recording && <p className="navigation-voice-note">{text.recording}</p>}
   {transcribing && !recording && <p className="navigation-voice-note" role="status">{text.transcribing}</p>}
   <p className="navigation-privacy">{text.voiceNote}</p>
   {voiceError && <div className="voice-error" role="alert">{voiceError}</div>}
   {message && choices.length === 0 && <p className="navigation-feedback" role="status">{message}</p>}
   {choices.length > 0 && <div className="navigation-choices" aria-label={message || text.suggestions}>
    {choices.map(target => <button type="button" key={target.id} disabled={recording || transcribing} onClick={() => chooseSuggestion(target)}>{targetLabel(target, lang)}</button>)}
   </div>}
  </section>}
 </>
}
