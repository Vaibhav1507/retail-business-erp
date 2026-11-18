import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { t } from './i18n'
import type { MessageKey } from './i18n'
import './first-shop-onboarding.css'

type Product = { id: string }
type StockRow = { productId: string; quantity: number }
type Purchase = { id: string }
type Sale = { id: string }
type Profile = { onboardingCompleted?: boolean }
type Progress = { profile: boolean; catalogue: boolean; openingStock: boolean; purchase: boolean; sale: boolean }
const EMPTY: Progress = { profile: false, catalogue: false, openingStock: false, purchase: false, sale: false }
const steps: { key: keyof Progress; title: MessageKey; detail: MessageKey; href: string }[] = [
  { key: 'profile', title: 'onboarding.profileTitle', detail: 'onboarding.profileDetail', href: '#/settings' },
  { key: 'catalogue', title: 'onboarding.catalogueTitle', detail: 'onboarding.catalogueDetail', href: '#/stock/products' },
  { key: 'openingStock', title: 'onboarding.stockTitle', detail: 'onboarding.stockDetail', href: '#/stock/opening' },
  { key: 'purchase', title: 'onboarding.purchaseTitle', detail: 'onboarding.purchaseDetail', href: '#/buy/receive' },
  { key: 'sale', title: 'onboarding.saleTitle', detail: 'onboarding.saleDetail', href: '#/sell/bill' },
]
export function FirstShopOnboarding({ offlineMode = false }: { offlineMode?: boolean }) {
  const [progress, setProgress] = useState<Progress>(EMPTY)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const refresh = useCallback(async () => {
    setLoading(true); setError(false)
    try {
      const [profile, products, stock, purchases, sales] = await Promise.all([
        api<Profile>('GET', '/business-profile'), api<Product[]>('GET', '/products'),
        api<StockRow[]>('GET', '/stock'), api<Purchase[]>('GET', '/purchases'), api<Sale[]>('GET', '/sales'),
      ])
      const hasPurchase = purchases.length > 0
      setProgress({ profile: profile.onboardingCompleted === true, catalogue: products.length > 0,
        openingStock: stock.some(row => Number(row.quantity) > 0) || hasPurchase, purchase: hasPurchase, sale: sales.length > 0 })
    } catch { setError(true) } finally { setLoading(false) }
  }, [])
  useEffect(() => { if (!offlineMode) void refresh() }, [offlineMode, refresh])
  if (offlineMode) return null
  const completed = steps.filter(step => progress[step.key]).length
  const next = steps.find(step => !progress[step.key])
  const percent = Math.round((completed / steps.length) * 100)
  return <section className="first-shop" aria-label="First shop setup">
    <div className="first-shop-head"><div><p className="first-shop-eyebrow">{t('onboarding.eyebrow')}</p><h2>{t('onboarding.title')}</h2><p className="first-shop-intro">{t('onboarding.intro')}</p></div>
      <div className="first-shop-count" aria-label={t('onboarding.progress', { done: completed, total: steps.length })}><strong>{completed}/{steps.length}</strong><span>{t('onboarding.doneLabel')}</span></div></div>
    <div className="first-shop-progress" role="progressbar" aria-label={t('onboarding.progressLabel')} aria-valuemin={0} aria-valuemax={steps.length} aria-valuenow={completed}><span style={{ width: percent + '%' }} /></div>
    {loading && <p role="status" className="first-shop-status">{t('onboarding.loading')}</p>}
    {error && <div className="first-shop-error" role="alert"><p>{t('onboarding.error')}</p><button type="button" className="secondary" onClick={() => void refresh()}>{t('onboarding.retry')}</button></div>}
    {!loading && !error && <><ol className="first-shop-steps">{steps.map((step, index) => {
      const done = progress[step.key], current = !done && next?.key === step.key
      return <li key={step.key} className={done ? 'is-done' : current ? 'is-current' : ''}>
        <span className="first-shop-marker" aria-hidden="true">{done ? '✓' : index + 1}</span>
        <div className="first-shop-copy"><div className="first-shop-step-title"><strong>{t(step.title)}</strong><span className={'first-shop-state ' + (done ? 'done' : 'pending')}>{done ? t('onboarding.saved') : t('onboarding.notDone')}</span></div>
          <p>{t(step.detail)}</p><a className="first-shop-link" href={step.href}>{done ? t('onboarding.reviewStep') : current ? t('onboarding.continue') : t('onboarding.openStep')}</a></div>
      </li>
    })}</ol><p className="first-shop-footnote">{t('onboarding.persistedNote')}</p>
      {completed === steps.length && <p className="first-shop-success" role="status">{t('onboarding.complete')}</p>}</>}
  </section>
}
