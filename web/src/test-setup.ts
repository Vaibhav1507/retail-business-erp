import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import type { ReactNode } from 'react'

// App-level tests exercise workspace behavior, not the device PIN interaction.
// Dedicated AppLock tests load the real component with vi.importActual.
vi.mock('./AppLock', () => ({
  AppLockGate: ({ children }: { children: ReactNode }) => children,
}))

afterEach(() => cleanup())
