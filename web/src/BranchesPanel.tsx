import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { t } from './i18n'

type Branch = { id: string; name: string; isPrimary: boolean; locations: { id: string; name: string }[] }

/** G2: branches and their stock locations. */
export function BranchesPanel() {
  const [branches, setBranches] = useState<Branch[] | null>(null)
  const [name, setName] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setBranches(await api<Branch[]>('GET', '/branches')); setError('') }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not load branches') }
  }, [])
  useEffect(() => { void load() }, [load])

  const addBranch = async () => {
    setBusy(true)
    try { await api('POST', '/branches', { name }); setName(''); await load() }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not add branch') }
    finally { setBusy(false) }
  }
  const addLocation = async (branchId: string) => {
    const locName = window.prompt(t('br.locPrompt'))
    if (!locName?.trim()) return
    try { await api('POST', '/locations', { branchId, name: locName.trim() }); await load() }
    catch (e) { setError(e instanceof Error ? e.message : 'Could not add location') }
  }

  return (
    <section aria-label="Branches">
      {error && <p role="alert">{error}</p>}
      <div>
        <label>{t('br.newName')}<input value={name} onChange={e => setName(e.target.value)} maxLength={120} /></label>
        <button onClick={addBranch} disabled={busy || !name.trim()}>{t('br.add')}</button>
      </div>
      {branches === null ? <p>{t('br.loading')}</p> : (
        <ul>
          {branches.map(b => (
            <li key={b.id}>
              <strong>{b.name}</strong>{b.isPrimary ? t('br.main') : ''}: {b.locations.map(l => l.name).join(', ') || t('br.noLoc')}{' '}
              <button onClick={() => addLocation(b.id)}>{t('br.addLoc')}</button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
