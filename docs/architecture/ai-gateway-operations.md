# Central AI Gateway — operator configuration and usage

**Status:** PR A draft; configuration examples only. The gateway is disabled by default and this document does not authorise production activation.

## Default safety posture

The checked-in `appsettings.json` deliberately sets the gateway, Gemini and Groq provider switches to false. Paid providers, hosted Whisper, voice intent and Groq invoice fallback also remain disabled by default.

Do not turn these on merely because credentials exist. Confirm provider billing tier, project/team identity, model availability, terms, privacy and pilot approval first. Unknown billing mode is intentionally ineligible.

## One explicitly verified free Gemini credential

Configure secrets through the deployment secret manager/environment, never in source control:

```text
AiGateway__Enabled=true
AiGateway__Providers__Gemini__Enabled=true
AiGateway__LegacyGeminiBillingMode=Free
Gemini__ProjectId=<verified Google Cloud project id>
Gemini__ApiKey=<secret-manager-injected key>
AiGateway__AllowPaidProviders=false
```

Set `LegacyGeminiBillingMode=Free` only after confirming the project/key is on the provider's free billing tier and the intended model is eligible. The code does not infer this from the key.

## One explicitly verified free Groq credential

```text
AiGateway__Enabled=true
AiGateway__Providers__Groq__Enabled=true
AiGateway__LegacyGroqBillingMode=Free
Groq__TeamId=<verified Groq team identity>
Groq__ApiKey=<secret-manager-injected key>
AiGateway__AllowPaidProviders=false
```

Keep task-level flags disabled unless that exact feature has been reviewed and approved: `Groq__InvoiceFallbackEnabled`, `Groq__VoiceIntentEnabled`, `VoiceIntent__Enabled`, `Gemini__VoiceIntentEnabled` and `Groq__WhisperEnabled`.

## Multiple authorised credentials — configuration only

The gateway reads an indexed credential list at startup. Add, remove, enable or disable entries in deployment configuration or the secret manager; **no application code change is required**. Restart/redeploy the API after changing the list because the credential pool is loaded once when the singleton is created.

Keep API keys in the secret manager. `SecretRef` is a configuration key path, not the secret itself. For example, inject `AiSecrets__GeminiA` or `AiSecrets__GroqA` as secret environment variables and point the corresponding entry at `AiSecrets:GeminiA` or `AiSecrets:GroqA`.

Example with five Gemini credentials and five Groq credentials (repeat the indexed pattern for as many authorised credentials as needed):

