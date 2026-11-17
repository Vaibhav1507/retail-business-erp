import './profile-menu.css'
import { useEffect, useRef, useState } from 'react'
import { t } from './i18n'

type Props = { businessName: string; email: string; dark: boolean; onToggleDark: () => void; onLogout: () => void }

export function ProfileMenu({ businessName, email, dark, onToggleDark, onLogout }: Props) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent | TouchEvent) => { if (!ref.current?.contains(e.target as Node)) setOpen(false) }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('touchstart', onDown)
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('touchstart', onDown); document.removeEventListener('keydown', onKey) }
  }, [open])

  const run = (fn: () => void) => () => { setOpen(false); fn() }

  return (
    <div className="profile-menu" ref={ref}>
      <span className="profile-name" title={email}>{businessName}</span>
      <button type="button" className="profile-btn" aria-label="Profile menu" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen(o => !o)}>
        <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="8" r="4" /><path d="M4 21c0-4.4 3.6-7 8-7s8 2.6 8 7" /></svg>
      </button>
      {open && (
        <div className="profile-dropdown" role="menu">
          <p className="profile-email">{email}</p>
          <a role="menuitem" href="#/settings" onClick={() => setOpen(false)}>{t('area.settings')}</a>
          <a role="menuitem" href="#/devices" onClick={() => setOpen(false)}>{t('app.devices')}</a>
          <button type="button" role="menuitem" onClick={run(() => window.dispatchEvent(new Event('rbos:lock')))}>Lock app</button>
          <button type="button" role="menuitem" onClick={run(onToggleDark)}>{dark ? t('theme.light') : t('theme.dark')}</button>
          <button type="button" role="menuitem" onClick={run(onLogout)}>{t('app.signOut')}</button>
        </div>
      )}
    </div>
  )
}
