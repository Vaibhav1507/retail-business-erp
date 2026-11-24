import { Paged } from './Paged'
import { useCallback, useEffect, useRef, useState } from 'react'
import { api, ApiError } from './api'
import { t as tr } from './i18n'
import { whatsappStatusLabel } from './whatsappStatus'
import './billing-forms.css'
import './sales-history.css'
import { InvoiceDocument } from './InvoiceDocument'

type SaleRow = { id: string; number: number; invoiceNo: string | null; date: string; saleType: string; total: number; amountPaid: number; balance: number; customer: string; whatsappStatus: string }
type SaleDetail = { sale: SaleRow & { payMethod: string | null; location: string; customerPhone: string | null; customerNotes: string | null; gstRate: number | null; whatsappStatus: string }; lines: { product: string; sku: string; quantity: number; unitPrice: number; discount: number; lineTotal: number; serials?: string[] }[] }
type Opt = { id: string; name: string }
type BizProfile = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstStatus: string; gstin: string | null }

const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')
const money = (value: number) => value.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function SalesHistory({ locations }: { locations: Opt[] }) {
  const [sales, setSales] = useState<SaleRow[]>([])
  const [detail, setDetail] = useState<SaleDetail | null>(null)
  const [error, setError] = useState('')
  const [saleSearch, setSaleSearch] = useState('')
  const [query, setQuery] = useState('')
  const [paper, setPaper] = useState('a4')
  const [unpaid, setUnpaid] = useState(false)
  const [biz, setBiz] = useState<BizProfile | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [loading, setLoading] = useState(false)
  const [openingId, setOpeningId] = useState('')
  const [retrying, setRetrying] = useState(false)
  const dialogRef = useRef<HTMLDivElement>(null)
  const openerRef = useRef<HTMLElement | null>(null)

  useEffect(() => { api<BizProfile>('GET', '/business-profile').then(setBiz).catch(() => undefined) }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(saleSearch.trim()), 250)
    return () => window.clearTimeout(timer)
  }, [saleSearch])

  const loadSales = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const rows = await api<SaleRow[]>('GET', '/sales' + (query ? '?q=' + encodeURIComponent(query) : ''))
      setSales(rows)
      setLoaded(true)
    } catch (e) {
      setError(msg(e))
      throw e
    } finally {
      setLoading(false)
    }
  }, [query])

  useEffect(() => { void loadSales().catch(() => undefined) }, [loadSales])

  useEffect(() => {
    if (!detail) {
      if (openerRef.current) {
        openerRef.current.focus()
        openerRef.current = null
      }
      return
    }
    const dialog = dialogRef.current
    dialog?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        setDetail(null)
        return
      }
      if (event.key !== 'Tab' || !dialog) return
      const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]), select:not([disabled]), a[href], input:not([disabled]), [tabindex]:not([tabindex="-1"])'
      ))
      if (!focusable.length) { event.preventDefault(); dialog.focus(); return }
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [detail])

  const shownSales = unpaid ? sales.filter(s => s.balance > 0) : sales
  const queryPending = saleSearch.trim() !== query
  const emptyMessage = !sales.length
    ? 'No sales have been recorded yet.'
    : unpaid
      ? 'No unpaid bills match this view. Clear the unpaid filter to see all sales.'
      : query
        ? 'No bills match this search. Try another name, mobile number or bill number.'
        : 'No bills found.'

  const open = async (id: string, trigger: HTMLElement) => {
    openerRef.current = trigger
    setOpeningId(id)
    setError('')
    try {
      setDetail(await api<SaleDetail>('GET', '/sales/' + id))
    } catch (e) {
      setError(msg(e))
      openerRef.current = null
    } finally {
      setOpeningId('')
    }
  }

  const retryWhatsApp = async () => {
    if (!detail) return
    const confirmPossibleDuplicate = detail.sale.whatsappStatus === 'NeedsReview'
    if (confirmPossibleDuplicate && !window.confirm('The previous send may already have reached the customer. Check WhatsApp first. Retry anyway? This could send a duplicate invoice.')) return
    setRetrying(true)
    setError('')
    try {
      await api('POST', '/sales/' + detail.sale.id + '/invoice/whatsapp/retry', { confirmPossibleDuplicate })
      await loadSales()
      setDetail(await api<SaleDetail>('GET', '/sales/' + detail.sale.id))
    } catch (e) {
      setError(msg(e))
    } finally {
      setRetrying(false)
    }
  }

  const closeDetail = () => setDetail(null)

  return (
    <section aria-label="Sales" className="billing-screen sales-history">
      <div className="sales-history-heading">
        <h3>Sales history</h3>
        <p>Find an earlier bill, check its balance or print a copy.</p>
      </div>

      <div className="sales-history-controls" role="search" aria-label="Search sales">
        <label htmlFor="sales-history-search">Search sales</label>
        <div className="sales-history-search-row">
          <input id="sales-history-search" type="search" value={saleSearch}
            onChange={e => setSaleSearch(e.target.value)}
            placeholder="Customer, mobile number or bill number" />
          {saleSearch && <button type="button" className="secondary" onClick={() => setSaleSearch('')}>Clear</button>}
        </div>
        <label className="sales-history-unpaid">
          <input type="checkbox" checked={unpaid} onChange={e => setUnpaid(e.target.checked)} />
          {tr('sales.unpaid')}
        </label>
      </div>

      {error && <div className="sales-history-error" role="alert"><span>{error}</span><button type="button" className="secondary" onClick={() => { void loadSales().catch(() => undefined) }} disabled={loading}>Retry</button></div>}
      {(loading || queryPending) && <p className="sales-history-status" role="status">{loaded ? 'Updating sales…' : tr('app.loading')}</p>}
      {loaded && !loading && !queryPending && <p className="sales-history-count" role="status">{shownSales.length} {shownSales.length === 1 ? 'bill' : 'bills'}{query ? ' found' : ''}</p>}

      {loaded && !loading && !queryPending && <Paged rows={shownSales} label="bills" resetKey={query + ':' + unpaid} empty={emptyMessage}>
        {page => (
          <>
            <div className="sales-history-desktop">
              <table aria-label="Sales history">
                <thead><tr><th>Bill no.</th><th>Date</th><th>Customer</th><th>Payment</th><th>Total (INR)</th><th>Paid (INR)</th><th>Balance (INR)</th><th>WhatsApp</th><th></th></tr></thead>
                <tbody>{page.map(s => <tr key={s.id}>
                  <td>{s.invoiceNo ?? s.number}</td><td>{s.date}</td><td>{s.customer}</td><td>{s.saleType}</td>
                  <td>{money(s.total)}</td><td>{money(s.amountPaid)}</td><td>{money(s.balance)}</td>
                  <td>{whatsappStatusLabel(s.whatsappStatus)}</td>
                  <td><button type="button" className="secondary" disabled={openingId === s.id} onClick={e => { void open(s.id, e.currentTarget) }}>{openingId === s.id ? 'Opening…' : 'View bill'}</button></td>
                </tr>)}</tbody>
              </table>
            </div>
            <ul className="sales-history-mobile" aria-label="Sales history">
              {page.map(s => (
                <li className="sales-history-card" key={s.id}>
                  <div className="sales-history-card-head"><strong>{s.invoiceNo ?? 'Bill ' + s.number}</strong><span>{s.date}</span></div>
                  <strong className="sales-history-customer">{s.customer}</strong>
                  <div className="sales-history-values">
                    <span><small>Total</small><strong>₹{money(s.total)}</strong></span>
                    <span><small>Paid</small><strong>₹{money(s.amountPaid)}</strong></span>
                    <span><small>Balance</small><strong>₹{money(s.balance)}</strong></span>
                  </div>
                  <div className="sales-history-card-meta"><span>{s.saleType}</span><span>{whatsappStatusLabel(s.whatsappStatus)}</span></div>
                  <button type="button" className="secondary" disabled={openingId === s.id} onClick={e => { void open(s.id, e.currentTarget) }}>{openingId === s.id ? 'Opening…' : 'View bill'}</button>
                </li>
              ))}
            </ul>
          </>
        )}
      </Paged>}

      {detail && <div className="invoice-modal sales-history-modal" role="presentation" onMouseDown={e => { if (e.target === e.currentTarget) closeDetail() }}>
        <div ref={dialogRef} className="invoice-modal-box sales-history-dialog" role="dialog" aria-modal="true" aria-labelledby="sales-history-dialog-title" tabIndex={-1}>
          <div className="sales-history-dialog-heading">
            <div><h3 id="sales-history-dialog-title">Bill details</h3><p>{detail.sale.invoiceNo ?? 'Bill ' + detail.sale.number} · {detail.sale.date} · {detail.sale.customer}</p></div>
            <button type="button" className="secondary" onClick={closeDetail}>Close</button>
          </div>
          <div className="invoice-tools no-print sales-history-toolbar">
            <span className="sales-history-whatsapp" role="status">WhatsApp: {whatsappStatusLabel(detail.sale.whatsappStatus)}</span>
            {(['Failed', 'WaitingForSetup', 'ConsentRequired', 'Suppressed', 'NoRecipient', 'NeedsReview'].includes(detail.sale.whatsappStatus)) &&
              <button type="button" className="secondary" disabled={retrying} onClick={retryWhatsApp}>{retrying ? 'Retrying…' : detail.sale.whatsappStatus === 'NeedsReview' ? 'Check & retry WhatsApp' : 'Retry WhatsApp'}</button>}
            <label className="sales-history-paper">Paper size
              <select aria-label="Paper size" value={paper} onChange={e => setPaper(e.target.value)}><option value="a4">A4</option><option value="thermal">80 mm thermal</option></select>
            </label>
            <style>{paper === 'thermal' ? '@page{size:80mm auto;margin:3mm} @media print{.invoice-modal-box{width:74mm} .invoice{font-size:11px}}' : '@page{size:A4;margin:10mm}'}</style>
            <button type="button" onClick={() => window.print()}>Print bill</button>
          </div>
          <InvoiceDocument biz={biz} showGst={detail.sale.gstRate != null} rate={detail.sale.gstRate ?? 0}
            invoiceNo={detail.sale.invoiceNo ?? 'INV-' + String(detail.sale.number).padStart(6, '0')}
            date={detail.sale.date} payment={detail.sale.payMethod ?? detail.sale.saleType}
            location={locations.length > 1 ? detail.sale.location : undefined}
            customerName={detail.sale.customer} customerPhone={detail.sale.customerPhone}
            customerAddress={(detail.sale.customerNotes ?? '').split('|').map(x => x.trim()).filter(Boolean).join(', ')}
            items={detail.lines.map(l => ({ name: l.product, quantity: l.quantity, unitPrice: l.unitPrice, discount: l.discount, serials: l.serials }))}
            paid={detail.sale.amountPaid} credit={detail.sale.saleType === 'Credit'} />
        </div>
      </div>}
    </section>
  )
}