```text
AiGateway__Enabled=true
AiGateway__AllowPaidProviders=false
AiGateway__Providers__Gemini__Enabled=true
AiGateway__Providers__Groq__Enabled=true

AiGateway__Credentials__0__Provider=Gemini
AiGateway__Credentials__0__Id=gemini-a
AiGateway__Credentials__0__SecretRef=AiSecrets:GeminiA
AiGateway__Credentials__0__ProjectId=<google-cloud-project-a>
AiGateway__Credentials__0__BillingMode=Free
AiGateway__Credentials__0__Enabled=true

AiGateway__Credentials__1__Provider=Gemini
AiGateway__Credentials__1__Id=gemini-b
AiGateway__Credentials__1__SecretRef=AiSecrets:GeminiB
AiGateway__Credentials__1__ProjectId=<google-cloud-project-b>
AiGateway__Credentials__1__BillingMode=Free
AiGateway__Credentials__1__Enabled=true

AiGateway__Credentials__2__Provider=Gemini
AiGateway__Credentials__2__Id=gemini-c
AiGateway__Credentials__2__SecretRef=AiSecrets:GeminiC
AiGateway__Credentials__2__ProjectId=<google-cloud-project-c>
AiGateway__Credentials__2__BillingMode=Free
AiGateway__Credentials__2__Enabled=true

AiGateway__Credentials__3__Provider=Gemini
AiGateway__Credentials__3__Id=gemini-d
AiGateway__Credentials__3__SecretRef=AiSecrets:GeminiD
AiGateway__Credentials__3__ProjectId=<google-cloud-project-d>
AiGateway__Credentials__3__BillingMode=Free
AiGateway__Credentials__3__Enabled=true

AiGateway__Credentials__4__Provider=Gemini
AiGateway__Credentials__4__Id=gemini-e
AiGateway__Credentials__4__SecretRef=AiSecrets:GeminiE
AiGateway__Credentials__4__ProjectId=<google-cloud-project-e>
AiGateway__Credentials__4__BillingMode=Free
AiGateway__Credentials__4__Enabled=true

AiGateway__Credentials__5__Provider=Groq
AiGateway__Credentials__5__Id=groq-a
AiGateway__Credentials__5__SecretRef=AiSecrets:GroqA
AiGateway__Credentials__5__TeamId=<groq-team-a>
AiGateway__Credentials__5__BillingMode=Free
AiGateway__Credentials__5__Enabled=true

AiGateway__Credentials__6__Provider=Groq
AiGateway__Credentials__6__Id=groq-b
AiGateway__Credentials__6__SecretRef=AiSecrets:GroqB
AiGateway__Credentials__6__TeamId=<groq-team-b>
AiGateway__Credentials__6__BillingMode=Free
AiGateway__Credentials__6__Enabled=true

AiGateway__Credentials__7__Provider=Groq
AiGateway__Credentials__7__Id=groq-c
AiGateway__Credentials__7__SecretRef=AiSecrets:GroqC
AiGateway__Credentials__7__TeamId=<groq-team-c>
AiGateway__Credentials__7__BillingMode=Free
AiGateway__Credentials__7__Enabled=true

AiGateway__Credentials__8__Provider=Groq
AiGateway__Credentials__8__Id=groq-d
AiGateway__Credentials__8__SecretRef=AiSecrets:GroqD
AiGateway__Credentials__8__TeamId=<groq-team-d>
AiGateway__Credentials__8__BillingMode=Free
AiGateway__Credentials__8__Enabled=true

AiGateway__Credentials__9__Provider=Groq
AiGateway__Credentials__9__Id=groq-e
AiGateway__Credentials__9__SecretRef=AiSecrets:GroqE
AiGateway__Credentials__9__TeamId=<groq-team-e>
AiGateway__Credentials__9__BillingMode=Free
AiGateway__Credentials__9__Enabled=true

# Secret-manager-injected values; never commit actual API keys:
AiSecrets__GeminiA=<gemini-key-a>
AiSecrets__GeminiB=<gemini-key-b>
AiSecrets__GeminiC=<gemini-key-c>
AiSecrets__GeminiD=<gemini-key-d>
AiSecrets__GeminiE=<gemini-key-e>
AiSecrets__GroqA=<groq-key-a>
AiSecrets__GroqB=<groq-key-b>
AiSecrets__GroqC=<groq-key-c>
AiSecrets__GroqD=<groq-key-d>
AiSecrets__GroqE=<groq-key-e>
```

The numbered list can mix providers; indexes should be unique and contiguous for operational clarity. Every entry has its own `Id`, `SecretRef`, `Enabled`, `BillingMode`, and provider quota identity (`ProjectId` for Gemini, `TeamId` for Groq). Optional `Capabilities` and `Models` restrict what an entry may serve.

**Rotation behaviour:** for each gateway request, the starting eligible credential advances round-robin. If a credential fails, the gateway tries other eligible credentials up to the configured per-provider attempt limit. Disabled, invalid, cooling-down, unsupported-model and ineligible billing entries are skipped. Rotation state and cooldowns are process-local; they reset on API restart.

**Important quota distinction:** a Gmail address is not a quota bucket. Gemini keys created in the same Google Cloud project share that project's quota, even if the keys were created while signed into different Google accounts. Separate `ProjectId` values are only treated as separate buckets when those projects really have independent quotas and their use is authorised under Google's terms. Likewise, Groq keys under the same `TeamId` share the team quota; different team IDs are treated as separate buckets only where they represent legitimately independent teams/entitlements. This mechanism is for legitimate key management, availability and failover—not for rotating accounts/projects to evade provider rate limits, free-tier restrictions or terms. It does not guarantee that creating more accounts/projects grants more free usage.

