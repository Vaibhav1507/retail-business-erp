import { useEffect, useState } from 'react'
import { api } from './api'
import { BusinessSetup, categoryLabel } from './BusinessSetup'
import type { Profile } from './BusinessSetup'
import './launcher.css'
import { t, getLanguage, setLanguage, toLang, formatInr } from './i18n'
import type { MessageKey } from './i18n'

type Summary = {
  salesToday: { bills: number; total: number }
  moneyToCollect: number
  moneyToPay: number
  lowStockCount: number
  cashAndBank: number
}

type Reminder = { kind: string; count: number | null }
type QuickAction = { icon: string; label: MessageKey; hint: MessageKey; href: string }

const QUICK_ACTIONS: QuickAction[] = [
  { icon: '＋', label: 'area.buy', hint: 'area.buyHint', href: '#/buy/receive' },
  { icon: '▦', label: 'home.checkStock', hint: 'area.stockHint', href: '#/stock' },
  { icon: '₹', label: 'home.collectPayment', hint: 'home.customerDues', href: '#/sell/collect' },
  { icon: '＋', label: 'home.addCustomer', hint: 'area.customersHint', href: '#/parties/customers' },
]

function MetricCard({ label, value, note, href }: { label: string; value: string; note?: string; href: string }) {
  return (
    <a className="home-metric" href={href}>
      <span className="home-metric-label">{label}</span>
      <strong className="home-metric-value">{value}</strong>
      {note && <span className="home-metric-note">{note}</span>}
    </a>
  )
}

export function HomePage({ businessName }: { businessName?: string }) {
  const [sum, setSum] = useState<Summary | null>(null)
  const [sumError, setSumError] = useState(false)
  const [sumLoading, setSumLoading] = useState(true)
  const [reloadKey, setReloadKey] = useState(0)
  const [reminders, setReminders] = useState<Reminder[]>([])

  useEffect(() => {
    let active = true
    setSumLoading(true)
    setSumError(false)
    api<Summary>('GET', '/dashboard')
      .then((value) => { if (active) setSum(value) })
      .catch(() => { if (active) setSumError(true) })
      .finally(() => { if (active) setSumLoading(false) })
    return () => { active = false }
  }, [reloadKey])

  useEffect(() => {
    let active = true
    api<Reminder[]>('GET', '/reminders')
      .then((value) => { if (active) setReminders(value) })
      .catch(() => undefined)
    return () => { active = false }
  }, [])

  const visibleReminders = reminders.filter((item) =>
    (item.kind === 'orders' || item.kind === 'dayClosing') && (item.count ?? 0) > 0
  )

  return (
    <div className="home-dashboard">
      <header className="home-greeting">
        <h2>{businessName ? t('home.welcomeNamed', { name: businessName }) : t('home.welcome')}</h2>
        <p>{t('home.prompt')}</p>
      </header>

      <a className="home-primary-action" href="#/sell/bill">
        <span className="home-primary-icon" aria-hidden="true">▤</span>
        <span className="home-primary-copy">
          <strong>{t('home.newBill')}</strong>
          <small>{t('home.newBillHint')}</small>
        </span>
        <span className="home-primary-arrow" aria-hidden="true">→</span>
      </a>

      <section className="home-section" aria-labelledby="home-quick-actions">
        <h3 id="home-quick-actions">{t('home.quickActions')}</h3>
        <div className="home-quick-grid">
          {QUICK_ACTIONS.map((action) => (
            <a className="home-quick-action" href={action.href} key={action.label}>
              <span className="home-quick-icon" aria-hidden="true">{action.icon}</span>
              <span className="home-quick-copy">
                <strong>{t(action.label)}</strong>
                <small>{t(action.hint)}</small>
              </span>
            </a>
          ))}
        </div>
      </section>

      <section className="home-section" aria-labelledby="home-overview">
        <h3 id="home-overview">{t('home.overview')}</h3>
        {sumLoading && !sum && <p className="home-status" role="status">{t('app.loading')}</p>}
        {sumError && (
          <div className="home-error" role="alert">
            <span>{t('dash.error')}</span>
            <button type="button" onClick={() => setReloadKey((key) => key + 1)}>{t('home.retry')}</button>
          </div>
        )}
        {sum && (
          <>
            <div className="home-metric-grid">
              <MetricCard label={t('dash.salesToday')} value={formatInr(sum.salesToday.total)} note={`${sum.salesToday.bills} ${t('dash.bills')}`} href="#/sell" />
              <MetricCard label={t('dash.collect')} value={formatInr(sum.moneyToCollect)} note={t('home.customerDues')} href="#/sell/collect" />
              <MetricCard label={t('dash.lowStock')} value={String(sum.lowStockCount)} note={t('home.lowStockHint')} href="#/stock" />
              <MetricCard label={t('home.toPay')} value={formatInr(sum.moneyToPay)} note={t('home.supplierDues')} href="#/parties/suppliers" />
            </div>
            <a className="home-cash-strip" href="#/money">
              <span className="home-cash-copy">
                <small>{t('dash.cash')}</small>
                <strong>{formatInr(sum.cashAndBank)}</strong>
              </span>
              <span className="home-cash-link">{t('home.viewMoney')} <span aria-hidden="true">›</span></span>
            </a>
          </>
        )}
      </section>

      {visibleReminders.length > 0 && (
        <section className="home-reminders" aria-label={t('home.reminders')}>
          {visibleReminders.filter((item) => item.kind === 'orders').map((item) => (
            <p key={item.kind}>{t('rem.orders', { n: item.count ?? 0 })}</p>
          ))}
          {visibleReminders.filter((item) => item.kind === 'dayClosing').map((item) => (
            <p key={item.kind}>{item.count === 1 ? t('rem.dayClosingOne') : t('rem.dayClosingMany', { n: item.count ?? 0 })}</p>
          ))}
        </section>
      )}
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
