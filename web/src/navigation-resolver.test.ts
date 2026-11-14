import { describe, expect, it } from 'vitest'
import { resolveNavigation, suggestNavigation, navigationTargets } from './navigation-resolver'
import { navigationKeywordProfiles } from './navigation-keywords'

describe('keyword navigation resolver', () => {
  it('has a keyword profile for every allowlisted screen and no profile for an arbitrary route', () => {
    expect(Object.keys(navigationKeywordProfiles).sort()).toEqual(navigationTargets.map(t => t.id).sort())
    expect(navigationTargets.some(t => t.hash.includes('://'))).toBe(false)
  })

  const accepted: [string, string][] = [
    ['home', 'dashboard'],
    ['home', 'ghar'],
    ['sell.menu', 'sell menu'],
    ['sell.menu', 'bikri menu'],
    ['sell.bill', 'new bil'],
    ['sell.bill', 'naya bill'],
    ['sell.bill', 'bill banana'],
    ['sell.bills', 'purana bill'],
    ['sell.bills', 'bill history'],
    ['sell.collect', 'customer payment'],
    ['sell.collect', 'grahak vasooli'],
    ['sell.return', 'customer wapas'],
    ['sell.return', 'grahak return'],
    ['buy.receive', 'purchase entry'],
    ['buy.receive', 'supplier maal aayo'],
    ['buy.receive', 'maal aayo'],
    ['buy.pay', 'supplier payment'],
    ['buy.pay', 'dealer chukana'],
    ['buy.pay', 'supplier ko payment dena'],
    ['buy.history', 'purchase history'],
    ['buy.history', 'supplier old list'],
    ['buy.history', 'supplier history'],
    ['parties.suppliers', 'vyapari'],
    ['parties.purchases', 'customer purchase history'],
    ['more.branches', 'shop location'],
    ['buy.return', 'supplier return'],
    ['buy.return', 'purchase wapas'],
    ['stock.current', 'stock available'],
    ['stock.current', 'check stock'],
    ['stock.current', 'maal bacha'],
    ['stock.products', 'product list'],
    ['stock.products', 'item add'],
    ['stock.opening', 'opening stock'],
    ['stock.opening', 'shuru maal'],
    ['stock.adjust', 'stock adjust'],
    ['stock.adjust', 'maal correction'],
    ['parties.customers', 'customer'],
    ['parties.customers', 'grahak list'],
    ['parties.suppliers', 'supplier'],
    ['parties.suppliers', 'vyapari list'],
    ['parties.purchases', 'customer purchase history'],
    ['parties.purchases', 'grahak ne kharid'],
    ['parties.dues', 'customer baki'],
    ['parties.dues', 'grahak udhar'],
    ['money.cash', 'galla bank'],
    ['money.cash', 'cashbook'],
    ['money.expenses', 'kharcha'],
    ['money.expenses', 'खर्चा'],
    ['money.expenses', 'expense'],
    ['money.owner', 'owner capital'],
    ['money.owner', 'malik withdrawal'],
    ['money.closing', 'day hisab'],
    ['money.closing', 'daily galla'],
    ['money.closing', 'aaj hisab band'],
    ['more.menu', 'more options'],
    ['more.menu', 'baaki menu'],
    ['more.reports', 'profit report'],
    ['more.reports', 'hisab report'],
    ['more.print', 'print bill'],
    ['more.print', 'chhapai'],
    ['more.lock', 'lock month'],
    ['more.lock', 'freeze period'],
    ['more.audit', 'audit history'],
    ['more.audit', 'change record'],
    ['more.gst', 'gst'],
    ['more.gst', 'tax'],
    ['more.staff', 'staff'],
    ['more.staff', 'employee'],
    ['more.branches', 'branch'],
    ['more.branches', 'shop location'],
    ['more.transfers', 'stock transfer'],
    ['more.transfers', 'goods move'],
    ['more.orders', 'supplier order'],
    ['more.orders', 'purchase po'],
    ['more.reconcile', 'bank reconcile'],
    ['more.reconcile', 'passbook milan'],
    ['more.loans', 'loan'],
    ['more.loans', 'karza'],
    ['settings', 'settings'],
    ['settings', 'dukaan details'],
  ]

  it.each(accepted)('resolves keyword input "%s" to the expected target', (target, input) => {
    expect(resolveNavigation(input)).toMatchObject({ kind: 'navigate', target: { id: target } })
  })

  it('normalises common ASR spellings and Bundeli-style vowel/aspiration variants conservatively', () => {
    expect(resolveNavigation('graahak payment')).toMatchObject({ kind: 'navigate', target: { id: 'sell.collect' } })
    expect(resolveNavigation('kharid entri')).toMatchObject({ kind: 'navigate', target: { id: 'buy.receive' } })
    expect(resolveNavigation('karid entry')).toMatchObject({ kind: 'navigate', target: { id: 'buy.receive' } })
    expect(resolveNavigation('kharcho')).toMatchObject({ kind: 'navigate', target: { id: 'money.expenses' } })
    expect(resolveNavigation('mujhe Ramesh ji ka purana bil').kind).toBe('not-understood')
    expect(resolveNavigation('रमेश जी का पुराना बिल').kind).toBe('not-understood')
    expect(suggestNavigation('mujhe Ramesh ji ka purana bil').some(target => target.id === 'sell.bills')).toBe(true)
    expect(suggestNavigation('रमेश जी का पुराना बिल').some(target => target.id === 'sell.bills')).toBe(true)
    expect(resolveNavigation('बकाया').kind).toBe('ask')
    expect(resolveNavigation('customer se udhar lena').kind).toMatch(/^(navigate|ask)$/)
    expect(resolveNavigation('बाकी').kind).toBe('ask')
    expect(resolveNavigation('उधार').kind).toBe('ask')
  })

  it('asks when a lone keyword does not identify a unique destination', () => {
    expect(resolveNavigation('return').kind).toBe('ask')
    expect(resolveNavigation('payment').kind).toBe('ask')
    expect(resolveNavigation('bill').kind).toBe('ask')
    expect(resolveNavigation('purchase').kind).toBe('ask')
    expect(resolveNavigation('stock').kind).toBe('ask')
    expect(resolveNavigation('history').kind).toBe('ask')
    expect(resolveNavigation('udhar').kind).toBe('ask')
  })

  it('rejects data questions, transaction execution, arbitrary URLs and numeric commands', () => {
    for (const input of [
      'how much cash do I have',
      'मेरे पास कितना पैसा है',
      'आज कितनी बिक्री हुई',
      'किस ग्राहक का कितना उधार बाकी है',
      'customer phone number',
      'delete the last bill',
      'ramesh ko 500 rupaye bhejo',
      'paise jama kar do',
      'transfer 12 stock items',
      'open google.com',
    ]) expect(resolveNavigation(input).kind, input).toBe('reject')
  })

  it('does not navigate from noise or an unrelated person name', () => {
    expect(resolveNavigation('xyzabc qwerty').kind).toBe('not-understood')
    expect(resolveNavigation('Ramesh ji nam batao').kind).toBe('not-understood')
  })

  it('suggests from partial keywords without auto-executing during typing', () => {
    expect(suggestNavigation('purana bil').some(target => target.id === 'sell.bills')).toBe(true)
    expect(suggestNavigation('new bil').some(target => target.id === 'sell.bill')).toBe(true)
    expect(resolveNavigation('new bil')).toMatchObject({ kind: 'navigate', target: { id: 'sell.bill' } })
  })
})
