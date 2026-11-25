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

const inr = (n: number) => '₹ ' + n.toLocaleString('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 2 })

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
    return text.includes(q.trim().toLowerCase()) && (!cat || (p?.category ?? r.category) === cat) && (!sup || (r.suppliers ?? []).includes(sup)) && (!avail || (avail === 'in' ? r.quantity > 0 : r.quantity <= 0))
  })
  const clearFilters = () => { setQ(''); setCat(''); setSup(''); setAvail('') }
  const hasFilters = Boolean(q || cat || sup || avail)
  return (
    <section aria-label="Stock on hand" className="stock-inventory">
      <header className="stock-inventory-header">
        <div><h3>Stock on hand</h3><p>Search and filter inventory.</p></div>
        <button type="button" className="stock-clear-filters" onClick={clearFilters} disabled={!hasFilters}>Clear filters</button>
      </header>
      <div role="search" className="stock-inventory-filters">
        <label className="stock-filter-search">Search
          <input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder="Name, brand, model, supplier or SKU" />
        </label>
        <label className="stock-filter-category">Category
          <select value={cat} onChange={e => setCat(e.target.value)}><option value="">All categories</option>{cats.map(x => <option key={x}>{x}</option>)}</select>
        </label>
        <label className="stock-filter-supplier">Supplier
          <select value={sup} onChange={e => setSup(e.target.value)}><option value="">All suppliers</option>{sups.map(x => <option key={x}>{x}</option>)}</select>
        </label>
        <label className="stock-filter-availability">Availability
          <select value={avail} onChange={e => setAvail(e.target.value)}><option value="">All</option><option value="in">In stock</option><option value="out">Out of stock</option></select>
        </label>
      </div>
      <p className="stock-inventory-note">Stock value = available quantity × average cost. Updated after posted purchases, sales and adjustments.</p>
      {shown.length === 0 ? (
        rows.length === 0
          ? <div className="empty"><p>{t('stock.empty')}</p><p><a className="btn" href="#/stock/products">Manage products</a> <a className="btn secondary" href="#/stock/opening">Add opening stock</a></p></div>
          : <p className="empty">{hasFilters ? 'No products match these filters.' : 'No stock items to show.'}</p>
      ) : (
        <Paged rows={shown} label="products" regionLabel="Stock products">
          {page => (
            <div className="stock-product-list">
              {page.map(row => {
                const product = byId.get(row.productId)
                const category = product?.category ?? row.category ?? 'Uncategorised'
                const brandModel = product?.isGeneric
                  ? 'Generic'
                  : [product?.brand, product?.model].filter(Boolean).join(' / ') || '—'
                const attributes = Object.entries({ ...(product?.attributes ?? {}), ...(product?.specs ?? {}) })
                const mark = (product?.brand || row.name).trim().charAt(0).toUpperCase() || 'P'
                return (
                  <article className="stock-product-card" key={row.productId + row.location}>
                    <div className="stock-product-heading">
                      <span className="stock-product-mark" aria-hidden="true">{mark}</span>
                      <div className="stock-product-identity">
                        <strong>{row.name}</strong>
                        <span>{row.sku} <span aria-hidden="true">·</span> {category}</span>
                      </div>
                      <a className="stock-product-edit" href={`#/stock/products?edit=${encodeURIComponent(row.productId)}`}>Edit</a>
                    </div>
                    <div className="stock-product-facts">
                      <div><span>Brand / model</span><strong>{brandModel}</strong></div>
                      <div><span>Unit</span><strong>{product?.unit ?? 'pcs'}</strong></div>
                      <div><span>Available</span><strong>{row.quantity}</strong></div>
                      <div><span>Status</span><strong><span className={'badge ' + (row.quantity > 0 ? 'ok' : 'out')}>{row.quantity > 0 ? 'In stock' : 'Out of stock'}</span></strong></div>
                      <div className="stock-product-value"><span>Stock value</span><strong>{inr(row.value)}</strong></div>
                    </div>
                    <details className="stock-product-more">
                      <summary>More details</summary>
                      <div className="stock-product-extra">
                        <div><span>Supplier</span><strong>{(row.suppliers ?? []).join(', ') || '—'}</strong></div>
                        <div><span>Stock location</span><strong>{row.location || '—'}</strong></div>
                        {attributes.length > 0 && <div className="stock-product-attributes"><span>Product details</span><strong>{attributes.map(([key, value]) => `${key.replace(/_/g, ' ')}: ${value}`).join(' · ')}</strong></div>}
                      </div>
                    </details>
                  </article>
                )
              })}
            </div>
          )}
        </Paged>
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
