import { useEffect, useState } from 'react'
import { api, otpEnabled, resetCsrf } from './api'
import { BusinessSetup } from './BusinessSetup'
import { passkeyEnabled, preparePasskeySignIn, signInWithPasskey } from './passkeys'

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
  const [passkeyOptions, setPasskeyOptions] = useState<PublicKeyCredentialRequestOptions | null>(null)
  useEffect(() => {
    otpEnabled().then(setOtp)
    passkeyEnabled().then(async enabled => {
      setPasskeyAvailable(enabled)
      if (enabled) {
        try { setPasskeyOptions(await preparePasskeySignIn()) }
        catch { setPasskeyAvailable(false) }
      }
    })
  }, [])

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
      if (!passkeyOptions) throw new Error('Passkey sign-in is still preparing. Please try again in a moment.')
      await signInWithPasskey(passkeyOptions)
      resetCsrf()
      onDone()
    } catch (err) {
      if (err instanceof DOMException && err.name === 'NotAllowedError') {
        setError('No passkey was selected. If this is your first sign-in on this phone, sign in with your password first, then register a passkey under More → Devices.')
      } else {
        setError(err instanceof Error ? err.message : 'Passkey sign-in failed')
      }
      try { setPasskeyOptions(await preparePasskeySignIn()) } catch { setPasskeyOptions(null) }
    }
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
		{passkeyAvailable && <button type="button" className="secondary wide" disabled={busy || !passkeyOptions} onClick={() => { void passkeySubmit() }}>{passkeyOptions ? 'Sign in with passkey' : 'Preparing passkey sign-in…'}</button>}
		<button type="button" className="secondary wide" onClick={() => { setRegister(true); setError('') }}>Create an account</button>
		{error && <p role="alert" className="wide">{error}</p>}
	  </form>
	</main>
  )
}
