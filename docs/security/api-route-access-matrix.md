# API route access matrix

**Implementation baseline:** 2026-11-17  
**Code source of truth:** `src/Rbos.Api/ApiRouteAccess.cs`  
**Automated inventory:** `tests/Rbos.IntegrationTests/ApiAuthorizationTests.cs`

This document records the route-access contract introduced by the fail-closed authorization implementation. The registry classifies the exact HTTP method and ASP.NET route template, including route parameters and constraints. It does not authorise routes by string prefix.

## Access classes

| Class | Enforcement |
|---|---|
| `Anonymous` | Deliberately public. Existing endpoint-level OTP, registration, anti-enumeration and handler checks remain in force. |
| `AuthenticatedUser` | Requires a valid authenticated Identity user and a non-empty GUID user ID. An active business membership is not required. Used for `/me`, initial `/bootstrap` and logout. |
| `AuthenticatedMember` | Requires a valid authenticated user and an active membership with one of the known RBOS roles. No additional permission is required. |
| `Permission(name)` | Requires a valid authenticated user, active known membership and the named permission in that role's existing permission bundle. |

A missing method/template classification is **not** an authenticated-member exception. It is denied with HTTP 403. Invalid or missing authenticated user identity is denied with HTTP 401. Missing, revoked or unknown-role membership is denied with HTTP 403.

## Explicitly anonymous routes

