import { useEffect, useState } from 'react'
import { api, otpEnabled, resetCsrf } from './api'
import { BusinessSetup } from './BusinessSetup'
import { passkeyEnabled, signInWithPasskey } from './passkeys'

const MOBILE_RE = /^[6-9][0-9]{9}$/

export function AccountAccess({ onDone }: { onDone: () => void }) {
  const [register, setRegister] = useState(false)
  const [id, setId] = useState('')
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')
  const [sent, setSent] = useState(false)
  const [dev, setDev] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [otp, setOtp] = useState(false)
  const [passkeyAvailable, setPasskeyAvailable] = useState(false)
  useEffect(() => { otpEnabled().then(setOtp); passkeyEnabled().then(setPasskeyAvailable) }, [])

  const value = id.trim()
  const isMobile = otp && /^[0-9]+$/.test(value)
  const isEmail = value.includes('@')

  function change(v: string) { setId(v); setSent(false); setCode(''); setError('') }

  async function submit(e: React.FormEvent) {
	e.preventDefault()
	setError('')
	if (!isEmail && !isMobile) { setError(otp ? 'Enter your 10-digit mobile number or your email.' : 'Enter your email address.'); return }
	if (isMobile && !MOBILE_RE.test(value)) { setError('Enter a valid 10-digit mobile number.'); return }
	setBusy(true)
	try {
	  if (isEmail) {
		await api('POST', '/auth/login', { email: value, password })
	  } else if (!sent) {
		const r = await api<{ devCode?: string }>('POST', '/auth/otp/send', { mobile: value, purpose: 'login' })
		setSent(true); setDev(r?.devCode ?? '')
		return
	  } else {
		await api('POST', '/auth/login/otp', { mobile: value, code })
	  }
	  resetCsrf()
	  onDone()
	} catch (err) { setError(err instanceof Error ? err.message : 'Something went wrong') } finally { setBusy(false) }
  }

  async function passkeySubmit() {
    setError('')
    setBusy(true)
    try {
      await signInWithPasskey()
      resetCsrf()
      onDone()
    } catch (err) { setError(err instanceof Error ? err.message : 'Passkey sign-in failed') }
    finally { setBusy(false) }
  }

  if (register) return (
	<main className="access">
	  <h1>Retail Business OS</h1>
	  <BusinessSetup signup hasBusiness={false} onDone={() => { resetCsrf(); onDone() }} onCancel={() => { setRegister(false); setError('') }} />
	</main>
  )

  return (
	<main className="access">
	  <h1>Retail Business OS</h1>
	  <p className="hint">Sign in to your business.</p>
	  <form onSubmit={submit} aria-label="Sign in">
		<h2>Sign in</h2>
		<label className="wide">{otp ? 'Mobile number or email' : 'Email'}<input value={id} onChange={e => change(e.target.value)} required autoComplete="username" placeholder={otp ? '10-digit mobile or email' : 'you@example.com'} /></label>
		{(isEmail || !otp) && <label className="wide">Password<input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" /></label>}
		{isMobile && sent && <>
		  <label className="wide">Enter the 6-digit code sent to {value}<input inputMode="numeric" value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} required autoComplete="one-time-code" /></label>
		  {dev && <p className="hint wide">Development code: {dev}</p>}
		</>}
		<button type="submit" className="wide" disabled={busy}>{isMobile ? (sent ? 'Verify and sign in' : 'Send code') : 'Sign in'}</button>
		{passkeyAvailable && <button type="button" className="secondary wide" disabled={busy} onClick={() => { void passkeySubmit() }}>Sign in with passkey</button>}
		<button type="button" className="secondary wide" onClick={() => { setRegister(true); setError('') }}>Create an account</button>
		{error && <p role="alert" className="wide">{error}</p>}
	  </form>
	</main>
  )
}
