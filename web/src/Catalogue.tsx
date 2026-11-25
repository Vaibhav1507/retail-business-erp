import './product-form.css'
import { Paged } from './Paged'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api } from './api'
import { SetupAssistant } from './SetupAssistant'
import { ManufacturerRegistry } from './ManufacturerRegistry'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'
import { BarcodeManager } from './BarcodeManager'

export type Product = {
  id: string; name: string; categoryId: string; category: string; isGeneric: boolean; sku: string; unit: string
  brand: string | null; model: string | null; attributes: Record<string, string>; isActive: boolean; hsnCode?: string | null; gstRate?: number | null; mrp?: number | null
  specs?: Record<string, string>; sources?: ProductSource[]; possibleDuplicate?: boolean
}
export type ProductSource = { documentId: string; documentName: string; brand: string | null; versionLabel: string | null; page: number | null }
export const sourceLabel = (s: ProductSource) => `${s.documentName}${s.versionLabel ? ` (${s.versionLabel})` : ''}${s.page ? `, page ${s.page}` : ''}`
export type Category = { id: string; name: string; productCount: number }
type MasterProduct = {
  id: string; name: string; categoryName: string; isGeneric: boolean; brand: string | null; model: string | null
  attributes: Record<string, string>; specs: Record<string, string>; hsnCode: string | null; gstRate: number | null
  alreadyInBusiness: boolean
}

const msg = (e: unknown) => (e instanceof Error ? e.message : 'Something went wrong')
const UNITS = ['pcs', 'set', 'pack', 'm']
const ATTRS: [string, string][] = [['colour', 'Colour'], ['capacity', 'Capacity'], ['connector_type', 'Connector type'], ['compatible_with', 'Compatible with']]

