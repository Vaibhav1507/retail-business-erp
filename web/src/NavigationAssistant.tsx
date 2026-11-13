import { useEffect, useRef, useState } from 'react'
import { getLanguage } from './i18n'
import { resolveNavigation, type NavigationTarget } from './navigation-resolver'
import './navigation-assistant.css'

const copy = {
 en: {
  title:'Ask your business assistant', placeholder:'Type a screen name or command…', go:'Go', shortcuts:'Quick actions',
  unclear:'I could not identify that screen. Try a shortcut or a clearer command.',
  reject:'I can only navigate screens. I cannot answer business questions or perform transactions.',
  choose:'Which screen do you mean?', open:'Open assistant', close:'Close assistant',
  enableVoice:'Allow microphone & start', loading:'Preparing voice…', ready:'Voice is ready',
  download:'Downloading speech model', start:'Start speaking', stop:'Stop and navigate', recording:'Listening… tap Stop when finished',
  micDenied:'Microphone permission was denied. You can still type.', micMissing:'No microphone was found.',
  secure:'Voice requires HTTPS. Typed navigation still works.',
  tooShort:'I did not hear enough speech. Please try again.', tooLong:'Please keep commands under 20 seconds.',
  failed:'Voice setup failed. Check the details below and retry. Typed navigation still works.',
  retry:'Retry microphone and voice', voiceNote:'Voice is processed on this device. Audio is discarded after transcription.'
 },
 hi: {
  title:'बिज़नेस असिस्टेंट', placeholder:'स्क्रीन या कमांड लिखें…', go:'जाएँ', shortcuts:'जल्दी खोलें',
  unclear:'स्क्रीन समझ नहीं आई। शॉर्टकट चुनें या स्पष्ट कमांड लिखें।',
  reject:'मैं केवल स्क्रीन खोल सकता हूँ। कारोबार के सवालों या लेन-देन में मदद नहीं कर सकता।',
  choose:'आप कौन-सी स्क्रीन खोलना चाहते हैं?', open:'असिस्टेंट खोलें', close:'असिस्टेंट बंद करें',
  enableVoice:'माइक्रोफोन की अनुमति दें और शुरू करें', loading:'वॉइस तैयार हो रही है…', ready:'वॉइस तैयार है',
  download:'स्पीच मॉडल डाउनलोड हो रहा है', start:'बोलना शुरू करें', stop:'रोकें और स्क्रीन खोलें', recording:'सुन रहा है… पूरा होने पर रोकें',
  micDenied:'माइक्रोफोन की अनुमति नहीं मिली। आप टाइप कर सकते हैं।', micMissing:'माइक्रोफोन नहीं मिला।',
  secure:'वॉइस के लिए HTTPS चाहिए। टाइप से नेविगेशन जारी रखें।',
  tooShort:'आवाज़ स्पष्ट नहीं मिली। फिर कोशिश करें।', tooLong:'कृपया 20 सेकंड से छोटा कमांड बोलें।',
  failed:'वॉइस सेटअप विफल हुआ। नीचे दिया विवरण देखें और फिर कोशिश करें। आप टाइप कर सकते हैं।',
  retry:'माइक्रोफोन और वॉइस फिर से आज़माएँ', voiceNote:'आवाज़ इसी डिवाइस पर प्रोसेस होती है और ट्रांसक्रिप्शन के बाद हटा दी जाती है।'
 }
}
const quick: NavigationTarget[] = [
 {id:'sell.bill',hash:'#/sell/bill',label:'New bill'}, {id:'buy.receive',hash:'#/buy/receive',label:'Add purchase'},
 {id:'stock.current',hash:'#/stock/current',label:'Check stock'}, {id:'sell.collect',hash:'#/sell/collect',label:'Collect money'},
 {id:'money.expenses',hash:'#/money/expenses',label:'Record expense'}, {id:'parties.customers',hash:'#/parties/customers',label:'Customers'}
]
const SAMPLE_RATE = 16000
const labelsHi: Record<string,string> = {
 'sell.bill':'नया बिल','buy.receive':'खरीद जोड़ें','stock.current':'स्टॉक देखें','sell.collect':'पैसे वसूलें','money.expenses':'खर्च दर्ज करें','parties.customers':'ग्राहक',
 'sell.return':'ग्राहक वापसी','buy.return':'सप्लायर को वापसी','sell.bills':'पुराने बिल','buy.history':'खरीद इतिहास','stock.history':'स्टॉक बदलाव इतिहास','parties.purchases':'ग्राहक खरीद इतिहास','more.audit':'ऑडिट इतिहास',
 'buy.pay':'सप्लायर को भुगतान','parties.dues':'ग्राहक बकाया','stock.adjust':'स्टॉक ठीक करें','stock.count':'स्टॉक गिनती','more.orders':'खरीद ऑर्डर','settings':'बिज़नेस सेटिंग',
 'home':'होम','sell.menu':'बिक्री','stock.products':'सामान','stock.opening':'शुरुआती स्टॉक','stock.import':'प्रोडक्ट इम्पोर्ट','stock.alerts':'कम स्टॉक अलर्ट','stock.units':'यूनिट','parties.suppliers':'सप्लायर',
 'money.cash':'नकद और बैंक','money.owner':'मालिक का पैसा','money.closing':'दिन बंद करें','more.menu':'और विकल्प','more.reports':'रिपोर्ट','more.print':'प्रिंट','more.lock':'पुरानी तारीखें लॉक','more.gst':'GST','more.staff':'स्टाफ','more.branches':'शाखाएँ','more.transfers':'स्टॉक ट्रांसफर','more.reconcile':'बैंक मिलान','more.loans':'लोन'
}
function targetLabel(target: NavigationTarget, language: 'en'|'hi') { return language === 'hi' ? labelsHi[target.id] || target.label : target.label }
type WorkerMessage = {type:string; stage?:string; loaded?:number; total?:number; text?:string; message?:string}

