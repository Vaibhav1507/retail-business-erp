import './product-form.css'
import { Paged } from './Paged'
import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { SetupAssistant } from './SetupAssistant'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'
import { BarcodeManager } from './BarcodeManager'

export type Product = {
  id: string; name: string; categoryId: string; category: string; isGeneric: boolean; sku: string; unit: string
  brand: string | null; model: string | null; attributes: Record<string, string>; isActive: boolean; hsnCode?: string | null; gstRate?: number | null
}
export type Category = { id: string; name: string; productCount: number }

const msg = (e: unknown) => (e instanceof Error ? e.message : 'Something went wrong')
const UNITS = ['pcs', 'set', 'pack', 'm']
const ATTRS: [string, string][] = [['colour', 'Colour'], ['capacity', 'Capacity'], ['connector_type', 'Connector type'], ['compatible_with', 'Compatible with']]

export type Draft = { name: string; category: string; unit: string; generic: boolean; brand: string; model: string; attrs: Record<string, string>; active: boolean; hsn: string; gst: string }
export const blank: Draft = { name: '', category: '', unit: 'pcs', generic: true, brand: '', model: '', attrs: {}, active: true, hsn: '', gst: '' }
export const toDraft = (p: Product): Draft => ({ name: p.name, category: p.category, unit: p.unit, generic: p.isGeneric, brand: p.brand ?? '', model: p.model ?? '', attrs: { ...p.attributes }, active: p.isActive, hsn: p.hsnCode ?? '', gst: p.gstRate == null ? '' : String(p.gstRate) })

const ELECTRONICS_ATTRS: [string, string][] = [['capacity', 'Screen size or capacity (e.g. 43 inch, 10000mAh)'], ['colour', 'Colour'], ['connector_type', 'Connector / resolution (e.g. USB-C, 4K)'], ['compatible_with', 'Compatible with']]

const ELECTRONICS_CATEGORIES = ['LED TVs', 'Mobile Phones', 'Chargers', 'Cables', 'Earphones and Headphones', 'Power Banks', 'Smartwatches', 'Speakers and Soundbars', 'Cases and Guards', 'Memory and Storage', 'Home Appliances', 'Lighting and Bulbs', 'Batteries', 'Computer Accessories', 'Accessories', 'Other']

const TV_BRANDS = ['Samsung', 'LG', 'Sony', 'Mi', 'Redmi', 'TCL', 'Hisense', 'Vu', 'Acer', 'OnePlus', 'Panasonic', 'Philips', 'Toshiba', 'Thomson', 'Kodak']
const TV_SIZES = [24, 32, 40, 43, 50, 55, 65, 75]
const PRODUCT_NAMES: string[] = [
  ...TV_BRANDS.flatMap(b => TV_SIZES.map(s => `${b} ${s} inch LED TV`)),
  ...['Samsung', 'Apple', 'Xiaomi', 'Realme', 'Vivo', 'Oppo', 'OnePlus', 'Motorola'].map(b => `${b} Smartphone`),
  ...['Apple 20W USB-C Adapter', 'Samsung 25W Charger', 'Mi 33W Charger', 'Realme 65W Charger', 'Type-C Cable 1m', 'Lightning Cable 1m', 'Micro USB Cable 1m', 'HDMI Cable 1.5m', 'HDMI Cable 3m', 'Aux Cable 1m'],
  ...['boAt Airdopes', 'boAt Rockerz Neckband', 'JBL Wired Earphones', 'Sony Headphones', 'Realme Buds', 'OnePlus Buds'],
  ...['Ambrane Power Bank 10000mAh', 'Mi Power Bank 20000mAh', 'Realme Power Bank 10000mAh', 'boAt Smartwatch', 'Noise Smartwatch', 'Fire-Boltt Smartwatch'],
  ...['JBL Bluetooth Speaker', 'boAt Soundbar', 'Sony Home Theatre', 'Zebronics Soundbar', 'Portronics Speaker'],
  ...['Tempered Glass', 'Back Cover', 'Flip Cover', 'SD Card 64GB', 'SD Card 128GB', 'Pen Drive 32GB', 'Pen Drive 64GB', 'Pen Drive 128GB', 'LED Bulb 9W', 'LED Bulb 12W', 'LED Tube Light 20W', 'Extension Board 4 Socket', 'Wi-Fi Router', 'Wireless Mouse', 'Keyboard', 'AA Battery Pack', 'AAA Battery Pack', 'Remote Control Universal', 'TV Wall Mount', 'Set-top Box'],
]

