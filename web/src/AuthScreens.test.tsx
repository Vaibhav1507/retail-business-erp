import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AccountAccess } from './AccountAccess'
import { BusinessSetup } from './BusinessSetup'

const mocks = vi.hoisted(() => ({
  api: vi.fn(),
  otpEnabled: vi.fn(),
  resetCsrf: vi.fn(),
  passkeyEnabled: vi.fn(),
  preparePasskeySignIn: vi.fn(),
  signInWithPasskey: vi.fn(),
}))

vi.mock('./api', () => ({
  api: mocks.api,
  otpEnabled: mocks.otpEnabled,
  resetCsrf: mocks.resetCsrf,
}))

vi.mock('./passkeys', () => ({
  passkeyEnabled: mocks.passkeyEnabled,
  preparePasskeySignIn: mocks.preparePasskeySignIn,
  signInWithPasskey: mocks.signInWithPasskey,
}))

beforeEach(() => {
  vi.clearAllMocks()
  localStorage.clear()
  mocks.api.mockResolvedValue({})
  mocks.otpEnabled.mockResolvedValue(false)
  mocks.passkeyEnabled.mockResolvedValue(false)
})

describe('RBOS authentication screens', () => {
  it('renders the action-first sign-in and uses the existing email/password endpoint', async () => {
    const user = userEvent.setup()
    const onDone = vi.fn()
    render(<AccountAccess onDone={onDone} />)

    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
    expect(screen.getByText('Retail Business OS')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sign in with passkey' })).not.toBeInTheDocument()

    await user.type(screen.getByLabelText('Email address'), 'owner@example.test')
    await user.type(screen.getByLabelText('Password'), 'CorrectHorseBattery123')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(mocks.api).toHaveBeenCalledWith('POST', '/auth/login', {
      email: 'owner@example.test',
      password: 'CorrectHorseBattery123',
    }))
    expect(mocks.resetCsrf).toHaveBeenCalled()
    expect(onDone).toHaveBeenCalledOnce()
  })

  it('keeps the signup wizard at five stages and preserves the existing register/bootstrap/profile sequence', async () => {
    const user = userEvent.setup()
    const onDone = vi.fn()
    render(<BusinessSetup signup hasBusiness={false} onDone={onDone} onCancel={vi.fn()} />)

    expect(await screen.findByRole('heading', { name: 'Create your account' })).toBeInTheDocument()
    expect(screen.getByText('STEP 1 OF 5')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Email *'), 'owner@example.test')
    await user.type(screen.getByLabelText(/Password \*/), 'CorrectHorseBattery123')
    await user.type(screen.getByLabelText('Mobile number *'), '9876543210')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    expect(screen.getByText('STEP 2 OF 5')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Business name *'), 'RBOS Test Electronics')
    await user.selectOptions(screen.getByLabelText('Business category *'), 'electronics_mobile')
    await user.type(screen.getByLabelText('Owner or contact name *'), 'Test Owner')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    expect(screen.getByText('STEP 3 OF 5')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Business address *'), '10 Test Road')
    await user.type(screen.getByLabelText('City or town *'), 'Bengaluru')
    await user.selectOptions(screen.getByLabelText('State *'), 'Karnataka')
    await user.type(screen.getByLabelText('PIN code *'), '560001')
    await user.selectOptions(screen.getByLabelText('GST registration *'), 'NotRegistered')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    expect(screen.getByText('STEP 4 OF 5')).toBeInTheDocument()
    expect(screen.getByText('Currency')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Review' }))

    expect(screen.getByText('STEP 5 OF 5')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Sample invoice preview' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(onDone).toHaveBeenCalledOnce())
    expect(mocks.api).toHaveBeenNthCalledWith(1, 'POST', '/auth/register', expect.objectContaining({
      email: 'owner@example.test',
      password: 'CorrectHorseBattery123',
      mobile: '9876543210',
    }))
    expect(mocks.api).toHaveBeenCalledWith('POST', '/bootstrap', expect.objectContaining({
      businessName: 'RBOS Test Electronics',
      branchName: 'Main Branch',
      locationName: 'Main Store',
    }), expect.any(String))
    expect(mocks.api).toHaveBeenCalledWith('PUT', '/business-profile', expect.objectContaining({
      businessName: 'RBOS Test Electronics',
      businessCategory: 'electronics_mobile',
      gstStatus: 'NotRegistered',
      pinCode: '560001',
    }))
  })

  it('loads the real local PIN gate and supports keypad digits, clear and backspace', async () => {
    const user = userEvent.setup()
    localStorage.setItem('rbos.local-pin.v1:owner@example.test', JSON.stringify({
      salt: 'AA==',
      hash: 'AA==',
      failures: 0,
      lockedUntil: 0,
    }))
    const { AppLockGate } = await vi.importActual<typeof import('./AppLock')>('./AppLock')

    render(
      <AppLockGate email="owner@example.test" onSignOut={vi.fn().mockResolvedValue(undefined)}>
        <div>Shop workspace</div>
      </AppLockGate>,
    )

    expect(await screen.findByRole('heading', { name: 'Unlock your shop' })).toBeInTheDocument()
    const pin = screen.getByLabelText('PIN')

    await user.click(screen.getByRole('button', { name: 'Digit 1' }))
    await user.click(screen.getByRole('button', { name: 'Digit 2' }))
    await user.click(screen.getByRole('button', { name: 'Digit 3' }))
    expect(pin).toHaveValue('123')

    await user.click(screen.getByRole('button', { name: 'Backspace' }))
    expect(pin).toHaveValue('12')
    await user.click(screen.getByRole('button', { name: 'Clear PIN field' }))
    expect(pin).toHaveValue('')
  })
})