export function NavigationAssistant() {
 const [open,setOpen] = useState(false)
 const [query,setQuery] = useState('')
 const [message,setMessage] = useState('')
 const [choices,setChoices] = useState<NavigationTarget[]>([])
 const [voiceReady,setVoiceReady] = useState(false)
 const [voiceLoading,setVoiceLoading] = useState(false)
 const [voiceProgress,setVoiceProgress] = useState(0)
 const [recording,setRecording] = useState(false)
 const [voiceError,setVoiceError] = useState('')
 const workerRef = useRef<Worker|null>(null)
 const streamRef = useRef<MediaStream|null>(null)
 const contextRef = useRef<AudioContext|null>(null)
 const nodeRef = useRef<AudioWorkletNode|null>(null)
 const blocksRef = useRef<Float32Array[]>([])
 const startedRef = useRef(0)
 const lang = getLanguage(), text = copy[lang]

 useEffect(() => () => {
  workerRef.current?.terminate()
  streamRef.current?.getTracks().forEach(track=>track.stop())
  void contextRef.current?.close()
 }, [])

 function go(target: NavigationTarget) {
  setChoices([]); setMessage(''); setQuery(''); setOpen(false); window.location.hash = target.hash
 }
 function processText(value: string) {
  const result = resolveNavigation(value)
  if (result.kind === 'navigate') { go(result.target); return }
  if (result.kind === 'ask') { setChoices(result.targets); setMessage(text.choose); return }
  setChoices([]); setMessage(result.kind === 'reject' ? text.reject : text.unclear)
 }
 function submit(e: React.FormEvent) { e.preventDefault(); processText(query) }

 function enableVoice() {
  if (workerRef.current || voiceLoading || voiceReady) return
  setVoiceLoading(true); setVoiceError(''); setVoiceProgress(0)
  try {
   const worker = new Worker('/shruti-worker.js')
   workerRef.current = worker
   worker.onmessage = (event: MessageEvent<WorkerMessage>) => {
    const data = event.data
    if (data.type === 'progress') setVoiceProgress(data.total ? Math.min(100,Math.round((data.loaded||0)/data.total*100)) : 0)
    if (data.type === 'ready') {
     setVoiceReady(true); setVoiceLoading(false); setVoiceProgress(100); setVoiceError('')
     if (streamRef.current) void beginCapture(streamRef.current)
    }
    if (data.type === 'error') {
     setVoiceLoading(false)
     if (data.stage !== 'transcribe') { setVoiceError(data.message || text.failed); worker.terminate(); workerRef.current = null; setVoiceReady(false) }
     else setVoiceError(data.message || text.failed)
    }
    if (data.type === 'result') {
     const transcript = (data.text || '').trim()
     if (!transcript) { setVoiceError(text.tooShort); return }
     setQuery(transcript); setVoiceError(''); processText(transcript)
    }
   }
   worker.onerror = (event) => { setVoiceLoading(false); setVoiceError(event.message || text.failed); worker.terminate(); workerRef.current = null }
   worker.postMessage({type:'init'})
  } catch { setVoiceLoading(false); setVoiceError(text.failed); workerRef.current = null }
 }

 async function beginCapture(stream: MediaStream) {
  try {
   const context = new AudioContext()
   await context.resume()
   await context.audioWorklet.addModule('/capture-worklet.js')
   const node = new AudioWorkletNode(context,'rbos-capture')
   const blocks: Float32Array[] = []
   node.port.onmessage = event => { blocks.push(event.data as Float32Array) }
   const source = context.createMediaStreamSource(stream)
   const mute = context.createGain(); mute.gain.value = 0
   source.connect(node); node.connect(mute); mute.connect(context.destination)
   contextRef.current = context; nodeRef.current = node; blocksRef.current = blocks
   startedRef.current = Date.now(); setRecording(true); setVoiceError('')
  } catch (error) {
   stream.getTracks().forEach(track=>track.stop()); streamRef.current=null
   const detail = error instanceof Error ? error.message : String(error)
   setVoiceError(`Could not start microphone capture: ${detail}`)
  }
 }

 async function startRecording() {
  setVoiceError('')
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) { setVoiceError(text.secure); return }
  try {
   // Request permission directly from the user's microphone-button tap, before downloading the model.
   const stream = await navigator.mediaDevices.getUserMedia({audio:{channelCount:1,echoCancellation:true,noiseSuppression:true,autoGainControl:true}})
   streamRef.current = stream
   if (voiceReady && workerRef.current) { await beginCapture(stream); return }
   enableVoice()
  } catch (error) {
   const name = (error as DOMException)?.name
   const detail = error instanceof Error ? error.message : ''
   setVoiceError(name === 'NotAllowedError' || name === 'PermissionDeniedError' ? text.micDenied
    : name === 'NotFoundError' || name === 'DevicesNotFoundError' ? text.micMissing
    : detail || text.failed)
  }
 }
 function resample(input: Float32Array, sourceRate: number): Float32Array {
  const length = Math.floor(input.length * SAMPLE_RATE / sourceRate), output = new Float32Array(length), ratio = sourceRate / SAMPLE_RATE
  for (let i=0;i<length;i++) { const position=i*ratio, left=Math.floor(position), fraction=position-left; output[i]=(input[left]||0)*(1-fraction)+(input[Math.min(left+1,input.length-1)]||0)*fraction }
  return output
 }
 async function stopRecording() {
  const elapsed = (Date.now()-startedRef.current)/1000
  nodeRef.current?.port.postMessage('stop')
  await new Promise(resolve=>window.setTimeout(resolve,60))
  const context=contextRef.current, blocks=blocksRef.current
  nodeRef.current?.disconnect(); streamRef.current?.getTracks().forEach(track=>track.stop())
  const rate=context?.sampleRate || SAMPLE_RATE
  await context?.close()
  nodeRef.current=null; streamRef.current=null; contextRef.current=null; blocksRef.current=[]; setRecording(false)
  if (elapsed < 0.4) { setVoiceError(text.tooShort); return }
  if (elapsed > 20) { setVoiceError(text.tooLong); return }
  const total=blocks.reduce((sum,block)=>sum+block.length,0), joined=new Float32Array(total)
  let offset=0; for(const block of blocks){joined.set(block,offset);offset+=block.length}
  const pcm=resample(joined,rate)
  workerRef.current?.postMessage({type:'transcribe',id:1,pcm},[pcm.buffer]); joined.fill(0)
 }

 return <>
  <button className="assistant-launcher" type="button" aria-label={open ? text.close : text.open} aria-expanded={open} aria-controls="rbos-assistant-panel" onClick={()=>setOpen(value=>!value)}>
   <span aria-hidden="true">✦</span><span>{open ? text.close : 'Assistant'}</span>
  </button>
  {open && <section id="rbos-assistant-panel" className="navigation-assistant" aria-label="Navigation assistant">
   <header className="assistant-heading"><div><strong>{text.title}</strong><p>Find any screen by typing or speaking</p></div><button type="button" className="assistant-close" aria-label={text.close} onClick={()=>setOpen(false)}>×</button></header>
   <form onSubmit={submit} className="navigation-assistant-form">
    <input id="rbos-go-to" aria-label={text.title} value={query} onChange={e=>{setQuery(e.target.value);setMessage('');setChoices([])}} placeholder={text.placeholder} autoComplete="off" />
    <button type="submit" disabled={!query.trim()}>{text.go}</button>
   </form>
   <div className="navigation-shortcuts" aria-label={text.shortcuts}>
    {quick.map(target=><button type="button" key={target.id} onClick={()=>go(target)}>{targetLabel(target,lang)}</button>)}
   </div>
   <div className="navigation-voice">
    <button type="button" className={recording?'is-recording':'voice-action'} onClick={recording?stopRecording:startRecording} disabled={voiceLoading || recording}>
     <span aria-hidden="true">{recording?'■':'🎙'}</span> {recording?text.stop:voiceReady?text.start:text.enableVoice}
    </button>
    {voiceLoading && <div className="voice-progress"><progress max="100" value={voiceProgress} aria-label={text.download}/><span>{text.loading} {voiceProgress}%</span></div>}
    {voiceReady && !recording && <span className="navigation-voice-note">{text.ready}</span>}
    {recording && <span className="navigation-voice-note">{text.recording}</span>}
   </div>
   <p className="navigation-privacy">{text.voiceNote}</p>
   {voiceError && <div className="voice-error" role="alert"><span>{voiceError}</span><button type="button" onClick={startRecording}>{text.retry}</button></div>}
   {message && <p className="navigation-feedback" role="status">{message}</p>}
   {choices.length>0 && <div className="navigation-choices" aria-label={text.choose}>{choices.map(target=><button type="button" key={target.id} onClick={()=>go(target)}>{targetLabel(target,lang)}</button>)}</div>}
  </section>}
 </>
}
