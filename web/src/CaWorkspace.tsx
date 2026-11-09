import { useState } from 'react'
import { api } from './api'
import { exportReport } from './reportExport'
import { t } from './i18n'

const monthStart = () => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1).toLocaleDateString('en-CA') }
const today = () => new Date().toLocaleDateString('en-CA')

type Pack = { key: string; label: string; path: (from: string, to: string) => string }

const packs: Pack[] = [
  { key: 'tb', label: 'Trial balance', path: (_f, t) => `/reports/trial-balance?asOf=${t}` },
  { key: 'pl', label: 'Profit & loss', path: (f, t) => `/reports/profit-loss?from=${f}&to=${t}` },
  { key: 'bs', label: 'Balance sheet', path: (_f, t) => `/reports/balance-sheet?asOf=${t}` },
  { key: 'cf', label: 'Cash flow', path: (f, t) => `/reports/cash-flow?from=${f}&to=${t}` },
  { key: 'ar', label: 'Receivable ageing', path: (_f, t) => `/reports/receivable-ageing?asOf=${t}` },
  { key: 'ap', label: 'Payable ageing', path: (_f, t) => `/reports/payable-ageing?asOf=${t}` },
  { key: 'sv', label: 'Stock valuation', path: () => '/reports/stock-valuation' },
]

/** G6: one place for the CA. Pick a period, download each statement as CSV. Read-only; no CA login or sharing. */
export function CaWorkspace() {
  const [from, setFrom] = useState(monthStart)
  const [to, setTo] = useState(today)
  const [busy, setBusy] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const download = async (p: Pack) => {
	setBusy(p.key); setError(''); setNotice('')
	try {
	  const data = await api<unknown>('GET', p.path(from, to))
	  if (exportReport(p.key, data, p.label.replace(/\W+/g, '-').toLowerCase())) setNotice(`${p.label} downloaded.`)
	  else setNotice(`${p.label}: nothing to export for this period.`)
	} catch (e) { setError(e instanceof Error ? e.message : 'Could not load report') }
	finally { setBusy('') }
  }

  return (
	<section aria-label="CA workspace">
	  <p>{t('ca.intro')}</p>
	  <div role="search">
		<label>{t('rng.from')}<input type="date" value={from} max={to} onChange={e => setFrom(e.target.value)} /></label>
		<label>{t('rng.to')}<input type="date" value={to} max={today()} onChange={e => setTo(e.target.value)} /></label>
	  </div>
	  {error && <p role="alert">{error}</p>}
	  {notice && <p role="status">{notice}</p>}
	  <ul>
		{packs.map(p => <li key={p.key}>{p.label} <button type="button" disabled={busy !== ''} onClick={() => void download(p)}>{busy === p.key ? t('ca.preparing') : t('ca.download')}</button></li>)}
	  </ul>
	</section>
  )
}