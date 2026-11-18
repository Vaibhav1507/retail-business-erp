import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FirstShopOnboarding } from './FirstShopOnboarding'

const emptyShop = {
  '/api/business-profile': { exists: true, businessName: 'Electronics Shop', businessCategory: 'electronics_mobile', onboardingCompleted: true },
  '/api/products': [],
  '/api/stock': [],
  '/api/purchases': [],
  '/api/sales': [],
}

function mock(records: Record<string, unknown>) {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => new Response(JSON.stringify(records[url]), { status: 200 })))
}

beforeEach(() => vi.unstubAllGlobals())

describe('FirstShopOnboarding', () => {
  it('derives the incomplete progress from saved records, not from screen visits', async () => {
    mock(emptyShop)
    render(<FirstShopOnboarding />)
    const progress = await screen.findByRole('progressbar', { name: 'First shop setup progress' })
    expect(progress).toHaveAttribute('aria-valuenow', '1')
    expect(screen.getByText('Shop details')).toBeInTheDocument()
    expect(screen.getByText('Add your products')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Continue setup' })).toHaveAttribute('href', '#/stock/products')
    expect(screen.getAllByText('Not done yet')).toHaveLength(4)
  })

  it('recognises persisted catalogue, opening stock, purchase and sale records after a fresh mount', async () => {
    mock({
      '/api/business-profile': { exists: true, businessName: 'Existing Shop', businessCategory: 'electronics_mobile', onboardingCompleted: true },
      '/api/products': [{ id: 'product-1' }],
      '/api/stock': [{ productId: 'product-1', quantity: 5 }],
      '/api/purchases': [{ id: 'purchase-1' }],
      '/api/sales': [{ id: 'sale-1' }],
    })
    render(<FirstShopOnboarding />)
    await waitFor(() => expect(screen.getByRole('progressbar', { name: 'First shop setup progress' })).toHaveAttribute('aria-valuenow', '5'))
    expect(screen.getByText('You have completed the first-shop setup. Your daily shop summary is below.')).toBeInTheDocument()
  })

  it('treats a posted first purchase as the source of initial stock when opening stock was skipped', async () => {
    mock({
      ...emptyShop,
      '/api/purchases': [{ id: 'purchase-1' }],
    })
    render(<FirstShopOnboarding />)
    const progress = await screen.findByRole('progressbar', { name: 'First shop setup progress' })
    expect(progress).toHaveAttribute('aria-valuenow', '3')
    expect(screen.getByText('Add the stock already in your shop')).toBeInTheDocument()
    expect(screen.getByText('Record your first purchase')).toBeInTheDocument()
  })

  it('never counts a page visit or unsaved screen as a saved purchase', async () => {
    mock(emptyShop)
    render(<FirstShopOnboarding />)
    const progress = await screen.findByRole('progressbar', { name: 'First shop setup progress' })
    expect(progress).toHaveAttribute('aria-valuenow', '1')
    expect(screen.getByText('Record your first purchase')).toBeInTheDocument()
    expect(screen.getAllByText('Not done yet')).toHaveLength(4)
  })

  it('does not show or call business APIs in offline snapshot mode', () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const { container } = render(<FirstShopOnboarding offlineMode />)
    expect(container).toBeEmptyDOMElement()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