Missing project/team identity is collapsed into a conservative `unidentified` bucket. A 429 cools down every key in the same quota bucket while independent eligible buckets remain available.

## Paid provider safeguards

Paid use requires owner approval and explicit `AiGateway:AllowPaidProviders=true`, an enabled provider switch, credential `BillingMode=Paid`, verified positive INR pricing, a calculable estimate and available global/per-tenant budget.

Pricing fields per credential are `InputCostPerMillionInr`, `OutputCostPerMillionInr`, optional `ImageCostPerImageInr` and `AudioCostPerMinuteInr`. Paid text/image/document calls require both input and output token prices; page-based input estimates are used for media, with a 25% cost-estimate contingency before reservation. A paid audio call requires an audio-per-minute price. Paid PDF calls are blocked if page count cannot be estimated. Paid calls are blocked if the estimate is missing or non-positive.

Defaults are ₹1,500 global/month and ₹50 per tenant/month, with a 75% warning threshold. These are hard ceilings, not a spend target. No automatic top-ups or billing upgrades exist. Keep paid use disabled for the free-first pilot.

## Usage and budget operator queries

Run these with the migration/operations role, not the application runtime role. The `rbos_app` role has no direct SELECT access to the usage table; it can only execute tenant-context-checked reservation/settlement functions.

Daily usage by feature/provider/model:

```sql
select date_trunc('day', created_at) as day,
       feature, provider, model, outcome,
       count(*) as attempts,
       sum(coalesce(actual_cost_inr, estimated_cost_inr)) as estimated_or_actual_inr,
       avg(latency_ms) as average_latency_ms
from ai_gateway.usage_event
where created_at >= date_trunc('month', now())
group by 1, 2, 3, 4, 5
order by 1 desc, 2, 3, 4, 5;
```

Per-tenant month-to-date usage:

```sql
select tenant_id,
       sum(coalesce(actual_cost_inr, estimated_cost_inr)) as estimated_or_actual_inr,
       count(*) as attempts
from ai_gateway.usage_event
where created_at >= date_trunc('month', now())
group by tenant_id
order by estimated_or_actual_inr desc;
```

Reserved or failed attempts with no actual cost continue to count at their estimated cost. This is intentionally conservative: a failed network response may still have incurred provider usage. The ledger stores no prompt, document bytes, transcript, model response or API secret.

## Feature behaviour and recovery

- Invoice extraction returns a reviewable draft; no purchase is posted.
- Catalogue extraction returns rows for preview/import mapping. Local OCR and manual mapping remain recovery paths.
- Voice intent can only select allowlisted destinations. It cannot answer business-data questions or post transactions.
- Shruti remains the local-first speech recognizer. Hosted Whisper is a separately flagged fallback.
- If the gateway or every provider is unavailable, return the existing manual/local recovery path; do not block billing, inventory or accounting.


## Invoice item-tag photo extraction (PR B)

The cashier photo workflow calls only the Central AI Gateway with feature name `InvoiceItemTagExtraction`, capability `ImageUnderstanding` and structured output. It does not call Gemini/Groq directly. The provider order is Gemini first; Groq fallback is separately controlled by `Groq__ItemTagFallbackEnabled` and remains false in checked-in configuration.

The feature flag is `AiGateway__Features__InvoiceItemTagExtraction__Enabled`. This flag alone does not activate AI: `AiGateway__Enabled`, provider switches, an eligible image-capable credential/model and the relevant budget reservation must also permit the call. The checked-in gateway and both providers remain disabled, and `AiGateway__AllowPaidProviders=false`. Never add keys to source control or enable paid providers as part of PR B.

If no provider is eligible, the capture remains saved with a manual-review status. Billing continues without AI. The extraction request treats the photograph as untrusted data, separates product-level tag fields from unit serial/IMEI values, and explicitly forbids inferring selling price, discount or tax.
