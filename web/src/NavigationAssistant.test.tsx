import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { NavigationAssistant } from './NavigationAssistant'
import { setLanguage } from './i18n'

beforeEach(() => { window.location.hash = ''; setLanguage('en') })

describe('NavigationAssistant', () => {
 it('stays collapsed until the user opens it', () => {
  render(<NavigationAssistant />)
  expect(screen.queryByLabelText('Go to a screen')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Open assistant' })).toBeInTheDocument()
 })
 it('opens a panel with a visible voice action and typed navigation', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  expect(screen.getByRole('button', { name: /Allow microphone & start/ })).toBeInTheDocument()
  expect(screen.getByLabelText('Ask your business assistant')).toBeInTheDocument()
 })
 it('closes the panel when a quick action navigates', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  await userEvent.click(screen.getByRole('button', { name: 'Check stock' }))
  expect(window.location.hash).toBe('#/stock/current')
  expect(screen.queryByRole('button', { name: 'Close assistant' })).not.toBeInTheDocument()
 })
 it('navigates to a verified screen from typed input', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), 'make a new bill')
  await userEvent.click(screen.getByRole('button', { name: 'Go' }))
  await waitFor(() => expect(window.location.hash).toBe('#/sell/bill'))
 })
 it('asks the user to choose when a command is ambiguous', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), 'return')
  await userEvent.click(screen.getByRole('button', { name: 'Go' }))
  expect(screen.getByText('Which screen do you mean?')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Customer return' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Return to supplier' })).toBeInTheDocument()
 })
 it('rejects business-data questions without navigating', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), 'how much cash do I have')
  await userEvent.click(screen.getByRole('button', { name: 'Go' }))
  expect(screen.getByRole('status')).toHaveTextContent('I can only navigate screens')
  expect(window.location.hash).toBe('')
 })
 it('uses Hindi labels when the saved app language is Hindi', async () => {
  setLanguage('hi')
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'असिस्टेंट खोलें' }))
  expect(screen.getByLabelText('बिज़नेस असिस्टेंट')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'स्टॉक देखें' })).toBeInTheDocument()
 })
})

it('requests microphone permission immediately when voice is tapped', async () => {
 const getUserMedia = vi.fn().mockRejectedValue(new DOMException('Permission denied', 'NotAllowedError'))
 Object.defineProperty(window, 'isSecureContext', { configurable: true, value: true })
 Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia } })
 render(<NavigationAssistant />)
 await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
 await userEvent.click(screen.getByRole('button', { name: /Allow microphone & start/ }))
 expect(getUserMedia).toHaveBeenCalledOnce()
 expect(await screen.findByRole('alert')).toHaveTextContent('Microphone permission was denied')
})
