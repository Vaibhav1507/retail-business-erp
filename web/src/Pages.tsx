import { Paged } from './Paged'
import { useEffect, useState } from 'react'
import { api } from './api'
import { Catalogue } from './Catalogue'
import { SetupAssistant } from './SetupAssistant'
import { FirstShopOnboarding } from './FirstShopOnboarding'
import { Procurement } from './Procurement'
import { SalesHistory } from './SalesHistory'
import { Collections } from './Collections'
import { CustomerReturns } from './CustomerReturns'
import { Customers } from './Customers'
import { GenerateInvoice } from './GenerateInvoice'
import { AfterSales } from './AfterSales'
import { Finance, LoansPanel, ReconcilePanel } from './Finance'
import { CashClosing } from './CashClosing'
import { AuditHistory } from './AuditHistory'
import { GstWorkspace } from './GstWorkspace'
import { TeamAccess } from './TeamAccess'
import { BranchesPanel } from './BranchesPanel'
import { LocationMovesPanel } from './LocationMovesPanel'
import { OrdersPanel } from './OrdersPanel'
import { StockCount, AdjustmentHistory, StockAlerts, UnitConversions } from './StockTools'
import { CustomerHistory, CustomerDues } from './CustomerTools'
import './launcher.css'
import { t as tr } from './i18n'

export type Opt = { id: string; name: string; sku?: string }
export type Common = { products: Opt[]; locations: Opt[]; reload: () => void; offlineMode?: boolean; ownerKey?: string; businessCategory?: string | null }

export const NAV = [
  { key: 'home', label: 'Home', ico: '\u{1F3E0}' },
  { key: 'sell', label: 'Sell', ico: '\u{1F9FE}' },
  { key: 'buy', label: 'Buy', ico: '\u{1F4E6}' },
  { key: 'stock', label: 'Stock', ico: '\u{1F3F7}\uFE0F' },
  { key: 'parties', label: 'People', ico: '\u{1F465}' },
  { key: 'money', label: 'Money', ico: '\u20B9' },
  { key: 'more', label: 'More', ico: '\u22EF' },
]

const inr = (n: number) => '\u20B9 ' + n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function Tabs({ page, tab, items }: { page: string; tab: string; items: [string, string][] }) {
  return (
    <nav className="tabs" aria-label="Sections">
      {items.map(([k, n]) => <a key={k} href={`#/${page}/${k}`} aria-current={tab === k ? 'true' : undefined}>{n}</a>)}
    </nav>
  )
}

function Title({ title, hint }: { title: string; hint?: string }) {
  return <div className="page-title"><h2>{title}</h2>{hint && <p>{hint}</p>}</div>
}

export function Home({ stockCount }: { stockCount: number }) {
  const [owed, setOwed] = useState<{ collect: number; pay: number } | null>(null)
  useEffect(() => {
    Promise.all([api<{ owed: number }[]>('GET', '/customers'), api<{ owed: number }[]>('GET', '/suppliers')])
      .then(([c, s]) => setOwed({ collect: c.reduce((t, x) => t + x.owed, 0), pay: s.reduce((t, x) => t + x.owed, 0) }))
      .catch(() => setOwed(null))
  }, [])
  return (
    <div>
      <Title title={tr('home.title')} />
      <div className="actions">
        <a className="action primary" href="#/sell/bill"><span className="ico">{'\u{1F9FE}'}</span><strong>Make Bill</strong></a>
        <a className="action" href="#/buy/receive"><span className="ico">{'\u{1F4E6}'}</span><strong>Add Purchase</strong></a>
        <a className="action" href="#/stock/current"><span className="ico">{'\u{1F3F7}\uFE0F'}</span><strong>Check Stock</strong></a>
        <a className="action" href="#/sell/collect"><span className="ico">{'\u{1F4B5}'}</span><strong>Collect Money</strong></a>
        <strong>{tr('home.pay')}</strong>
        <a className="action" href="#/money/expenses"><span className="ico">{'\u{1F9EE}'}</span><strong>Record Expense</strong></a>
        <a className="action" href="#/stock/products"><span className="ico">{'\u2795'}</span><strong>Add Product</strong></a>
      </div>
      <div className="tiles">
        <a className="tile collect" href="#/parties/customers"><span>Money to Collect</span><b>{owed ? inr(owed.collect) : '\u2014'}</b></a>
        <a className="tile pay" href="#/parties/suppliers"><span>{tr('home.toPay')}</span><b>{owed ? inr(owed.pay) : '\u2014'}</b></a>
        <a className="tile" href="#/stock/current"><span>Items in stock</span><b>{stockCount}</b></a>
        <a className="tile" href="#/money/cash"><span>Cash &amp; Bank</span><b>View</b></a>
      </div>
    </div>
  )
}

