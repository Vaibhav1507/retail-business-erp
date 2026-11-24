import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { api, ApiError } from './api'
import { Paged } from './Paged'
import './billing-forms.css'
import './customer-returns.css'

type Sale = { id: string; number: number; invoiceNo?: string | null; date?: string; customer: string; total: number; cancelled: boolean }
type Returnable = { saleLineId: string; productId: string | null; product: string; sold: number; returned: number; remaining: number }
type SaleReturn = { id: string; number: number; date: string; saleNumber: number; customer: string; total: number; balanceCredit: number; cashRefund: number; reason: string | null }
type ReturnDetail = { return: SaleReturn; lines: { product: string; sku: string; quantity: number; amount: number }[] }

const newKey = () => crypto.randomUUID()
const today = () => new Date().toISOString().slice(0, 10)
const money = (value: number) => value.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

export function CustomerReturns({ reload }: { reload: () => void }) {
  const [sales, setSales] = useState<Sale[]>([])
  const [saleReturns, setSaleReturns] = useState<SaleReturn[]>([])
  const [retSale, setRetSale] = useState('')
  const [retLines, setRetLines] = useState<Returnable[]>([])
  const [retQty, setRetQty] = useState<Record<string, string>>({})
  const [retReason, setRetReason] = useState('')
  const [retMethod, setRetMethod] = useState('Cash')
  const [retKey, setRetKey] = useState(newKey)
  const [cancelKey, setCancelKey] = useState(newKey)
  const [cancelConfirm, setCancelConfirm] = useState(false)
  const [returnDetail, setReturnDetail] = useState<ReturnDetail | null>(null)
  const [returnSearch, setReturnSearch] = useState('')
  const [historyQuery, setHistoryQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [linesLoading, setLinesLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [historyError, setHistoryError] = useState('')
  const [done, setDone] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const returnDialogRef = useRef<HTMLDivElement>(null)
  const cancelDialogRef = useRef<HTMLDivElement>(null)

  const loadData = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [salesResult, historyResult] = await Promise.allSettled([
        api<Sale[]>('GET', '/sales'),
        api<SaleReturn[]>('GET', '/sale-returns' + (historyQuery ? '?q=' + encodeURIComponent(historyQuery) : ''))
      ])
      if (salesResult.status === 'fulfilled') setSales(salesResult.value)
      else setError(msg(salesResult.reason))
      if (historyResult.status === 'fulfilled') { setSaleReturns(historyResult.value); setHistoryError('') }
      else setHistoryError(msg(historyResult.reason))
    } finally {
      setLoading(false)
    }
  }, [historyQuery])

  useEffect(() => { void loadData().catch(() => undefined) }, [loadData])

  useEffect(() => {
    const dialog = cancelConfirm ? cancelDialogRef.current : returnDetail ? returnDialogRef.current : null
    if (!dialog) return
    const previous = document.activeElement as HTMLElement | null
    dialog.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        if (cancelConfirm) setCancelConfirm(false)
        else setReturnDetail(null)
        return
      }
      if (event.key !== 'Tab') return
      const focusable = Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'))
      if (!focusable.length) { event.preventDefault(); dialog.focus(); return }
      const first = focusable[0], last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      if (previous?.isConnected) previous.focus()
    }
  }, [cancelConfirm, returnDetail])

  useEffect(() => {
    const timer = window.setTimeout(() => setHistoryQuery(returnSearch.trim()), 250)
    return () => window.clearTimeout(timer)
  }, [returnSearch])

  const loadReturnable = async (saleId: string) => {
    if (!saleId) { setRetLines([]); return }
    setLinesLoading(true)
    setError('')
    try {
      setRetLines(await api<Returnable[]>('GET', '/sales/' + saleId + '/returnable'))
    } catch (e) {
      setError(msg(e))
      setRetLines([])
    } finally {
      setLinesLoading(false)
    }
  }

  const pickSale = async (id: string) => {
    setRetSale(id)
    setRetLines([])
    setRetQty({})
    setFieldErrors({})
    setDone('')
    setCancelConfirm(false)
    await loadReturnable(id)
  }

  const eligibleSales = sales.filter(s => !s.cancelled)
  const selectedSale = eligibleSales.find(s => s.id === retSale)
  const selectedLines = retLines.filter(line => Number(retQty[line.saleLineId] ?? 0) > 0)

  const fillRemaining = () => {
    setRetQty(Object.fromEntries(retLines.filter(line => line.remaining > 0).map(line => [line.saleLineId, String(line.remaining)])))
    setFieldErrors({})
  }

  const postReturn = async (event: FormEvent) => {
    event.preventDefault()
    if (!retSale || submitting) return
    const nextErrors: Record<string, string> = {}
    const lines = retLines.map(line => {
      const raw = retQty[line.saleLineId] ?? ''
      if (!raw.trim()) return null
      const quantity = Number(raw)
      if (!Number.isFinite(quantity) || quantity < 0) nextErrors[line.saleLineId] = 'Enter a valid quantity.'
      else if (quantity > line.remaining) nextErrors[line.saleLineId] = 'Cannot return more than ' + line.remaining + '.'
      return quantity > 0 && Number.isFinite(quantity) ? {
        saleLineId: line.saleLineId,
        productId: line.productId,
        quantity
      } : null
    }).filter((line): line is { saleLineId: string; productId: string | null; quantity: number } => line !== null)

    if (Object.keys(nextErrors).length) {
      setFieldErrors(nextErrors)
      return
    }
    if (!lines.length) {
      setFieldErrors({ form: 'Enter a return quantity for at least one item.' })
      return
    }

    setSubmitting(true)
    setError('')
    setDone('')
    setFieldErrors({})
    try {
      const result = await api<{ total: number; balanceCredit: number; cashRefund: number }>('POST', '/sale-returns', {
        saleId: retSale,
        returnDate: today(),
        reason: retReason.trim() || null,
        refundMethod: retMethod,
        lines
      }, retKey)
      setRetKey(newKey())
      setRetQty({})
      setRetReason('')
      setDone('Return posted: ₹' + money(result.total) + '. Customer balance credit: ₹' + money(result.balanceCredit) + '. Cash refund: ₹' + money(result.cashRefund) + '. For an exchange, create a replacement bill separately and settle the difference there.')
      await loadReturnable(retSale)
      await loadData()
      reload()
    } catch (e) {
      setError(msg(e))
    } finally {
      setSubmitting(false)
    }
  }

  const cancelSale = async () => {
    if (!retSale || submitting) return
    setSubmitting(true)
    setError('')
    setDone('')
    try {
      await api('POST', '/sales/' + retSale + '/cancel', { reason: retReason.trim() || null }, cancelKey)
      setCancelKey(newKey())
      setCancelConfirm(false)
      setRetSale('')
      setRetLines([])
      setRetQty({})
      setRetReason('')
      setDone('Sale cancelled. Stock restored and the books reversed.')
      await loadData()
      reload()
    } catch (e) {
      setError(msg(e))
    } finally {
      setSubmitting(false)
    }
  }

  const viewReturn = async (id: string) => {
    setError('')
    try {
      setReturnDetail(await api<ReturnDetail>('GET', '/sale-returns/' + id))
    } catch (e) {
      setError(msg(e))
    }
  }

  return (
    <section className="billing-screen customer-returns-screen" aria-label="Customer returns">
      <div className="customer-returns-heading">
        <h3>Customer returns</h3>
        <p>Return items from an earlier bill.</p>
      </div>

      <form className="customer-return-form billing-card" aria-label="Return items from a sale" onSubmit={postReturn}>
        <div className="billing-field">
          <label htmlFor="return-sale">Choose the original bill</label>
          <select id="return-sale" value={retSale} onChange={e => { void pickSale(e.target.value) }}>
            <option value="">Select a sale</option>
            {eligibleSales.map(s => <option key={s.id} value={s.id}>{s.invoiceNo ?? 'Bill ' + s.number} · {s.date ?? ''} · {s.customer} · ₹{money(s.total)}</option>)}
          </select>
        </div>

        {loading && <p className="customer-returns-muted" role="status">Loading sales and return history…</p>}
        {!loading && eligibleSales.length === 0 && <p className="customer-returns-muted">No eligible sales found. A return must be linked to an earlier sale that has not been cancelled.</p>}
        {linesLoading && <p className="customer-returns-muted" role="status">Loading items from this bill…</p>}

        {selectedSale && !linesLoading && <div className="customer-return-sale-summary">
          <div><small>Selected bill</small><strong>{selectedSale.invoiceNo ?? 'Bill ' + selectedSale.number}</strong></div>
          <div><small>Customer</small><strong>{selectedSale.customer}</strong></div>
          <div><small>Bill total</small><strong>₹{money(selectedSale.total)}</strong></div>
        </div>}

        {retSale && !linesLoading && retLines.length > 0 && <div className="customer-return-lines">
          <div className="customer-returns-section-title"><h4>Items from this bill</h4><p>Enter the quantity being returned. It cannot be more than the remaining quantity.</p></div>
          {retLines.map(line => <div className="customer-return-line" key={line.saleLineId}>
            <div className="customer-return-line-name"><strong>{line.product}</strong><small>Sold {line.sold} · Returned {line.returned} · Remaining {line.remaining}</small></div>
            <div className="billing-field">
              <label htmlFor={'return-qty-' + line.saleLineId}>Return quantity</label>
              <input id={'return-qty-' + line.saleLineId} type="number" min="0" max={line.remaining} step="any" inputMode="decimal"
                value={retQty[line.saleLineId] ?? ''} aria-invalid={Boolean(fieldErrors[line.saleLineId])}
                onChange={e => { setRetQty(current => ({ ...current, [line.saleLineId]: e.target.value })); setFieldErrors(current => ({ ...current, [line.saleLineId]: '', form: '' })) }} />
              {fieldErrors[line.saleLineId] && <small className="field-error">{fieldErrors[line.saleLineId]}</small>}
            </div>
          </div>)}
          {retLines.every(line => line.remaining <= 0) && <p className="customer-returns-muted">All quantities from this bill have already been returned.</p>}
          {retLines.some(line => line.remaining > 0) && <button type="button" className="secondary customer-return-fill" onClick={fillRemaining}>Return all remaining items</button>}
        </div>}

        {retSale && !linesLoading && retLines.length === 0 && <p className="customer-returns-muted">No remaining items can be returned from this bill.</p>}

        {retSale && <div className="customer-return-details">
          <div className="billing-field">
            <label htmlFor="return-reason">Reason (optional)</label>
            <input id="return-reason" value={retReason} onChange={e => setRetReason(e.target.value)} placeholder="For example, damaged or wrong item" />
          </div>
          <div className="billing-field">
            <label htmlFor="return-method">Refund method</label>
            <select id="return-method" value={retMethod} onChange={e => setRetMethod(e.target.value)}>
              <option>Cash</option><option>Bank</option><option>UPI</option>
            </select>
          </div>
        </div>}

        {fieldErrors.form && <p className="customer-returns-error" role="alert">{fieldErrors.form}</p>}
        {error && <p className="customer-returns-error" role="alert">{error}</p>}
        {done && <p className="customer-returns-success" role="status">{done}</p>}

        {retSale && <div className="customer-return-actions">
          <button type="submit" disabled={submitting || linesLoading || selectedLines.length === 0 || retLines.every(line => line.remaining <= 0)}>
            {submitting ? 'Posting return…' : 'Post customer return'}
          </button>
        </div>}
      </form>

      {retSale && selectedSale && <section className="customer-cancel-section" aria-label="Cancel entire sale">
        <div><h4>Cancel entire sale</h4><p>Use this only when the whole bill must be reversed. Stock will be restored and the books reversed.</p></div>
        <button type="button" className="customer-return-danger" disabled={submitting} onClick={() => setCancelConfirm(true)}>Cancel entire sale</button>
      </section>}

      <section className="customer-return-history" aria-label="Customer return history">
        <div className="customer-returns-section-title">
          <div><h3>Customer return history</h3><p>Find a return already recorded.</p></div>
          <button type="button" className="secondary" disabled={loading} onClick={() => void loadData().catch(() => undefined)}>Refresh</button>
        </div>
        <div className="billing-field">
          <label htmlFor="customer-return-search">Search customer returns</label>
          <input id="customer-return-search" type="search" value={returnSearch} onChange={e => setReturnSearch(e.target.value)} placeholder="Customer or return number" />
        </div>
        {loading && <p className="customer-returns-muted" role="status">Loading return history…</p>}
        {historyError && <p className="customer-returns-error" role="alert">{historyError} <button type="button" className="secondary" onClick={() => void loadData().catch(() => undefined)}>Retry</button></p>}
        {!loading && !historyError && saleReturns.length === 0 && <p className="customer-returns-muted">{historyQuery ? 'No returns match this search.' : 'No customer returns recorded yet.'}</p>}
        <Paged rows={saleReturns} label="returns">{page => (
          <>
            <div className="customer-return-history-desktop"><table aria-label="Customer returns">
              <thead><tr><th>Return no.</th><th>Date</th><th>Original bill</th><th>Customer</th><th>Total (INR)</th><th></th></tr></thead>
              <tbody>{page.map(r => <tr key={r.id}><td>{r.number}</td><td>{r.date}</td><td>{r.saleNumber}</td><td>{r.customer}</td><td>{money(r.total)}</td><td><button type="button" className="secondary" onClick={() => void viewReturn(r.id)}>View return</button></td></tr>)}</tbody>
            </table></div>
            <ul className="customer-return-history-mobile" aria-label="Customer returns">
              {page.map(r => <li key={r.id}>
                <div><strong>Return #{r.number}</strong><span>{r.date}</span></div>
                <strong>{r.customer}</strong>
                <p>Original bill #{r.saleNumber} · ₹{money(r.total)}</p>
                <button type="button" className="secondary" onClick={() => void viewReturn(r.id)}>View return</button>
              </li>)}
            </ul>
          </>
        )}</Paged>
      </section>

      {returnDetail && <div className="customer-return-detail-overlay" role="presentation" onMouseDown={e => { if (e.target === e.currentTarget) setReturnDetail(null) }}>
        <div ref={returnDialogRef} className="customer-return-detail" role="dialog" aria-modal="true" aria-labelledby="customer-return-detail-title" tabIndex={-1}>
          <div className="customer-returns-section-title"><div><h3 id="customer-return-detail-title">Return #{returnDetail.return.number}</h3><p>Against bill #{returnDetail.return.saleNumber} · {returnDetail.return.date} · {returnDetail.return.customer}</p></div><button type="button" className="secondary" onClick={() => setReturnDetail(null)}>Close</button></div>
          <p>{returnDetail.return.reason || 'No reason recorded.'}</p>
          <ul>{returnDetail.lines.map((line, index) => <li key={line.sku + index}>{line.product}: {line.quantity} · ₹{money(line.amount)}</li>)}</ul>
          <p><strong>Total returned:</strong> ₹{money(returnDetail.return.total)}</p>
          <p><strong>Customer balance credit:</strong> ₹{money(returnDetail.return.balanceCredit)}</p>
          <p><strong>Cash refund:</strong> ₹{money(returnDetail.return.cashRefund)}</p>
        </div>
      </div>}

      {cancelConfirm && selectedSale && <div className="customer-return-detail-overlay" role="presentation" onMouseDown={e => { if (e.target === e.currentTarget) setCancelConfirm(false) }}>
        <div ref={cancelDialogRef} className="customer-return-confirm" role="dialog" aria-modal="true" aria-labelledby="cancel-sale-title" tabIndex={-1}>
          <h3 id="cancel-sale-title">Cancel entire sale?</h3>
          <p><strong>{selectedSale.invoiceNo ?? 'Bill ' + selectedSale.number}</strong> for {selectedSale.customer}</p>
          <p>This reverses the whole sale, restores stock and reverses the books. Use this only if the entire bill must be cancelled. For selected items, use Post customer return instead.</p>
          <div className="customer-return-confirm-actions">
            <button type="button" className="secondary" disabled={submitting} onClick={() => setCancelConfirm(false)}>Keep sale</button>
            <button type="button" className="customer-return-danger" disabled={submitting} onClick={() => void cancelSale()}>{submitting ? 'Cancelling…' : 'Confirm sale cancellation'}</button>
          </div>
        </div>
      </div>}
    </section>
  )
}
