import { useState } from 'react'
import { api } from './api'
import { downloadCsv } from './exportCsv'
import { t } from './i18n'

type Line = { rate: number; interState: boolean; invoices: number; taxable: number; cgst: number; sgst: number; igst: number; tax: number; total: number }
type Summary = { lines: Line[]; totals: { taxable: number; cgst: number; sgst: number; igst: number; tax: number }; note: string }

const money = (n: number) => n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const monthStart = () => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1).toLocaleDateString('en-CA') }
const today = () => new Date().toLocaleDateString('en-CA')

/** G3: GST preparation workspace. Draft output-tax summary for the CA; not a filed return. */
export function GstWorkspace() {
  const [from, setFrom] = useState(monthStart)
  const [to, setTo] = useState(today)
  const [data, setData] = useState<Summary | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = async () => {
    setBusy(true); setError('')
    try { setData(await api<Summary>('GET', `/reports/gst-summary?from=${from}&to=${to}`)) }
    catch (e) { setData(null); setError(e instanceof Error ? e.message : 'Could not load GST summary') }
    finally { setBusy(false) }
  }

  const exportCsv = () => data && downloadCsv(`gst-summary_${from}_${to}.csv`,
    ['Rate %', 'Supply', 'Invoices', 'Taxable value', 'CGST', 'SGST', 'IGST', 'Total tax', 'Invoice value'],
    data.lines.map(l => [l.rate, l.interState ? 'Inter-state' : 'Intra-state', l.invoices, l.taxable, l.cgst, l.sgst, l.igst, l.tax, l.total]))

  return (
    <section aria-label="GST workspace">
      <div role="search">
        <label>{t('rng.from')}<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label>{t('rng.to')}<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
        <button onClick={load} disabled={busy}>{busy ? t('gst.loading') : t('gst.show')}</button>
        <button onClick={exportCsv} disabled={!data || data.lines.length === 0}>{t('gst.csv')}</button>
      </div>
      {error && <p role="alert">{error}</p>}
      {data && (data.lines.length === 0 ? <p>{t('gst.none')}</p> : (
        <table>
          <thead><tr><th>{t('gst.rate')}</th><th>{t('gst.supply')}</th><th>{t('gst.invoices')}</th><th>{t('gst.taxable')}</th><th>CGST</th><th>SGST</th><th>IGST</th><th>{t('gst.totalTax')}</th></tr></thead>
          <tbody>
            {data.lines.map(l => <tr key={`${l.rate}-${l.interState}`}><td>{l.rate}%</td><td>{l.interState ? t('gst.inter') : t('gst.intra')}</td><td>{l.invoices}</td><td>{money(l.taxable)}</td><td>{money(l.cgst)}</td><td>{money(l.sgst)}</td><td>{money(l.igst)}</td><td>{money(l.tax)}</td></tr>)}
            <tr><th colSpan={3}>{t('gst.total')}</th><th>{money(data.totals.taxable)}</th><th>{money(data.totals.cgst)}</th><th>{money(data.totals.sgst)}</th><th>{money(data.totals.igst)}</th><th>{money(data.totals.tax)}</th></tr>
          </tbody>
        </table>
      ))}
      {data && <p><small>{data.note}</small></p>}
    </section>
  )
}
