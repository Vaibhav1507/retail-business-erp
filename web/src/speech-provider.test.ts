import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  createSpeechWorker,
  requestPersistentVoiceStorage,
  DEFAULT_SPEECH_PROVIDER,
  getSpeechProvider,
} from './speech-provider'

describe('speech provider registry', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('defaults to the existing local Shruti runtime', () => {
    expect(DEFAULT_SPEECH_PROVIDER).toBe('shruti-local')
    expect(getSpeechProvider()).toMatchObject({
      id: 'shruti-local',
      execution: 'on-device',
      workerUrl: '/shruti-worker.js',
      modelDownloadBytes: 29_546_527,
    })
  })

  it('requests persistent origin storage when supported', async () => {
    const descriptor = Object.getOwnPropertyDescriptor(navigator, 'storage')
    const persist = vi.fn().mockResolvedValue(true)
    Object.defineProperty(navigator, 'storage', { configurable: true, value: { persist } })
    try {
      await expect(requestPersistentVoiceStorage()).resolves.toBe(true)
      expect(persist).toHaveBeenCalledOnce()
    } finally {
      if (descriptor) Object.defineProperty(navigator, 'storage', descriptor)
      else Reflect.deleteProperty(navigator, 'storage')
    }
  })

  it('treats unavailable persistent storage as a recoverable browser limitation', async () => {
    const descriptor = Object.getOwnPropertyDescriptor(navigator, 'storage')
    Object.defineProperty(navigator, 'storage', { configurable: true, value: {} })
    try {
      await expect(requestPersistentVoiceStorage()).resolves.toBe(false)
    } finally {
      if (descriptor) Object.defineProperty(navigator, 'storage', descriptor)
      else Reflect.deleteProperty(navigator, 'storage')
    }
  })

  it('creates the selected provider worker without UI-owned runtime paths', () => {
    const worker = { terminate: vi.fn() }
    const WorkerMock = vi.fn(function (this: unknown) { return worker })
    vi.stubGlobal('Worker', WorkerMock)

    expect(createSpeechWorker()).toBe(worker)
    expect(WorkerMock).toHaveBeenCalledOnce()
    expect(WorkerMock).toHaveBeenCalledWith('/shruti-worker.js')
  })
})
