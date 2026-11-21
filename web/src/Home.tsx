import { useEffect, useState } from 'react'
import { api } from './api'
import { BusinessSetup, categoryLabel } from './BusinessSetup'
import type { Profile } from './BusinessSetup'
import './launcher.css'
import { t, getLanguage, setLanguage, toLang, formatInr } from './i18n'
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

type Summary = { salesToday: { bills: number; total: number }; moneyToCollect: number; moneyToPay: number; lowStockCount: number; cashAndBank: number }

export function HomePage({ businessName }: { businessName?: string }) {
  const [sum, setSum] = useState<Summary | null>(null)
  const [sumError, setSumError] = useState(false)
  const [rem, setRem] = useState<{ kind: string; count: number | null }[]>([])
  useEffect(() => { api<Summary>('GET', '/dashboard').then(setSum).catch(() => setSumError(true)) }, [])
  useEffect(() => { api<{ kind: string; count: number | null }[]>('GET', '/reminders').then(setRem).catch(() => undefined) }, [])
  return (
    <div className="launcher-page">
      <div className="page-title"><h2>{businessName ? t('home.welcomeNamed', { name: businessName }) : t('home.welcome')}</h2><p>{t('home.prompt')}</p></div>
      {sumError && <p role="alert">{t('dash.error')}</p>}
      {sum && <div className="dash-tiles">
        <a className="dash-tile" href="#/sell"><small>{t('dash.salesToday')}</small><strong>{formatInr(sum.salesToday.total)}</strong><small>{sum.salesToday.bills} {t('dash.bills')}</small></a>
        <a className="dash-tile" href="#/sell/collect"><small>{t('dash.collect')}</small><strong>{formatInr(sum.moneyToCollect)}</strong></a>
        <a className="dash-tile" href="#/parties/suppliers"><small>{t('home.toPay')}</small><strong>{formatInr(sum.moneyToPay)}</strong></a>
        <a className="dash-tile" href="#/stock"><small>{t('dash.lowStock')}</small><strong>{sum.lowStockCount}</strong></a>
        <a className="dash-tile" href="#/money"><small>{t('dash.cash')}</small><strong>{formatInr(sum.cashAndBank)}</strong></a>
      </div>}
      {sum && <ul className="insights" aria-label="Insights">
        {sum.salesToday.bills === 0 && <li>{t('ins.noSales')}</li>}
        {sum.moneyToCollect > 0 && <li>{t('ins.collect', { amount: formatInr(sum.moneyToCollect) })}</li>}
        {sum.lowStockCount > 0 && <li>{t('ins.stock', { n: sum.lowStockCount })}</li>}
        {sum.moneyToPay > 0 && <li>{t('ins.pay', { amount: formatInr(sum.moneyToPay) })}</li>}
      </ul>}
      {rem.some(r => r.kind === 'orders' || r.kind === 'dayClosing') && <ul className="insights" aria-label="Reminders">
        {rem.filter(r => r.kind === 'orders').map(r => <li key={r.kind}>{t('rem.orders', { n: r.count ?? 0 })}</li>)}
        {rem.filter(r => r.kind === 'dayClosing').map(r => <li key={r.kind}>{t('rem.dayClosing', { n: r.count ?? 0 })}</li>)}
      </ul>}
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
