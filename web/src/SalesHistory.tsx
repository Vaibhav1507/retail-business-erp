import { Paged } from './Paged'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { t as tr } from './i18n'
import { whatsappStatusLabel } from './whatsappStatus'
import './billing-forms.css'
import { InvoiceDocument } from './InvoiceDocument'

type SaleRow = { id: string; number: number; invoiceNo: string | null; date: string; saleType: string; total: number; amountPaid: number; balance: number; customer: string; whatsappStatus: string }
type SaleDetail = { sale: SaleRow & { payMethod: string | null; location: string; customerPhone: string | null; customerNotes: string | null; gstRate: number | null; whatsappStatus: string }; lines: { product: string; sku: string; quantity: number; unitPrice: number; discount: number; lineTotal: number; serials?: string[] }[] }
type Opt = { id: string; name: string }
type BizProfile = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstStatus: string; gstin: string | null }

const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

export function SalesHistory({ locations }: { locations: Opt[] }) {
  const [sales, setSales] = useState<SaleRow[]>([])
  const [detail, setDetail] = useState<SaleDetail | null>(null)
  const [error, setError] = useState('')
  const [saleSearch, setSaleSearch] = useState('')
  const [paper, setPaper] = useState('a4')
  const [unpaid, setUnpaid] = useState(false)
  const [biz, setBiz] = useState<BizProfile | null>(null)
  useEffect(() => { api<BizProfile>('GET', '/business-profile').then(setBiz).catch(() => undefined) }, [])
  const [loaded, setLoaded] = useState(false)
  const [retrying, setRetrying] = useState(false)
  const loadSales = useCallback(async () => { setSales(await api<SaleRow[]>('GET', '/sales' + (saleSearch.trim() ? '?q=' + encodeURIComponent(saleSearch.trim()) : ''))); setLoaded(true) }, [saleSearch])
  useEffect(() => { loadSales().catch(e => setError(msg(e))) }, [loadSales])
  useEffect(() => { if (!detail) return; const k = (e: KeyboardEvent) => { if (e.key === 'Escape') setDetail(null) }; window.addEventListener('keydown', k); return () => window.removeEventListener('keydown', k) }, [detail])
  const shownSales = unpaid ? sales.filter(s => s.balance > 0) : sales
  const open = async (id: string) => { setError(''); try { setDetail(await api<SaleDetail>('GET', '/sales/' + id)) } catch (e) { setError(msg(e)) } }
  const retryWhatsApp = async () => {
    if (!detail) return
    const confirmPossibleDuplicate = detail.sale.whatsappStatus === 'NeedsReview'
    if (confirmPossibleDuplicate && !window.confirm('The previous send may already have reached the customer. Check WhatsApp first. Retry anyway? This could send a duplicate invoice.')) return
    setRetrying(true); setError('')
    try {
      await api('POST', '/sales/' + detail.sale.id + '/invoice/whatsapp/retry', { confirmPossibleDuplicate })
      await loadSales()
      setDetail(await api<SaleDetail>('GET', '/sales/' + detail.sale.id))
    } catch (e) { setError(msg(e)) } finally { setRetrying(false) }
  }
  return (
    <section aria-label="Sales" className="billing-screen">
      {error && <p role="alert">{error}</p>}
      <h3>Sales history</h3>
      <label>Search sales <input value={saleSearch} onChange={e => setSaleSearch(e.target.value)} placeholder="mobile number, customer, Cash, Credit, bill no." /></label>
      <label><input type="checkbox" checked={unpaid} onChange={e => setUnpaid(e.target.checked)} /> {tr('sales.unpaid')}</label>
      {!loaded && !error && <p role="status">{tr('app.loading')}</p>}
      {loaded && shownSales.length === 0 && <p>{tr('sales.none')}</p>}
      <Paged rows={shownSales}>{page => (
      <table aria-label="Sales history">
        <thead><tr><th>No.</th><th>Date</th><th>Customer</th><th>Type</th><th>Total (INR)</th><th>Paid (INR)</th><th>Balance (INR)</th><th>WhatsApp</th><th></th></tr></thead>
        <tbody>{page.map(s => <tr key={s.id}><td>{s.invoiceNo ?? s.number}</td><td>{s.date}</td><td>{s.customer}</td><td>{s.saleType}</td><td>{s.total.toFixed(2)}</td><td>{s.amountPaid.toFixed(2)}</td><td>{s.balance.toFixed(2)}</td><td>{whatsappStatusLabel(s.whatsappStatus)}</td><td><button type="button" onClick={() => open(s.id)}>View</button></td></tr>)}</tbody>
      </table>
      )}</Paged>
	  {detail && <div className="invoice-modal" role="dialog" aria-modal="true" aria-label="Sale detail" onClick={e => { if (e.target === e.currentTarget) setDetail(null) }}>
		<div className="invoice-modal-box">
		  <div className="invoice-tools no-print"><span role="status">WhatsApp: {whatsappStatusLabel(detail.sale.whatsappStatus)}</span>
            {(['Failed', 'WaitingForSetup', 'ConsentRequired', 'Suppressed', 'NoRecipient', 'NeedsReview'].includes(detail.sale.whatsappStatus)) && <button type="button" disabled={retrying} onClick={retryWhatsApp}>{retrying ? 'Retrying…' : detail.sale.whatsappStatus === 'NeedsReview' ? 'Check & retry WhatsApp' : 'Retry WhatsApp'}</button>}
			<select aria-label="Paper size" value={paper} onChange={e => setPaper(e.target.value)}><option value="a4">A4</option><option value="thermal">80 mm thermal</option></select><style>{paper === "thermal" ? "@page{size:80mm auto;margin:3mm} @media print{.invoice-modal-box{width:74mm} .invoice{font-size:11px}}" : "@page{size:A4;margin:10mm}"}</style><button type="button" onClick={() => window.print()}>Print bill</button>
			<button type="button" className="secondary" onClick={() => setDetail(null)}>Close</button>
		  </div>
		  <InvoiceDocument biz={biz} showGst={!!biz?.gstin && detail.sale.gstRate != null} rate={detail.sale.gstRate ?? 0} invoiceNo={detail.sale.invoiceNo ?? 'INV-' + String(detail.sale.number).padStart(6, '0')} date={detail.sale.date} payment={detail.sale.payMethod ?? detail.sale.saleType} location={locations.length > 1 ? detail.sale.location : undefined} customerName={detail.sale.customer} customerPhone={detail.sale.customerPhone} customerAddress={(detail.sale.customerNotes ?? '').split('|').map(x => x.trim()).filter(Boolean).join(', ')} items={detail.lines.map(l => ({ name: l.product, quantity: l.quantity, unitPrice: l.unitPrice, discount: l.discount, serials: l.serials }))} paid={detail.sale.amountPaid} credit={detail.sale.saleType === 'Credit'} />
		</div>
	  </div>}
    </section>
  )
}
