import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { t } from './i18n'

type Opt = { id: string; name: string }
type Order = { id: string; number: number; supplier: string; date: string; expected: string | null; status: string; note: string | null; items: string; total: number }

/** B8: purchase orders. Receiving goods still happens through Purchases; here the order is just closed. */
export function OrdersPanel() {
  const [suppliers, setSuppliers] = useState<Opt[]>([])
  const [products, setProducts] = useState<Opt[]>([])
  const [orders, setOrders] = useState<Order[]>([])
  const [supplier, setSupplier] = useState('')
  const [product, setProduct] = useState('')
  const [qty, setQty] = useState('')
  const [cost, setCost] = useState('')
  const [expected, setExpected] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      const [s, p, o] = await Promise.all([api<Opt[]>('GET', '/suppliers'), api<Opt[]>('GET', '/products'), api<Order[]>('GET', '/purchase-orders')])
      setSuppliers(s); setProducts(p); setOrders(o); setError('')
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not load orders') }
  }, [])
  useEffect(() => { void load() }, [load])

  const create = async () => {
    setBusy(true)
    try {
      await api('POST', '/purchase-orders', {
        supplierId: supplier, date: new Date().toISOString().slice(0, 10), expectedDate: expected || null, note: null,
        lines: [{ productId: product, quantity: Number(qty), unitCost: Number(cost) }],
      })
      setQty(''); setCost(''); setExpected(''); await load()
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not create order') }
    finally { setBusy(false) }
  }
  const close = async (id: string, status: 'Received' | 'Cancelled') => {
    try { await api('PUT', `/purchase-orders/${id}/status`, { status, receiptId: null }); await load() }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not update order') }
  }

  const ready = supplier && product && Number(qty) > 0 && Number(cost) >= 0 && cost !== ''
  return (
    <section aria-label="Purchase orders">
      {error && <p role="alert">{error}</p>}
      <label>{t('po.supplier')}<select value={supplier} onChange={e => setSupplier(e.target.value)}><option value="">{t('mv.select')}</option>{suppliers.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <label>{t('mv.product')}<select value={product} onChange={e => setProduct(e.target.value)}><option value="">{t('mv.select')}</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
      <label>{t('mv.qty')}<input inputMode="decimal" value={qty} onChange={e => setQty(e.target.value)} /></label>
      <label>{t('po.unitCost')}<input inputMode="decimal" value={cost} onChange={e => setCost(e.target.value)} /></label>
      <label>{t('po.expected')}<input type="date" value={expected} onChange={e => setExpected(e.target.value)} /></label>
      <button onClick={create} disabled={busy || !ready}>{t('po.create')}</button>
      <ul>
        {orders.map(o => (
          <li key={o.id}>
            PO-{o.number} · {o.supplier} · {o.items} · ₹{o.total.toFixed(2)} · {o.status}
            {o.status === 'Ordered' && <> <button onClick={() => close(o.id, 'Received')}>{t('po.received')}</button> <button onClick={() => close(o.id, 'Cancelled')}>{t('po.cancel')}</button></>}
          </li>
        ))}
      </ul>
    </section>
  )
}
