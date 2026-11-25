import './auth-access.css'

export function AuthBrand() {
  return (
    <header className="auth-brand" aria-label="Dhandha Mitra">
      <img className="auth-brand-mark" src="/favicon.svg" alt="" width="44" height="44" />
      <span className="auth-brand-copy">
        <strong>Dhandha Mitra</strong>
        <span>Your business. Better managed.</span>
      </span>
    </header>
  )
}
