import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { resetCsrf } from './api'
import { setLanguage } from './i18n'

function mock() {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    const routes: Record<string, unknown> = {
      '/api/csrf': { token: 't' },
      '/api/me': { email: 'owner@example.test', tenantId: 't1', profileComplete: true, businessCategory: null },
      '/api/products': [],
      '/api/locations': [],
      '/api/stock': [],
      '/api/dashboard': { salesToday: { bills: 0, total: 0 }, moneyToCollect: 0, moneyToPay: 0, lowStockCount: 0, cashAndBank: 0 },
      '/api/reminders': [],
    }
    const body = routes[url.split('?')[0]]
    return new Response(body === undefined ? JSON.stringify({ error: 'not found' }) : JSON.stringify(body), { status: body === undefined ? 404 : 200 })
  }))
}

beforeEach(() => {
  resetCsrf()
  vi.unstubAllGlobals()
  localStorage.clear()
  setLanguage('en')
  delete document.documentElement.dataset.theme
  window.location.hash = ''
})

describe('theme toggle (H4)', () => {
  it('defaults to light and applies the data-theme attribute', async () => {
    mock()
    render(<App />)
    await screen.findByRole('button', { name: 'Dark mode' })
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('rbos.theme')).toBe('light')
  })

  it('switches to dark, persists the choice and offers light mode back', async () => {
    mock()
    render(<App />)
    await userEvent.click(await screen.findByRole('button', { name: 'Dark mode' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem('rbos.theme')).toBe('dark')
    await userEvent.click(screen.getByRole('button', { name: 'Light mode' }))
    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('restores a saved dark preference on load', async () => {
    localStorage.setItem('rbos.theme', 'dark')
    mock()
    render(<App />)
    await screen.findByRole('button', { name: 'Light mode' })
    expect(document.documentElement.dataset.theme).toBe('dark')
  })
})
