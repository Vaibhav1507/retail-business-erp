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

const ACTIONS: [string, string, string, string][] = [
  ['products', 'Manage Products', 'Products, catalogue and categories', '▦'],
  ['opening', 'Add Opening Stock', 'Set the starting quantity in your shop', '＋'],
  ['purchase', 'Add Recent Purchase', 'Record goods received from a supplier', '📦'],
  ['adjust', 'Adjust Stock', 'Correct a counted quantity', '⇄'],
]

function Back() { return <p><a className="back-link" href="#/stock">← Back to Stock</a></p> }

function Inventory({ rows, products }: { rows: StockRow[]; products: Product[] }) {
  const [q, setQ] = useState('')
  const [cat, setCat] = useState('')
  const [sup, setSup] = useState('')
  const [avail, setAvail] = useState('')
  const byId = useMemo(() => new Map(products.map(p => [p.id, p])), [products])
  const cats = useMemo(() => [...new Set(products.map(p => p.category))].sort(), [products])
  const sups = useMemo(() => [...new Set(rows.flatMap(r => r.suppliers ?? []))].sort(), [rows])
  const shown = rows.filter(r => {
    const p = byId.get(r.productId)
    const text = `${r.name} ${r.sku} ${p?.brand ?? ''} ${p?.model ?? ''} ${(r.suppliers ?? []).join(' ')}`.toLowerCase()
    return text.includes(q.trim().toLowerCase()) && (!cat || p?.category === cat) && (!sup || (r.suppliers ?? []).includes(sup)) && (!avail || (avail === 'in' ? r.quantity > 0 : r.quantity <= 0))
  })
  const status = (n: number) => (n > 0 ? 'In stock' : 'Out of stock')
  return (
    <section aria-label="Stock on hand" className="billing-screen">
      <div role="search">
        <label>Search<input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder="Name, brand, model, supplier or SKU" /></label>
        <label>Category<select value={cat} onChange={e => setCat(e.target.value)}><option value="">All categories</option>{cats.map(x => <option key={x}>{x}</option>)}</select></label>
        <label>Supplier<select value={sup} onChange={e => setSup(e.target.value)}><option value="">All suppliers</option>{sups.map(x => <option key={x}>{x}</option>)}</select></label>
        <label>Availability<select value={avail} onChange={e => setAvail(e.target.value)}><option value="">All</option><option value="in">In stock</option><option value="out">Out of stock</option></select></label>
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
  const quantities = useMemo(() => {
    const totals = new Map<string, number>()
    rows.forEach(row => totals.set(row.productId, (totals.get(row.productId) ?? 0) + row.quantity))
    return totals
  }, [rows])
  const activeProducts = c.products.filter(product => product.isActive)
  const inStock = activeProducts.filter(product => (quantities.get(product.id) ?? 0) > 0).length
  const outOfStock = activeProducts.length - inStock
  const stockValue = rows.reduce((total, row) => total + row.value, 0)
  return (
    <div className="stock-page">
      <div className="page-title"><h2>{t('stock.title')}</h2><p>{t('stock.subtitle')}</p></div>
      {key === '' && <>
        <div className="stock-actions" aria-label="Stock actions">{ACTIONS.map(([k, name, description, icon], i) => <a key={k} className={`btn stock-action${i === 0 ? ' primary' : ' secondary'}`} href={`#/stock/${k}`}><span className="stock-action-icon" aria-hidden="true">{icon}</span><span className="stock-action-copy"><strong>{name}</strong><small>{description}</small></span><span className="stock-action-arrow" aria-hidden="true">→</span></a>)}</div>
        <section className="stock-summary" aria-label="Stock summary">
          <div><span>Active products</span><strong>{activeProducts.length}</strong></div>
          <div><span>In stock</span><strong>{inStock}</strong></div>
          <div><span>Out of stock</span><strong>{outOfStock}</strong></div>
          <div><span>Stock value</span><strong>{inr(stockValue)}</strong></div>
        </section>
        <Inventory rows={rows} products={c.products} />
      </>}
      {key === 'products' && <><Back /><Catalogue reload={c.reload} businessCategory={businessCategory} /></>}
      {key === 'opening' && <><Back />{opening}</>}
      {key === 'purchase' && <><Back /><Procurement {...c} show="receive" /></>}
            {key === 'adjust' && <><Back /><Finance key="adjust" only={['adjust']} /></>}
    </div>
  )
}
