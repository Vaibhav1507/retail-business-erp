import { expect, type Page } from '@playwright/test'

export const PASSWORD = 'Synthetic#Pass123'

/** A random valid Indian mobile number so each run gets a unique account. */
export const randomMobile = () => '9' + Math.floor(100_000_000 + Math.random() * 899_999_999).toString()

/**
 * Creates an account through the single signup flow: account + mobile OTP, business, address and tax, preferences, review.
 * Relies on the Development API showing the OTP on screen (no real SMS is sent).
 */
export async function signUp(page: Page, opts: { email: string; businessName: string; city?: string; state?: string; pin?: string; mobile?: string }) {
  const mobile = opts.mobile ?? randomMobile()
  await page.goto('/')
  await page.getByRole('button', { name: 'Create an account' }).click()
  await page.getByLabel('Email').fill(opts.email)
  await page.getByLabel(/Password/).fill(PASSWORD)
  await page.getByLabel('Mobile number').fill(mobile)
  await page.getByRole('button', { name: 'Send code' }).click()
  const hint = await page.getByText(/Development code: \d{6}/).textContent()
  const code = /(\d{6})/.exec(hint ?? '')![1]
  await page.getByLabel('Enter the 6-digit code').fill(code)
  await page.getByRole('button', { name: 'Verify code' }).click()
  await expect(page.getByText('Mobile number verified.')).toBeVisible()
  await page.getByRole('button', { name: 'Next' }).click()
  await page.getByLabel('Business name').fill(opts.businessName)
  await page.getByLabel('Business category').selectOption({ index: 1 })
  await page.getByLabel('Owner or contact name').fill('E2E Owner')
  await page.getByRole('button', { name: 'Next' }).click()
  await page.getByLabel('Business address').fill('1 Test Street')
  await page.getByLabel('City or town').fill(opts.city ?? 'Chennai')
  await page.getByLabel('State').selectOption(opts.state ?? 'Tamil Nadu')
  await page.getByLabel('PIN code').fill(opts.pin ?? '600001')
  await page.getByLabel('GST registration').selectOption('NotRegistered')
  await page.getByRole('button', { name: 'Next' }).click()
  await page.getByRole('button', { name: 'Review' }).click()
  await page.getByRole('button', { name: 'Create account' }).click()
}
