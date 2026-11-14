import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  createSpeechWorker,
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

  it('creates the selected provider worker without UI-owned runtime paths', () => {
    const worker = { terminate: vi.fn() }
    const WorkerMock = vi.fn(function (this: unknown) { return worker })
    vi.stubGlobal('Worker', WorkerMock)

    expect(createSpeechWorker()).toBe(worker)
    expect(WorkerMock).toHaveBeenCalledOnce()
    expect(WorkerMock).toHaveBeenCalledWith('/shruti-worker.js')
  })
})
