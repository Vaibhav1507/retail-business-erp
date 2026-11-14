import { describe, expect, it } from 'vitest'
import { encodeNavigationWav } from './speech-fallback'

describe('navigation WAV encoding', () => {
  it('encodes mono 16 kHz signed 16-bit PCM with a valid RIFF header', async () => {
    const wav = encodeNavigationWav(new Float32Array([-1, -0.5, 0, 0.5, 1]))
    const bytes = new Uint8Array(await wav.arrayBuffer())
    const view = new DataView(bytes.buffer)
    expect(wav.type).toBe('audio/wav')
    expect(bytes.length).toBe(54)
    expect(new TextDecoder().decode(bytes.slice(0, 4))).toBe('RIFF')
    expect(new TextDecoder().decode(bytes.slice(8, 12))).toBe('WAVE')
    expect(view.getUint16(22, true)).toBe(1)
    expect(view.getUint32(24, true)).toBe(16000)
    expect(view.getUint16(34, true)).toBe(16)
    expect(view.getInt16(44, true)).toBe(-32768)
    expect(view.getInt16(52, true)).toBe(32767)
  })

  it('rejects empty and over-20-second captures', () => {
    expect(() => encodeNavigationWav(new Float32Array())).toThrow()
    expect(() => encodeNavigationWav(new Float32Array(16000 * 20 + 1))).toThrow()
  })
})
