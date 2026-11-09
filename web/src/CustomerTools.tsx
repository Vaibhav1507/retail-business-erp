import { useEffect, useState } from 'react'
import { api, ApiError } from './api'

type Cust = { id: string; name: string }
type Hist = {
  customer: string
  sales: { id: string; number: number; date: string; total: number; paid: number; cancelled: boolean; quantity: number }[]
  topProducts: { productId: string; product: string; quantity: number; amount: number; lastSold: string }[]
}
type Dues = { customerId: string; customer: string; phone: string; outstanding: number; buckets: { label: string; amount: number }[] }

const err = (e: unknown, f: string) => (e instanceof ApiError ? e.message : f)
const money = (n: number) => Number(n).toFixed(2)

export function CustomerHistory() {
  const [customers, setCustomers] = useState<Cust[]>([])
  const [id, setId] = useState('')
  const [data, setData] = useState<Hist | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    api<Cust[]>('GET', '/customers').then(setCustomers).catch(e => setError(err(e, 'Could not load customers.')))
  }, [])
  useEffect(() => {
    if (!id) { setData(null); return }
    setError('')
    api<Hist>('GET', `/customers/${id}/history`).then(setData).catch(e => setError(err(e, 'Could not load history.')))
  }, [id])
  return (
    <section aria-label="Customer purchase history">
      <h3>Customer purchase history</h3>
      <label>Customer <select value={id} onChange={e => setId(e.target.value)}><option value="">Choose a customer</option>{customers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
      {error && <p role="alert">{error}</p>}
      {data && (
        <>
          <h4>Bills</h4>
          {data.sales.length === 0 ? <p>No purchases yet.</p> : (
            <table aria-label="Bills"><thead><tr><th>No.</th><th>Date</th><th>Qty</th><th>Total</th><th>Paid</th><th>Status</th></tr></thead>
              <tbody>{data.sales.map(s => <tr key={s.id}><td>{s.number}</td><td>{s.date}</td><td>{s.quantity}</td><td>{money(s.total)}</td><td>{money(s.paid)}</td><td>{s.cancelled ? 'Cancelled' : 'OK'}</td></tr>)}</tbody></table>
          )}
          <h4>Most bought items</h4>
          {data.topProducts.length === 0 ? <p>No items yet.</p> : (
            <table aria-label="Top products"><thead><tr><th>Item</th><th>Qty</th><th>Amount</th><th>Last bought</th></tr></thead>
              <tbody>{data.topProducts.map(p => <tr key={p.productId}><td>{p.product}</td><td>{p.quantity}</td><td>{money(p.amount)}</td><td>{p.lastSold}</td></tr>)}</tbody></table>
          )}
        </>
      )}
    </section>
  )
}

export function CustomerDues() {
  const [rows, setRows] = useState<Dues[] | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    api<Dues[]>('GET', '/customers/ageing').then(setRows).catch(e => setError(err(e, 'Could not load dues.')))
  }, [])
  const labels = rows?.[0]?.buckets.map(b => b.label) ?? []
  return (
    <section aria-label="Customer dues ageing">
      <h3>Customer dues by age (days)</h3>
      {error && <p role="alert">{error}</p>}
      {rows && rows.length === 0 && <p>No customer owes you anything.</p>}
      {rows && rows.length > 0 && (
        <table aria-label="Dues"><thead><tr><th>Customer</th><th>Phone</th>{labels.map(l => <th key={l}>{l}</th>)}<th>Total due</th></tr></thead>
          <tbody>{rows.map(r => <tr key={r.customerId}><td>{r.customer}</td><td>{r.phone}</td>{r.buckets.map(b => <td key={b.label}>{money(b.amount)}</td>)}<td>{money(r.outstanding)}</td></tr>)}</tbody></table>
      )}
    </section>
  )
}
