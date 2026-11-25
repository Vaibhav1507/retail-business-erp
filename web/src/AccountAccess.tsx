import { useEffect, useState } from 'react'
import { api, otpEnabled, resetCsrf } from './api'
import { BusinessSetup } from './BusinessSetup'
import { AuthBrand } from './AuthBrand'
import { passkeyEnabled, preparePasskeySignIn, signInWithPasskey } from './passkeys'

const MOBILE_RE = /^[6-9][0-9]{9}$/

export function AccountAccess({ onDone }: { onDone: () => void }) {
  const [register, setRegister] = useState(false)
  const [id, setId] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [code, setCode] = useState('')
  const [sent, setSent] = useState(false)
  const [dev, setDev] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [otp, setOtp] = useState(false)
  const [passkeyAvailable, setPasskeyAvailable] = useState(false)
  const [passkeyOptions, setPasskeyOptions] = useState<PublicKeyCredentialRequestOptions | null>(null)

  useEffect(() => {
    let active = true
    otpEnabled().then(enabled => { if (active) setOtp(enabled) })
    passkeyEnabled().then(async enabled => {
      if (!active) return
      setPasskeyAvailable(enabled)
      if (enabled) {
        try {
          const options = await preparePasskeySignIn()
          if (active) setPasskeyOptions(options)
        } catch {
          if (active) setPasskeyAvailable(false)
        }
      }
    })
    return () => { active = false }
  }, [])

  const value = id.trim()
  const isMobile = otp && /^[0-9]+$/.test(value)
  const isEmail = value.includes('@')

  function change(v: string) {
    setId(v)
    setSent(false)
    setCode('')
    setDev('')
    setError('')
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    if (!isEmail && !isMobile) {
      setError(otp ? 'Enter your 10-digit mobile number or your email.' : 'Enter your email address.')
      return
    }
    if (isMobile && !MOBILE_RE.test(value)) {
      setError('Enter a valid 10-digit mobile number.')
      return
    }
    if (isEmail && !password) {
      setError('Enter your password.')
      return
    }
    if (isMobile && sent && code.length !== 6) {
      setError('Enter the 6-digit code.')
      return
    }

    setBusy(true)
    try {
      if (isEmail) {
        await api('POST', '/auth/login', { email: value, password })
      } else if (!sent) {
        const r = await api<{ devCode?: string }>('POST', '/auth/otp/send', { mobile: value, purpose: 'login' })
        setSent(true)
        setDev(r?.devCode ?? '')
        return
      } else {
        await api('POST', '/auth/login/otp', { mobile: value, code })
      }
      resetCsrf()
      onDone()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong')
    } finally {
      setBusy(false)
    }
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
      try {
        setPasskeyOptions(await preparePasskeySignIn())
      } catch {
        setPasskeyOptions(null)
      }
    } finally {
      setBusy(false)
    }
  }

  if (register) return (
    <main className="access auth-access">
      <AuthBrand />
      <BusinessSetup
        signup
        hasBusiness={false}
        onDone={() => { resetCsrf(); onDone() }}
        onCancel={() => { setRegister(false); setError('') }}
      />
    </main>
  )

  return (
    <main className="access auth-access">
      <AuthBrand />
      <section className="auth-content">
        <div className="auth-intro">
          <h1>Welcome back</h1>
          <p>Sign in to manage your shop.</p>
        </div>

        <form onSubmit={submit} aria-label="Sign in" className="auth-form">
          <label className="auth-field">
            <span>{otp ? 'Mobile number or email' : 'Email address'}</span>
            <input
              type={otp ? 'text' : 'email'}
              inputMode={otp && /^[0-9]*$/.test(id) ? 'numeric' : 'email'}
              value={id}
              onChange={e => change(e.target.value)}
              required
              autoComplete="username"
              placeholder={otp ? '10-digit mobile or email' : 'you@example.com'}
              aria-invalid={!!error && !id.trim()}
            />
          </label>

          {(isEmail || !otp) && (
            <label className="auth-field">
              <span>Password</span>
              <span className="auth-password-control">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={e => { setPassword(e.target.value); setError('') }}
                  autoComplete="current-password"
                  aria-label="Password"
                />
                <button
                  type="button"
                  className="auth-password-toggle"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  onClick={() => setShowPassword(v => !v)}
                >
                  {showPassword ? 'Hide' : 'Show'}
                </button>
              </span>
            </label>
          )}

          {isMobile && sent && (
            <>
              <label className="auth-field">
                <span>Enter the 6-digit code</span>
                <input
                  inputMode="numeric"
                  value={code}
                  onChange={e => { setCode(e.target.value.replace(/\D/g, '').slice(0, 6)); setError('') }}
                  required
                  autoComplete="one-time-code"
                  placeholder="6-digit code"
                  aria-label="Enter the 6-digit code"
                />
              </label>
              {dev && <p className="auth-dev-code">Development code: {dev}</p>}
            </>
          )}

          <button type="submit" className="auth-primary" disabled={busy || (isMobile && sent && code.length !== 6)}>
            {busy ? 'Please wait…' : isMobile ? (sent ? 'Verify and sign in' : 'Send code') : 'Sign in'}
          </button>

          {passkeyAvailable && (
            <button
              type="button"
              className="auth-secondary auth-passkey"
              disabled={busy || !passkeyOptions}
              onClick={() => { void passkeySubmit() }}
            >
              {passkeyOptions ? 'Sign in with passkey' : 'Preparing passkey sign-in…'}
            </button>
          )}

          {error && <p role="alert" className="auth-error">{error}</p>}
        </form>

        <div className="auth-account-switch">
          <p>New to RBOS?</p>
          <button type="button" onClick={() => { setRegister(true); setError('') }}>
            Create your shop account
          </button>
        </div>
      </section>
    </main>
  )
}
