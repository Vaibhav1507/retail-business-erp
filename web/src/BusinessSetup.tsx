import { useEffect, useState } from 'react'
import { api, otpEnabled, resetCsrf } from './api'
import { t } from './i18n'

export type Profile = {
  businessName: string; businessCategory: string; ownerName: string; mobile: string; address: string; city: string; state: string
  pinCode: string; gstStatus: string; gstin: string; financialYearStartMonth: number; language: string; mobileVerification?: string
}

export const CATEGORIES: [string, string, string?][] = [
  ['electronics_mobile', 'Electronics and mobile'],
  ['electrical_hardware', 'Electrical and hardware'],
  ['general_retail', 'General retail'],
  ['garments_apparel', 'Garments and apparel'],
  ['automobile_spares', 'Automobile and spare parts'],
  ['sports_fitness', 'Sports and fitness'],
  ['pharmacy_medical', 'Pharmacy or medical', 'needs extra setup later'],
  ['other_retail', 'Other retail'],
]
export const categoryLabel = (k: string | null | undefined) => CATEGORIES.find(c => c[0] === k)?.[1] ?? 'Not set'

const GST: [string, string][] = [['NotRegistered', 'Not registered'], ['Regular', 'Regular GST'], ['Composition', 'Composition scheme'], ['Undecided', 'Not sure, decide later']]
const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const STATES = ['Andhra Pradesh', 'Arunachal Pradesh', 'Assam', 'Bihar', 'Chhattisgarh', 'Delhi', 'Goa', 'Gujarat', 'Haryana', 'Himachal Pradesh', 'Jharkhand', 'Karnataka', 'Kerala', 'Madhya Pradesh', 'Maharashtra', 'Manipur', 'Meghalaya', 'Mizoram', 'Nagaland', 'Odisha', 'Punjab', 'Rajasthan', 'Sikkim', 'Tamil Nadu', 'Telangana', 'Tripura', 'Uttar Pradesh', 'Uttarakhand', 'West Bengal', 'Jammu and Kashmir', 'Ladakh', 'Chandigarh', 'Puducherry', 'Other']

export const emptyProfile: Profile = { businessName: '', businessCategory: '', ownerName: '', mobile: '', address: '', city: '', state: '', pinCode: '', gstStatus: '', gstin: '', financialYearStartMonth: 4, language: 'en' }

const needsGstin = (s: string) => s === 'Regular' || s === 'Composition'
const GSTIN_RE = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][A-Z0-9]Z[A-Z0-9]$/

function stepError(step: number, p: Profile): string {
  if (step === 0) {
    if (!p.businessName.trim()) return 'Enter your business name.'
    if (!p.businessCategory) return 'Choose a business category.'
    if (!p.ownerName.trim()) return 'Enter the owner or contact name.'
  }
  if (step === 1) {
    if (!p.address.trim()) return 'Enter the business address.'
    if (!p.city.trim()) return 'Enter the city or town.'
    if (!p.state) return 'Choose the state.'
    if (!/^[1-9][0-9]{5}$/.test(p.pinCode.trim())) return 'Enter a 6-digit PIN code.'
    if (!p.gstStatus) return 'Choose a GST registration status.'
    if (needsGstin(p.gstStatus) && !GSTIN_RE.test(p.gstin.trim().toUpperCase())) return 'Enter a valid 15-character GSTIN.'
  }
  return ''
}

const STEPS = ['Business', 'Address and tax', 'Preferences']
const SIGNUP_STEP_TITLES = ['Your account', 'Your shop', 'Shop address', 'Preferences', 'Review']
const MOBILE_RE = /^[6-9][0-9]{9}$/

