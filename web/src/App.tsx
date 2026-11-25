import './billing-forms.css'
import { PrintPage } from './AfterSales'
import { BuyPage, MorePage, MoneyPage, NAV, PartiesPage, SellPage } from './Pages'
import { StockPage } from './Stock'
import { HomePage, BusinessSettings } from './Home'
import { AccountAccess } from './AccountAccess'
import { ProfileMenu } from './ProfileMenu'
import { DevicesPage } from './Devices'
import { AppLockGate } from './AppLock'
import { NavigationAssistant } from './NavigationAssistant'
import { BusinessSetup } from './BusinessSetup'
import type { Product } from './Catalogue'
import './styles.css'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, resetCsrf } from './api'
import { clearOfflineWorkspace, describeOfflineStorageError, loadOfflineSnapshot, saveOfflineSnapshot } from './offlineStore'
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
  const [review, setReview] = useState(false)
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const loc = locationId || locations[0]?.id || ''
  const productOf = (id: string) => products.find(p => p.id === id)
  const nameOf = (id: string) => productOf(id)?.name ?? id
  const lineValue = (line: { quantity: number; unitCost: number }) => Math.round(line.quantity * line.unitCost * 100) / 100
  const totalValue = lines.reduce((sum, line) => sum + lineValue(line), 0)

  function addLine() {
    setError('')
    const quantity = Number(qty), unitCost = Number(cost)
    if (!productId || !(quantity > 0) || !(unitCost > 0)) {
      setError('Choose a product and enter a quantity and cost greater than zero.')
      return
    }
    if (lines.some(line => line.productId === productId)) {
      setError('This product is already in the list. Remove its existing entry before adding it again.')
      return
    }
    setLines(current => [...current, { productId, quantity, unitCost }])
    setProductId('')
    setQty('')
    setCost('')
    setReview(false)
  }

  async function post() {
    setError('')
    setDone('')
    try {
      const r = await api<{ total: number; replayed: boolean }>('POST', '/opening-stock', { locationId: loc, effectiveDate: date, lines }, key)
      setDone(`Posted. Inventory and Opening Balance Equity total: ${r.total.toFixed(2)} INR${r.replayed ? ' (already posted earlier)' : ''}`)
      setLines([])
      setReview(false)
      setKey(newKey())
      reload()
    } catch (err) {
      setError(err instanceof ApiError && err.status >= 500 ? 'Server error. Retrying is safe.' : msg(err))
    }
  }

  return (
    <section aria-label="Opening stock" className="billing-screen opening-stock-screen">
      <div className="opening-stock-heading">
        <div className="opening-stock-step">01</div>
        <p className="opening-stock-eyebrow">Initial inventory</p>
        <h2>Opening stock</h2>
        <p className="opening-stock-description">Record what is physically in the shop as of a date.</p>
      </div>

      <div className="opening-stock-field opening-stock-location">
        <label htmlFor="opening-stock-location">Stock location</label>
        <select id="opening-stock-location" value={loc} onChange={e => setLocationId(e.target.value)}>
          {locations.map(location => <option key={location.id} value={location.id}>{location.name}</option>)}
        </select>
      </div>
      <div className="opening-stock-field opening-stock-date">
        <label htmlFor="opening-stock-date">Stock counted as of</label>
        <input id="opening-stock-date" type="date" value={date} onChange={e => setDate(e.target.value)} />
      </div>

      <div className="opening-stock-add-section">
        <h3>Add products to opening stock</h3>
        <div className="opening-stock-field opening-stock-product">
          <label htmlFor="opening-stock-product">Product</label>
          <select id="opening-stock-product" value={productId} onChange={e => setProductId(e.target.value)}>
            <option value="">Select a product</option>
            {products.map(product => <option key={product.id} value={product.id}>{product.name} ({product.sku})</option>)}
          </select>
        </div>
        <div className="opening-stock-entry-grid">
          <div className="opening-stock-field">
            <label htmlFor="opening-stock-quantity">Quantity</label>
            <input id="opening-stock-quantity" inputMode="decimal" type="number" min="0" step="any" value={qty} onChange={e => setQty(e.target.value)} placeholder="0" />
          </div>
          <div className="opening-stock-field">
            <label htmlFor="opening-stock-cost">Cost for one item (₹)</label>
            <input id="opening-stock-cost" inputMode="decimal" type="number" min="0" step="any" value={cost} onChange={e => setCost(e.target.value)} placeholder="Enter cost" />
          </div>
        </div>
        <p className="opening-stock-cost-help">Use the actual or owner-estimated cost. RBOS does not guess a zero cost.</p>
        <button type="button" className="opening-stock-add-button" onClick={addLine}><span aria-hidden="true">＋ </span>Add item</button>
      </div>

      {lines.length > 0 && <div className="opening-stock-lines" aria-label="Opening stock items">
        <h3>Added items ({lines.length})</h3>
        {lines.map(line => <article className="opening-stock-line" key={line.productId}>
          <div><strong>{nameOf(line.productId)}</strong><span>{line.quantity} × ₹{line.unitCost.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span></div>
          <strong>₹{lineValue(line).toLocaleString('en-IN', { maximumFractionDigits: 2 })}</strong>
          <button type="button" className="opening-stock-remove" onClick={() => { setLines(current => current.filter(item => item.productId !== line.productId)); setReview(false) }} aria-label={`Remove ${nameOf(line.productId)}`}>Remove</button>
        </article>)}
      </div>}

      {error && <p className="opening-stock-message" role="alert">{error}</p>}
      {done && <p className="opening-stock-message success" role="status">{done}</p>}

      {review && lines.length > 0 && <div className="opening-stock-review" role="region" aria-label="Review opening stock">
        <h3>Review starting stock</h3>
        <p>{t('onboarding.stockReviewNote')}</p>
        <ul>{lines.map(line => <li key={line.productId}>{nameOf(line.productId)} — {line.quantity} × ₹{line.unitCost.toFixed(2)} = ₹{lineValue(line).toFixed(2)}</li>)}</ul>
        <p><strong>Total stock value: ₹{totalValue.toFixed(2)}</strong></p>
        <div className="opening-stock-review-actions">
          <button type="button" className="secondary" onClick={() => setReview(false)}>Back to edit</button>
          <button type="button" onClick={post}>Confirm &amp; post opening stock</button>
        </div>
      </div>}

      <div className="opening-stock-footer">
        <div className="opening-stock-footer-summary">
          <strong>{lines.length} opening {lines.length === 1 ? 'item' : 'items'}</strong>
          <span>₹{totalValue.toLocaleString('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 2 })} estimated value</span>
        </div>
        <button type="button" disabled={lines.length === 0 || !loc} onClick={() => { setError(''); setReview(true) }}>Review starting stock</button>
      </div>
    </section>
  )
}
function parseRoute() {
  const m = /^#\/([a-z]+)(?:\/([a-z]+))?/.exec(window.location.hash)
  return { page: m?.[1] ?? 'home', tab: m?.[2] ?? '' }
}

export default function App() {
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const [authError, setAuthError] = useState('')
  const [offlineMode, setOfflineMode] = useState(false)
  const [products, setProducts] = useState<Product[]>([])
  const [locations, setLocations] = useState<Location[]>([])
  const [stock, setStock] = useState<StockRow[]>([])
  const [route, setRoute] = useState(parseRoute)
  const [businessName, setBusinessName] = useState<string | undefined>()
  const [lang, setLang] = useState<Lang>('en')
  const [dark, setDark] = useState(() => localStorage.getItem('rbos.theme') === 'dark')
  useEffect(() => { document.documentElement.dataset.theme = dark ? 'dark' : 'light'; localStorage.setItem('rbos.theme', dark ? 'dark' : 'light') }, [dark])

  const loadMe = useCallback(async () => {
    setAuthError('')
    setMe(current => current === null ? undefined : current)
    try {
      const current = await api<Me>('GET', '/me')
      setMe(current)
      setOfflineMode(false)
      if (current.tenantId) localStorage.setItem('rbos.offline.identity', JSON.stringify({ email: current.email, tenantId: current.tenantId }))
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setOfflineMode(false)
        localStorage.removeItem('rbos.offline.identity')
        setMe(null)
        return
      }
      let offlineStorageError: unknown
      try {
        const identity = JSON.parse(localStorage.getItem('rbos.offline.identity') ?? 'null') as { email?: string; tenantId?: string } | null
        if (identity?.email && identity.tenantId) {
          const snapshot = await loadOfflineSnapshot(identity.email.trim().toLowerCase() + ':' + identity.tenantId)
          if (snapshot) {
            setMe(snapshot.me)
            setProducts(snapshot.products as Product[])
            setLocations(snapshot.locations as Location[])
            setStock(snapshot.stock as StockRow[])
            setOfflineMode(true)
            setAuthError('Offline mode: showing the last saved shop snapshot. New bills remain pending until the server confirms them.')
            return
          }
        }
      } catch (storageError) {
        offlineStorageError = storageError
      }
      setAuthError(offlineStorageError
        ? `Could not open the protected offline workspace (${describeOfflineStorageError(offlineStorageError)}). Check browser site-storage permissions, then retry.`
        : 'Could not verify your session or find a saved offline workspace. Connect to the internet and retry.')
    }
  }, [])
  const loadData = useCallback(async () => {
	const [p, l, s] = await Promise.all([api<Product[]>('GET', '/products'), api<Location[]>('GET', '/locations'), api<StockRow[]>('GET', '/stock')])
	setProducts(p); setLocations(l); setStock(s)
    if (me?.tenantId) {
      try {
        await saveOfflineSnapshot(me.email.trim().toLowerCase() + ':' + me.tenantId, {
          me: { email: me.email, tenantId: me.tenantId, profileComplete: me.profileComplete, businessCategory: me.businessCategory },
          products: p, locations: l, stock: s
        })
      } catch (error) {
        setAuthError(`Shop data is loaded, but the protected offline snapshot could not be saved (${describeOfflineStorageError(error)}). Keep this device online until the snapshot is saved successfully.`)
      }
    }
  }, [me])

  useEffect(() => { loadMe() }, [loadMe])
  useEffect(() => { const onOnline = () => { void loadMe() }; window.addEventListener('online', onOnline); return () => window.removeEventListener('online', onOnline) }, [loadMe])
  useEffect(() => {
    if (!me?.tenantId || !me.profileComplete || offlineMode) return
    api<{ businessName: string; language: string }>('GET', '/business-profile').then(p => { setBusinessName(p.businessName); const l = toLang(localStorage.getItem('rbos.lang') ?? p.language); setLanguage(l); setLang(l) }).catch(() => undefined)
  }, [me?.tenantId, me?.profileComplete, offlineMode])
  useEffect(() => { document.documentElement.lang = lang; document.title = t('app.title') }, [lang])
  useEffect(() => {
    const root = document.getElementById('root')
    if (!root) return
    const labelTables = () => {
      root.querySelectorAll<HTMLTableElement>('table').forEach(table => {
        // Mobile CSS turns native table elements into block/grid cards. Restore explicit
        // table roles so assistive technology still exposes the row/column relationships.
        table.setAttribute('role', 'table')
        table.querySelectorAll('thead, tbody, tfoot').forEach(group => group.setAttribute('role', 'rowgroup'))
        table.querySelectorAll<HTMLTableRowElement>('tr').forEach(row => row.setAttribute('role', 'row'))
        table.querySelectorAll<HTMLTableCellElement>('th').forEach(cell => {
          cell.setAttribute('role', cell.scope === 'row' ? 'rowheader' : 'columnheader')
        })
        table.querySelectorAll<HTMLTableCellElement>('td').forEach(cell => cell.setAttribute('role', 'cell'))

        const labels = Array.from(table.tHead?.querySelectorAll('th') ?? []).map(th => (th.textContent ?? '').trim())
        if (labels.length === 0) return
        table.querySelectorAll<HTMLTableRowElement>('tbody tr').forEach(row => {
          Array.from(row.cells).forEach((cell, index) => {
            if (!cell.hasAttribute('data-label') && labels[index]) cell.setAttribute('data-label', labels[index])
          })
        })
      })
    }
    labelTables()
    const observer = new MutationObserver(labelTables)
    observer.observe(root, { childList: true, subtree: true })
    return () => observer.disconnect()
  }, [])
  useEffect(() => { if (me?.tenantId && !offlineMode) loadData().catch(() => undefined) }, [me?.tenantId, loadData, offlineMode])
  useEffect(() => {
    let previousHash = window.location.hash
    const protectedRoute = /^#\/(sell\/bill|buy\/receive|stock\/opening)$/
    const on = () => {
      const nextHash = window.location.hash
      if (nextHash !== previousHash && protectedRoute.test(previousHash) && !window.confirm(t('navigation.leave'))) { window.location.hash = previousHash; return }
      previousHash = nextHash; setRoute(parseRoute()); window.scrollTo(0, 0)
    }
    window.addEventListener('hashchange', on)
    return () => window.removeEventListener('hashchange', on)
  }, [])

  function chooseLang(l: Lang) { localStorage.setItem('rbos.lang', l); setLanguage(l); setLang(l) }
  async function logout() {
    await api('POST', '/auth/logout')
    resetCsrf()
    try {
      if (me?.tenantId) await clearOfflineWorkspace(me.email.trim().toLowerCase() + ':' + me.tenantId, ['sale'])
    } catch {
      setAuthError('Signed out, but this browser could not clear its encrypted offline records. Keep this device private until storage can be cleared.')
    } finally {
      localStorage.removeItem('rbos.offline.identity')
      window.location.hash = ''
      setMe(null)
      setOfflineMode(false)
    }
  }

  if (me === undefined) return <main className="access"><p className="loading" role={authError ? "alert" : undefined}>{authError || t('app.loading')}</p>{authError && <button type="button" onClick={() => { void loadMe() }}>Retry connection</button>}</main>
  if (me === null) return <AccountAccess onDone={loadMe} />
  if (!me.tenantId || !me.profileComplete) return <main className="access"><BusinessSetup hasBusiness={!!me.tenantId} onDone={loadMe} /></main>
  const printMatch = /^#\/print\/(sale|purchase|payment)\/([0-9a-f-]{36})$/i.exec(window.location.hash)
  if (printMatch) return <PrintPage kind={printMatch[1]} id={printMatch[2]} />

  const c = { products, locations, reload: () => { if (!offlineMode) loadData().catch(() => undefined) }, offlineMode, ownerKey: me.email.trim().toLowerCase() + ':' + (me.tenantId ?? ''), businessCategory: me.businessCategory }
  const { page, tab } = route
  const first = (d: string) => tab || d
  let body: React.ReactNode
  switch (page) {
	case 'sell': body = <SellPage tab={first('menu')} c={c} />; break
	case 'buy': body = <BuyPage tab={first('receive')} c={c} />; break
	case 'stock': body = <StockPage tab={first('current')} c={c} rows={stock} opening={<OpeningStock products={products} locations={locations} reload={c.reload} />} businessCategory={me.businessCategory} />; break
	case 'devices': body = <DevicesPage onCurrentDeviceRevoked={loadMe} />; break
	case 'settings': body = <BusinessSettings onSaved={loadMe} onLanguage={chooseLang} />; break
	case 'parties': body = <PartiesPage tab={first('customers')} c={c} />; break
	case 'money': body = <MoneyPage tab={first('cash')} />; break
	case 'more': body = <MorePage tab={tab} onLogout={logout} email={me.email} />; break
	default: body = <HomePage businessName={businessName} />
  }
  const active = NAV.some(n => n.key === page) ? page : 'home'
  const invoiceFocus = page === 'sell' && first('menu') === 'bill'
  return (
	<AppLockGate email={me.email} onSignOut={logout}><div className={invoiceFocus ? 'shell invoice-focus' : 'shell'}>
	  <header className="topbar"><img className="brand-logo" src="/logo-mark.svg" alt="Dhandha Mitra" width="40" height="40" /><ProfileMenu businessName={businessName ?? me.email} email={me.email} dark={dark} onToggleDark={() => setDark(d => !d)} onLogout={logout} /></header>
      <NavigationAssistant />
	  {!invoiceFocus && <nav className="nav" aria-label="Main">
		{NAV.map(n => <a key={n.key} href={'#/' + n.key} aria-current={active === n.key ? 'page' : undefined}><span className="ico" aria-hidden="true">{n.ico}</span>{t(('nav.' + n.key) as MessageKey)}</a>)}
	  </nav>}
	  {authError && <p role="status" className="connection-warning">{authError} <button type="button" onClick={() => { void loadMe() }}>Retry connection</button></p>}
	
	  <main className="content">{body}</main>
	</div></AppLockGate>
  )
}

