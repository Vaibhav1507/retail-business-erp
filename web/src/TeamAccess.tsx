import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { t } from './i18n'

type Member = { userId: string; email: string | null; role: string; active: boolean; isYou: boolean }
type RoleInfo = { role: string; permissions: string[] }

/** G1: owner-only team management (server enforces staff.manage). */
export function TeamAccess() {
  const [members, setMembers] = useState<Member[] | null>(null)
  const [roles, setRoles] = useState<RoleInfo[]>([])
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('Cashier')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      const [m, r] = await Promise.all([api<Member[]>('GET', '/staff'), api<RoleInfo[]>('GET', '/staff/roles')])
      setMembers(m); setRoles(r); setError('')
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not load staff') }
  }, [])
  useEffect(() => { void load() }, [load])

  const add = async () => {
    setBusy(true)
    try { await api('POST', '/staff', { email, password, role }); setEmail(''); setPassword(''); await load() }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not add staff') }
    finally { setBusy(false) }
  }
  const update = async (id: string, body: { role?: string; active?: boolean }) => {
    try { await api('PUT', `/staff/${id}`, body); await load() }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not update staff') }
  }

  return (
    <section aria-label="Team access">
      {error && <p role="alert">{error}</p>}
      <div>
        <label>{t('ta.email')}<input type="email" value={email} onChange={e => setEmail(e.target.value)} /></label>
        <label>{t('ta.password')}<input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password" /></label>
        <label>{t('ta.role')}<select value={role} onChange={e => setRole(e.target.value)}>{roles.map(r => <option key={r.role}>{r.role}</option>)}</select></label>
        <button onClick={add} disabled={busy || !email || !password}>{t('ta.add')}</button>
      </div>
      {members === null ? <p>{t('ta.loading')}</p> : (
        <table>
          <thead><tr><th>{t('ta.email')}</th><th>{t('ta.role')}</th><th>{t('ta.status')}</th><th /></tr></thead>
          <tbody>
            {members.map(m => (
              <tr key={m.userId}>
                <td>{m.email}{m.isYou ? t('ta.you') : ''}</td>
                <td>{m.role === 'Owner' || m.isYou ? m.role : <select value={m.role} onChange={e => update(m.userId, { role: e.target.value })}>{roles.map(r => <option key={r.role}>{r.role}</option>)}</select>}</td>
                <td>{m.active ? t('ta.active') : t('ta.disabled')}</td>
                <td>{m.role !== 'Owner' && !m.isYou && <button onClick={() => update(m.userId, { active: !m.active })}>{m.active ? t('ta.disable') : t('ta.enable')}</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
