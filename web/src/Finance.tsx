import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { exportReport } from './reportExport'

type Money = { id: string; name: string; kind: string; balance: number }
type Opt = { id: string; name: string }
type Tab = 'money' | 'expenses' | 'owner' | 'adjust' | 'reports' | 'lock'
type Report = 'tb' | 'pl' | 'bs' | 'ar' | 'ap' | 'sv' | 'gl' | 'cf'

const today = () => new Date().toISOString().slice(0, 10)
const yearStart = () => new Date().getFullYear() + '-01-01'
const inr = (n: unknown) => Number(n ?? 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

function Row({ l, r, strong }: { l: React.ReactNode; r: React.ReactNode; strong?: boolean }) {
  return <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, padding: '4px 0', fontWeight: strong ? 700 : 400, borderBottom: '1px solid #eee' }}><span>{l}</span><span>{r}</span></div>
}

export function Finance({ only }: { only?: Tab[] }) {
  const [tab, setTab] = useState<Tab>(only?.[0] ?? 'money')
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const [money, setMoney] = useState<Money[]>([])
  const [categories, setCategories] = useState<string[]>([])
  const [products, setProducts] = useState<Opt[]>([])
  const [locations, setLocations] = useState<Opt[]>([])
  const [rows, setRows] = useState<any[]>([])
  const [lock, setLock] = useState<{ lockedThrough: string | null; history: any[] } | null>(null)

  const loadBase = useCallback(async () => {
    try {
      const [m, c, p, l] = await Promise.all([api<Money[]>('GET', '/money-accounts'), api<string[]>('GET', '/expense-categories'), api<Opt[]>('GET', '/products'), api<Opt[]>('GET', '/locations')])
      setMoney(m); setCategories(c); setProducts(p); setLocations(l)
    } catch (e) { setError(msg(e)) }
  }, [])
  useEffect(() => { void loadBase() }, [loadBase])

  const loadTab = useCallback(async (t: Tab) => {
    try {
      if (t === 'expenses') setRows(await api<any[]>('GET', '/expenses'))
      if (t === 'owner') setRows(await api<any[]>('GET', '/owner-transactions'))
      if (t === 'adjust') setRows(await api<any[]>('GET', '/stock-adjustments'))
      if (t === 'lock') setLock(await api('GET', '/period-lock'))
    } catch (e) { setError(msg(e)) }
  }, [])
  useEffect(() => { setError(''); setDone(''); setRows([]); void loadTab(tab) }, [tab, loadTab])

  async function submit(fn: () => Promise<unknown>, ok: string) {
    setError(''); setDone('')
    try { await fn(); setDone(ok); await loadBase(); await loadTab(tab) } catch (e) { setError(msg(e)) }
  }

  const tabs: [Tab, string][] = ([['money', 'Cash & bank'], ['expenses', 'Expenses'], ['owner', 'Capital & drawings'], ['adjust', 'Stock adjustment'], ['reports', 'Reports'], ['lock', 'Period lock']] as [Tab, string][]).filter(([k]) => !only || only.includes(k))
  return (
    <section aria-labelledby="fin-h">
      <h2 id="fin-h">Accounts &amp; reports</h2>
      <div className="tabs" role="tablist" hidden={tabs.length < 2}>
        {tabs.map(([k, n]) => <button key={k} role="tab" aria-selected={tab === k} onClick={() => setTab(k)} style={{ fontWeight: tab === k ? 700 : 400 }}>{n}</button>)}
      </div>
      {error && <p role="alert" className="error">{error}</p>}
      {done && <p role="status">{done}</p>}
      {tab === 'money' && <MoneyTab money={money} submit={submit} />}
      {tab === 'expenses' && <ExpenseTab money={money} categories={categories} rows={rows} submit={submit} />}
      {tab === 'owner' && <OwnerTab money={money} rows={rows} submit={submit} />}
      {tab === 'adjust' && <AdjustTab products={products} locations={locations} rows={rows} submit={submit} />}
      {tab === 'reports' && <Reports setError={setError} />}
      {tab === 'lock' && <LockTab lock={lock} submit={submit} />}
    </section>
  )
}

type Submit = (fn: () => Promise<unknown>, ok: string) => Promise<void>

