import './billing-forms.css'
import { PrintPage } from './AfterSales'
import { BuyPage, MorePage, MoneyPage, NAV, PartiesPage, SellPage } from './Pages'
import { StockPage } from './Stock'
import { HomePage, BusinessSettings } from './Home'
import { AccountAccess } from './AccountAccess'
import { BusinessSetup } from './BusinessSetup'
import type { Product } from './Catalogue'
import './styles.css'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, resetCsrf } from './api'
import { setLanguage, t, toLang } from './i18n'
import type { Lang, MessageKey } from './i18n'

type Me = { email: string; tenantId: string | null; profileComplete: boolean; businessCategory: string | null }
type Location = { id: string; name: string }
type StockRow = { productId: string; name: string; sku: string; location: string; quantity: number; wac: number; value: number; category: string; suppliers: string[] }

const newKey = () => crypto.randomUUID()
const msg = (e: unknown) => (e instanceof Error ? e.message : 'Something went wrong')

function OpeningStock({ products, locations, reload }: { products: Product[]; locations: Location[]; reload: () => void }) {
  const [key, setKey] = useState(newKey)
  const [locationId, setLocationId] = useState('')
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10))
  const [productId, setProductId] = useState('')
  const [qty, setQty] = useState('')
  const [cost, setCost] = useState('')
  const [lines, setLines] = useState<{ productId: string; quantity: number; unitCost: number }[]>([])
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const loc = locationId || locations[0]?.id || ''
  const nameOf = (id: string) => products.find(p => p.id === id)?.name ?? id
  function addLine() {
	setError('')
	const quantity = Number(qty), unitCost = Number(cost)
	if (!productId || !(quantity > 0) || !(unitCost > 0)) { setError('Choose a product, quantity > 0 and unit cost > 0.'); return }
	if (lines.some(l => l.productId === productId)) { setError('Product already added.'); return }
	setLines([...lines, { productId, quantity, unitCost }]); setQty(''); setCost('')
  }
  async function post() {
	setError(''); setDone('')
	try {
	  const r = await api<{ total: number; replayed: boolean }>('POST', '/opening-stock', { locationId: loc, effectiveDate: date, lines }, key)
	  setDone(`Posted. Inventory and Opening Balance Equity total: ${r.total.toFixed(2)} INR${r.replayed ? ' (already posted earlier)' : ''}`)
	  setLines([]); setKey(newKey()); reload()
	} catch (err) {
	  setError(err instanceof ApiError && err.status >= 500 ? 'Server error. Retrying is safe.' : msg(err))
	}
  }
  return (
	<section aria-label="Opening stock" className="billing-screen">
	  <h2>Opening stock</h2>
	  <label>Location
		<select value={loc} onChange={e => setLocationId(e.target.value)}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select>
	  </label>
	  <label>Effective date <input type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
	  <div>
		<label>Product
		  <select value={productId} onChange={e => setProductId(e.target.value)}>
			<option value="">Select</option>
			{products.map(p => <option key={p.id} value={p.id}>{p.name} ({p.sku})</option>)}
		  </select>
		</label>
		<label>Quantity <input inputMode="decimal" value={qty} onChange={e => setQty(e.target.value)} /></label>
		<label>Unit cost (INR) <input inputMode="decimal" value={cost} onChange={e => setCost(e.target.value)} /></label>
		<button type="button" onClick={addLine}>Add line</button>
	  </div>
	  <ul>{lines.map(l => <li key={l.productId}>{nameOf(l.productId)}: {l.quantity} x {l.unitCost}</li>)}</ul>
	  <p data-testid="estimated-total">Estimated total (the server recomputes this): {lines.reduce((s, l) => s + Math.round(l.quantity * l.unitCost * 100) / 100, 0).toFixed(2)} INR</p>
	  <button type="button" onClick={post} disabled={lines.length === 0}>Post opening stock</button>
	  {error && <p role="alert">{error}</p>}
	  {done && <p role="status">{done}</p>}
	</section>
  )
}

function parseRoute() {
  const m = /^#\/([a-z]+)(?:\/([a-z]+))?/.exec(window.location.hash)
  return { page: m?.[1] ?? 'home', tab: m?.[2] ?? '' }
}