export function ProductForm({ initial, categories, editingId, onSaved, onCancel, businessCategory, barcode }: { initial: Draft; categories: Category[]; editingId: string | null; onSaved: () => void; onCancel?: () => void; businessCategory?: string | null; barcode?: ScanResult | null }) {
  const electronics = businessCategory === 'electronics_mobile'
  const attrFields = electronics ? ELECTRONICS_ATTRS : ATTRS
  const [d, setD] = useState<Draft>(!editingId && electronics ? { ...initial, generic: false } : initial)
  const [error, setError] = useState('')
  const set = (patch: Partial<Draft>) => setD(prev => ({ ...prev, ...patch }))
  const setAttr = (k: string, value: string) => setD(prev => ({ ...prev, attrs: { ...prev.attrs, [k]: value } }))
  const [otherCat, setOtherCat] = useState(false)
  const pickCategory = (e: React.ChangeEvent<HTMLSelectElement>) => {
	if (e.target.value === '__other') { setOtherCat(true); set({ category: '' }) } else { setOtherCat(false); set({ category: e.target.value }) }
  }
  const categoryOptions = [...new Set([...(electronics ? ELECTRONICS_CATEGORIES : []), ...categories.map(c => c.name), ...(d.category && !otherCat ? [d.category] : [])])]
  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    const body = { name: d.name, categoryName: d.category, isGeneric: d.generic, baseUnit: d.unit, brand: d.generic ? null : d.brand, model: d.generic ? null : d.model, attributes: d.attrs, isActive: d.active, hsnCode: d.hsn.trim() || null, gstRate: d.gst.trim() === '' ? null : Number(d.gst) }
    try {
      if (editingId) await api('PUT', `/products/${editingId}`, body)
      else {
        const created = await api<{ id: string }>('POST', '/products', body)
        if (barcode) await api('POST', `/products/${created.id}/barcodes`, { code: barcode.code, symbology: barcode.symbology })
      }
      if (!editingId) setD(electronics ? { ...blank, generic: false } : blank)
      onSaved()
    } catch (err) { setError(msg(err)) }
  }
  return (
    <form className="product-form" onSubmit={submit} aria-label={editingId ? 'Edit product' : 'Add product'}>
      <h3>{editingId ? 'Edit product' : 'Add product'}</h3>
      <label>Name <input list="product-name-options" value={d.name} onChange={e => set({ name: e.target.value })} required maxLength={160} /></label>
      <datalist id="product-name-options">{(electronics ? PRODUCT_NAMES : []).map(n => <option key={n} value={n} />)}</datalist>
	  <label>Category
		<select value={otherCat ? '__other' : d.category} required={!otherCat} onChange={pickCategory}>
		  <option value="">Select a category</option>
		  {categoryOptions.map(c => <option key={c} value={c}>{c}</option>)}
		  <option value="__other">Other (type a new one)...</option>
		</select>
	  </label>
	  {otherCat && <label>New category <input value={d.category} onChange={e => set({ category: e.target.value })} required maxLength={80} placeholder="e.g. Screen Guards" /></label>}
      <label>Unit
        <select value={d.unit} disabled={!!editingId} onChange={e => set({ unit: e.target.value })}>{UNITS.map(u => <option key={u}>{u}</option>)}</select>
      </label>
      <label>HSN code (optional) <input value={d.hsn} onChange={e => set({ hsn: e.target.value.replace(/\D/g, '') })} inputMode="numeric" maxLength={8} placeholder="4, 6 or 8 digits" /></label>
      <label>GST rate % (optional) <select value={d.gst} onChange={e => set({ gst: e.target.value })}><option value="">Not set</option>{[0, 0.25, 3, 5, 12, 18, 28].map(r => <option key={r} value={String(r)}>{r}%</option>)}</select></label>
      <label className="check"><input type="checkbox" checked={d.generic} onChange={e => set({ generic: e.target.checked })} /> Generic / unbranded</label>
      {!d.generic && <>
        <label>Brand <input value={d.brand} onChange={e => set({ brand: e.target.value })} maxLength={80} /></label>
        <label>Model <input value={d.model} onChange={e => set({ model: e.target.value })} maxLength={80} /></label>
      </>}
      <fieldset>
        <legend>Details (optional)</legend>
        {attrFields.map(([k, label]) => <label key={k}>{label} <input value={d.attrs[k] ?? ''} onChange={e => setAttr(k, e.target.value)} maxLength={100} /></label>)}
      </fieldset>
      {editingId && <label className="check"><input type="checkbox" checked={d.active} onChange={e => set({ active: e.target.checked })} /> Active (untick to hide from lists)</label>}
      {barcode && !editingId && <p className="hint-line">Barcode <strong>{barcode.code}</strong> will be linked to this product when you save.</p>}
      {editingId && <BarcodeManager productId={editingId} />}
      <div className="form-actions">
        <button type="submit">{editingId ? 'Save changes' : 'Add product'}</button>
        {onCancel && <button type="button" className="secondary" onClick={onCancel}>Cancel</button>}
      </div>
      {error && <p role="alert">{error}</p>}
    </form>
  )
}

