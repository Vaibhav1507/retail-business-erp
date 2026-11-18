import { spawnSync } from 'node:child_process'

const advisory = 'https://github.com/advisories/GHSA-fc5q-39xc-vjpf'
const exceptionPackages = new Set(['picomatch', 'fdir', 'tinyglobby', 'vite', 'vitest', '@vitejs/plugin-react'])
const owner = 'RBOS repository maintainer (Vaibhav1507)'
const expiresOn = '2026-12-02'

if (new Date() > new Date(expiresOn + 'T23:59:59Z')) {
  console.error(`Dependency audit exception expired on ${expiresOn}; review and renew explicitly.`)
  process.exit(1)
}

// Production dependencies must have no high/critical advisories.
const production = spawnSync('npm', ['audit', '--omit=dev', '--audit-level=high'], { stdio: 'inherit' })
if (production.status !== 0) process.exit(production.status ?? 1)

// Inspect the complete tree. Only the documented development-tooling chain is excepted.
const full = spawnSync('npm', ['audit', '--json'], { encoding: 'utf8' })
let report
try {
  report = JSON.parse(full.stdout || '{}')
} catch {
  console.error('npm audit did not return valid JSON.')
  process.exit(1)
}
const findings = Object.entries(report.vulnerabilities ?? {}).filter(([, v]) => ['high', 'critical'].includes(v.severity))
const unexpected = findings.filter(([name, v]) => {
  if (!exceptionPackages.has(name)) return true
  const via = Array.isArray(v.via) ? v.via : []
  return !via.some(x => x === 'picomatch' || (typeof x === 'object' && x.url === advisory))
})
if (unexpected.length) {
  console.error('Unexpected high/critical dependency vulnerabilities:', unexpected.map(([name]) => name).join(', '))
  process.exit(1)
}
if (findings.length) {
  console.warn(`Accepted time-bound development-only exception: ${findings.length} high/critical finding(s) in the documented picomatch chain.`)
  console.warn(`Advisory: ${advisory}; owner: ${owner}; expiry: ${expiresOn}.`)
} else {
  console.log('No high/critical dependency vulnerabilities found; the temporary exception is no longer needed.')
}
