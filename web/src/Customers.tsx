import { t as tr } from './i18n'
import { Paged } from './Paged'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import './billing-forms.css'

type Customer = { id: string; name: string; phone: string | null; notes: string | null; isActive: boolean; owed: number; whatsappConsentStatus: 'Unknown' | 'OptedIn' | 'OptedOut' }

const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

export function Customers({ reload }: { reload: () => void }) {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const [cname, setCname] = useState('')
  const [cphone, setCphone] = useState(''); const [cnotes, setCnotes] = useState(''); const [consent, setConsent] = useState<'Unknown' | 'OptedIn' | 'OptedOut'>('Unknown'); const [custEditId, setCustEditId] = useState<string | null>(null)
  const [custSearch, setCustSearch] = useState('')
  const [loaded, setLoaded] = useState(false)
  const loadCustomers = useCallback(async () => { setCustomers(await api<Customer[]>('GET', '/customers' + (custSearch.trim() ? '?q=' + encodeURIComponent(custSearch.trim()) : ''))); setLoaded(true) }, [custSearch])
  useEffect(() => { loadCustomers().catch(e => setError(msg(e))) }, [loadCustomers])
  const run = async (fn: () => Promise<string>) => {
    setError(''); setDone('')
    try { setDone(await fn()); await loadCustomers(); reload() } catch (e) { setError(msg(e)) }
  }
  const addCustomer = () => run(async () => { const body = { name: cname, phone: cphone || null, notes: cnotes || null, isActive: customers.find(c => c.id === custEditId)?.isActive ?? true, whatsappConsentStatus: consent }; const was = custEditId; if (was) await api('PUT', '/customers/' + was, body); else await api('POST', '/customers', body); setCname(''); setCphone(''); setCnotes(''); setConsent('Unknown'); setCustEditId(null); return was ? 'Customer updated.' : 'Customer added.' })
  return (
    <section aria-label="Customers" className="billing-screen">
      {done && <p role="status">{done}</p>}
      {error && <p role="alert">{error}</p>}
      <h3>Customers</h3>
      <div>
        <label>Customer name <input value={cname} onChange={e => setCname(e.target.value)} /></label>
        <label>Phone <input value={cphone} onChange={e => setCphone(e.target.value)} /></label>
        <label>Notes <input value={cnotes} onChange={e => setCnotes(e.target.value)} /></label>
        <label>WhatsApp invoice permission <select value={consent} onChange={e => setConsent(e.target.value as 'Unknown' | 'OptedIn' | 'OptedOut')}><option value="Unknown">Not confirmed</option><option value="OptedIn">Customer agreed to receive invoices</option><option value="OptedOut">Customer opted out</option></select></label>
        <p className="billing-hint">Choose agreed only after the customer has given permission. This setting is not asked at every checkout.</p>
		<button type="button" onClick={addCustomer} disabled={!cname.trim()}>{custEditId ? 'Save changes' : 'Add customer'}</button>
		{custEditId && <button type="button" className="secondary" onClick={() => { setCustEditId(null); setCname(''); setCphone(''); setCnotes('') }}>Cancel</button>}
      </div>
      <label>Search customers <input value={custSearch} onChange={e => setCustSearch(e.target.value)} /></label>
      {!loaded && !error && <p role="status">{tr('app.loading')}</p>}`r`n      <Paged rows={customers} empty={loaded ? tr('cust.none') : undefined}>{page => (
      <table aria-label="Customers">
        <thead><tr><th>Customer</th><th>Phone</th><th>Notes</th><th>Outstanding (INR)</th><th>Actions</th></tr></thead>
        <tbody>{page.map(c => <tr key={c.id}><td>{c.name}</td><td>{c.phone}</td><td>{c.notes}</td><td>{c.owed.toFixed(2)}</td><td><button type="button" className="secondary" onClick={() => { setCustEditId(c.id); setCname(c.name); setCphone(c.phone ?? ''); setCnotes(c.notes ?? ''); setConsent(c.whatsappConsentStatus ?? 'Unknown'); window.scrollTo({ top: 0, behavior: 'smooth' }) }}>Edit</button></td></tr>)}</tbody>
      </table>
      )}</Paged>
    </section>
  )
}
