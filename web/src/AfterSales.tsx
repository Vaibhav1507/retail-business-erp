import { Paged } from './Paged'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import './billing-forms.css'

type Customer = { id: string; name: string; owed: number }
type SaleRow = { id: string; number: number; customer: string; total: number; cancelled: boolean }
type Receipt = { id: string; date: string; supplier: string; total: number }
type Payment = { id: string; number: number; date: string; customer: string; amount: number; method: string; reference: string | null }
type Entry = { type: string; number: number; date: string; debit: number; credit: number; balance: number; id: string }
type Statement = { customer: string; outstanding: number; entries: Entry[] }
type Returnable = { saleLineId: string; productId: string | null; product: string; sold: number; returned: number; remaining: number }
type PurchaseDetail = { receipt: { number: number }; lines: { productId: string; product: string; quantity: number; returned: number; remaining: number }[] }
type SaleReturn = { id: string; number: number; date: string; saleNumber: number; customer: string; total: number; balanceCredit: number; cashRefund: number; reason: string | null }
type PurchaseReturn = { id: string; number: number; date: string; supplier: string; receiptNumber: number; total: number; reason: string | null }
type ReturnDetail<T> = { return: T; lines: { product: string; sku: string; quantity: number; amount: number }[] }

const newKey = () => crypto.randomUUID()
const today = () => new Date().toISOString().slice(0, 10)
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')
const printLink = (kind: string, id: string) => <a className="billing-action-link" href={'#/print/' + kind + '/' + id} target="_blank" rel="noreferrer">Print</a>

export type AfterPart = 'collect' | 'sreturn' | 'preturn' | 'print'