export default function App() {
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const [products, setProducts] = useState<Product[]>([])
  const [locations, setLocations] = useState<Location[]>([])
  const [stock, setStock] = useState<StockRow[]>([])
  const [route, setRoute] = useState(parseRoute)
  const [businessName, setBusinessName] = useState<string | undefined>()
  const [lang, setLang] = useState<Lang>('en')
  const [dark, setDark] = useState(() => localStorage.getItem('rbos.theme') === 'dark')
  useEffect(() => { document.documentElement.dataset.theme = dark ? 'dark' : 'light'; localStorage.setItem('rbos.theme', dark ? 'dark' : 'light') }, [dark])

  const loadMe = useCallback(async () => {
	try { setMe(await api<Me>('GET', '/me')) } catch { setMe(null) }
  }, [])
  const loadData = useCallback(async () => {
	const [p, l, s] = await Promise.all([api<Product[]>('GET', '/products'), api<Location[]>('GET', '/locations'), api<StockRow[]>('GET', '/stock')])
	setProducts(p); setLocations(l); setStock(s)
  }, [])

  useEffect(() => { loadMe() }, [loadMe])
  useEffect(() => {
    if (!me?.tenantId || !me.profileComplete) return
    api<{ businessName: string; language: string }>('GET', '/business-profile').then(p => { setBusinessName(p.businessName); const l = toLang(localStorage.getItem('rbos.lang') ?? p.language); setLanguage(l); setLang(l) }).catch(() => undefined)
  }, [me?.tenantId, me?.profileComplete])
  useEffect(() => { document.documentElement.lang = lang; document.title = t('app.title') }, [lang])
  useEffect(() => { if (me?.tenantId) loadData().catch(() => undefined) }, [me?.tenantId, loadData])
  useEffect(() => {
	const on = () => { setRoute(parseRoute()); window.scrollTo(0, 0) }
	window.addEventListener('hashchange', on)
	return () => window.removeEventListener('hashchange', on)
  }, [])

  function chooseLang(l: Lang) { localStorage.setItem('rbos.lang', l); setLang(l) }
  async function logout() { await api('POST', '/auth/logout'); resetCsrf(); window.location.hash = ''; setMe(null) }

  if (me === undefined) return <p className="loading">{t('app.loading')}</p>
  if (me === null) return <AccountAccess onDone={loadMe} />
  if (!me.tenantId || !me.profileComplete) return <main className="access"><BusinessSetup hasBusiness={!!me.tenantId} onDone={loadMe} /></main>
  const printMatch = /^#\/print\/(sale|purchase|payment)\/([0-9a-f-]{36})$/i.exec(window.location.hash)
  if (printMatch) return <PrintPage kind={printMatch[1]} id={printMatch[2]} />

  const c = { products, locations, reload: () => { loadData().catch(() => undefined) } }
  const { page, tab } = route
  const first = (d: string) => tab || d
  let body: React.ReactNode
  switch (page) {
	case 'sell': body = <SellPage tab={first('menu')} c={c} />; break
	case 'buy': body = <BuyPage tab={first('receive')} c={c} />; break
	case 'stock': body = <StockPage tab={first('current')} c={c} rows={stock} opening={<OpeningStock products={products} locations={locations} reload={c.reload} />} businessCategory={me.businessCategory} />; break
	case 'settings': body = <BusinessSettings onSaved={loadMe} onLanguage={chooseLang} />; break
	case 'parties': body = <PartiesPage tab={first('customers')} c={c} />; break
	case 'money': body = <MoneyPage tab={first('cash')} />; break
	case 'more': body = <MorePage tab={tab} onLogout={logout} email={me.email} />; break
	default: body = <HomePage businessName={businessName} />
  }
  const active = NAV.some(n => n.key === page) ? page : 'home'
  return (
	<div className="shell">
	  <header className="topbar"><h1>{t('app.title')}</h1><span className="who">{me.email}</span><button type="button" onClick={() => setDark(d => !d)}>{dark ? t('theme.light') : t('theme.dark')}</button><button type="button" onClick={logout}>{t('app.signOut')}</button></header>
	  <nav className="nav" aria-label="Main">
		{NAV.map(n => <a key={n.key} href={'#/' + n.key} aria-current={active === n.key ? 'page' : undefined}><span className="ico" aria-hidden="true">{n.ico}</span>{t(('nav.' + n.key) as MessageKey)}</a>)}
	  </nav>
	  <main className="content">{body}</main>
	</div>
  )
}