function MoneyTab({ money, submit }: { money: Money[]; submit: Submit }) {
  const [name, setName] = useState(''); const [kind, setKind] = useState('Bank')
  const [hist, setHist] = useState<{ id: string; name: string; rows: any[] } | null>(null)
  return <div>
    <h3>Accounts</h3>
    {money.map(m => <div key={m.id} style={{ borderBottom: '1px solid #eee', padding: '6px 0' }}>
      <Row l={<>{m.name} <small>({m.kind})</small></>} r={'â‚¹ ' + inr(m.balance)} />
      <button onClick={async () => setHist({ id: m.id, name: m.name, rows: await api<any[]>('GET', `/money-accounts/${m.id}/history`) })}>History</button>
    </div>)}
    <form className="form" onSubmit={e => { e.preventDefault(); void submit(async () => { await api('POST', '/money-accounts', { name, kind }); setName('') }, 'Account added.') }}>
      <label>New account name <input value={name} onChange={e => setName(e.target.value)} maxLength={80} required /></label>
      <label>Type <select value={kind} onChange={e => setKind(e.target.value)}><option>Bank</option><option>Cash</option><option>UPI</option></select></label>
      <button type="submit">Add account</button>
    </form>
    {hist && <div><h3>{hist.name} â€” history</h3>
      {hist.rows.length === 0 && <p>No transactions yet.</p>}
      {hist.rows.map((r, i) => <Row key={i} l={<>{r.date} Â· {r.source}</>} r={<>{Number(r.in) > 0 ? '+' + inr(r.in) : '-' + inr(r.out)} <small>bal {inr(r.balance)}</small></>} />)}
    </div>}
  </div>
}

