import { useState } from 'react'
import { getLanguage } from './i18n'
import { resolveNavigation, type NavigationTarget } from './navigation-resolver'
import './navigation-assistant.css'

const copy = {
 en: { title: 'Go to a screen', placeholder: 'Try “new bill”, “stock”, “supplier payment”…', go: 'Go', shortcuts: 'Quick actions', unclear: 'I could not identify a screen. Try a shortcut or a more specific command.', reject: 'I can only navigate screens. I cannot answer business questions or perform transactions.', choose: 'Which screen do you mean?', leave: 'You may have unsaved work on this screen. Leave without saving?' },
 hi: { title: 'किसी स्क्रीन पर जाएँ', placeholder: 'जैसे “नया बिल”, “स्टॉक”, “सप्लायर पेमेंट”…', go: 'जाएँ', shortcuts: 'जल्दी खोलें', unclear: 'स्क्रीन समझ नहीं आई। कोई शॉर्टकट चुनें या थोड़ा स्पष्ट लिखें।', reject: 'मैं केवल स्क्रीन खोल सकता हूँ। मैं कारोबार के सवालों का जवाब या लेन-देन नहीं कर सकता।', choose: 'आप कौन-सी स्क्रीन खोलना चाहते हैं?', leave: 'इस स्क्रीन पर आपका काम सेव नहीं हुआ हो सकता है। बिना सेव किए आगे बढ़ें?' },
}
const quick: NavigationTarget[] = [
 { id:'sell.bill', hash:'#/sell/bill', label:'New bill' }, { id:'buy.receive', hash:'#/buy/receive', label:'Add purchase' }, { id:'stock.current', hash:'#/stock/current', label:'Check stock' }, { id:'sell.collect', hash:'#/sell/collect', label:'Collect money' }, { id:'money.expenses', hash:'#/money/expenses', label:'Record expense' }, { id:'parties.customers', hash:'#/parties/customers', label:'Customers' },
]
const riskyRoutes = new Set(['sell.bill','buy.receive','stock.opening'])
export function NavigationAssistant() {
 const [query,setQuery] = useState('')
 const [message,setMessage] = useState('')
 const [choices,setChoices] = useState<NavigationTarget[]>([])
 const lang = getLanguage(), text = copy[lang]
 function go(target: NavigationTarget) {
  const current = window.location.hash
  const currentId = current.replace(/^#\\//,'').replace('/','.')
  if (riskyRoutes.has(currentId) && current !== target.hash && !window.confirm(text.leave)) return
  setChoices([]); setMessage(''); setQuery(''); window.location.hash = target.hash
 }
 function submit(e: React.FormEvent) {
  e.preventDefault(); const result = resolveNavigation(query)
  if (result.kind === 'navigate') { go(result.target); return }
  if (result.kind === 'ask') { setChoices(result.targets); setMessage(text.choose); return }
  setChoices([]); setMessage(result.kind === 'reject' ? text.reject : text.unclear)
 }
 return <section className="navigation-assistant" aria-label="Navigation assistant">
  <form onSubmit={submit} className="navigation-assistant-form">
   <label htmlFor="rbos-go-to">{text.title}</label>
   <input id="rbos-go-to" value={query} onChange={e=>{setQuery(e.target.value);setMessage('');setChoices([])}} placeholder={text.placeholder} autoComplete="off" />
   <button type="submit" disabled={!query.trim()}>{text.go}</button>
  </form>
  <div className="navigation-shortcuts" aria-label={text.shortcuts}>
   {quick.map(target=><button type="button" key={target.id} onClick={()=>go(target)}>{target.label}</button>)}
  </div>
  {message && <p className="navigation-feedback" role="status">{message}</p>}
  {choices.length>0 && <div className="navigation-choices" aria-label={text.choose}>{choices.map(target=><button type="button" key={target.id} onClick={()=>go(target)}>{target.label}</button>)}</div>}
 </section>
}
