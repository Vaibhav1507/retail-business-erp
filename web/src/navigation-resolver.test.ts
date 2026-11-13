import { describe, expect, it } from 'vitest'
import { resolveNavigation } from './navigation-resolver'

describe('navigation resolver', () => {
 it('resolves direct English, Hindi and Hinglish screen requests', () => {
  expect(resolveNavigation('make a new bill').kind).toBe('navigate')
  expect(resolveNavigation('नया बिल बनाओ').kind).toBe('navigate')
  expect(resolveNavigation('मुझे बिल बनाना है').kind).toBe('navigate')
  expect(resolveNavigation('bill banana hai').kind).toBe('navigate')
  expect(resolveNavigation('मेरा स्टॉक दिखाओ').kind).toBe('navigate')
  expect(resolveNavigation('मुझे स्टॉक दिखाना है').kind).toBe('navigate')
  expect(resolveNavigation('supplier ko payment karna hai').kind).toBe('navigate')
  expect(resolveNavigation('मुझे नया customer add करना है')).toMatchObject({ kind: 'navigate', target: { id: 'parties.customers' } })
  expect(resolveNavigation('naya customer banana hai')).toMatchObject({ kind: 'navigate', target: { id: 'parties.customers' } })
  expect(resolveNavigation('ग्राहक की एंट्री करनी है')).toMatchObject({ kind: 'navigate', target: { id: 'parties.customers' } })
  expect(resolveNavigation('customer ka naam likhna hai')).toMatchObject({ kind: 'navigate', target: { id: 'parties.customers' } })
  expect(resolveNavigation('आज खरीद की एंट्री करनी है')).toMatchObject({ kind: 'navigate', target: { id: 'buy.receive' } })
  expect(resolveNavigation('दुकान का स्टॉक दिखाओ')).toMatchObject({ kind: 'navigate', target: { id: 'stock.current' } })
  expect(resolveNavigation('customer se udhar lena hai')).toMatchObject({ kind: 'navigate', target: { id: 'sell.collect' } })
 })
 it('asks instead of guessing on ambiguous commands', () => {
  expect(resolveNavigation('return').kind).toBe('ask')
  expect(resolveNavigation('history').kind).toBe('ask')
 })
 it('rejects business-data questions and transaction requests', () => {
  expect(resolveNavigation('how much cash do I have').kind).toBe('reject')
  expect(resolveNavigation('मेरे पास कितना पैसा है').kind).toBe('reject')
  expect(resolveNavigation('आजकल खाता कितना हुआ').kind).toBe('reject')
  expect(resolveNavigation('किस ग्राहक का कितना उधार बाकी है').kind).toBe('reject')
  expect(resolveNavigation('create a new bill').kind).toBe('navigate')
  expect(resolveNavigation('delete the last bill').kind).toBe('reject')
  expect(resolveNavigation('ramesh ko 500 rupaye bhejo').kind).toBe('reject')
 })
 it('does not generate arbitrary routes for unknown requests', () => {
  expect(resolveNavigation('open google.com').kind).toBe('reject')
  expect(resolveNavigation('xyzabc qwerty').kind).toBe('not-understood')
 })
})