function ExpenseTab({ money, categories, rows, submit }: { money: Money[]; categories: string[]; rows: any[]; submit: Submit }) {
  const [date, setDate] = useState(today()); const [category, setCategory] = useState('Rent'); const [acc, setAcc] = useState(''); const [amount, setAmount] = useState(''); const [note, setNote] = useState('')
  const a = acc || money[0]?.id || ''
  return <div>
    <form className="form" onSubmit={e => { e.preventDefault(); void submit(async () => { await api('POST', '/expenses', { date, category, moneyAccountId: a, amount: Number(amount), note }, crypto.randomUUID()); setAmount(''); setNote('') }, 'Expense recorded.') }}>
      <label>Date <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} required /></label>
      <label>Category <select value={category} onChange={e => setCategory(e.target.value)}>{categories.map(c => <option key={c}>{c}</option>)}</select></label>
      <label>Paid from <select value={a} onChange={e => setAcc(e.target.value)}>{money.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}</select></label>
      <label>Amount (â‚¹) <input type="number" step="0.01" min="0.01" value={amount} onChange={e => setAmount(e.target.value)} required /></label>
      <label>Note <input value={note} maxLength={200} onChange={e => setNote(e.target.value)} /></label>
      <button type="submit">Record expense</button>
    </form>
    <h3>Recent expenses</h3>
    {rows.length === 0 && <p>No expenses yet.</p>}
    {rows.map(r => <Row key={r.id} l={<>#{r.number} Â· {r.date} Â· {r.category}<br /><small>{r.paidFrom}{r.note ? ' Â· ' + r.note : ''}</small></>} r={'â‚¹ ' + inr(r.amount)} />)}
  </div>
}

function OwnerTab({ money, rows, submit }: { money: Money[]; rows: any[]; submit: Submit }) {
  const [kind, setKind] = useState('Capital'); const [date, setDate] = useState(today()); const [acc, setAcc] = useState(''); const [amount, setAmount] = useState(''); const [note, setNote] = useState('')
  const a = acc || money[0]?.id || ''
  return <div>
    <form className="form" onSubmit={e => { e.preventDefault(); void submit(async () => { await api('POST', '/owner-transactions', { kind, date, moneyAccountId: a, amount: Number(amount), note }, crypto.randomUUID()); setAmount(''); setNote('') }, kind + ' recorded.') }}>
      <label>Type <select value={kind} onChange={e => setKind(e.target.value)}><option value="Capital">Capital introduced</option><option value="Drawing">Owner drawing</option></select></label>
      <label>Date <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} required /></label>
      <label>Account <select value={a} onChange={e => setAcc(e.target.value)}>{money.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}</select></label>
      <label>Amount (â‚¹) <input type="number" step="0.01" min="0.01" value={amount} onChange={e => setAmount(e.target.value)} required /></label>
      <label>Note <input value={note} maxLength={200} onChange={e => setNote(e.target.value)} /></label>
      <button type="submit">Record</button>
    </form>
    <h3>History</h3>
    {rows.length === 0 && <p>Nothing recorded yet.</p>}
    {rows.map(r => <Row key={r.id} l={<>{r.date} Â· {r.kind}<br /><small>{r.account}{r.note ? ' Â· ' + r.note : ''}</small></>} r={'â‚¹ ' + inr(r.amount)} />)}
  </div>
}

function AdjustTab({ products, locations, rows, submit }: { products: Opt[]; locations: Opt[]; rows: any[]; submit: Submit }) {
  const [p, setP] = useState(''); const [l, setL] = useState(''); const [date, setDate] = useState(today()); const [qty, setQty] = useState(''); const [reason, setReason] = useState('Damage'); const [note, setNote] = useState('')
  const direction = reason === 'FoundOnCount' ? 'Increase' : 'Decrease'
  return <div>
    <form className="form" onSubmit={e => { e.preventDefault(); if (!window.confirm('Post this stock adjustment? It changes stock and the books.')) return; void submit(async () => { await api('POST', '/stock-adjustments', { productId: p || products[0]?.id, locationId: l || locations[0]?.id, date, quantity: Number(qty), direction, reason, note }, crypto.randomUUID()); setQty(''); setNote('') }, 'Stock adjusted.') }}>
      <label>Product <select value={p || products[0]?.id || ''} onChange={e => setP(e.target.value)}>{products.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Location <select value={l || locations[0]?.id || ''} onChange={e => setL(e.target.value)}>{locations.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Date <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} required /></label>
      <label>Reason (required) <select value={reason} onChange={e => setReason(e.target.value)}>
        <option value="Damage">Damage</option><option value="Theft">Theft</option><option value="Shortage">Shortage</option><option value="WriteOff">Write-off</option>
        <option value="Expiry">Expiry</option><option value="FoundOnCount">Found on count (increase)</option><option value="OwnerConsumption">Owner consumption</option><option value="Other">Other (note required)</option></select></label>
      <label>Quantity ({direction.toLowerCase()}) <input type="number" step="0.001" min="0.001" value={qty} onChange={e => setQty(e.target.value)} required /></label>
      <label>Note <input value={note} maxLength={200} onChange={e => setNote(e.target.value)} required={reason === 'Other'} /></label>
      <button type="submit">Apply adjustment</button>
    </form>
    <h3>Recent adjustments</h3>
    {rows.length === 0 && <p>No adjustments yet.</p>}
    {rows.map(r => <Row key={r.id} l={<>#{r.number} Â· {r.date} Â· {r.product}<br /><small>{r.reason}{r.note ? ' Â· ' + r.note : ''}</small></>} r={<>{r.quantity > 0 ? '+' : ''}{r.quantity} <small>(â‚¹ {inr(r.value)})</small></>} />)}
  </div>
}

function LockTab({ lock, submit }: { lock: { lockedThrough: string | null; history: any[] } | null; submit: Submit }) {
  const [date, setDate] = useState(''); const [reason, setReason] = useState('')
  return <div>
    <p>Books locked through: <strong>{lock?.lockedThrough ?? 'not locked'}</strong>. Nothing can be posted on or before this date.</p>
    <form className="form" onSubmit={e => { e.preventDefault(); if (!window.confirm('Change the period lock? Entries on or before the locked date cannot be edited.')) return; void submit(() => api('PUT', '/period-lock', { lockedThrough: date || null, reason }), 'Lock updated.') }}>
      <label>Lock through <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} /></label>
      <label>Reason (required to move backward or unlock) <input value={reason} maxLength={200} onChange={e => setReason(e.target.value)} /></label>
      <button type="submit">Save lock</button>
    </form>
    <h3>Lock history</h3>
    {(lock?.history ?? []).map((h, i) => <Row key={i} l={<>{String(h.at).slice(0, 10)} <small>{h.reason}</small></>} r={<>{h.previous || 'â€”'} â†’ {h.value || 'â€”'}</>} />)}
  </div>
}

function Reports({ setError }: { setError: (s: string) => void }) {
  const [rep, setRep] = useState<Report>('tb')
  const [from, setFrom] = useState(yearStart()); const [to, setTo] = useState(today())
  const [raw, setRaw] = useState<{ rep: Report; d: any } | null>(null)
  const data = raw && raw.rep === rep ? raw.d : null
  const [account, setAccount] = useState('')
  const [accounts, setAccounts] = useState<any[]>([])
  const run = useCallback(async () => {
    const setData = (d: any) => setRaw({ rep, d })
    setError(''); setData(null)
    try {
      if (rep === 'tb') setData(await api('GET', `/reports/trial-balance?asOf=${to}`))
      if (rep === 'pl') setData(await api('GET', `/reports/profit-loss?from=${from}&to=${to}`))
      if (rep === 'bs') setData(await api('GET', `/reports/balance-sheet?asOf=${to}`))
      if (rep === 'cf') setData(await api('GET', `/reports/cash-flow?from=${from}&to=${to}`))
      if (rep === 'ar') setData(await api('GET', `/reports/receivable-ageing?asOf=${to}`))
      if (rep === 'ap') setData(await api('GET', `/reports/payable-ageing?asOf=${to}`))
      if (rep === 'sv') setData(await api('GET', '/reports/stock-valuation'))
      if (rep === 'gl') {
        const tb: any = await api('GET', `/reports/trial-balance?asOf=${to}`)
        setAccounts(tb.lines)
        const id = account || tb.lines[0]?.id
        if (id) setData(await api('GET', `/reports/ledger/${id}?from=${from}&to=${to}`))
      }
    } catch (e) { setError(msg(e)) }
  }, [rep, from, to, account, setError])
  useEffect(() => { void run() }, [run])
  const names: [Report, string][] = [['tb', 'Trial Balance'], ['gl', 'General Ledger'], ['pl', 'Profit & Loss'], ['bs', 'Balance Sheet'], ['cf', 'Cash Flow'], ['ar', 'Receivable ageing'], ['ap', 'Payable ageing'], ['sv', 'Stock valuation']]
  const ageing = data && (rep === 'ar' || rep === 'ap')
  return <div>
    <label>Report <select value={rep} onChange={e => setRep(e.target.value as Report)}>{names.map(([k, n]) => <option key={k} value={k}>{n}</option>)}</select></label>
    {(rep === 'pl' || rep === 'gl' || rep === 'cf') && <label> From <input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>}
    {rep !== 'sv' && <label> {rep === 'pl' || rep === 'gl' || rep === 'cf' ? 'To' : 'As of'} <input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>}
    {rep === 'gl' && <label> Account <select value={account || accounts[0]?.id || ''} onChange={e => setAccount(e.target.value)}>{accounts.map(a => <option key={a.id} value={a.id}>{a.code} {a.name}</option>)}</select></label>}
    {data && rep !== 'gl' && <button type="button" onClick={() => { if (!exportReport(rep, data, names.find(n => n[0] === rep)?.[1] ?? rep)) setError('Nothing to export.') }} style={{ marginTop: 16 }}>Download CSV</button>}
    {!data && <p>Loadingâ€¦</p>}
    {data && rep === 'tb' && <div><h3>Trial Balance</h3>
      {data.lines.map((l: any) => <Row key={l.id} l={<>{l.code} {l.name}</>} r={l.debit > 0 ? 'Dr ' + inr(l.debit) : 'Cr ' + inr(l.credit)} />)}
      <Row strong l="Total debit" r={inr(data.totalDebit)} /><Row strong l="Total credit" r={inr(data.totalCredit)} /></div>}
    {data && rep === 'cf' && <div><h3>Cash Flow</h3>
      <Row strong l="Opening cash" r={inr(data.openingCash)} />
      <h4>Operating</h4>
      {data.operating.map((x: any) => <Row key={x.source} l={x.source} r={inr(x.amount)} />)}
      <Row strong l="Net operating" r={inr(data.netOperating)} />
      <h4>Financing (owner capital / drawings)</h4>
      {data.financing.map((x: any) => <Row key={x.source} l={x.source} r={inr(x.amount)} />)}
      <Row strong l="Net financing" r={inr(data.netFinancing)} />
      <Row strong l="Net change" r={inr(data.netChange)} />
      <Row strong l="Closing cash" r={inr(data.closingCash)} /></div>}
    {data && rep === 'gl' && <div><h3>{data.code} {data.name}</h3>
      <Row strong l="Opening" r={inr(data.opening)} />
      {data.lines.map((l: any, i: number) => <Row key={i} l={<>{l.date} Â· {l.source}</>} r={<>{l.debit > 0 ? 'Dr ' + inr(l.debit) : 'Cr ' + inr(l.credit)} <small>bal {inr(l.balance)}</small></>} />)}
      <Row strong l="Closing" r={inr(data.closing)} /></div>}
    {data && rep === 'pl' && <div><h3>Profit &amp; Loss</h3><h4>Income</h4>
      {data.income.map((l: any) => <Row key={l.code} l={l.name} r={inr(l.amount)} />)}<Row strong l="Total income" r={inr(data.totalIncome)} />
      <h4>Expenses</h4>{data.expense.map((l: any) => <Row key={l.code} l={l.name} r={inr(l.amount)} />)}<Row strong l="Total expenses" r={inr(data.totalExpense)} />
      <Row strong l="Gross profit" r={inr(data.grossProfit)} /><Row strong l="Net profit" r={inr(data.netProfit)} /></div>}
    {data && rep === 'bs' && <div><h3>Balance Sheet</h3><h4>Assets</h4>
      {data.assets.map((l: any) => <Row key={l.code} l={l.name} r={inr(l.amount)} />)}<Row strong l="Total assets" r={inr(data.totalAssets)} />
      <h4>Liabilities</h4>{data.liabilities.map((l: any) => <Row key={l.code} l={l.name} r={inr(l.amount)} />)}<Row strong l="Total liabilities" r={inr(data.totalLiabilities)} />
      <h4>Equity</h4>{data.equity.map((l: any) => <Row key={l.code} l={l.name} r={inr(l.amount)} />)}<Row l="Current year result" r={inr(data.currentYearResult)} />
      <Row strong l="Total equity" r={inr(data.totalEquity)} /><p>{data.balanced ? 'Balanced âœ”' : 'Not balanced'}</p></div>}
    {ageing && <div><h3>{rep === 'ar' ? 'Receivable' : 'Payable'} ageing</h3>
      {data.parties.length === 0 && <p>Nothing outstanding.</p>}
      {data.parties.map((p: any) => <div key={p.id} style={{ borderBottom: '1px solid #eee', padding: '6px 0' }}><Row strong l={p.name} r={inr(p.total)} />
        <small>0â€“30: {inr(p.current)} Â· 31â€“60: {inr(p.d31to60)} Â· 61â€“90: {inr(p.d61to90)} Â· 90+: {inr(p.over90)}</small></div>)}
      <Row strong l="Total" r={inr(data.total)} />
      <small>0â€“30: {inr(data.current)} Â· 31â€“60: {inr(data.d31to60)} Â· 61â€“90: {inr(data.d61to90)} Â· 90+: {inr(data.over90)}</small></div>}
    {data && rep === 'sv' && <div><h3>Stock valuation</h3>
      {data.items.map((i: any) => <Row key={i.id} l={<>{i.name}<br /><small>{i.quantity} Ã— â‚¹ {inr(i.wac)}</small></>} r={inr(i.value)} />)}
      <Row strong l="Total stock value" r={inr(data.totalValue)} /></div>}
  </div>
}

type Loan = { id: string; number: number; lender: string; principal: number; startDate: string; interestRate: number; principalPaid: number; interestPaid: number; outstanding: number }

/** A9: record loans taken, and repayments split into principal and interest. */
export function LoansPanel() {
  const [accounts, setAccounts] = useState<Money[]>([])
  const [loans, setLoans] = useState<Loan[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [lender, setLender] = useState('')
  const [amount, setAmount] = useState('')
  const [rate, setRate] = useState('0')
  const [date, setDate] = useState(today())
  const [account, setAccount] = useState('')
  const [payLoan, setPayLoan] = useState('')
  const [principal, setPrincipal] = useState('')
  const [interest, setInterest] = useState('')

  const load = useCallback(async () => {
    try {
      const [a, l] = await Promise.all([api<Money[]>('GET', '/money-accounts'), api<Loan[]>('GET', '/loans')])
      setAccounts(a); setLoans(l); setError('')
    } catch (e) { setError(msg(e)) }
  }, [])
  useEffect(() => { void load() }, [load])

  const acct = account || accounts[0]?.id || ''
  const run = async (fn: () => Promise<unknown>, done: string) => {
    try { await fn(); setNotice(done); setError(''); await load() } catch (e) { setNotice(''); setError(msg(e)) }
  }

  return (
    <section aria-label="Loans">
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      <form className="form" onSubmit={e => { e.preventDefault(); void run(async () => { await api('POST', '/loans', { lender, date, moneyAccountId: acct, principal: Number(amount), interestRate: Number(rate), note: '' }, crypto.randomUUID()); setLender(''); setAmount('') }, 'Loan recorded.') }}>
        <h3>New loan received</h3>
        <label>Lender<input value={lender} onChange={e => setLender(e.target.value)} maxLength={100} required /></label>
        <label>Amount<input inputMode="decimal" value={amount} onChange={e => setAmount(e.target.value)} required /></label>
        <label>Interest rate % per year<input inputMode="decimal" value={rate} onChange={e => setRate(e.target.value)} /></label>
        <label>Date<input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} /></label>
        <label>Received into<select value={acct} onChange={e => setAccount(e.target.value)}>{accounts.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
        <button type="submit">Save loan</button>
      </form>
      <form className="form" onSubmit={e => { e.preventDefault(); void run(async () => { await api('POST', `/loans/${payLoan || loans[0]?.id}/repayments`, { date, moneyAccountId: acct, principal: Number(principal || 0), interest: Number(interest || 0), note: '' }, crypto.randomUUID()); setPrincipal(''); setInterest('') }, 'Repayment recorded.') }}>
        <h3>Record repayment</h3>
        <label>Loan<select value={payLoan || loans[0]?.id || ''} onChange={e => setPayLoan(e.target.value)}>{loans.map(l => <option key={l.id} value={l.id}>{l.lender} - outstanding {inr(l.outstanding)}</option>)}</select></label>
        <label>Principal<input inputMode="decimal" value={principal} onChange={e => setPrincipal(e.target.value)} /></label>
        <label>Interest<input inputMode="decimal" value={interest} onChange={e => setInterest(e.target.value)} /></label>
        <button type="submit" disabled={loans.length === 0}>Save repayment</button>
      </form>
      <ul>
        {loans.map(l => <li key={l.id}>{l.startDate} - {l.lender} - borrowed {inr(l.principal)} at {l.interestRate}% - repaid {inr(l.principalPaid)} (interest paid {inr(l.interestPaid)}) - outstanding {inr(l.outstanding)}</li>)}
      </ul>
    </section>
  )
}

type StockCheck = { ok: boolean; items: { productId: string; name: string; costQuantity: number; costValue: number; movementQuantity: number; movementValue: number; balanceQuantity: number }[] }

/** A12: replays stock movements and flags products whose stored cost or quantity disagrees. Read-only. */
export function StockVerifyPanel() {
  const [res, setRes] = useState<StockCheck | null>(null)
  const [error, setError] = useState('')
  const check = async () => { try { setRes(await api<StockCheck>('GET', '/reports/stock-verification')); setError('') } catch (e) { setError(msg(e)) } }
  return (
    <section aria-label="Stock verification">
      <h3>Stock cost check</h3>
      <button type="button" onClick={() => void check()}>Verify stock values</button>
      {error && <p role="alert">{error}</p>}
      {res?.ok && <p role="status">All products match their stock movements.</p>}
      {res && !res.ok && <ul>{res.items.map(i => <li key={i.productId}>{i.name}: stored {i.costQuantity} / {inr(i.costValue)}, movements {i.movementQuantity} / {inr(i.movementValue)}, locations {i.balanceQuantity}</li>)}</ul>}
    </section>
  )
}

type Acct = { id: string; code: string; name: string; type: string; custom: boolean; balance: number }

/** A10: chart of accounts. System accounts are fixed; owner-created accounts can be added and renamed. */
export function AccountsPanel() {
  const [accts, setAccts] = useState<Acct[]>([])
  const [error, setError] = useState('')
  const [name, setName] = useState('')
  const [type, setType] = useState('Expense')
  const [editing, setEditing] = useState('')
  const [newName, setNewName] = useState('')

  const load = useCallback(async () => {
    try { setAccts(await api<Acct[]>('GET', '/accounts')); setError('') } catch (e) { setError(msg(e)) }
  }, [])
  useEffect(() => { void load() }, [load])

  const run = async (fn: () => Promise<unknown>) => {
    try { await fn(); setError(''); await load() } catch (e) { setError(msg(e)) }
  }

  return (
    <section aria-label="Chart of accounts">
      {error && <p role="alert">{error}</p>}
      <form className="form" onSubmit={e => { e.preventDefault(); void run(async () => { await api('POST', '/accounts', { name, type }); setName('') }) }}>
        <h3>New account</h3>
        <label>Name<input value={name} onChange={e => setName(e.target.value)} maxLength={80} required /></label>
        <label>Type<select value={type} onChange={e => setType(e.target.value)}>{['Asset', 'Liability', 'Income', 'Expense'].map(t => <option key={t}>{t}</option>)}</select></label>
        <button type="submit">Add account</button>
      </form>
      <ul>
        {accts.map(a => (
          <li key={a.id}>
            {a.code} - {editing === a.id
              ? <><input aria-label="New name" value={newName} onChange={e => setNewName(e.target.value)} maxLength={80} /> <button type="button" onClick={() => void run(async () => { await api('PUT', `/accounts/${a.id}`, { name: newName, type: a.type }); setEditing('') })}>Save</button></>
              : a.name} ({a.type}) - {inr(a.balance)}
            {a.custom && editing !== a.id && <button type="button" onClick={() => { setEditing(a.id); setNewName(a.name) }}>Rename</button>}
          </li>
        ))}
      </ul>
    </section>
  )
}

type ReconLine = { id: string; date: string; source: string; amount: number; cleared: boolean }
type Recon = { lines: ReconLine[]; bookBalance: number; clearedBalance: number; truncated: boolean }

/** A7: tick ledger lines that appear on the bank statement and compare the cleared balance to the statement balance. */
export function ReconcilePanel() {
  const [accounts, setAccounts] = useState<Money[]>([])
  const [account, setAccount] = useState('')
  const [data, setData] = useState<Recon | null>(null)
  const [statement, setStatement] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    api<Money[]>('GET', '/money-accounts').then(a => setAccounts(a.filter(x => x.kind !== 'Cash'))).catch(e => setError(e instanceof Error ? e.message : 'Could not load accounts'))
  }, [])

  const load = useCallback(async (id: string) => {
    if (!id) { setData(null); return }
    try { setData(await api<Recon>('GET', `/reconciliation?moneyAccountId=${id}`)); setError('') }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not load ledger lines') }
  }, [])
  useEffect(() => { void load(account) }, [account, load])

  const toggle = async (l: ReconLine) => {
    try { await api('POST', '/reconciliation/clear', { moneyAccountId: account, lineIds: [l.id], cleared: !l.cleared }); await load(account) }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not update line') }
  }

  const diff = data && statement !== '' ? Number(statement) - data.clearedBalance : null
  return (
    <section aria-label="Bank reconciliation">
      {error && <p role="alert">{error}</p>}
      <label>Account<select value={account} onChange={e => setAccount(e.target.value)}><option value="">Select</option>{accounts.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
      {data && (
        <>
          <Row strong l="Book balance" r={inr(data.bookBalance)} />
          <Row strong l="Cleared balance" r={inr(data.clearedBalance)} />
          <label>Bank statement balance<input inputMode="decimal" value={statement} onChange={e => setStatement(e.target.value)} /></label>
          {diff !== null && <p>{Math.abs(diff) < 0.005 ? 'Reconciled: cleared balance matches the statement.' : `Difference: ${inr(diff)}`}</p>}
          {data.truncated && <p>Showing the latest 500 lines.</p>}
          <ul>
            {data.lines.map(l => (
              <li key={l.id}><label><input type="checkbox" checked={l.cleared} onChange={() => toggle(l)} /> {l.date} - {l.source} - {inr(l.amount)}</label></li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

