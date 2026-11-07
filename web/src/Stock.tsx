import './billing-forms.css'
import { Paged } from './Paged'
import { useMemo, useState } from 'react'
import { Catalogue } from './Catalogue'
import type { Product } from './Catalogue'

import { Procurement } from './Procurement'
import { Finance } from './Finance'
import { t } from './i18n'

export type StockRow = { productId: string; name: string; sku: string; location: string; quantity: number; wac: number; value: number; category: string; suppliers: string[] }
type Common = { products: Product[]; locations: { id: string; name: string }[]; reload: () => void }

const inr = (n: number) => '₹ ' + n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const ACTIONS: [string, string][] = [['products', 'Manage Products'], ['opening', 'Add Opening Stock'], ['purchase', 'Add Recent Purchase'], ['adjust', 'Adjust Stock']]

function Back() { return <p><a className="back-link" href="#/stock">← Back to Stock</a></p> }

function Inventory({ rows, products }: { rows: StockRow[]; products: Product[] }) {
  const [q, setQ] = useState('')
  const [cat, setCat] = useState('')
  const [sup, setSup] = useState('')
  const byId = useMemo(() => new Map(products.map(p => [p.id, p])), [products])
  const cats = useMemo(() => [...new Set(products.map(p => p.category))].sort(), [products])
  const sups = useMemo(() => [...new Set(rows.flatMap(r => r.suppliers ?? []))].sort(), [rows])
  const shown = rows.filter(r => {
    const p = byId.get(r.productId)
    const text = `${r.name} ${r.sku} ${p?.brand ?? ''} ${p?.model ?? ''} ${(r.suppliers ?? []).join(' ')}`.toLowerCase()
    return text.includes(q.trim().toLowerCase()) && (!cat || p?.category === cat) && (!sup || (r.suppliers ?? []).includes(sup))
  })
  const status = (n: number) => (n > 0 ? 'In stock' : 'Out of stock')
  return (
    <section aria-label="Stock on hand" className="billing-screen">
      <div role="search">
        <label>Search<input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder="Name, brand, model, supplier or SKU" /></label>
        <label>Category<select value={cat} onChange={e => setCat(e.target.value)}><option value="">All categories</option>{cats.map(x => <option key={x}>{x}</option>)}</select></label>
        <label>Supplier<select value={sup} onChange={e => setSup(e.target.value)}><option value="">All suppliers</option>{sups.map(x => <option key={x}>{x}</option>)}</select></label>
      </div>
      {shown.length === 0 ? (rows.length === 0 ? <div className="empty"><p>{t('stock.empty')}</p><p><a className="btn" href="#/stock/products">{t('stock.addProducts')}</a> <a className="btn secondary" href="#/stock/opening">{t('stock.addOpening')}</a></p></div> : <p className="empty">Nothing matches your search.</p>) : (
        <>
          <p className="hint-line">Stock value = available quantity × average cost, updated automatically after every purchase, sale or adjustment.</p>
      <Paged rows={shown}>{page => (
          <div className="table-scroll stock-table"><table>
            <thead><tr><th>Product</th><th>Category</th><th>Brand / model</th><th>Supplier</th><th>SKU</th><th>Available</th><th>Unit</th><th>Stock value</th><th>Status</th></tr></thead>
            <tbody>{page.map(r => { const p = byId.get(r.productId); return (
              <tr key={r.productId + r.location}>
                <td data-label="Product">{r.name}</td><td data-label="Category">{p?.category}</td>
                <td data-label="Brand / model">{p?.isGeneric ? 'Generic' : `${p?.brand ?? ''} ${p?.model ?? ''}`.trim()}</td>
                <td data-label="Supplier">{(r.suppliers ?? []).join(", ") || "-"}</td><td data-label="SKU">{r.sku}</td><td data-label="Available">{r.quantity}</td><td data-label="Unit">{p?.unit}</td>
                <td data-label="Stock value">{inr(r.value)}</td>
                <td data-label="Status"><span className={'badge ' + (r.quantity > 0 ? 'ok' : 'out')}>{status(r.quantity)}</span></td>
              </tr>) })}</tbody>
          </table></div>
      )}</Paged>
        </>
      )}
    </section>
  )
}

export function StockPage({ tab, c, rows, opening, businessCategory }: { tab: string; c: Common; rows: StockRow[]; opening: React.ReactNode; businessCategory: string | null }) {
  const key = tab === 'current' ? '' : tab
  return (
    <div className="stock-page">
      <div className="page-title"><h2>{t('stock.title')}</h2><p>{t('stock.subtitle')}</p></div>
      {key === '' && <>
        <div className="stock-actions">{ACTIONS.map(([k, n], i) => <a key={k} className={'btn' + (i === 0 ? '' : ' secondary')} href={`#/stock/${k}`}>{n}</a>)}</div>
        <Inventory rows={rows} products={c.products} />
      </>}
      {key === 'products' && <><Back /><Catalogue reload={c.reload} businessCategory={businessCategory} /></>}
      {key === 'opening' && <><Back />{opening}</>}
      {key === 'purchase' && <><Back /><Procurement {...c} show="receive" /></>}
            {key === 'adjust' && <><Back /><Finance key="adjust" only={['adjust']} /></>}
    </div>
  )
}
