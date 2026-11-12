import { useEffect, useRef, useState } from 'react'
import { getLanguage } from './i18n'
import { resolveNavigation, type NavigationTarget } from './navigation-resolver'
import './navigation-assistant.css'

const copy = {
 en: {
  title:'Go to a screen', placeholder:'Try “new bill”, “stock”, “supplier payment”…', go:'Go', shortcuts:'Quick actions',
  unclear:'I could not identify a screen. Try a shortcut or a more specific command.',
  reject:'I can only navigate screens. I cannot answer business questions or perform transactions.',
  choose:'Which screen do you mean?',
  enableVoice:'Enable voice · ~30 MB download', loading:'Preparing on-device voice…', ready:'Voice ready on this device',
  download:'Downloading speech model', start:'Start speaking', stop:'Stop and navigate', recording:'Listening · tap Stop when finished',
  micDenied:'Microphone access was blocked. Typed commands and shortcuts are still available.',
  micMissing:'No microphone was found on this device.', secure:'Voice requires HTTPS or localhost. Typed navigation still works.',
  tooShort:'I did not hear enough speech. Please try again.', tooLong:'Please keep commands under 20 seconds.',
  failed:'Voice recognition could not start. You can still type your command.', heard:'Heard:', voiceNote:'Audio stays on this device and is discarded after transcription.'
 },
 hi: {
  title:'किसी स्क्रीन पर जाएँ', placeholder:'जैसे “नया बिल”, “स्टॉक”, “सप्लायर पेमेंट”…', go:'जाएँ', shortcuts:'जल्दी खोलें',
  unclear:'स्क्रीन समझ नहीं आई। कोई शॉर्टकट चुनें या थोड़ा स्पष्ट लिखें।',
  reject:'मैं केवल स्क्रीन खोल सकता हूँ। मैं कारोबार के सवालों का जवाब या लेन-देन नहीं कर सकता।',
  choose:'आप कौन-सी स्क्रीन खोलना चाहते हैं?',
  enableVoice:'वॉइस चालू करें · लगभग 30 MB डाउनलोड', loading:'डिवाइस पर वॉइस तैयार हो रही है…', ready:'वॉइस इस डिवाइस पर तैयार है',
  download:'स्पीच मॉडल डाउनलोड हो रहा है', start:'बोलना शुरू करें', stop:'रोकें और स्क्रीन खोलें', recording:'सुन रहा है · पूरा होने पर रोकें',
  micDenied:'माइक्रोफोन की अनुमति नहीं मिली। टाइप और शॉर्टकट अभी भी काम करेंगे।',
  micMissing:'इस डिवाइस पर माइक्रोफोन नहीं मिला।', secure:'वॉइस के लिए HTTPS या localhost चाहिए। टाइप से नेविगेशन जारी है।',
  tooShort:'आवाज़ स्पष्ट नहीं मिली। कृपया फिर कोशिश करें।', tooLong:'कृपया 20 सेकंड से छोटा कमांड बोलें।',
  failed:'वॉइस शुरू नहीं हो सकी। आप कमांड टाइप कर सकते हैं।', heard:'सुना:', voiceNote:'आवाज़ इसी डिवाइस पर रहती है और लिखित रूप में बदलने के बाद हटा दी जाती है।'
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

type WorkerMessage = {type:string; stage?:string; loaded?:number; total?:number; source?:string; saved?:boolean; id?:number; text?:string; message?:string}
export function NavigationAssistant() {
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
  setChoices([]); setMessage(''); setQuery(''); window.location.hash = target.hash
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
  const worker = new Worker('/shruti-worker.js')
  workerRef.current = worker
  worker.onmessage = (event: MessageEvent<WorkerMessage>) => {
   const data = event.data
   if (data.type === 'progress') setVoiceProgress(data.total ? Math.min(100,Math.round((data.loaded||0)/data.total*100)) : 0)
   if (data.type === 'ready') { setVoiceReady(true); setVoiceLoading(false); setVoiceProgress(100) }
   if (data.type === 'error') { setVoiceLoading(false); setVoiceError(data.message || text.failed); if (data.stage !== 'transcribe') { worker.terminate(); workerRef.current = null; setVoiceReady(false) } }
   if (data.type === 'result') {
    const transcript = (data.text || '').trim()
    if (!transcript) { setVoiceError(text.tooShort); return }
    setQuery(transcript); setVoiceError(''); processText(transcript)
   }
  }
  worker.onerror = () => { setVoiceLoading(false); setVoiceError(text.failed); worker.terminate(); workerRef.current = null }
  worker.postMessage({type:'init'})
 }

 async function startRecording() {
  setVoiceError('')
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) { setVoiceError(text.secure); return }
  if (!voiceReady) { enableVoice(); return }
  try {
   const stream = await navigator.mediaDevices.getUserMedia({audio:{channelCount:1,echoCancellation:true,noiseSuppression:true,autoGainControl:true}})
   const context = new AudioContext()
   await context.resume()
   await context.audioWorklet.addModule('/capture-worklet.js')
   const node = new AudioWorkletNode(context,'rbos-capture')
   const blocks: Float32Array[] = []
   node.port.onmessage = event => { blocks.push(event.data as Float32Array) }
   const source = context.createMediaStreamSource(stream)
   const mute = context.createGain(); mute.gain.value = 0
   source.connect(node); node.connect(mute); mute.connect(context.destination)
   streamRef.current = stream; contextRef.current = context; nodeRef.current = node; blocksRef.current = blocks
   startedRef.current = Date.now(); setRecording(true)
  } catch (error) {
   const name = (error as DOMException)?.name
   setVoiceError(name === 'NotAllowedError' ? text.micDenied : name === 'NotFoundError' ? text.micMissing : text.failed)
  }
 }

 function resample(input: Float32Array, sourceRate: number): Float32Array {
  const length = Math.floor(input.length * SAMPLE_RATE / sourceRate)
  const output = new Float32Array(length)
  const ratio = sourceRate / SAMPLE_RATE
  for (let i=0;i<length;i++) {
   const position=i*ratio, left=Math.floor(position), fraction=position-left
   output[i]=(input[left]||0)*(1-fraction)+(input[Math.min(left+1,input.length-1)]||0)*fraction
  }
  return output
 }
 async function stopRecording() {
  const elapsed = (Date.now()-startedRef.current)/1000
  nodeRef.current?.port.postMessage('stop')
  await new Promise(resolve=>window.setTimeout(resolve,60))
  const context=contextRef.current, blocks=blocksRef.current
  nodeRef.current?.disconnect()
  streamRef.current?.getTracks().forEach(track=>track.stop())
  const rate=context?.sampleRate || SAMPLE_RATE
  await context?.close()
  nodeRef.current=null; streamRef.current=null; contextRef.current=null; blocksRef.current=[]
  setRecording(false)
  if (elapsed < 0.4) { setVoiceError(text.tooShort); return }
  if (elapsed > 20) { setVoiceError(text.tooLong); return }
  const total=blocks.reduce((sum,block)=>sum+block.length,0)
  const joined=new Float32Array(total); let offset=0
  for(const block of blocks){joined.set(block,offset);offset+=block.length}
  const pcm=resample(joined,rate)
  workerRef.current?.postMessage({type:'transcribe',id:1,pcm},[pcm.buffer])
  joined.fill(0)
 }

 return <section className="navigation-assistant" aria-label="Navigation assistant">
  <form onSubmit={submit} className="navigation-assistant-form">
   <label htmlFor="rbos-go-to">{text.title}</label>
   <input id="rbos-go-to" value={query} onChange={e=>{setQuery(e.target.value);setMessage('');setChoices([])}} placeholder={text.placeholder} autoComplete="off" />
   <button type="submit" disabled={!query.trim()}>{text.go}</button>
  </form>
  <div className="navigation-shortcuts" aria-label={text.shortcuts}>
   {quick.map(target=><button type="button" key={target.id} onClick={()=>go(target)}>{targetLabel(target,lang)}</button>)}
  </div>
  <div className="navigation-voice">
   {!voiceReady && <button type="button" onClick={enableVoice} disabled={voiceLoading}>{voiceLoading ? text.loading : text.enableVoice}</button>}
   {voiceLoading && <progress max="100" value={voiceProgress} aria-label={text.download} />}
   {voiceReady && <button type="button" className={recording?'is-recording':''} onClick={recording?stopRecording:startRecording}>{recording?text.stop:text.start}</button>}
   {voiceReady && <span className="navigation-voice-note">{recording?text.recording:text.ready}</span>}
  </div>
  <p className="navigation-privacy">{text.voiceNote}</p>
  {voiceError && <p className="navigation-feedback" role="alert">{voiceError}</p>}
  {message && <p className="navigation-feedback" role="status">{message}</p>}
  {choices.length>0 && <div className="navigation-choices" aria-label={text.choose}>{choices.map(target=><button type="button" key={target.id} onClick={()=>go(target)}>{targetLabel(target,lang)}</button>)}</div>}
 </section>
}
