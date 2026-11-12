import { beforeEach, describe, expect, it } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { NavigationAssistant } from './NavigationAssistant'
import { setLanguage } from './i18n'

beforeEach(() => { window.location.hash = ''; setLanguage('en') })

describe('NavigationAssistant', () => {
 it('navigates to a verified screen from typed input', async () => {
  render(<NavigationAssistant />)
  await userEvent.type(screen.getByLabelText('Go to a screen'), 'make a new bill')
  await userEvent.click(screen.getByRole('button', { name: 'Go' }))
  await waitFor(() => expect(window.location.hash).toBe('#/sell/bill'))
 })
 it('asks the user to choose when a command is ambiguous', async () => {
  render(<NavigationAssistant />)
  await userEvent.type(screen.getByLabelText('Go to a screen'), 'return')
  await userEvent.click(screen.getByRole('button', { name: 'Go' }))
  expect(screen.getByText('Which screen do you mean?')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Customer return' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Return to supplier' })).toBeInTheDocument()
 })
 it('rejects business-data questions without navigating', async () => {
  render(<NavigationAssistant />)
  await userEvent.type(screen.getByLabelText('Go to a screen'), 'how much cash do I have')
  await userEvent.click(screen.getByRole('button', { name: 'Go' }))
  expect(screen.getByRole('status')).toHaveTextContent('I can only navigate screens')
  expect(window.location.hash).toBe('')
 })
 it('keeps quick actions available without loading speech recognition', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Check stock' }))
  expect(window.location.hash).toBe('#/stock/current')
  expect(screen.queryByText(/Preparing on-device voice/)).not.toBeInTheDocument()
 })
 it('uses Hindi labels when the saved app language is Hindi', () => {
  setLanguage('hi')
  render(<NavigationAssistant />)
  expect(screen.getByLabelText('किसी स्क्रीन पर जाएँ')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'स्टॉक देखें' })).toBeInTheDocument()
 })
})
