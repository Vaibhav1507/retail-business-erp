import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api, ApiError } from './api'
import { Paged } from './Paged'
import './billing-forms.css'
import './collections.css'

type Customer = { id: string; name: string; owed: number }
type Payment = { id: string; number: number; date: string; customer: string; amount: number; method: string; reference: string | null }
type OpenSale = { id: string; date: string; invoiceNo: string | null; number: number; open: number }
type Entry = { type: string; number: number; date: string; debit: number; credit: number; balance: number; id: string }
type Statement = { customer: string; outstanding: number; entries: Entry[] }

const newKey = () => crypto.randomUUID()
const today = () => new Date().toISOString().slice(0, 10)
const money = (value: number) => value.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

export function Collections({ reload }: { reload: () => void }) {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [payments, setPayments] = useState<Payment[]>([])
  const [payCustomer, setPayCustomer] = useState('')
  const [payAmount, setPayAmount] = useState('')
  const [payMethod, setPayMethod] = useState('Cash')
  const [payDate, setPayDate] = useState(today)
  const [payRef, setPayRef] = useState('')
  const [payKey, setPayKey] = useState(newKey)
  const [saleBillId, setSaleBillId] = useState('')
  const [openSales, setOpenSales] = useState<OpenSale[]>([])
  const [openSalesLoading, setOpenSalesLoading] = useState(false)
  const [openSalesError, setOpenSalesError] = useState('')
  const [statement, setStatement] = useState<Statement | null>(null)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [done, setDone] = useState('')

  const loadData = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [c, p] = await Promise.all([
        api<Customer[]>('GET', '/customers'),
        api<Payment[]>('GET', '/customer-payments')
      ])
      setCustomers(c)
      setPayments(p)
    } catch (e) {
      setError(msg(e))
      throw e
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void loadData().catch(() => undefined) }, [loadData])

  useEffect(() => {
    setSaleBillId('')
    setOpenSalesError('')
    setStatement(null)
    if (!payCustomer) {
      setOpenSales([])
      return
    }
    let active = true
    setOpenSalesLoading(true)
    api<OpenSale[]>('GET', '/customers/' + payCustomer + '/open-sales')
      .then(rows => { if (active) setOpenSales(rows) })
      .catch(e => { if (active) { setOpenSales([]); setOpenSalesError(msg(e)) } })
      .finally(() => { if (active) setOpenSalesLoading(false) })
    return () => { active = false }
  }, [payCustomer])

  const showStatement = async () => {
    if (!payCustomer) return
    setError('')
    try {
      setStatement(await api<Statement>('GET', '/customers/' + payCustomer + '/statement'))
    } catch (e) {
      setError(msg(e))
    }
  }

  const collect = async (event: FormEvent) => {
    event.preventDefault()
    if (!payCustomer || !(Number(payAmount) > 0) || submitting) return
    setSubmitting(true)
    setError('')
    setDone('')
    try {
      const result = await api<{ owed: number; replayed?: boolean }>('POST', '/customer-payments', {
        customerId: payCustomer,
        paymentDate: payDate,
        amount: Number(payAmount),
        method: payMethod,
        reference: payRef.trim() || null,
        allocations: saleBillId
          ? [{ billId: saleBillId, amount: Math.min(Number(payAmount), openSales.find(b => b.id === saleBillId)?.open ?? 0) }]
          : undefined
      }, payKey)
      setDone('Payment recorded. Customer now owes ' + money(result.owed) + ' INR.')
      setPayAmount('')
      setPayRef('')
      setSaleBillId('')
      setPayKey(newKey())
      await loadData()
      if (statement) {
        try { setStatement(await api<Statement>('GET', '/customers/' + payCustomer + '/statement')) }
        catch { setStatement(null) }
      }
      reload()
    } catch (e) {
      setError(msg(e))
    } finally {
      setSubmitting(false)
    }
  }

  const selectedCustomer = customers.find(c => c.id === payCustomer)

  return (
    <section className="billing-screen collections-screen" aria-label="Customer collections">
      <div className="collections-heading">
        <h3>Collect payment</h3>
        <p>Record money received from a customer.</p>
      </div>

      <form className="collections-form billing-card" aria-label="Record customer payment" onSubmit={collect}>
        <div className="collections-customer">
          <div className="billing-field">
            <label htmlFor="collection-customer">Customer</label>
            <select id="collection-customer" value={payCustomer}
              onChange={e => { setPayCustomer(e.target.value); setPayAmount(''); setPayRef(''); setDone('') }}>
              <option value="">Choose a customer</option>
              {customers.map(c => <option key={c.id} value={c.id}>{c.name} · owes ₹{money(c.owed)}</option>)}
            </select>
          </div>
          {selectedCustomer && <div className="collections-outstanding" aria-live="polite">
            <small>Outstanding balance</small>
            <strong>₹{money(selectedCustomer.owed)}</strong>
          </div>}
        </div>

        {loading && <p className="collections-muted" role="status">Loading customers and collection history…</p>}
        {!loading && customers.length === 0 && <p className="collections-muted">No customers found. Add a customer before recording a collection.</p>}

        <div className="collections-payment-grid">
          <div className="billing-field">
            <label htmlFor="collection-amount">Amount received (INR)</label>
            <input id="collection-amount" type="number" min="0.01" step="0.01" inputMode="decimal"
              value={payAmount} onChange={e => setPayAmount(e.target.value)} aria-invalid={Boolean(payAmount) && !(Number(payAmount) > 0)} />
            {payAmount && !(Number(payAmount) > 0) && <small className="field-error">Enter an amount greater than zero.</small>}
            {selectedCustomer && selectedCustomer.owed > 0 &&
              <button type="button" className="collections-fill" onClick={() => setPayAmount(String(selectedCustomer.owed))}>Fill full balance</button>}
          </div>

          {openSales.length > 0 && <div className="billing-field">
            <label htmlFor="collection-bill">Apply to a bill (optional)</label>
            <select id="collection-bill" value={saleBillId} onChange={e => setSaleBillId(e.target.value)}>
              <option value="">On account (no specific bill)</option>
              {openSales.map(b => <option key={b.id} value={b.id}>{b.date} · {b.invoiceNo ?? 'Bill ' + b.number} · balance ₹{money(b.open)}</option>)}
            </select>
            <small>Leave as On account to record this payment against the customer's account.</small>
          </div>}
          {openSalesLoading && <p className="collections-muted" role="status">Loading unpaid bills…</p>}
          {openSalesError && <p className="collections-muted" role="status">Bill balances could not be loaded. You can still record an on-account payment.</p>}

          <div className="billing-field">
            <label htmlFor="collection-method">Payment method</label>
            <select id="collection-method" value={payMethod} onChange={e => setPayMethod(e.target.value)}>
              <option>Cash</option><option>UPI</option><option>Bank</option>
            </select>
          </div>
          <div className="billing-field">
            <label htmlFor="collection-date">Payment date</label>
            <input id="collection-date" type="date" value={payDate} onChange={e => setPayDate(e.target.value)} />
          </div>
          <div className="billing-field collections-reference">
            <label htmlFor="collection-reference">Reference (optional)</label>
            <input id="collection-reference" value={payRef} onChange={e => setPayRef(e.target.value)} placeholder="UPI or bank reference" />
          </div>
        </div>

        {error && <p className="collections-error" role="alert">{error}</p>}
        {done && <p className="collections-success" role="status">{done}</p>}

        <div className="collections-form-actions">
          <button type="submit" disabled={!payCustomer || !(Number(payAmount) > 0) || submitting || loading}>
            {submitting ? 'Recording payment…' : 'Record payment'}
          </button>
          <button type="button" className="secondary" disabled={!payCustomer} onClick={() => void showStatement()}>View customer statement</button>
        </div>
      </form>

      {statement && <section className="collections-statement" aria-label="Customer statement">
        <div className="collections-section-heading">
          <div><h3>{statement.customer} — statement</h3><p>Outstanding: ₹{money(statement.outstanding)}</p></div>
          <button type="button" className="secondary" onClick={() => setStatement(null)}>Close</button>
        </div>
        <div className="table-scroll"><Paged rows={statement.entries} label="entries" empty="No statement entries yet.">{page => (
          <table aria-label="Customer statement entries">
            <thead><tr><th>Date</th><th>Type</th><th>No.</th><th>Debit (INR)</th><th>Credit (INR)</th><th>Balance (INR)</th></tr></thead>
            <tbody>{page.map((entry, index) => <tr key={entry.id || index}>
              <td>{entry.date}</td><td>{entry.type}</td><td>{entry.number}</td><td>{money(entry.debit)}</td><td>{money(entry.credit)}</td><td>{money(entry.balance)}</td>
            </tr>)}</tbody>
          </table>
        )}</Paged></div>
      </section>}

      <section className="collections-history" aria-label="Collection history">
        <div className="collections-section-heading">
          <div><h3>Collection history</h3><p>Payments received from customers.</p></div>
          <button type="button" className="secondary" disabled={loading} onClick={() => void loadData().catch(() => undefined)}>Refresh</button>
        </div>
        {loading && <p className="collections-muted" role="status">Loading collection history…</p>}
        {!loading && payments.length === 0 && <p className="collections-muted">No collections recorded yet.</p>}
        <Paged rows={payments} label="payments">{page => (
          <>
            <div className="collections-history-desktop"><table aria-label="Collection history">
              <thead><tr><th>Receipt no.</th><th>Date</th><th>Customer</th><th>Method</th><th>Amount (INR)</th><th></th></tr></thead>
              <tbody>{page.map(p => <tr key={p.id}><td>{p.number}</td><td>{p.date}</td><td>{p.customer}</td><td>{p.method}</td><td>{money(p.amount)}</td><td><a href={'#/print/payment/' + p.id} target="_blank" rel="noreferrer">Print receipt</a></td></tr>)}</tbody>
            </table></div>
            <ul className="collections-history-mobile" aria-label="Collection history">
              {page.map(p => <li key={p.id}>
                <div><strong>Receipt #{p.number}</strong><span>{p.date}</span></div>
                <strong>{p.customer}</strong>
                <p>{p.method} · ₹{money(p.amount)}</p>
                <a href={'#/print/payment/' + p.id} target="_blank" rel="noreferrer">Print receipt</a>
              </li>)}
            </ul>
          </>
        )}</Paged>
      </section>
    </section>
  )
}