export type Draft = { name: string; category: string; unit: string; generic: boolean; brand: string; model: string; attrs: Record<string, string>; active: boolean; hsn: string; gst: string; mrp: string }
export const blank: Draft = { name: '', category: '', unit: 'pcs', generic: true, brand: '', model: '', attrs: {}, active: true, hsn: '', gst: '', mrp: '' }
export const toDraft = (p: Product): Draft => ({ name: p.name, category: p.category, unit: p.unit, generic: p.isGeneric, brand: p.brand ?? '', model: p.model ?? '', attrs: { ...p.attributes }, active: p.isActive, hsn: p.hsnCode ?? '', gst: p.gstRate == null ? '' : String(p.gstRate), mrp: p.mrp == null ? '' : String(p.mrp) })

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
  const [brandNames, setBrandNames] = useState<string[]>([])
  useEffect(() => { api<{ name: string; isActive: boolean }[]>('GET', '/brands').then(b => setBrandNames(b.filter(x => x.isActive).map(x => x.name))).catch(() => undefined) }, [])
  const pickCategory = (e: React.ChangeEvent<HTMLSelectElement>) => {
	if (e.target.value === '__other') { setOtherCat(true); set({ category: '' }) } else { setOtherCat(false); set({ category: e.target.value }) }
  }
  const categoryOptions = [...new Set([...(electronics ? ELECTRONICS_CATEGORIES : []), ...categories.map(c => c.name), ...(d.category && !otherCat ? [d.category] : [])])]
  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    const body = { name: d.name, categoryName: d.category, isGeneric: d.generic, baseUnit: d.unit, brand: d.generic ? null : d.brand, model: d.generic ? null : d.model, attributes: d.attrs, isActive: d.active, hsnCode: d.hsn.trim() || null, gstRate: d.gst.trim() === '' ? null : Number(d.gst), mrp: d.mrp.trim() === '' ? null : Number(d.mrp), barcode: !editingId && barcode ? barcode.code : null }
    try {
      if (editingId) await api('PUT', `/products/${editingId}`, body)
      else {
        await api('POST', '/products', body)
      }
      if (!editingId) setD(electronics ? { ...blank, generic: false } : blank)
      onSaved()
    } catch (err) { setError(msg(err)) }
  }
  return (
    <form className="product-form" onSubmit={submit} aria-label={editingId ? 'Edit product' : 'Add product'}>
      <header className="product-form-heading">
        <span className="product-form-eyebrow">PRODUCT DETAILS</span>
        <span className="product-form-step" aria-hidden="true">01</span>
        <h3>{editingId ? 'Edit product' : 'Add product'}</h3>
        <p>Start with the name and category. Add optional details when needed.</p>
      </header>
      <section className="product-form-section" aria-labelledby="product-basics-heading">
        <h3 id="product-basics-heading">Product basics</h3>
        <label><span className="product-field-label">Name <span aria-hidden="true" className="required-mark">*</span></span><input list="product-name-options" value={d.name} onChange={e => set({ name: e.target.value })} required maxLength={160} placeholder="Enter the product name" /></label>
        <datalist id="product-name-options">{(electronics ? PRODUCT_NAMES : []).map(n => <option key={n} value={n} />)}</datalist>
        <label><span className="product-field-label">Category <span aria-hidden="true" className="required-mark">*</span></span>
          <select value={otherCat ? '__other' : d.category} required={!otherCat} onChange={pickCategory}>
            <option value="">Select a category</option>
            {categoryOptions.map(c => <option key={c} value={c}>{c}</option>)}
            <option value="__other">＋ Add a new category</option>
          </select>
        </label>
        {otherCat && <label>New category <input value={d.category} onChange={e => set({ category: e.target.value })} required maxLength={80} placeholder="e.g. Screen Guards" /></label>}
        <label><span className="product-field-label">Unit</span><select value={d.unit} disabled={!!editingId} onChange={e => set({ unit: e.target.value })} required>{UNITS.map(u => <option key={u}>{u}</option>)}</select></label>
      </section>
      <details className="product-form-disclosure">
        <summary><span>Brand and product identity</span><small>Optional for generic products</small></summary>
        <label className="check"><input type="checkbox" checked={d.generic} onChange={e => set({ generic: e.target.checked })} /> Generic / unbranded</label>
        {!d.generic && <>
          <label>Brand <input list="brand-options" value={d.brand} onChange={e => set({ brand: e.target.value })} maxLength={80} placeholder="Pick or type a brand" /></label><datalist id="brand-options">{brandNames.map(n => <option key={n} value={n} />)}</datalist>
          <label>Model number <input value={d.model} onChange={e => set({ model: e.target.value })} maxLength={80} placeholder="Enter exact model number" /></label>
        </>}
      </details>
      <details className="product-form-disclosure">
        <summary><span>Tax and pricing references</span><small>Optional</small></summary>
        <label>HSN code <input value={d.hsn} onChange={e => set({ hsn: e.target.value.replace(/\D/g, '') })} inputMode="numeric" maxLength={8} placeholder="4, 6 or 8 digits" /></label>
        <label>GST rate <select value={d.gst} onChange={e => set({ gst: e.target.value })}><option value="">Not set</option>{[0, 0.25, 3, 5, 12, 18, 28].map(r => <option key={r} value={String(r)}>{r}%</option>)}</select></label>
        <label>MRP <small>Reference only, not selling price</small><input value={d.mrp} onChange={e => set({ mrp: e.target.value.replace(/[^0-9.]/g, '') })} inputMode="decimal" maxLength={12} placeholder="₹ 0.00" /></label>
      </details>
      <details className="product-form-disclosure">
        <summary><span>Product details</span><small>Colour, capacity and compatibility</small></summary>
        <fieldset>
          <legend>Additional details</legend>
          {attrFields.map(([k, label]) => <label key={k}>{label} <input value={d.attrs[k] ?? ''} onChange={e => setAttr(k, e.target.value)} maxLength={100} /></label>)}
        </fieldset>
      </details>
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
  const [masterItems, setMasterItems] = useState<MasterProduct[]>([])
  const [editing, setEditing] = useState<Product | null>(null)
  const [renaming, setRenaming] = useState<{ id: string; name: string } | null>(null)
  const [error, setError] = useState('')
  // Prevent an older catalogue response from replacing results for a newer search/filter.
  const loadSequence = useRef(0)
  const masterLoadSequence = useRef(0)
  const [scanning, setScanning] = useState(false)
  const [scanned, setScanned] = useState<ScanResult | null>(null)
  type Suggestion = { id: string; name: string; brand: string | null; model: string | null; sku: string; match: string }
	type ExternalSuggestion = { source: string; name: string | null; brand: string | null; model: string | null; description: string | null; imageUrl: string | null; attribution?: string | null; lookedUpAtUtc?: string | null }
  type ProviderResult = { provider: string; status: 'Found' | 'NotFound' | 'Timeout' | 'RateLimited' | 'Unavailable' | 'Error'; message?: string | null; lookedUpAtUtc?: string | null }
  const [suggestions, setSuggestions] = useState<Suggestion[]>([])
  const [external, setExternal] = useState<ExternalSuggestion[]>([])
  const [providerResults, setProviderResults] = useState<ProviderResult[]>([])

  async function onScan(r: ScanResult) {
    setScanning(false); setError('')
    try {
      const hit = await api<{ sku: string }>('GET', `/products/by-barcode?code=${encodeURIComponent(r.code)}`)
      const found = (await api<Product[]>('GET', `/products?q=${encodeURIComponent(hit.sku)}&includeInactive=true`)).find(p => p.sku === hit.sku)
      if (found) { setScanned(null); setEditing(found); setView('form') }
    } catch (e) {
      if (msg(e).includes('No product has this barcode')) {
		setScanned(r); setEditing(null); setView('form'); setSuggestions([]); setExternal([]); setProviderResults([])
		api<{ local: { suggestions: Suggestion[] }; external?: ExternalSuggestion[]; providerResults?: ProviderResult[] }>('GET', `/products/suggest-for-barcode?code=${encodeURIComponent(r.code)}`)
		  .then(s => { setSuggestions(s.local?.suggestions ?? []); setExternal(s.external ?? []); setProviderResults(s.providerResults ?? []) })
		  .catch(() => undefined)
      } else setError(msg(e))
    }
  }

  const load = useCallback(async () => {
    const requestId = ++loadSequence.current
    const params = new URLSearchParams()
    if (q.trim()) params.set('q', q.trim())
    if (categoryId) params.set('categoryId', categoryId)
    if (showInactive) params.set('includeInactive', 'true')
    try {
      const [p, c] = await Promise.all([api<Product[]>('GET', `/products?${params}`), api<Category[]>('GET', '/categories')])
      if (requestId !== loadSequence.current) return
      setItems(p); setCategories(c)
    } catch (err) {
      if (requestId === loadSequence.current) throw err
    }
  }, [q, categoryId, showInactive])

  useEffect(() => { const t = setTimeout(() => { load().catch(e => setError(msg(e))) }, 200); return () => clearTimeout(t) }, [load])

  const saved = () => { setEditing(null); setView('list'); load().catch(() => undefined); reload() }

  const loadMaster = useCallback(async () => {
    const requestId = ++masterLoadSequence.current
    const params = new URLSearchParams()
    if (q.trim()) params.set('q', q.trim())
    try {
      const rows = await api<MasterProduct[]>('GET', `/products/master?${params}`)
      if (requestId === masterLoadSequence.current) setMasterItems(rows)
    } catch (err) {
      if (requestId === masterLoadSequence.current) throw err
    }
  }, [q])

  async function addFromMaster(id: string) {
    setError('')
    try {
      await api('POST', `/products/master/${id}/add`)
      await Promise.all([load(), loadMaster()])
      reload()
    } catch (err) { setError(msg(err)) }
  }

  async function rename(e: React.FormEvent) {
    e.preventDefault()
    if (!renaming) return
    setError('')
    try { await api('PUT', `/categories/${renaming.id}`, { name: renaming.name }); setRenaming(null); load().catch(() => undefined); reload() } catch (err) { setError(msg(err)) }
  }

  const [view, setView] = useState<'list' | 'form' | 'categories' | 'master'>('list')
  useEffect(() => {
    if (view !== 'master') return
    const t = setTimeout(() => { loadMaster().catch(e => setError(msg(e))) }, 150)
    return () => clearTimeout(t)
  }, [loadMaster, view])
  const tabs: [typeof view, string][] = [['list', 'My products'], ['master', 'Shared catalogue'], ['categories', 'Categories']]
  const [requestedEditId] = useState(() => new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('edit'))
  const editRouteHandled = useRef(false)
  const similarCategoryGroups = useMemo(() => {
    const groups = new Map<string, string[]>()
    categories.forEach(category => {
      const key = category.name.trim().toLowerCase().replace(/\s+/g, ' ').replace(/s$/, '')
      groups.set(key, [...(groups.get(key) ?? []), category.name])
    })
    return [...groups.values()].filter(group => group.length > 1)
  }, [categories])
  useEffect(() => {
    if (editRouteHandled.current || !requestedEditId || items.length === 0) return
    const product = items.find(item => item.id === requestedEditId)
    if (!product) return
    editRouteHandled.current = true
    setEditing(product)
    setView('form')
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}#/stock/products`)
  }, [items, requestedEditId])
  const startAdd = () => { setEditing(null); setScanned(null); setView('form') }
  const closeForm = () => { setEditing(null); setScanned(null); setView('list') }

  return (
	<section aria-label="Products" className="catalogue-screen">
	  <nav className="tabs" aria-label="Product sections">
		{tabs.map(([k, n]) => <a key={k} href="#/stock/products" aria-current={(view === k || (view === 'form' && k === 'list')) ? 'true' : undefined} onClick={e => { e.preventDefault(); setEditing(null); setView(k) }}>{n}</a>)}
	  </nav>
	  {error && <p role="alert">{error}</p>}
	  {view === 'form' && (
    <div className="product-form-page">
      <div className="page-title product-form-page-title">
        <span className="catalogue-stock-badge">STOCK</span>
        <h2>{editing ? 'Edit product' : 'Add product'}</h2>
        <p>{editing ? 'Update the information for this product.' : 'Add the information needed to identify this product.'}</p>
      </div>
      <section className="product-form-shell">
        <p className="product-form-back"><a href="#/stock/products" onClick={e => { e.preventDefault(); closeForm() }}>← Back to products</a></p>
        {!editing && scanned && suggestions.length > 0 && (
          <div className="import-block" role="region" aria-label="Possible matches">
            <h3>Possible matches for {scanned.code}</h3>
            <p className="hint-line">Link this barcode to an existing product only after verification, or add a new one manually below.</p>
            <ul>{suggestions.map(s => <li key={s.id}>{s.name} ({s.brand ?? 'generic'} {s.model ?? ''}, {s.sku}, {s.match}) <button type="button" className="secondary" onClick={() => { api('POST', `/products/${s.id}/barcodes`, { code: scanned.code, symbology: scanned.symbology }).then(() => { setScanned(null); setSuggestions([]); setView('list'); load().catch(() => undefined) }).catch(err => setError(msg(err))) }}>Link barcode</button></li>)}</ul>
          </div>
        )}
        {!editing && scanned && external.length > 0 && (
          <div className="import-block" role="region" aria-label="External barcode suggestions">
            <h3>External references for {scanned.code}</h3>
            <p className="hint-line">Review only. This data never auto-creates or auto-updates products.</p>
            <ul>{external.map((x, i) => <li key={`${x.source}-${i}`}>{x.name ?? 'Unnamed'} ({x.brand ?? 'unknown brand'}) — {x.source}{x.model ? `, model ${x.model}` : ''}{x.attribution ? ` · ${x.attribution}` : ''}</li>)}</ul>
          </div>
        )}
        {!editing && scanned && providerResults.length > 0 && (
          <div className="import-block" role="region" aria-label="External provider status">
            <h3>External lookup status</h3>
            <ul>{providerResults.map((p, i) => <li key={`${p.provider}-${i}`}>{p.provider}: {p.status}{p.message ? ` (${p.message})` : ''}</li>)}</ul>
          </div>
        )}
        {editing
          ? <ProductForm key={editing.id} initial={toDraft(editing)} categories={categories} editingId={editing.id} businessCategory={businessCategory} onSaved={saved} onCancel={closeForm} />
          : <ProductForm key={scanned?.code ?? 'new'} initial={blank} categories={categories} editingId={null} businessCategory={businessCategory} barcode={scanned} onSaved={saved} onCancel={closeForm} />}
      </section>
    </div>
  )}  {view === 'list' && (
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
			  <table aria-label="My products">
				<thead><tr><th>SKU</th><th>Name</th><th>Category</th><th>Unit</th><th>Brand / model</th><th>Details</th><th>Source</th><th>Actions</th></tr></thead>
				<tbody>
				  {page.map(p => (
					<tr key={p.id} style={p.isActive ? undefined : { opacity: 0.55 }}>
					  <td data-label="SKU">{p.sku}</td><td data-label="Name">{p.name}{p.isActive ? '' : ' (inactive)'}</td><td data-label="Category">{p.category}</td><td data-label="Unit">{p.unit}</td>
					  <td data-label="Brand / model">{p.isGeneric ? 'Generic' : `${p.brand ?? ''} ${p.model ?? ''}`}</td>
					  <td data-label="Details">{Object.entries(p.attributes).map(([k, v]) => `${k.replace('_', ' ')}: ${v}`).join(', ')}{p.specs && Object.keys(p.specs).length > 0 && <div className="hint-line">{Object.entries(p.specs).map(([k, v]) => `${k.replace(/_/g, ' ')}: ${v}`).join(', ')}</div>}{p.possibleDuplicate && <div className="hint-line" role="status">Possible duplicate: same brand and model number as another product. Review before merging.</div>}</td>
					  <td data-label="Source">{(p.sources ?? []).length === 0 ? '-' : <ul className="plain-list">{p.sources!.map((s, i) => <li key={i}>{sourceLabel(s)}</li>)}</ul>}</td>
					  <td data-label="Actions"><button type="button" className="secondary" onClick={() => { setEditing(p); setView('form') }} aria-label={`Edit ${p.name}`}>Edit</button></td>
					</tr>
				  ))}
				  {items.length === 0 && <tr><td colSpan={8}>No products match.</td></tr>}
				</tbody>
			  </table>
			</div>
		  )}</Paged>
		</>
	  )}
	  {view === 'master' && (
        <>
          <div className="catalogue-privacy" role="note"><span aria-hidden="true">🔒</span><p><strong>Shared product information</strong><br />Products are shared within your business category. Adding one creates a product in your shop. Your SKU, barcode, stock, buying cost and selling price stay private.</p></div>
          <label>Search shared products <input type="search" placeholder="Product name, brand or model..." value={q} onChange={e => setQ(e.target.value)} /></label>
          <Paged rows={masterItems}>{page => (
            <div className="table-scroll">
              <table aria-label="Shared product catalogue">
                <thead><tr><th>Name</th><th>Category</th><th>Brand / model</th><th>Details</th><th>Action</th></tr></thead>
                <tbody>
                  {page.map(p => (
                    <tr key={p.id}>
                      <td data-label="Name">{p.name}</td><td data-label="Category">{p.categoryName}</td>
                      <td data-label="Brand / model">{p.isGeneric ? 'Generic' : `${p.brand ?? ''} ${p.model ?? ''}`}</td>
                      <td data-label="Details">{Object.entries({ ...p.attributes, ...p.specs }).map(([k, v]) => `${k.replace(/_/g, ' ')}: ${v}`).join(', ') || '-'}</td>
                      <td data-label="Action"><button type="button" className="secondary" disabled={p.alreadyInBusiness} onClick={() => addFromMaster(p.id)}>{p.alreadyInBusiness ? 'Already added' : 'Add to my products'}</button></td>
                    </tr>
                  ))}
                  {masterItems.length === 0 && <tr><td colSpan={5}>No shared products match. Importing or adding a verified product adds it to this shared catalogue.</td></tr>}
                </tbody>
              </table>
            </div>
          )}</Paged>
        </>
      )}
	  {view === 'categories' && (
		<>
		  <p className="hint-line">Groups used to organise your products. Rename a group here and every product in it follows.</p>
          {similarCategoryGroups.length > 0 && <div className="category-similarity-warning" role="note"><strong>Review similar category names</strong><span>{similarCategoryGroups.map(group => group.join(' / ')).join(' · ')}</span><small>These names may describe the same group. Review before renaming; products are not merged automatically.</small></div>}
		  <div className="table-scroll">
			<table aria-label="Product categories">
			  <thead><tr><th>Category</th><th>Products</th><th>Actions</th></tr></thead>
			  <tbody>
				{categories.map(c => (
				  <tr key={c.id}>
					<td data-label="Category">{c.name}</td>
					<td data-label="Products">{c.productCount}</td>
					<td data-label="Actions"><button type="button" className="secondary category-rename" onClick={() => setRenaming({ id: c.id, name: c.name })} aria-label={`Rename ${c.name}`}>Rename</button></td>
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
