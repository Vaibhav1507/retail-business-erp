import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { t } from './i18n'

type Opt = { id: string; name: string }
type Move = { id: string; date: string; from: string; to: string; note: string | null; items: string }

/** B7: move stock between locations (value moves at WAC; no ledger impact). */
export function LocationMovesPanel() {
  const [products, setProducts] = useState<Opt[]>([])
  const [locations, setLocations] = useState<Opt[]>([])
  const [moves, setMoves] = useState<Move[]>([])
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [product, setProduct] = useState('')
  const [qty, setQty] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      const [p, l, m] = await Promise.all([api<Opt[]>('GET', '/products'), api<Opt[]>('GET', '/locations'), api<Move[]>('GET', '/stock-transfers')])
      setProducts(p); setLocations(l); setMoves(m); setError('')
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not load transfers') }
  }, [])
  useEffect(() => { void load() }, [load])

  const submit = async () => {
    setBusy(true)
    try {
      await api('POST', '/stock-transfers', {
        date: new Date().toISOString().slice(0, 10), fromLocationId: from, toLocationId: to, note: note || null,
        lines: [{ productId: product, quantity: Number(qty) }],
      })
      setQty(''); setNote(''); await load()
    } catch (e) { setError(e instanceof Error ? e.message : 'Transfer failed') }
    finally { setBusy(false) }
  }

  const ready = from && to && from !== to && product && Number(qty) > 0
  return (
    <section aria-label="Stock transfers">
      {error && <p role="alert">{error}</p>}
      <label>{t('mv.from')}<select value={from} onChange={e => setFrom(e.target.value)}><option value="">{t('mv.select')}</option>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <label>{t('mv.to')}<select value={to} onChange={e => setTo(e.target.value)}><option value="">{t('mv.select')}</option>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <label>{t('mv.product')}<select value={product} onChange={e => setProduct(e.target.value)}><option value="">{t('mv.select')}</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
      <label>{t('mv.qty')}<input inputMode="decimal" value={qty} onChange={e => setQty(e.target.value)} /></label>
      <label>{t('mv.note')}<input value={note} maxLength={300} onChange={e => setNote(e.target.value)} /></label>
      <button onClick={submit} disabled={busy || !ready}>{t('mv.submit')}</button>
      <ul>{moves.map(m => <li key={m.id}>{m.date}: {m.from} → {m.to}: {m.items}{m.note ? ` (${m.note})` : ''}</li>)}</ul>
    </section>
  )
}