export function SellPage({ tab, c }: { tab: string; c: Common }) {
  const menu = !['bill', 'bills', 'collect', 'return'].includes(tab)
  return (
    <div className={menu ? 'sell-hub' : undefined}>
      {menu ? (
        <>
          <Title title={tr('area.sell')} hint={tr('sell.hint')} />
          <div className="sell-hub-actions">
            <a className="sell-hub-primary" href="#/sell/bill">
              <span className="sell-hub-icon" aria-hidden="true">{'\u{1F9FE}'}</span>
              <span className="sell-hub-copy"><strong>{tr('sell.createBill')}</strong><small>{tr('sell.createBillHint')}</small></span>
              <span className="sell-hub-arrow" aria-hidden="true">{'\u2192'}</span>
            </a>
            <div className="sell-hub-secondary">
              <a className="sell-hub-action" href="#/sell/bills">
                <span className="sell-hub-action-icon" aria-hidden="true">{'\u{1F5C2}\uFE0F'}</span>
                <span className="sell-hub-copy"><strong>{tr('sell.history')}</strong><small>{tr('sell.historyHint')}</small></span>
                <span className="sell-hub-action-arrow" aria-hidden="true">{'\u2192'}</span>
              </a>
              <a className="sell-hub-action" href="#/sell/collect">
                <span className="sell-hub-action-icon" aria-hidden="true">{'\u{1F4B0}'}</span>
                <span className="sell-hub-copy"><strong>{tr('sell.collect')}</strong><small>{tr('sell.collectHint')}</small></span>
                <span className="sell-hub-action-arrow" aria-hidden="true">{'\u2192'}</span>
              </a>
              <a className="sell-hub-action" href="#/sell/return">
                <span className="sell-hub-action-icon" aria-hidden="true">{'\u21A9\uFE0F'}</span>
                <span className="sell-hub-copy"><strong>{tr('sell.customerReturns')}</strong><small>{tr('sell.customerReturnsHint')}</small></span>
                <span className="sell-hub-action-arrow" aria-hidden="true">{'\u2192'}</span>
              </a>
            </div>
          </div>
        </>
      ) : (
        <>
          {tab !== 'bill' && <p className="sell-route-back"><a className="back-link" href="#/sell/menu">{'\u2190'} {tr('area.sell')}</a></p>}
        </>
      )}
      {tab === 'bill' && <GenerateInvoice products={c.products} locations={c.locations} reload={c.reload} offlineMode={c.offlineMode ?? false} ownerKey={c.ownerKey ?? ''} businessCategory={c.businessCategory} />}
      {tab === 'bills' && <SalesHistory locations={c.locations} />}
      {tab === 'collect' && <Collections reload={c.reload} />}
      {tab === 'return' && <CustomerReturns reload={c.reload} />}
    </div>
  )
}

export function BuyPage({ tab, c }: { tab: string; c: Common }) {
  return (
    <div className="stock-page">
      <Title title={tr('nav.buy')} hint={tr('buy.hint')} />
      <Tabs page="buy" tab={tab} items={[['receive', tr('buy.add')], ['pay', tr('buy.pay')], ['history', tr('buy.history')], ['return', tr('buy.return')]]} />
      {tab === 'receive' && <Procurement {...c} show="receive" />}
      {tab === 'pay' && <Procurement {...c} show="pay" />}
      {tab === 'history' && <Procurement {...c} show="history" />}
      {tab === 'return' && <AfterSales reload={c.reload} show="preturn" />}
    </div>
  )
}

type StockRow = { productId: string; name: string; sku: string; location: string; quantity: number; wac: number; value: number; category: string; suppliers: string[] }