export function AfterSales({ reload, show }: { reload: () => void; show?: AfterPart }) {
  const v = (p: AfterPart) => !show || show === p
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const [customers, setCustomers] = useState<Customer[]>([])
  const [sales, setSales] = useState<SaleRow[]>([])
  const [cancelKey, setCancelKey] = useState(newKey())
  const [receipts, setReceipts] = useState<Receipt[]>([])
  const [payments, setPayments] = useState<Payment[]>([])
  const [statement, setStatement] = useState<Statement | null>(null)
  const [saleReturns, setSaleReturns] = useState<SaleReturn[]>([])
  const [purchaseReturns, setPurchaseReturns] = useState<PurchaseReturn[]>([])
  const [srSearch, setSrSearch] = useState('')
  const [prSearch, setPrSearch] = useState('')
  const [srDetail, setSrDetail] = useState<ReturnDetail<SaleReturn> | null>(null)
  const [prDetail, setPrDetail] = useState<ReturnDetail<PurchaseReturn> | null>(null)

  const [payCustomer, setPayCustomer] = useState('')
  const [payAmount, setPayAmount] = useState('')
  const [payMethod, setPayMethod] = useState('Cash')
  const [payDate, setPayDate] = useState(today)
  const [payRef, setPayRef] = useState('')
  const [payKey, setPayKey] = useState(newKey)

  const [retSale, setRetSale] = useState('')
  const [retLines, setRetLines] = useState<Returnable[]>([])
  const [retQty, setRetQty] = useState<Record<string, string>>({})
  const [retReason, setRetReason] = useState('')
  const [retKey, setRetKey] = useState(newKey)
  const [retMethod, setRetMethod] = useState('Cash')

  const [retReceipt, setRetReceipt] = useState('')
  const [recLines, setRecLines] = useState<PurchaseDetail['lines']>([])
  const [recQty, setRecQty] = useState<Record<string, string>>({})
  const [recReason, setRecReason] = useState('')
  const [recKey, setRecKey] = useState(newKey)

  const loadAll = useCallback(async () => {
    const [c, s, r, p, sr, pr] = await Promise.all([
      api<Customer[]>('GET', '/customers'), api<SaleRow[]>('GET', '/sales'), api<Receipt[]>('GET', '/purchases'),
      api<Payment[]>('GET', '/customer-payments'),
      api<SaleReturn[]>('GET', '/sale-returns' + (srSearch.trim() ? '?q=' + encodeURIComponent(srSearch.trim()) : '')),
      api<PurchaseReturn[]>('GET', '/purchase-returns' + (prSearch.trim() ? '?q=' + encodeURIComponent(prSearch.trim()) : '')),
    ])
    setCustomers(c); setSales(s); setReceipts(r); setPayments(p); setSaleReturns(sr); setPurchaseReturns(pr)
  }, [srSearch, prSearch])
  useEffect(() => { loadAll().catch(e => setError(msg(e))) }, [loadAll])

  const run = async (fn: () => Promise<string>) => {
    setError(''); setDone('')
    try { setDone(await fn()); await loadAll(); reload() } catch (e) { setError(msg(e)) }
  }

  const payCust = customers.find(c => c.id === payCustomer)
  const [saleBillId, setSaleBillId] = useState(''); const [openSales, setOpenSales] = useState<{ id: string; date: string; invoiceNo: string | null; number: number; open: number }[]>([])
  useEffect(() => { setSaleBillId(''); if (!payCustomer) { setOpenSales([]); return } api<typeof openSales>('GET', '/customers/' + payCustomer + '/open-sales').then(setOpenSales).catch(() => setOpenSales([])) }, [payCustomer, payments])
  const collect = () => run(async () => {
    const r = await api<{ owed: number; replayed?: boolean }>('POST', '/customer-payments', { customerId: payCustomer, paymentDate: payDate, amount: Number(payAmount), method: payMethod, reference: payRef || null, allocations: saleBillId ? [{ billId: saleBillId, amount: Math.min(Number(payAmount), openSales.find(b => b.id === saleBillId)?.open ?? 0) }] : undefined }, payKey)
    setPayAmount(''); setPayRef(''); setSaleBillId(''); setPayKey(newKey())
    if (statement) await showStatement(payCustomer)
    return 'Payment recorded. Customer now owes ' + r.owed.toFixed(2) + ' INR.'
  })
  async function showStatement(id: string) { try { setStatement(await api<Statement>('GET', '/customers/' + id + '/statement')) } catch (e) { setError(msg(e)) } }

  async function pickSale(id: string) {
    setRetSale(id); setRetLines([]); setRetQty({})
    if (!id) return
    try { setRetLines(await api<Returnable[]>('GET', '/sales/' + id + '/returnable')) } catch (e) { setError(msg(e)) }
  }
  const cancelSale = () => run(async () => {
    await api('POST', '/sales/' + retSale + '/cancel', { reason: retReason || null }, cancelKey)
    setCancelKey(newKey()); setRetSale(''); setRetLines([]); setRetReason('')
    return 'Sale cancelled. Stock restored and the books reversed.'
  })
  const toLines = (q: Record<string, string>) => Object.entries(q).map(([productId, v]) => ({ productId, quantity: Number(v) })).filter(x => x.quantity > 0)
  const toSaleLines = (q: Record<string, string>) => Object.entries(q).map(([saleLineId, v]) => ({ saleLineId, productId: retLines.find(line => line.saleLineId === saleLineId)?.productId ?? null, quantity: Number(v) })).filter(x => x.quantity > 0)
  const returnSale = () => run(async () => {
    const r = await api<{ total: number; balanceCredit: number; cashRefund: number }>('POST', '/sale-returns', { saleId: retSale, returnDate: today(), reason: retReason || null, refundMethod: retMethod, lines: toSaleLines(retQty) }, retKey)
    setRetKey(newKey()); setRetQty({}); setRetReason(''); await pickSale(retSale)
    return 'Return posted: ' + r.total.toFixed(2) + ' INR. For an exchange, bill the replacement items from Sell > New bill and settle the difference there (balance reduced ' + r.balanceCredit.toFixed(2) + ', cash refund ' + r.cashRefund.toFixed(2) + '). Stock increased.'
  })

  async function pickReceipt(id: string) {
    setRetReceipt(id); setRecLines([]); setRecQty({})
    if (!id) return
    try { setRecLines((await api<PurchaseDetail>('GET', '/purchases/' + id)).lines) } catch (e) { setError(msg(e)) }
  }
  const returnPurchase = () => run(async () => {
    const r = await api<{ total: number }>('POST', '/purchase-returns', { receiptId: retReceipt, returnDate: today(), reason: recReason || null, lines: toLines(recQty) }, recKey)
    setRecKey(newKey()); setRecQty({}); setRecReason(''); await pickReceipt(retReceipt)
    return 'Supplier return posted: ' + r.total.toFixed(2) + ' INR. Stock decreased.'
  })

  return (
    <section aria-label="Collections and returns" className="billing-screen">
      {!show && <h2>Collections and returns</h2>}
      <button type="button" onClick={() => { setError(''); loadAll().catch(e => setError(msg(e))) }}>Refresh</button>
      {error && <p role="alert">{error}</p>}
      {done && <p role="status">{done}</p>}

      {v('collect') && <>
      <h3>Collect customer payment</h3>
      <label>Collect from <select value={payCustomer} onChange={e => setPayCustomer(e.target.value)}><option value="">Select</option>{customers.map(c => <option key={c.id} value={c.id}>{c.name} (owes {c.owed.toFixed(2)})</option>)}</select></label>
      {payCust && <p data-testid="collect-owed">Outstanding: {payCust.owed.toFixed(2)} INR</p>}
      <label>Collection amount (INR) <input inputMode="decimal" value={payAmount} onChange={e => setPayAmount(e.target.value)} /></label>
      {openSales.length > 0 && <label>Apply to bill (optional) <select value={saleBillId} onChange={e => setSaleBillId(e.target.value)}><option value="">On account (no specific bill)</option>{openSales.map(b => <option key={b.id} value={b.id}>{b.date} {b.invoiceNo ?? b.number} - open {b.open.toFixed(2)}</option>)}</select></label>}
        <label>Collection method <select value={payMethod} onChange={e => setPayMethod(e.target.value)}><option>Cash</option><option>UPI</option><option>Bank</option></select></label>
      <label>Collection date <input type="date" value={payDate} onChange={e => setPayDate(e.target.value)} /></label>
      <label>Collection reference <input value={payRef} onChange={e => setPayRef(e.target.value)} /></label>
      <button type="button" onClick={collect} disabled={!payCustomer || !(Number(payAmount) > 0)}>Record payment</button>
      {payCustomer && <button type="button" onClick={() => showStatement(payCustomer)}>Show statement</button>}
      {payCust && payCust.owed > 0 && <button type="button" onClick={() => setPayAmount(String(payCust.owed))}>Fill full balance</button>}

      {statement && <div role="region" aria-label="Customer statement">
        <h4>Statement: {statement.customer} (owes {statement.outstanding.toFixed(2)} INR)</h4>
      <Paged rows={statement.entries} regionLabel="Customer statement entries">{page => (
        <table><thead><tr><th>Date</th><th>Type</th><th>No.</th><th>Debit</th><th>Credit</th><th>Balance</th></tr></thead>
          <tbody>{page.map((e, i) => <tr key={i}><td>{e.date}</td><td>{e.type}</td><td>{e.number}</td><td>{e.debit.toFixed(2)}</td><td>{e.credit.toFixed(2)}</td><td>{e.balance.toFixed(2)}</td></tr>)}</tbody></table>
      )}</Paged>
        <button type="button" onClick={() => setStatement(null)}>Close statement</button>
      </div>}

      <h3>Collection history</h3>
      <Paged rows={payments} regionLabel="Collection history results">{page => (
      <table aria-label="Collection history"><thead><tr><th>No.</th><th>Date</th><th>Customer</th><th>Method</th><th>Amount (INR)</th><th></th></tr></thead>
        <tbody>{page.map(p => <tr key={p.id}><td>{p.number}</td><td>{p.date}</td><td>{p.customer}</td><td>{p.method}</td><td>{p.amount.toFixed(2)}</td><td>{printLink('payment', p.id)}</td></tr>)}</tbody></table>
      )}</Paged>

      </>}
      {v('sreturn') && <>
      <h3>Customer return</h3>
      <label>Return against sale <select value={retSale} onChange={e => pickSale(e.target.value)}><option value="">Select</option>{sales.filter(s => !s.cancelled).map(s => <option key={s.id} value={s.id}>#{s.number} {s.customer} {s.total.toFixed(2)}</option>)}</select></label>
      {retLines.map(l => <label key={l.saleLineId}>Return qty: {l.product} (remaining {l.remaining})
        <input inputMode="decimal" value={retQty[l.saleLineId] ?? ''} onChange={e => setRetQty({ ...retQty, [l.saleLineId]: e.target.value })} /></label>)}
      {retLines.length > 0 && <button type="button" onClick={() => setRetQty(Object.fromEntries(retLines.filter(l => l.remaining > 0).map(l => [l.saleLineId, String(l.remaining)])))}>Return everything remaining</button>}
      <label>Customer return reason <input value={retReason} onChange={e => setRetReason(e.target.value)} /></label>
      <label>Refund method <select value={retMethod} onChange={e => setRetMethod(e.target.value)}><option>Cash</option><option>Bank</option><option>UPI</option></select></label>
      <button type="button" onClick={returnSale} disabled={!retSale || toLines(retQty).length === 0}>Post customer return</button>
            <button type="button" onClick={() => { if (window.confirm('Cancel this whole sale? Stock is restored and the books are reversed.')) void cancelSale() }} disabled={!retSale}>Cancel entire sale</button>

      <h3>Customer returns</h3>
      <label>Search customer returns <input value={srSearch} onChange={e => setSrSearch(e.target.value)} /></label>
      <Paged rows={saleReturns} regionLabel="Customer return history results">{page => (
      <table aria-label="Customer returns"><thead><tr><th>No.</th><th>Date</th><th>Sale</th><th>Customer</th><th>Total (INR)</th><th></th></tr></thead>
        <tbody>{page.map(r => <tr key={r.id}><td>{r.number}</td><td>{r.date}</td><td>{r.saleNumber}</td><td>{r.customer}</td><td>{r.total.toFixed(2)}</td>
          <td><button type="button" onClick={async () => setSrDetail(await api<ReturnDetail<SaleReturn>>('GET', '/sale-returns/' + r.id))}>View return</button></td></tr>)}</tbody></table>
      )}</Paged>
      {srDetail && <div role="region" aria-label="Customer return detail">
        <h4>Customer return #{srDetail.return.number} against sale #{srDetail.return.saleNumber}</h4>
        <p>{srDetail.return.date} - {srDetail.return.customer}. Balance reduced {srDetail.return.balanceCredit.toFixed(2)}, cash refund {srDetail.return.cashRefund.toFixed(2)}. {srDetail.return.reason}</p>
        <ul>{srDetail.lines.map(l => <li key={l.sku}>{l.product}: {l.quantity} = {l.amount.toFixed(2)}</li>)}</ul>
        <button type="button" onClick={() => setSrDetail(null)}>Close return</button>
      </div>}

      </>}
      {v('preturn') && <>
      <h3>Supplier return</h3>
      <label>Return against purchase <select value={retReceipt} onChange={e => pickReceipt(e.target.value)}><option value="">Select</option>{receipts.map(r => <option key={r.id} value={r.id}>{r.date} {r.supplier} {r.total.toFixed(2)}</option>)}</select></label>
      {recLines.map(l => <label key={l.productId}>Supplier return qty: {l.product} (remaining {l.remaining})
        <input inputMode="decimal" value={recQty[l.productId] ?? ''} onChange={e => setRecQty({ ...recQty, [l.productId]: e.target.value })} /></label>)}
      {recLines.length > 0 && <button type="button" onClick={() => setRecQty(Object.fromEntries(recLines.filter(l => l.remaining > 0).map(l => [l.productId, String(l.remaining)])))}>Return whole receipt</button>}
      <label>Supplier return reason <input value={recReason} onChange={e => setRecReason(e.target.value)} /></label>
      <button type="button" onClick={returnPurchase} disabled={!retReceipt || toLines(recQty).length === 0}>Post supplier return</button>

      <h3>Supplier returns</h3>
      <label>Search supplier returns <input value={prSearch} onChange={e => setPrSearch(e.target.value)} /></label>
      <Paged rows={purchaseReturns} regionLabel="Supplier return history results">{page => (
      <table aria-label="Supplier returns"><thead><tr><th>No.</th><th>Date</th><th>Supplier</th><th>Receipt</th><th>Total (INR)</th><th></th></tr></thead>
        <tbody>{page.map(r => <tr key={r.id}><td>{r.number}</td><td>{r.date}</td><td>{r.supplier}</td><td>{r.receiptNumber}</td><td>{r.total.toFixed(2)}</td>
          <td><button type="button" onClick={async () => setPrDetail(await api<ReturnDetail<PurchaseReturn>>('GET', '/purchase-returns/' + r.id))}>View supplier return</button></td></tr>)}</tbody></table>
      )}</Paged>
      {prDetail && <div role="region" aria-label="Supplier return detail">
        <h4>Supplier return #{prDetail.return.number} against receipt #{prDetail.return.receiptNumber}</h4>
        <p>{prDetail.return.date} - {prDetail.return.supplier}. {prDetail.return.reason}</p>
        <ul>{prDetail.lines.map(l => <li key={l.sku}>{l.product}: {l.quantity} = {l.amount.toFixed(2)}</li>)}</ul>
        <button type="button" onClick={() => setPrDetail(null)}>Close supplier return</button>
      </div>}
      </>}

      {v('print') && <>
      <h3>Print documents</h3>
      <ul aria-label="Print sale invoices">{sales.slice(0, 10).map(s => <li key={s.id}>Sale invoice #{s.number} {printLink('sale', s.id)}</li>)}</ul>
      <ul aria-label="Print purchase receipts">{receipts.slice(0, 10).map(r => <li key={r.id}>Purchase receipt {r.date} {r.supplier} {printLink('purchase', r.id)}</li>)}</ul>
      </>}
    </section>
  )
}

export function PrintPage({ kind, id }: { kind: string; id: string }) {
  const [biz, setBiz] = useState('')
  const [data, setData] = useState<any>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    const path = kind === 'sale' ? '/sales/' : kind === 'purchase' ? '/purchases/' : '/customer-payments/'
    Promise.all([api<{ name: string }>('GET', '/business-name'), api<any>('GET', path + id)])
      .then(([b, d]) => { setBiz(b.name); setData(d) }).catch(e => setError(msg(e)))
  }, [kind, id])
  if (error) return <p role="alert">{error}</p>
  if (!data) return <p>Loading...</p>
  const f = (n: number) => Number(n).toFixed(2)
  return (
    <main className="print-page" aria-label="Printable document">
      <style>{'@media print{body *{visibility:hidden!important}.print-page,.print-page *{visibility:visible!important}.print-page{position:absolute!important;inset:0 auto auto 0!important;width:100%!important;margin:0!important;padding:0!important;background:#fff!important;color:#111!important}.print-page .no-print{display:none!important}} .print-page table{border-collapse:collapse;width:100%} .print-page th,.print-page td{border:1px solid #888;padding:4px;text-align:left}'}</style>
      <h1>{biz}</h1>
      {kind === 'sale' && <>
        <h2>Sale invoice #{data.sale.number}</h2>
        <p>Date: {data.sale.date} | Customer: {data.sale.customer} | Type: {data.sale.saleType}</p>
        <table><thead><tr><th>Item</th><th>Qty</th><th>Price</th><th>Discount</th><th>Amount</th></tr></thead>
          <tbody>{data.lines.map((l: any) => <tr key={l.sku}><td>{l.product}</td><td>{l.quantity}</td><td>{f(l.unitPrice)}</td><td>{f(l.discount)}</td><td>{f(l.lineTotal)}</td></tr>)}</tbody></table>
        <p>Total: {f(data.sale.total)} INR | Paid: {f(data.sale.amountPaid)} INR | Balance: {f(data.sale.balance)} INR</p>
      </>}
      {kind === 'purchase' && <>
        <h2>Purchase receipt #{data.receipt.number}</h2>
        <p>Date: {data.receipt.date} | Supplier: {data.receipt.supplier} | Location: {data.receipt.location}</p>
        <table><thead><tr><th>Item</th><th>Qty</th><th>Unit cost</th><th>Amount</th></tr></thead>
          <tbody>{data.lines.map((l: any) => <tr key={l.sku}><td>{l.product}</td><td>{l.quantity}</td><td>{f(l.unitCost)}</td><td>{f(l.lineTotal)}</td></tr>)}</tbody></table>
        <p>Total: {f(data.receipt.total)} INR</p>
      </>}
      {kind === 'payment' && <>
        <h2>Payment receipt #{data.payment.number}</h2>
        <p>Date: {data.payment.date} | Received from: {data.payment.customer}</p>
        <p>Amount received: {f(data.payment.amount)} INR by {data.payment.method}{data.payment.reference ? ' (ref ' + data.payment.reference + ')' : ''}</p>
        <p>Outstanding balance after this receipt: {f(data.outstandingNow)} INR</p>
      </>}
      <button type="button" className="no-print" onClick={() => window.print()}>Print this page</button>
    </main>
  )
}
