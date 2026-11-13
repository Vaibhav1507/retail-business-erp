import { describe, expect, it } from 'vitest'
import { resampleAudio } from './audio-utils'

function rms(samples: Float32Array, skip = 0): number {
 const usable = samples.subarray(skip, samples.length - skip)
 const meanSquare = usable.reduce((sum, sample) => sum + sample * sample, 0) / usable.length
 return Math.sqrt(meanSquare)
}

function sine(frequency: number, sampleRate: number, seconds = 1): Float32Array {
 return Float32Array.from({ length: sampleRate * seconds }, (_, index) => Math.sin(2 * Math.PI * frequency * index / sampleRate))
}

describe('resampleAudio', () => {
 it('preserves samples when the source is already at the target rate', () => {
  const input = Float32Array.from([0, 0.25, -0.5, 1])
  const output = resampleAudio(input, 16000)
  expect(output).toEqual(input)
  expect(output).not.toBe(input)
 })

 it('converts 48 kHz audio to 16 kHz with the expected duration', () => {
  const output = resampleAudio(sine(1000, 48000), 48000)
  expect(output).toHaveLength(16000)
  expect(rms(output, 64)).toBeGreaterThan(0.6)
 })

 it('suppresses frequencies above the 16 kHz output Nyquist limit when downsampling', () => {
  const output = resampleAudio(sine(12000, 48000), 48000)
  expect(rms(output, 64)).toBeLessThan(0.08)
 })

 it('rejects invalid sample rates', () => {
  expect(() => resampleAudio(new Float32Array([0]), 0)).toThrow(RangeError)
 })
})