export function StockList({ rows }: { rows: StockRow[] }) {
  const [q, setQ] = useState('')
  const [cat, setCat] = useState('')
  const [sup, setSup] = useState('')
  const cats = [...new Set(rows.map(r => r.category))].sort()
  const sups = [...new Set(rows.flatMap(r => r.suppliers))].sort()
  const shown = rows.filter(r => (!cat || r.category === cat) && (!sup || r.suppliers.includes(sup)) && (r.name + ' ' + r.sku + ' ' + r.category + ' ' + r.suppliers.join(' ')).toLowerCase().includes(q.trim().toLowerCase()))
  return (
    <section aria-label="Stock on hand">
      <h2>{tr('stock.current')}</h2>
      <div role="search"><label>{tr('stock.search')} <input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder={tr('stock.searchPh')} /></label> <label>{tr('stock.category')} <select value={cat} onChange={e => setCat(e.target.value)}><option value="">{tr('stock.all')}</option>{cats.map(c => <option key={c}>{c}</option>)}</select></label> <label>{tr('stock.supplier')} <select value={sup} onChange={e => setSup(e.target.value)}><option value="">{tr('stock.all')}</option>{sups.map(s => <option key={s}>{s}</option>)}</select></label></div>
      {shown.length === 0 ? <p className="empty">{tr('stock.none')}</p> : (
      <Paged rows={shown}>{page => (
        <div className="table-scroll"><table>
          <thead><tr><th>{tr('stock.item')}</th><th>{tr('stock.code')}</th><th>{tr('stock.category')}</th><th>{tr('stock.supplier')}</th><th>{tr('stock.place')}</th><th>{tr('stock.qty')}</th><th>{tr('stock.avg')}</th><th>{tr('stock.value')}</th></tr></thead>
          <tbody>{page.map(r => <tr key={r.productId + r.location}><td>{r.name}</td><td>{r.sku}</td><td>{r.category}</td><td>{r.suppliers.join(", ")}</td><td>{r.location}</td><td>{r.quantity}</td><td>{r.wac.toFixed(2)}</td><td>{r.value.toFixed(2)}</td></tr>)}</tbody>
        </table></div>
      )}</Paged>
      )}
    </section>
  )
}

export function StockPage({ tab, c, rows, opening }: { tab: string; c: Common; rows: StockRow[]; opening: React.ReactNode }) {
  return (
    <div>
      <Title title={tr('nav.stock')} hint={tr('stock.hint')} />
      <Tabs page="stock" tab={tab} items={[['current', tr('stock.tabCheck')], ['products', tr('stock.tabProducts')], ['opening', tr('stock.tabOpening')], ['import', tr('stock.tabImport')], ['adjust', tr('stock.tabAdjust')], ['count', 'Stock count'], ['history', 'Adjustment history'], ['alerts', 'Alerts and ageing'], ['units', 'Units']]} />
      {tab === 'current' && <StockList rows={rows} />}
      {tab === 'products' && <Catalogue reload={c.reload} />}
      {tab === 'opening' && opening}
      {tab === 'import' && <SetupAssistant reload={c.reload} />}
      {tab === 'adjust' && <Finance key="adjust" only={['adjust']} />}
      {tab === 'count' && <StockCount />}
      {tab === 'history' && <AdjustmentHistory />}
      {tab === 'alerts' && <StockAlerts />}
      {tab === 'units' && <UnitConversions />}
    </div>
  )
}

export function PartiesPage({ tab, c }: { tab: string; c: Common }) {
  return (
    <div>
      <Title title={tr('nav.parties')} hint={tr('parties.hint')} />
      <Tabs page="parties" tab={tab} items={[['customers', tr('area.customers')], ['suppliers', tr('area.suppliers')], ['purchases', 'Purchase history'], ['dues', 'Dues ageing']]} />
      {tab === 'customers' && <Customers reload={c.reload} />}
      {tab === 'suppliers' && <Procurement {...c} show="suppliers" />}
      {tab === 'purchases' && <CustomerHistory />}
      {tab === 'dues' && <CustomerDues />}
    </div>
  )
}