- `GET /api/csrf`
- `GET /api/auth/config`
- `POST /api/auth/otp/send`
- `POST /api/auth/otp/verify`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/login/otp`
- `GET /api/openapi/{documentName}.json`
- `* /api/health`
- `* /api/{**path}` — the terminal API fallback only returns 404 for unmatched requests; it is outside the API route-group filter.

The WhatsApp provider webhook remains separately mapped at `/webhooks/meta/whatsapp`. Its GET verification-token and POST HMAC signature validation are unchanged; it is not reclassified as a browser-cookie route and does not use the API group's CSRF policy.

## Authenticated-user routes

- `POST /api/auth/logout`
- `GET /api/me`
- `POST /api/bootstrap`

These routes intentionally work before the first tenant is provisioned or after a business membership has been revoked, so a user can complete setup or sign out.

## Authenticated-member exceptions

The following routes require an active known business membership, but no additional role permission. This list intentionally preserves the prior behaviour of known routes for which `Permissions.Required` returned null; it does not make unknown routes permissive.

- Voice navigation/speech support: `GET /api/voice/intent/config`, `POST /api/voice/intent`, `GET /api/voice/transcribe/config`, `POST /api/voice/transcribe`
- Core reads: `GET /api/locations`, `GET /api/stock`, `GET /api/business-profile`
- Catalogue reads: `GET /api/products`, `GET /api/products/master`, `GET /api/categories`, `GET /api/products/suggest-for-barcode`, `GET /api/catalogue/source-documents`, `GET /api/catalogue-sources`, `GET /api/products/by-barcode`, `GET /api/products/{id:guid}/barcodes`
- Procurement and supplier reads: `GET /api/suppliers`, `GET /api/suppliers/{id:guid}/statement`, `GET /api/suppliers/{id:guid}/open-bills`, `GET /api/purchases`, `GET /api/purchases/{id:guid}`, `GET /api/purchases/{id:guid}/invoice`, `GET /api/purchase-returns`, `GET /api/purchase-returns/{id:guid}`, `GET /api/supplier-payments`, `GET /api/purchase-orders`
- Sales/customer reads: `GET /api/customers`, `GET /api/customers/{id:guid}/statement`, `GET /api/customers/{id:guid}/open-sales`, `GET /api/customers/{id:guid}/history`, `GET /api/customers/ageing`, `GET /api/sales`, `GET /api/sales/{id:guid}`, `GET /api/sales/{id:guid}/returnable`, `GET /api/customer-payments`, `GET /api/customer-payments/{id:guid}`, `GET /api/sale-returns`, `GET /api/sale-returns/{id:guid}`, `GET /api/business-name`
- Finance and cash reads retained from the existing permission resolver: `GET /api/accounts`, `GET /api/money-accounts`, `GET /api/money-accounts/{id:guid}/history`, `GET /api/expense-categories`, `GET /api/expenses`, `GET /api/owner-transactions`, `GET /api/loans`, `GET /api/loans/{id:guid}/schedule`, `GET /api/stock-adjustments`, `GET /api/cash-closings`, `GET /api/cash-closings/preview`, `GET /api/period-lock`, `GET /api/reconciliation`
- Stock and setup reads: `GET /api/branches`, `GET /api/stock-transfers`, `GET /api/unit-conversions`, `GET /api/stock-count/sheet`, `GET /api/stock-adjustments/history`, `GET /api/stock/low`, `GET /api/stock/dead`, `GET /api/stock/ageing`
- Reminder and brand reads: `GET /api/reminders`, `GET /api/brands`
- Export audit event: `POST /api/exports/audit` records an export event only; the actual report/export routes remain subject to their own access classification.

## Named permission routes

| Permission | Methods and route templates |
|---|---|
| `settings.manage` | `PUT /api/business-profile`; `PUT /api/period-lock`; `POST /api/branches`; `POST /api/locations`; `POST /api/profile/mobile/send-otp`; `POST /api/profile/mobile/verify` |
| `catalogue.manage` | Product create/update and master-product add; category rename; product import read/preview/commit; barcode add/delete; serial tracking; unit conversion update; reorder-level update; brand create/update |
| `purchases.manage` | Supplier create/update; purchase create/extract; supplier payment create; purchase return create; purchase order create/status update |
| `sales.create` | Sale create; WhatsApp invoice retry |
| `customers.manage` | Customer create/update; customer payment create |
| `sales.cancel` | Sale cancellation |
| `sales.return` | Sale return creation |
| `stock.adjust` | Opening stock posting; stock transfer create; stock count posting; stock adjustment create/history |
| `finance.manage` | Account create/update/activate; money-account create; expense create; owner transaction create; loan create/repayment;  cash closing create; reconciliation clear |
| `reports.view` | Report reads (stock verification, trial balance, ledger, profit/loss, balance sheet, receivable/payable ageing, stock valuation, cash flow, WAC verification and GST summary) and dashboard |
| `audit.view` | Audit search |
| `staff.manage` | Staff role catalogue, staff list, staff create/update |

For the precise method + route-template mapping for each entry, the C# registry is authoritative. The integration route-inventory test enumerates the endpoints produced by the running application and fails when any registered `/api` method lacks a classification.

## Role bundle preservation

This implementation does not add, remove or widen role permissions:

- **Owner:** every currently defined permission.
- **Manager:** sales, customer, catalogue, stock, purchase, reports and audit permissions.
- **Accountant:** finance, reports and audit permissions.
- **Cashier:** sales creation and customer management permissions.

## Explicit policy limitation requiring owner decision

`docs/security/authorization-matrix.md` remains marked **Proposed** and is not silently promoted to the enforced policy by this implementation. In particular, the historic route resolver allowed authenticated-member access to several Finance GET routes (accounts, expenses, owner transactions, loans, cash closings, reconciliation and period-lock reads). Those are now explicit `AuthenticatedMember` rules to preserve current behaviour, but the proposed matrix expects narrower Owner/Accountant access for some finance and ledger data. This discrepancy requires an explicit business-policy decision and a separate tested change; this PR does not claim field-level or complete role-matrix enforcement.

The same distinction applies to stock read payloads: a route-level access classification does not replace field-level response shaping. Review cost, WAC, margin and stock-value exposure as a separate authorization/DTO task before real staff accounts or pilot data are enabled.

## Registration notes

- `BrandEndpoints.Map(api)` is now registered. Brand create/update requires `catalogue.manage`.
- `ReminderEndpoints.Map(api)` was already registered; it is explicitly classified as an authenticated-member route.
- No database migration is required for this route-policy change.
