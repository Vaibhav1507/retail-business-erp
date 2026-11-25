# Dhandha Mitra — Brand identity and application naming

**Status:** Selected brand direction; implementation in progress  
**Product name:** Dhandha Mitra  
**Tagline:** Your business. Better managed.  
**Engineering codename:** RBOS  
**Repository:** `Vaibhav1507/retail-business-erp`

## Decision

Use **Dhandha Mitra** as the customer-facing product name across the installable app, authentication, browser/PWA metadata, customer-facing help copy and product positioning.

Keep **RBOS** as the internal engineering/project codename where it is embedded in repository names, .NET namespaces, technical identifiers, historical records and infrastructure. Do not describe the product to customers as “RBOS” or “Retail Business OS”.

## Selected visual direction

Use the simpler MagicPath **Mitra Store** storefront mark, not the more detailed generated hand-and-shop illustration. The selected icon is a compact storefront: dark green square, pale mint awning, warm-white shop facade, simple doorway and a small friendly gesture detail. It is intentionally flat and low-detail so it remains legible at launcher/icon sizes.

The SVG source is `web/public/favicon.svg` and the transparent white mark for the green in-app top bar is `web/public/logo-mark.svg`. The corresponding raster outputs are:

- `web/public/icon-192.png`
- `web/public/icon-512.png`
- `web/public/icon-maskable-512.png`
- `web/public/apple-touch-icon.png`

## Brand tokens

| Token | Value | Use |
|---|---|---|
| Primary green | `#087653` | App tile, brand emphasis |
| Deep action green | `#006449` | Primary action in prototype |
| Pale mint | `#BCEFD8` | Awning/accent |
| Warm white | `#F4FFF8` | Storefront/facade |
| App background | `#F5F8F6` | PWA canvas |
| Primary text | `#14231F` | Wordmark/headings |
| Secondary text | `#6C7B73` | Supporting copy |

Use the existing application font stack. Do not add a network font dependency for the product rebrand.

## Usage rules

- Always render the name as **Dhandha Mitra** (title case, with a space).
- Use the tagline **“Your business. Better managed.”** in the shared sign-in/PIN brand header where there is enough room.
- Use the Mitra Store storefront + friendly gesture icon consistently, not the plain Classic Shop mark or a single-letter “R” badge.
- Keep the full name as the PWA `name`, `short_name`, browser title and installation label. Validate the actual Android launcher truncation/wrapping on the pilot device.
- Hindi UI can retain the brand name in Latin script; do not translate or respell the registered product name inconsistently.
- Keep business names, manufacturer brands, customer names and supplier names independent from the product brand.

## Technical compatibility rules

Do not rename any of the following as part of this product-facing rebrand:

- `rbos.*` localStorage/IndexedDB names or offline workspace keys.
- `rbos.auth` authentication cookie.
- `rbos-shell-` service-worker cache prefix, `RBOS_SKIP_WAITING` message and `rbos:*` DOM events.
- WebAuthn relying-party domain, origin, credential identity or deployment hostname.
- API route names, request/response contracts, database schemas, migration names or roles such as `rbos_app` / `rbos_worker`.
- .NET solution/project names and `Rbos.*` namespaces.
- GitHub repository name, Cloud Run service/project names, environment-variable prefixes or secrets.

Changing the visible display name must not sign users out, reset local PINs, delete offline data, invalidate passkeys or alter tenant/business records.

## Rollout and acceptance

1. Update visible app name, logo, browser/PWA metadata and icon assets in one release.
2. Keep the service-worker cache namespace stable; verify the updated worker refreshes its shell and manifest without deleting offline customer data.
3. Check current and already-installed Android PWA behavior, launcher label, icon masking, browser favicon, sign-in and PIN headers.
4. Verify English and Hindi title/copy, passkey display-name fallback and install prompt.
5. Confirm no changes to local storage identifiers, authentication cookie, WebAuthn RP settings, API contracts or tenant data.
6. Treat icon/name display on already-installed Android launchers as a real-device acceptance check; a green web build alone does not prove the launcher refreshed.