function MobileVerify({ signup, mobile, setMobile, verified, onVerified }: { signup: boolean; mobile: string; setMobile: (m: string) => void; verified: boolean; onVerified: (m: string, proof?: string) => void }) {
  const [code, setCode] = useState('')
  const [sent, setSent] = useState(false)
  const [dev, setDev] = useState('')
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)
  async function send() {
    setErr('')
    if (!MOBILE_RE.test(mobile)) { setErr('Enter a 10-digit mobile number.'); return }
    setBusy(true)
    try {
      const r = await api<{ devCode?: string }>('POST', signup ? '/auth/otp/send' : '/profile/mobile/send-otp', signup ? { mobile, purpose: 'signup' } : { mobile })
      setSent(true); setDev(r?.devCode ?? '')
    } catch (e) { setErr(e instanceof Error ? e.message : 'Could not send the code') } finally { setBusy(false) }
  }
  async function verify() {
    setErr(''); setBusy(true)
    try {
      if (signup) { const r = await api<{ proof: string }>('POST', '/auth/otp/verify', { mobile, code }); onVerified(mobile, r.proof) }
      else { await api('POST', '/profile/mobile/verify', { mobile, code }); onVerified(mobile) }
      setSent(false); setCode('')
    } catch (e) { setErr(e instanceof Error ? e.message : 'Could not verify the code') } finally { setBusy(false) }
  }
  return (
    <div className="wide mobile-verify">
      <label>Mobile number *<input inputMode="numeric" value={mobile} onChange={e => { setMobile(e.target.value.replace(/\D/g, '').slice(0, 10)); setSent(false) }} placeholder="10-digit number" autoComplete="tel-national" /></label>
      {verified ? <p className="hint">Mobile number verified.</p> : <>
        {!sent && <button type="button" className="secondary" onClick={send} disabled={busy}>Send code</button>}
        {sent && <>
          <label>Enter the 6-digit code<input inputMode="numeric" value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} autoComplete="one-time-code" /></label>
          {dev && <p className="hint">Development code: {dev}</p>}
          <button type="button" onClick={verify} disabled={busy || code.length !== 6}>Verify code</button>
          <button type="button" className="secondary" onClick={send} disabled={busy}>Resend code</button>
        </>}
      </>}
      {err && <p role="alert">{err}</p>}
    </div>
  )
}

