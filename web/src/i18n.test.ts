import { afterEach, describe, expect, it } from 'vitest'
import { catalogueKeys, formatInr, setLanguage, t, toLang } from './i18n'

afterEach(() => setLanguage('en'))

describe('i18n', () => {
  it('returns English by default', () => {
    expect(t('nav.sell')).toBe('Sell')
  })
  it('substitutes params', () => {
    expect(t('home.welcomeNamed', { name: 'Sharma Store' })).toBe('Welcome, Sharma Store')
  })
  it('switches to Hindi and back', () => {
    setLanguage('hi')
    expect(t('nav.sell')).not.toBe('Sell')
    setLanguage('en')
    expect(t('nav.sell')).toBe('Sell')
  })
  it('falls back to English for unknown language values', () => {
    expect(toLang('fr')).toBe('en')
    expect(toLang('hi')).toBe('hi')
    expect(toLang(null)).toBe('en')
  })
  it('formats INR with Indian digit grouping', () => {
    expect(formatInr(1234567.5)).toContain('12,34,567.50')
  })
  it('provides Hindi entries for every Buy and invoice-import message', () => {
    const enKeys = catalogueKeys('en').filter(key => key.startsWith('buy.'))
    const hiKeys = new Set(catalogueKeys('hi'))
    expect(enKeys.filter(key => !hiKeys.has(key))).toEqual([])
    setLanguage('hi')
    expect(t('buy.payTitle')).toBe('सप्लायर को भुगतान')
    expect(t('buy.invoiceUpload')).toContain('PDF')
    expect(t('buy.returnTitle')).toBe('सप्लायर को सामान लौटाएँ')
  })

})
