import { useEffect, useState } from 'react'
import { api } from './api'
import { BusinessSetup, categoryLabel } from './BusinessSetup'
import type { Profile } from './BusinessSetup'
import './launcher.css'
import { t, getLanguage, setLanguage, toLang } from './i18n'
import type { MessageKey } from './i18n'

const AREAS: [string, MessageKey, MessageKey, string][] = [
  ['🧾', 'area.sell', 'area.sellHint', '#/sell'],
  ['📦', 'area.buy', 'area.buyHint', '#/buy/receive'],
  ['🏷️', 'area.stock', 'area.stockHint', '#/stock'],
  ['👥', 'area.customers', 'area.customersHint', '#/parties/customers'],
  ['🚚', 'area.suppliers', 'area.suppliersHint', '#/parties/suppliers'],
  ['💵', 'area.collect', 'area.collectHint', '#/sell/collect'],
  ['₹', 'area.money', 'area.moneyHint', '#/money'],
  ['📊', 'area.reports', 'area.reportsHint', '#/more/reports'],
  ['⚙️', 'area.settings', 'area.settingsHint', '#/settings'],
]

export function HomePage({ businessName }: { businessName?: string }) {
  return (
    <div className="launcher-page">
      <div className="page-title"><h2>{businessName ? t('home.welcomeNamed', { name: businessName }) : t('home.welcome')}</h2><p>{t('home.prompt')}</p></div>
      <div className="areas">
        {AREAS.map(([ico, name, hint, href]) => <a key={name} className="area" href={href}><span className="ico" aria-hidden="true">{ico}</span><strong>{t(name)}</strong><small>{t(hint)}</small></a>)}
      </div>
    </div>
  )
}

type Loaded = Profile & { mobileVerification: string }

export function BusinessSettings({ onSaved, onLanguage }: { onSaved: () => void; onLanguage: (l: 'en' | 'hi') => void }) {
  const [p, setP] = useState<Loaded | null>(null)
  const [editing, setEditing] = useState(false)
  const load = () => api<Loaded>('GET', '/business-profile').then(setP).catch(() => undefined)
  useEffect(() => { load() }, [])
  if (!p) return <p>Loading...</p>
  if (editing) return <BusinessSetup editing hasBusiness initial={{ ...p, mobile: p.mobile ?? '', gstin: p.gstin ?? '' }} onCancel={() => setEditing(false)} onDone={() => { setEditing(false); load(); onSaved() }} />
  return (
    <section aria-label="Business settings">
      <h2>Business Settings</h2>
      <dl className="review">
        <dt>Business</dt><dd>{p.businessName}</dd>
        <dt>Category</dt><dd>{categoryLabel(p.businessCategory)}</dd>
        <dt>Contact</dt><dd>{p.ownerName}, {p.mobile} ({p.mobileVerification === 'Verified' ? 'verified' : 'not verified'})</dd>
        <dt>Address</dt><dd>{p.address}, {p.city}, {p.state} {p.pinCode}</dd>
        <dt>GST</dt><dd>{p.gstStatus}{p.gstin ? `, ${p.gstin}` : ''}</dd>
        <dt>Language and currency</dt><dd>English, INR</dd>
      </dl>
      <label>{t('lang.label')}<select value={getLanguage()} onChange={e => { const l = toLang(e.target.value); setLanguage(l); onLanguage(l) }}><option value="en">English</option><option value="hi">हिन्दी (draft)</option></select></label>
      <button type="button" onClick={() => setEditing(true)}>Edit business details</button>
    </section>
  )
}