export function MoneyPage({ tab }: { tab: string }) {
  const map: Record<string, 'money' | 'expenses' | 'owner'> = { cash: 'money', expenses: 'expenses', owner: 'owner' }
  const t = map[tab] ?? 'money'
  return (
    <div>
      <Title title={tr('nav.money')} hint={tr('money.hint')} />
      <Tabs page="money" tab={tab} items={[['cash', tr('money.cash')], ['expenses', tr('money.expenses')], ['owner', tr('money.owner')], ['closing', 'Day closing']]} />
      {tab === 'closing' ? <CashClosing /> : <Finance key={t} only={[t]} />}
    </div>
  )
}

export function MorePage({ tab, onLogout, email }: { tab: string; onLogout: () => void; email: string }) {
  const moreTabs: [string, string][] = [['reports', tr('more.reports')], ['print', tr('more.print')], ['lock', tr('more.lock')], ['audit', tr('hist.title')], ['gst', 'GST'], ['staff', 'Staff'], ['branches', 'Branches'], ['transfers', 'Transfers'], ['orders', 'Orders'], ['reconcile', 'Reconcile'], ['loans', 'Loans'], ['setup', 'Shop setup & help']]
  if (tab === 'setup') return <div><Title title="Shop setup &amp; help" hint="Review the guided steps whenever you need a refresher." /><Tabs page="more" tab="setup" items={moreTabs} /><FirstShopOnboarding /></div>
  if (tab === 'reports') return <div><Title title={tr('more.reports')} hint={tr('more.reportsHint')} /><Tabs page="more" tab="reports" items={moreTabs} /><Finance key="reports" only={['reports']} /></div>
  if (tab === 'print') return <div><Title title={tr('more.print')} /><Tabs page="more" tab="print" items={moreTabs} /><AfterSales reload={() => undefined} show="print" /></div>
  if (tab === 'gst') return <div><Title title="GST" /><Tabs page="more" tab="gst" items={moreTabs} /><GstWorkspace /></div>
  if (tab === 'staff') return <div><Title title="Staff" /><Tabs page="more" tab="staff" items={moreTabs} /><TeamAccess /></div>
  if (tab === 'loans') return <div><Title title="Loans" /><Tabs page="more" tab="loans" items={moreTabs} /><LoansPanel /></div>
      if (tab === 'reconcile') return <div><Title title="Bank reconciliation" /><Tabs page="more" tab="reconcile" items={moreTabs} /><ReconcilePanel /></div>
  if (tab === 'orders') return <div><Title title="Purchase orders" /><Tabs page="more" tab="orders" items={moreTabs} /><OrdersPanel /></div>
  if (tab === 'transfers') return <div><Title title="Stock transfers" /><Tabs page="more" tab="transfers" items={moreTabs} /><LocationMovesPanel /></div>
  if (tab === 'branches') return <div><Title title="Branches" /><Tabs page="more" tab="branches" items={moreTabs} /><BranchesPanel /></div>
  if (tab === 'audit') return <div><Title title={tr('hist.title')} /><Tabs page="more" tab="audit" items={moreTabs} /><AuditHistory /></div>
  if (tab === 'lock') return <div><Title title={tr('more.lock')} hint={tr('more.lockHint')} /><Tabs page="more" tab="lock" items={moreTabs} /><Finance key="lock" only={['lock']} /></div>
  return (
    <div>
      <Title title={tr('more.title')} hint={email} />
      <div className="linklist">
        <a href="#/more/setup"><span className="ico">{'\u{1F4D6}'}</span>Shop setup &amp; help</a>
        <a href="#/more/reports"><span className="ico">{'\u{1F4CA}'}</span>{tr('more.reportsLink')}</a>
        <a href="#/more/print"><span className="ico">{'\u{1F5A8}\uFE0F'}</span>{tr('more.printLink')}</a>
        <a href="#/more/lock"><span className="ico">{'\u{1F512}'}</span>{tr('more.lock')}</a>
        <a href="#/stock/import"><span className="ico">{'\u{1F4E5}'}</span>{tr('more.import')}</a>
        <a href="#/sell/return"><span className="ico">{'\u21A9\uFE0F'}</span>{tr('more.custReturns')}</a>
        <a href="#/buy/return"><span className="ico">{'\u21AA\uFE0F'}</span>{tr('more.returns')}</a>
      </div>
      <p style={{ marginTop: 16 }}><button className="secondary" onClick={onLogout}>{tr('more.signout')}</button></p>
    </div>
  )
}
