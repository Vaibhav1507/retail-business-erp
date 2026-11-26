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
  const [recovery, setRecovery] = useState(false)
  const [recoveryEmail, setRecoveryEmail] = useState('')
  const [recoverySent, setRecoverySent] = useState(false)
  const [recoveryCode, setRecoveryCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')
  const [recoveryBusy, setRecoveryBusy] = useState(false)
  const [recoveryError, setRecoveryError] = useState('')
  const [recoveryMessage, setRecoveryMessage] = useState('')
  const [recoveryComplete, setRecoveryComplete] = useState(false)

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


  function openRecovery() {
    setRecoveryEmail(isEmail ? value : '')
    setRecovery(true)
    setRecoverySent(false)
    setRecoveryCode('')
    setNewPassword('')
    setConfirmNewPassword('')
    setRecoveryError('')
    setRecoveryMessage('')
    setRecoveryComplete(false)
  }

  function backToSignIn() {
    setRecovery(false)
    setId(recoveryEmail)
    setPassword('')
    setError('')
    setRecoveryError('')
  }

  async function requestPasswordReset(e?: React.FormEvent) {
    e?.preventDefault()
    setRecoveryError('')
    setRecoveryMessage('')
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(recoveryEmail.trim())) {
      setRecoveryError('Enter a valid email address.')
      return
    }
    setRecoveryBusy(true)
    try {
      await api('POST', '/auth/password-reset/request', { email: recoveryEmail.trim() })
      setRecoverySent(true)
      setRecoveryMessage('If an account matches this email and email delivery is available, a 6-digit code will be sent. Check your inbox and spam folder.')
    } catch (err) {
      setRecoveryError(err instanceof Error ? err.message : 'Could not request a password reset.')
    } finally {
      setRecoveryBusy(false)
    }
  }

  async function submitPasswordReset(e: React.FormEvent) {
    e.preventDefault()
    setRecoveryError('')
    if (!/^\d{6}$/.test(recoveryCode)) {
      setRecoveryError('Enter the 6-digit code from your email.')
      return
    }
    if (newPassword.length < 12 || !/[a-z]/.test(newPassword) || !/[A-Z]/.test(newPassword) ||
        !/\d/.test(newPassword) || !/[^A-Za-z0-9]/.test(newPassword)) {
      setRecoveryError('Use at least 12 characters with uppercase, lowercase, a number and a symbol.')
      return
    }
    if (newPassword !== confirmNewPassword) {
      setRecoveryError('The passwords do not match.')
      return
    }
    setRecoveryBusy(true)
    try {
      await api('POST', '/auth/password-reset/confirm', {
        email: recoveryEmail.trim(),
        code: recoveryCode,
        newPassword,
      })
      setRecoveryComplete(true)
      setRecoveryMessage('Your password has been updated. Sign in with your new password.')
      setRecoveryCode('')
      setNewPassword('')
      setConfirmNewPassword('')
    } catch (err) {
      setRecoveryError(err instanceof Error ? err.message : 'Could not reset the password.')
    } finally {
      setRecoveryBusy(false)
    }
  }

  if (recovery) return (
    <main className="access auth-access">
      <AuthBrand />
      <section className="auth-content">
        <div className="auth-intro">
          <h1>{recoveryComplete ? 'Password updated' : 'Reset your password'}</h1>
          <p>{recoveryComplete
            ? 'Your account is secure with the new password.'
            : 'We will send a one-time code to your account email address.'}</p>
        </div>

        {!recoveryComplete && (
          <form className="auth-form" aria-label="Reset password" onSubmit={recoverySent ? submitPasswordReset : requestPasswordReset}>
            <label className="auth-field">
              <span>Email address</span>
              <input
                type="email"
                inputMode="email"
                autoComplete="email"
                value={recoveryEmail}
                onChange={e => {
                  setRecoveryEmail(e.target.value)
                  setRecoverySent(false)
                  setRecoveryCode('')
                  setRecoveryMessage('')
                  setRecoveryError('')
                  setNewPassword('')
                  setConfirmNewPassword('')
                }}
                required
                placeholder="you@example.com"
              />
            </label>

            {recoverySent && (
              <>
                <label className="auth-field">
                  <span>6-digit email code</span>
                  <input
                    type="text"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    maxLength={6}
                    value={recoveryCode}
                    onChange={e => setRecoveryCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                    required
                    placeholder="Enter code"
                  />
                </label>
                <label className="auth-field">
                  <span>New password</span>
                  <input type="password" maxLength={128} autoComplete="new-password" value={newPassword}
                    onChange={e => setNewPassword(e.target.value)} required />
                </label>
                <p className="auth-field-hint">At least 12 characters, including uppercase, lowercase, a number and a symbol.</p>
                <label className="auth-field">
                  <span>Confirm new password</span>
                  <input type="password" maxLength={128} autoComplete="new-password" value={confirmNewPassword}
                    onChange={e => setConfirmNewPassword(e.target.value)} required />
                </label>
              </>
            )}

            {recoveryMessage && <p className="auth-success" role="status">{recoveryMessage}</p>}
            {recoveryError && <p className="auth-error" role="alert">{recoveryError}</p>}
            <button type="submit" className="auth-primary" disabled={recoveryBusy}>
              {recoveryBusy ? 'Please wait…' : recoverySent ? 'Reset password' : 'Send code'}
            </button>
            {recoverySent && (
              <button type="button" className="auth-inline-link" onClick={() => void requestPasswordReset()} disabled={recoveryBusy}>
                Send a new code
              </button>
            )}
          </form>
        )}

        {recoveryComplete && <p className="auth-success" role="status">{recoveryMessage}</p>}
        <div className="auth-account-switch">
          <button type="button" onClick={backToSignIn}>← Back to sign in</button>
        </div>
      </section>
    </main>
  )

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
            <button type="button" className="auth-forgot-link" onClick={openRecovery}>
              Forgot password?
            </button>
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
          <p>New to Dhandha Mitra?</p>
          <button type="button" onClick={() => { setRegister(true); setError('') }}>
            Create your shop account
          </button>
        </div>
      </section>
    </main>
  )
}
