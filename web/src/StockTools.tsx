import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'

type Product = { id: string; name: string }
type Sheet = { productId: string; product: string; unit: string; system: number }
type Hist = { id: string; number: number; date: string; product: string; location: string; quantity: number; value: number; reason: string; note: string }
type Low = { productId: string; product: string; unit: string; reorderLevel: number; onHand: number }
type Dead = { productId: string; product: string; unit: string; onHand: number; value: number; lastSale: string }
type Ageing = { productId: string; product: string; value: number; buckets: { label: string; quantity: number }[] }
type Conv = { productId: string; product: string; baseUnit: string; unit: string; factor: number }
type Loc = { id: string; name: string }

const today = () => new Date().toISOString().slice(0, 10)
const err = (e: unknown, f: string) => (e instanceof ApiError ? e.message : f)

function Note({ m }: { m: { ok: boolean; text: string } | null }) {
  return m ? <p role={m.ok ? 'status' : 'alert'}>{m.text}</p> : null
}

export function StockCount() {
  const [locs, setLocs] = useState<Loc[]>([])
  const [loc, setLoc] = useState('')
  const [sheet, setSheet] = useState<Sheet[]>([])
  const [counted, setCounted] = useState<Record<string, string>>({})
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  useEffect(() => {
    api<Loc[]>('GET', '/locations').then(l => { setLocs(l); setLoc(l[0]?.id ?? '') }).catch(e => setMsg({ ok: false, text: err(e, 'Could not load locations.') }))
  }, [])
  const load = useCallback(async () => {
    if (!loc) return
    setSheet(await api<Sheet[]>('GET', `/stock-count/sheet?locationId=${loc}`)); setCounted({})
  }, [loc])
  useEffect(() => { load().catch(e => setMsg({ ok: false, text: err(e, 'Could not load the count sheet.') })) }, [load])

  const lines = sheet.filter(s => counted[s.productId] !== undefined && counted[s.productId] !== '').map(s => ({ productId: s.productId, counted: Number(counted[s.productId]) }))
  const submit = async () => {
    setMsg(null)
    try {
      const r = await api<{ adjusted: number }>('POST', '/stock-count', { locationId: loc, date: today(), lines }, crypto.randomUUID())
      setMsg({ ok: true, text: `Count saved. ${r.adjusted} item(s) adjusted.` }); await load()
    } catch (e) { setMsg({ ok: false, text: err(e, 'Could not save the count.') }) }
  }
  return (
    <section aria-label="Physical stock count">
      <h3>Physical stock count</h3>
      <label>Location <select value={loc} onChange={e => setLoc(e.target.value)}>{locs.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <table aria-label="Count sheet"><thead><tr><th>Item</th><th>In books</th><th>Counted</th><th>Difference</th></tr></thead>
        <tbody>{sheet.map(s => {
          const v = counted[s.productId] ?? ''
          return <tr key={s.productId}><td>{s.product}</td><td>{s.system} {s.unit}</td>
            <td><input inputMode="decimal" aria-label={`Counted ${s.product}`} value={v} onChange={e => setCounted(c => ({ ...c, [s.productId]: e.target.value }))} /></td>
            <td>{v === '' ? '' : (Number(v) - s.system).toFixed(3)}</td></tr>
        })}</tbody></table>
      <button type="button" onClick={submit} disabled={lines.length === 0 || lines.some(l => !(l.counted >= 0))}>Save count</button>
      <Note m={msg} />
    </section>
  )
}

export function AdjustmentHistory() {
  const [rows, setRows] = useState<Hist[]>([])
  const [reason, setReason] = useState('')
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)
  useEffect(() => {
    api<Hist[]>('GET', `/stock-adjustments/history${reason ? `?reason=${encodeURIComponent(reason)}` : ''}`).then(setRows).catch(e => setMsg({ ok: false, text: err(e, 'Could not load history.') }))
  }, [reason])
  return (
    <section aria-label="Adjustment history">
      <h3>Adjustment history</h3>
      <label>Reason <select value={reason} onChange={e => setReason(e.target.value)}><option value="">All</option>{['Shortage', 'Damage', 'Theft', 'Expired', 'FoundOnCount', 'Other'].map(r => <option key={r}>{r}</option>)}</select></label>
      <Note m={msg} />
      {rows.length === 0 ? <p>No adjustments found.</p> : (
        <table aria-label="Adjustments"><thead><tr><th>No.</th><th>Date</th><th>Item</th><th>Location</th><th>Qty</th><th>Value</th><th>Reason</th><th>Note</th></tr></thead>
          <tbody>{rows.map(r => <tr key={r.id}><td>{r.number}</td><td>{r.date}</td><td>{r.product}</td><td>{r.location}</td><td>{r.quantity}</td><td>{Number(r.value).toFixed(2)}</td><td>{r.reason}</td><td>{r.note}</td></tr>)}</tbody></table>
      )}
    </section>
  )
}