export function BusinessSetup({ initial, hasBusiness, onDone, onCancel, editing, signup }: { initial?: Partial<Profile>; hasBusiness: boolean; onDone: () => void; onCancel?: () => void; editing?: boolean; signup?: boolean }) {
  const [p, setP] = useState<Profile>({ ...emptyProfile, ...initial })
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [verifiedMobile, setVerifiedMobile] = useState(initial?.mobileVerification === 'Verified' ? initial.mobile ?? '' : '')
  const [proof, setProof] = useState('')
  const off = signup ? 1 : 0
  const locked = !!editing
  const [otp, setOtp] = useState(false)
  useEffect(() => { otpEnabled().then(setOtp) }, [])
  const mobileOk = otp ? !!verifiedMobile && p.mobile === verifiedMobile && (!signup || !!proof) : MOBILE_RE.test(p.mobile)
  const [step, setStep] = useState(0)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [key] = useState(() => crypto.randomUUID())
  const set = (patch: Partial<Profile>) => setP(prev => ({ ...prev, ...patch }))
  const review = step === 3 + off
  const passwordError = password.length < 12 ? 'Password must be at least 12 characters.' : !/\d/.test(password) ? 'Password must include at least one number.' : ''
  const signupProgress = Math.round(((step + 1) / 5) * 100)

  function next() {
    let e = ''
    if (signup && step === 0) {
      if (!email.trim() || !email.includes('@')) e = 'Enter a valid email address.'
      else if (passwordError) e = passwordError
      else if (!mobileOk) e = otp ? 'Verify your mobile number with the code we send you.' : 'Enter a valid 10-digit mobile number.'
    } else e = stepError(step - off, p)
    if (e) { setError(e); return }
    setError(''); setStep(step + 1)
  }
  async function save() {
    setError(''); setBusy(true)
    try {
      if (signup) {
        await api('POST', '/auth/register', { email: email.trim(), password, mobile: p.mobile, otpProof: otp ? proof : undefined })
        await api('POST', '/auth/login', { email: email.trim(), password })
        resetCsrf()
      }
      if (!hasBusiness) await api('POST', '/bootstrap', { businessName: p.businessName.trim(), branchName: 'Main Branch', locationName: 'Main Store' }, key)
      await api('PUT', '/business-profile', { ...p, gstin: needsGstin(p.gstStatus) ? p.gstin.trim().toUpperCase() : null })
      onDone()
    } catch (err) { setError(err instanceof Error ? err.message : 'Something went wrong') } finally { setBusy(false) }
  }

  return (
    <div className={signup ? 'setup auth-signup-flow' : 'setup'}>
      {signup && onCancel && (
        <button type="button" className="auth-back-link" onClick={onCancel}>← Back to sign in</button>
      )}
      <h2>{editing ? 'Business profile' : signup ? 'Set up your shop' : 'Set up your business'}</h2>
      {!editing && <p className="hint">{signup ? 'A few details to get your business ready. You can review everything before creating your account.' : 'This takes about a minute and is asked only once. Fields marked * are required.'}</p>}
      {locked && <p className="hint">Business name, category and owner cannot be changed after setup. The mobile number can be changed after verifying the new number with a code.</p>}

      {signup ? (
        <section className="auth-signup-progress" aria-label="Signup progress">
          <div className="auth-signup-progress-head">
            <span>STEP {step + 1} OF 5</span>
            <span>{signupProgress}% complete</span>
          </div>
          <div
            className="auth-signup-progress-track"
            role="progressbar"
            aria-label="Account setup progress"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={signupProgress}
            aria-valuetext={`Step ${step + 1} of 5`}
          >
            <div className="auth-signup-progress-fill" style={{ width: `${signupProgress}%` }} />
          </div>
          <p className="auth-signup-step-title">{SIGNUP_STEP_TITLES[step]}</p>
        </section>
      ) : (
        <ol className="progress" aria-label="Progress">
          {STEPS.concat('Review').map((s, i) => (
            <li key={s} aria-current={i === step ? 'step' : undefined} className={i < step ? 'done' : ''}><span>{i + 1}</span>{s}</li>
          ))}
        </ol>
      )}

      <form className={signup ? 'auth-signup-form' : undefined} onSubmit={e => { e.preventDefault(); review ? save() : next() }} aria-label="Business profile">
        {signup && step === 0 && <>
          <label className="wide">Email *<input type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" /></label>
          <label className="wide">Password * (at least 12 characters, including a number)<input type="password" value={password} onChange={e => setPassword(e.target.value)} minLength={12} autoComplete="new-password" aria-invalid={!!password && !!passwordError} /></label>
          {password && passwordError && <p className="error" role="alert">{passwordError}</p>}
          {otp ? <MobileVerify signup mobile={p.mobile} setMobile={m => set({ mobile: m })} verified={mobileOk} onVerified={(m, pr) => { setVerifiedMobile(m); if (pr) setProof(pr) }} />
            : <label className="wide">Mobile number *<input inputMode="numeric" value={p.mobile} onChange={e => set({ mobile: e.target.value.replace(/\D/g, '').slice(0, 10) })} placeholder="10-digit number" autoComplete="tel-national" /></label>}
        </>}
        {step === off && <>
          <label>Business name *<input disabled={locked} value={p.businessName} onChange={e => set({ businessName: e.target.value })} maxLength={120} placeholder="e.g. Sharma Mobile Store" /></label>
          <label>Business category *
            <select disabled={locked} value={p.businessCategory} onChange={e => set({ businessCategory: e.target.value })}>
              <option value="">Choose one</option>
              {CATEGORIES.map(([k, n, note]) => <option key={k} value={k}>{n}{note ? ` (${note})` : ''}</option>)}
            </select>
          </label>
          {p.businessCategory === 'pharmacy_medical' && <p className="notice">Pharmacy and medical shops need additional setup and rules that are not available yet. You can still save this category.</p>}
          <label>Owner or contact name *<input disabled={locked} value={p.ownerName} onChange={e => set({ ownerName: e.target.value })} maxLength={120} placeholder="e.g. Ramesh Sharma" /></label>
          {!signup && otp && <MobileVerify signup={false} mobile={p.mobile} setMobile={m => set({ mobile: m })} verified={mobileOk} onVerified={m => setVerifiedMobile(m)} />}
        </>}
        {step === 1 + off && <>
          <label className="wide">Business address *<input value={p.address} onChange={e => set({ address: e.target.value })} maxLength={250} placeholder="Shop number, street, area" /></label>
          <label>City or town *<input value={p.city} onChange={e => set({ city: e.target.value })} maxLength={80} /></label>
          <label>State *
            <select value={p.state} onChange={e => set({ state: e.target.value })}><option value="">Choose state</option>{STATES.map(s => <option key={s}>{s}</option>)}</select>
          </label>
          <label>PIN code *<input inputMode="numeric" value={p.pinCode} onChange={e => set({ pinCode: e.target.value.replace(/\D/g, '').slice(0, 6) })} placeholder="6 digits" /></label>
          <label>GST registration *
            <select value={p.gstStatus} onChange={e => set({ gstStatus: e.target.value })}><option value="">Choose one</option>{GST.map(([k, n]) => <option key={k} value={k}>{n}</option>)}</select>
          </label>
          <p className="hint">{t('onboarding.gstHelp')}</p>
          {needsGstin(p.gstStatus) && <label>GSTIN *<input value={p.gstin} onChange={e => set({ gstin: e.target.value.toUpperCase().slice(0, 15) })} placeholder="e.g. 29ABCDE1234F1Z5" /></label>}
        </>}
        {step === 2 + off && <>
          <label>Financial year starts in
            <select value={p.financialYearStartMonth} onChange={e => set({ financialYearStartMonth: Number(e.target.value) })}>{MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}</select>
          </label>
          <label>Language<select value={p.language} onChange={e => set({ language: e.target.value })}><option value="en">English</option></select></label>
          <label>Currency<input value="Indian Rupee (INR)" disabled readOnly /></label>
        </>}
        {review && <>
          <dl className="review"><dt>Business</dt><dd>{p.businessName}</dd><dt>Category</dt><dd>{categoryLabel(p.businessCategory)}</dd>
            {otp ? ` (${mobileOk ? 'verified' : 'not verified'})` : ''}
            <dt>Address</dt><dd>{p.address}, {p.city}, {p.state} {p.pinCode}</dd>
            <dt>GST</dt><dd>{GST.find(g => g[0] === p.gstStatus)?.[1]}{needsGstin(p.gstStatus) ? `, ${p.gstin}` : ''}</dd>
            <dt>Financial year</dt><dd>Starts in {MONTHS[p.financialYearStartMonth - 1]}</dd><dt>Language and currency</dt><dd>English, INR</dd>
          </dl>
          <aside className="billing-card setup-invoice-preview" aria-label="Sample invoice preview"><strong>{t('onboarding.sampleInvoiceTitle')}</strong>
            <p>{p.businessName || 'Your shop name'} · {p.city || 'Your city'}</p>
            <div className="table-scroll"><table><thead><tr><th>Item</th><th>Qty</th><th>Amount</th></tr></thead><tbody><tr><td>Sample product</td><td>1</td><td>₹100.00</td></tr></tbody></table></div>
            <p><strong>Total: ₹100.00</strong></p>
            <p className="hint">{p.gstStatus === 'Regular' || p.gstStatus === 'Composition' ? t('onboarding.sampleInvoiceRegistered') : p.gstStatus === 'NotRegistered' ? t('onboarding.sampleInvoiceNotRegistered') : t('onboarding.sampleInvoicePending')}</p>
          </aside>
        </>}
        {error && <p role="alert" className="wide">{error}</p>}
        <div className="step-actions">
          {step > 0 && <button type="button" className="secondary" onClick={() => { setError(''); setStep(step - 1) }}>Back</button>}
          {step === 0 && onCancel && <button type="button" className="secondary" onClick={onCancel}>Cancel</button>}
          <button type="submit" disabled={busy}>{review ? (busy ? 'Saving...' : editing ? 'Save changes' : signup ? 'Create account' : 'Finish setup') : signup ? (step === 2 + off ? 'Review' : 'Continue') : step === 2 + off ? 'Review' : 'Next'}</button>
        </div>
      </form>
    </div>
  )
}