export function Catalogue({ reload, businessCategory }: { reload: () => void; businessCategory?: string | null }) {
  const [q, setQ] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const [items, setItems] = useState<Product[]>([])
  const [categories, setCategories] = useState<Category[]>([])
  const [editing, setEditing] = useState<Product | null>(null)
  const [renaming, setRenaming] = useState<{ id: string; name: string } | null>(null)
  const [error, setError] = useState('')
  const [scanning, setScanning] = useState(false)
  const [scanned, setScanned] = useState<ScanResult | null>(null)

  async function onScan(r: ScanResult) {
    setScanning(false); setError('')
    try {
      const hit = await api<{ sku: string }>('GET', `/products/by-barcode?code=${encodeURIComponent(r.code)}`)
      const found = (await api<Product[]>('GET', `/products?q=${encodeURIComponent(hit.sku)}&includeInactive=true`)).find(p => p.sku === hit.sku)
      if (found) { setScanned(null); setEditing(found); setView('form') }
    } catch (e) {
      if (msg(e).includes('No product has this barcode')) { setScanned(r); setEditing(null); setView('form') } else setError(msg(e))
    }
  }

  const load = useCallback(async () => {
    const params = new URLSearchParams()
    if (q.trim()) params.set('q', q.trim())
    if (categoryId) params.set('categoryId', categoryId)
    if (showInactive) params.set('includeInactive', 'true')
    const [p, c] = await Promise.all([api<Product[]>('GET', `/products?${params}`), api<Category[]>('GET', '/categories')])
    setItems(p); setCategories(c)
  }, [q, categoryId, showInactive])

  useEffect(() => { const t = setTimeout(() => { load().catch(e => setError(msg(e))) }, 200); return () => clearTimeout(t) }, [load])

  const saved = () => { setEditing(null); setView('list'); load().catch(() => undefined); reload() }

  async function rename(e: React.FormEvent) {
    e.preventDefault()
    if (!renaming) return
    setError('')
    try { await api('PUT', `/categories/${renaming.id}`, { name: renaming.name }); setRenaming(null); load().catch(() => undefined); reload() } catch (err) { setError(msg(err)) }
  }

  const [view, setView] = useState<'list' | 'form' | 'categories'>('list')
  const tabs: [typeof view, string][] = [['list', 'Products'], ['categories', 'Categories']]
  const startAdd = () => { setEditing(null); setScanned(null); setView('form') }
  const closeForm = () => { setEditing(null); setScanned(null); setView('list') }

  return (
	<section aria-label="Products" className="catalogue-screen">
	  <nav className="tabs" aria-label="Product sections">
		{tabs.map(([k, n]) => <a key={k} href="#/stock/products" aria-current={(view === k || (view === 'form' && k === 'list')) ? 'true' : undefined} onClick={e => { e.preventDefault(); setEditing(null); setView(k) }}>{n}</a>)}
	  </nav>
	  {error && <p role="alert">{error}</p>}
	  {view === 'form' && (
		<>
		  <p><a href="#/stock/products" onClick={e => { e.preventDefault(); closeForm() }}>&larr; Back to products</a></p>
		  {!editing && <div className="import-block"><SetupAssistant reload={() => { load().catch(() => undefined); reload() }} /></div>}

		  {!editing && <h3>Or add one product by hand</h3>}

		  {editing

			? <ProductForm key={editing.id} initial={toDraft(editing)} categories={categories} editingId={editing.id} businessCategory={businessCategory} onSaved={saved} onCancel={closeForm} />

			: <ProductForm key={scanned?.code ?? 'new'} initial={blank} categories={categories} editingId={null} businessCategory={businessCategory} barcode={scanned} onSaved={saved} onCancel={closeForm} />}
		</>
	  )}
	  {view === 'list' && (
		<>
		  <div className="stock-actions"><button type="button" onClick={startAdd}>Add product</button> <button type="button" className="secondary" onClick={() => setScanning(true)}>Scan barcode</button></div>
		  {scanning && <BarcodeScanner onScan={onScan} onClose={() => setScanning(false)} />}
		  <div role="search">
			<label>Search <input type="search" placeholder="Name, brand, model, SKU, colour..." value={q} onChange={e => setQ(e.target.value)} /></label>
			<label>Category
			  <select value={categoryId} onChange={e => setCategoryId(e.target.value)}>
				<option value="">All categories</option>
				{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
			  </select>
			</label>
			<label><input type="checkbox" checked={showInactive} onChange={e => setShowInactive(e.target.checked)} /> Show inactive</label>
		  </div>
		  <Paged rows={items}>{page => (
			<div className="table-scroll">
			  <table>
				<thead><tr><th>SKU</th><th>Name</th><th>Category</th><th>Unit</th><th>Brand / model</th><th>Details</th><th>Actions</th></tr></thead>
				<tbody>
				  {page.map(p => (
					<tr key={p.id} style={p.isActive ? undefined : { opacity: 0.55 }}>
					  <td>{p.sku}</td><td>{p.name}{p.isActive ? '' : ' (inactive)'}</td><td>{p.category}</td><td>{p.unit}</td>
					  <td>{p.isGeneric ? 'Generic' : `${p.brand ?? ''} ${p.model ?? ''}`}</td>
					  <td>{Object.entries(p.attributes).map(([k, v]) => `${k.replace('_', ' ')}: ${v}`).join(', ')}</td>
					  <td><button type="button" className="secondary" onClick={() => { setEditing(p); setView('form') }} aria-label={`Edit ${p.name}`}>Edit</button></td>
					</tr>
				  ))}
				  {items.length === 0 && <tr><td colSpan={7}>No products match.</td></tr>}
				</tbody>
			  </table>
			</div>
		  )}</Paged>
		</>
	  )}
	  {view === 'categories' && (
		<>
		  <p className="hint-line">Groups used to organise your products. Rename a group here and every product in it follows.</p>
		  <div className="table-scroll">
			<table aria-label="Product categories">
			  <thead><tr><th>Category</th><th>Products</th><th>Actions</th></tr></thead>
			  <tbody>
				{categories.map(c => (
				  <tr key={c.id}>
					<td data-label="Category">{c.name}</td>
					<td data-label="Products">{c.productCount}</td>
					<td data-label="Actions"><button type="button" className="secondary" onClick={() => setRenaming({ id: c.id, name: c.name })} aria-label={`Rename ${c.name}`}>Rename</button></td>
				  </tr>
				))}
			  </tbody>
			</table>
		  </div>
		  {renaming && (
			<form onSubmit={rename} aria-label="Rename category">
			  <label>New name <input value={renaming.name} onChange={e => setRenaming({ ...renaming, name: e.target.value })} required maxLength={80} /></label>
			  <button type="submit">Save</button> <button type="button" className="secondary" onClick={() => setRenaming(null)}>Cancel</button>
			</form>
		  )}
		</>
	  )}
	</section>
  )
}
