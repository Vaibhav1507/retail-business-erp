import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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
  expect(screen.getByRole('button', { name: /Hold to talk/ })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /Set up voice/ })).not.toBeInTheDocument()
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
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), '{Enter}')
  await waitFor(() => expect(window.location.hash).toBe('#/sell/bill'))
 })
 it('asks the user to choose when a command is ambiguous', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), 'return')
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), '{Enter}')
  expect(screen.getByText('Which screen do you mean?')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Customer return' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Return to supplier' })).toBeInTheDocument()
 })
 it('rejects business-data questions without navigating', async () => {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), 'how much cash do I have')
  await userEvent.type(screen.getByLabelText('Ask your business assistant'), '{Enter}')
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


afterEach(() => { vi.unstubAllGlobals() })

class MockSpeechWorker {
 static instance: MockSpeechWorker | null = null
 onmessage: ((event: MessageEvent) => void) | null = null
 onerror: ((event: ErrorEvent) => void) | null = null
 messages: Array<{type:string; pcm?:Float32Array}> = []
 constructor() { MockSpeechWorker.instance = this }
 postMessage(message: {type:string; pcm?:Float32Array}) {
  this.messages.push(message)
  if (message.type === 'init') queueMicrotask(() => this.emit({type:'ready'}))
 }
 emit(data: Record<string, unknown>) { this.onmessage?.({data} as MessageEvent) }
 terminate() {}
}

class MockAudioContext {
 sampleRate = 16000
 state: AudioContextState = 'running'
 destination = {}
 audioWorklet = { addModule: vi.fn().mockResolvedValue(undefined) }
 resume = vi.fn().mockResolvedValue(undefined)
 createMediaStreamSource = vi.fn(() => ({connect:vi.fn()}))
 createGain = vi.fn(() => ({gain:{value:1},connect:vi.fn()}))
 close = vi.fn(async () => { this.state = 'closed' })
}

class MockAudioWorkletNode {
 static instance: MockAudioWorkletNode | null = null
 port: {onmessage:((event:MessageEvent)=>void)|null; postMessage:(message:unknown)=>void}
 connect = vi.fn()
 disconnect = vi.fn()
 constructor() {
  MockAudioWorkletNode.instance = this
  this.port = {
   onmessage:null,
   postMessage:(message:unknown) => {
    if (message === 'stop') this.port.onmessage?.({data:{type:'flushed'}} as MessageEvent)
   }
  }
 }
 emitAudio(samples: Float32Array) { this.port.onmessage?.({data:samples} as MessageEvent) }
}

function installVoiceMocks(getUserMedia: ReturnType<typeof vi.fn>) {
 const mediaDescriptor = Object.getOwnPropertyDescriptor(navigator, 'mediaDevices')
 const secureDescriptor = Object.getOwnPropertyDescriptor(window, 'isSecureContext')
 Object.defineProperty(navigator, 'mediaDevices', {configurable:true, value:{getUserMedia}})
 Object.defineProperty(window, 'isSecureContext', {configurable:true, value:true})
 vi.stubGlobal('Worker', MockSpeechWorker)
 vi.stubGlobal('AudioContext', MockAudioContext)
 vi.stubGlobal('AudioWorkletNode', MockAudioWorkletNode)
 return () => {
  if (mediaDescriptor) Object.defineProperty(navigator, 'mediaDevices', mediaDescriptor)
  else delete (navigator as {mediaDevices?: MediaDevices}).mediaDevices
  if (secureDescriptor) Object.defineProperty(window, 'isSecureContext', secureDescriptor)
  else delete (window as {isSecureContext?: boolean}).isSecureContext
 }
}

it('updates suggestions as an incomplete command is typed without navigating', async () => {
 render(<NavigationAssistant />)
 await userEvent.click(screen.getByRole('button', {name:'Open assistant'}))
 const input = screen.getByLabelText('Ask your business assistant')
 await userEvent.type(input, 'mujhe Ramesh ji ka purana bil')
 expect(screen.getByRole('button', {name:'Old sale bills'})).toBeInTheDocument()
 expect(window.location.hash).toBe('')
})

it('starts microphone capture on the first press-and-hold and auto-submits the final transcript', async () => {
 const track = {stop:vi.fn()}
 const stream = {getTracks:() => [track]} as unknown as MediaStream
 const getUserMedia = vi.fn().mockResolvedValue(stream)
 const restore = installVoiceMocks(getUserMedia)
 try {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', {name:'Open assistant'}))
  const mic = screen.getByRole('button', {name:'Hold to talk'})
  fireEvent.pointerDown(mic, {pointerId:7, button:0})
  await waitFor(() => expect(getUserMedia).toHaveBeenCalledOnce())
  expect(await screen.findByText('Listening… release to send')).toBeInTheDocument()
  expect(screen.queryByRole('button', {name:/Set up voice/})).not.toBeInTheDocument()

  // A partial Shruti result is allowed to surface a likely screen, but must never navigate.
  MockSpeechWorker.instance?.emit({
   type:'partial-result',
   captureId:1,
   text:'mujhe Ramesh ji ka purana bil'
  })
  await waitFor(() => expect(screen.getByRole('button', {name:'Old sale bills'})).toBeInTheDocument())
  expect(window.location.hash).toBe('')

  MockAudioWorkletNode.instance?.emitAudio(new Float32Array(16000).fill(0.05))
  await new Promise(resolve => setTimeout(resolve, 450))
  fireEvent.pointerUp(mic, {pointerId:7, button:0})
  await waitFor(() => expect(MockSpeechWorker.instance?.messages.some(item => item.type === 'transcribe')).toBe(true))

  MockSpeechWorker.instance?.emit({type:'result', text:'make a new bill'})
  await waitFor(() => expect(window.location.hash).toBe('#/sell/bill'))
  expect(screen.getByText('make a new bill')).toBeInTheDocument()
  expect(screen.getByText('Opening New sale bill.')).toBeInTheDocument()
  expect((screen.getByLabelText('Ask your business assistant') as HTMLInputElement).value).toBe('')
 } finally {
  restore()
 }
})

it('reports a denied microphone permission from the first press-and-hold', async () => {
 const getUserMedia = vi.fn().mockRejectedValue(new DOMException('Permission denied', 'NotAllowedError'))
 const restore = installVoiceMocks(getUserMedia)
 try {
  render(<NavigationAssistant />)
  await userEvent.click(screen.getByRole('button', {name:'Open assistant'}))
  fireEvent.pointerDown(screen.getByRole('button', {name:'Hold to talk'}), {pointerId:2, button:0})
  expect(getUserMedia).toHaveBeenCalledOnce()
  expect(await screen.findByRole('alert')).toHaveTextContent('Microphone access was denied')
 } finally {
  restore()
 }
})