export function StockAlerts() {
  const [products, setProducts] = useState<Product[]>([])
  const [low, setLow] = useState<Low[]>([])
  const [dead, setDead] = useState<Dead[]>([])
  const [ageing, setAgeing] = useState<Ageing[]>([])
  const [days, setDays] = useState('90')
  const [pid, setPid] = useState('')
  const [level, setLevel] = useState('')
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const loadLow = useCallback(async () => setLow(await api<Low[]>('GET', '/stock/low')), [])
  useEffect(() => {
    Promise.all([api<Product[]>('GET', '/products'), loadLow(), api<Ageing[]>('GET', '/stock/ageing').then(setAgeing)])
      .then(([p]) => { setProducts(p); setPid(prev => prev || p[0]?.id || '') })
      .catch(e => setMsg({ ok: false, text: err(e, 'Could not load stock reports.') }))
  }, [loadLow])
  useEffect(() => {
    const d = Number(days)
    if (!(d >= 1 && d <= 3650)) return
    api<Dead[]>('GET', `/stock/dead?days=${d}`).then(setDead).catch(e => setMsg({ ok: false, text: err(e, 'Could not load dead stock.') }))
  }, [days])

  const save = async () => {
    setMsg(null)
    try { await api('PUT', '/reorder-levels', { productId: pid, level: Number(level) }); setMsg({ ok: true, text: 'Reorder level saved.' }); setLevel(''); await loadLow() }
    catch (e) { setMsg({ ok: false, text: err(e, 'Could not save the level.') }) }
  }
  return (
    <section aria-label="Stock alerts">
      <h3>Reorder level</h3>
      <label>Item <select value={pid} onChange={e => setPid(e.target.value)}>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
      <label>Alert when stock is at or below <input inputMode="decimal" value={level} onChange={e => setLevel(e.target.value)} /></label>
      <button type="button" onClick={save} disabled={!pid || level === '' || !(Number(level) >= 0)}>Save level</button>
      <Note m={msg} />
      <h3>Low stock</h3>
      {low.length === 0 ? <p>Nothing is running low.</p> : (
        <table aria-label="Low stock"><thead><tr><th>Item</th><th>On hand</th><th>Reorder level</th></tr></thead>
          <tbody>{low.map(r => <tr key={r.productId}><td>{r.product}</td><td>{r.onHand} {r.unit}</td><td>{r.reorderLevel}</td></tr>)}</tbody></table>
      )}
      <h3>Dead stock</h3>
      <label>Not sold for (days) <input inputMode="numeric" value={days} onChange={e => setDays(e.target.value)} /></label>
      {dead.length === 0 ? <p>No dead stock.</p> : (
        <table aria-label="Dead stock"><thead><tr><th>Item</th><th>On hand</th><th>Value</th><th>Last sale</th></tr></thead>
          <tbody>{dead.map(r => <tr key={r.productId}><td>{r.product}</td><td>{r.onHand} {r.unit}</td><td>{Number(r.value).toFixed(2)}</td><td>{r.lastSale || 'Never'}</td></tr>)}</tbody></table>
      )}
      <h3>Stock ageing</h3>
      {ageing.length === 0 ? <p>No stock on hand.</p> : (
        <table aria-label="Stock ageing"><thead><tr><th>Item</th>{ageing[0].buckets.map(b => <th key={b.label}>{b.label} days</th>)}<th>Value</th></tr></thead>
          <tbody>{ageing.map(r => <tr key={r.productId}><td>{r.product}</td>{r.buckets.map(b => <td key={b.label}>{b.quantity}</td>)}<td>{Number(r.value).toFixed(2)}</td></tr>)}</tbody></table>
      )}
    </section>
  )
}

export function UnitConversions() {
  const [products, setProducts] = useState<Product[]>([])
  const [rows, setRows] = useState<Conv[]>([])
  const [pid, setPid] = useState('')
  const [unit, setUnit] = useState('')
  const [factor, setFactor] = useState('')
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const load = useCallback(async () => setRows(await api<Conv[]>('GET', '/unit-conversions')), [])
  useEffect(() => {
    Promise.all([api<Product[]>('GET', '/products'), load()]).then(([p]) => { setProducts(p); setPid(prev => prev || p[0]?.id || '') })
      .catch(e => setMsg({ ok: false, text: err(e, 'Could not load units.') }))
  }, [load])
  const save = async () => {
    setMsg(null)
    try { await api('PUT', '/unit-conversions', { productId: pid, unit: unit.trim(), factor: Number(factor) }); setMsg({ ok: true, text: 'Unit saved.' }); setUnit(''); setFactor(''); await load() }
    catch (e) { setMsg({ ok: false, text: err(e, 'Could not save the unit.') }) }
  }
  return (
    <section aria-label="Unit conversions">
      <h3>Alternative units</h3>
      <label>Item <select value={pid} onChange={e => setPid(e.target.value)}>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
      <label>Unit (for example box) <input maxLength={20} value={unit} onChange={e => setUnit(e.target.value)} /></label>
      <label>Base units in one of these <input inputMode="decimal" value={factor} onChange={e => setFactor(e.target.value)} /></label>
      <button type="button" onClick={save} disabled={!pid || !unit.trim() || !(Number(factor) > 0)}>Save unit</button>
      <Note m={msg} />
      {rows.length === 0 ? <p>No alternative units yet.</p> : (
        <table aria-label="Units"><thead><tr><th>Item</th><th>Unit</th><th>Equals</th></tr></thead>
          <tbody>{rows.map(r => <tr key={`${r.productId}-${r.unit}`}><td>{r.product}</td><td>1 {r.unit}</td><td>{r.factor} {r.baseUnit}</td></tr>)}</tbody></table>
      )}
    </section>
  )
}
