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

## Multiple authorised credentials

For multiple keys, prefer explicit credential entries and keep secrets in the secret manager. Example shape:

```text
AiGateway__Credentials__0__Provider=Gemini
AiGateway__Credentials__0__Id=gemini-project-a-key-1
AiGateway__Credentials__0__SecretRef=AiSecrets:GeminiProjectAKey1
AiGateway__Credentials__0__ProjectId=<project-a>
AiGateway__Credentials__0__BillingMode=Free
AiGateway__Credentials__0__Enabled=true
```

Use the same `ProjectId` for keys belonging to the same Gemini project. They rotate as credentials but share one quota bucket; a 429 cools down the entire project bucket. Groq keys sharing one team identity share a team bucket. A missing project/team identity collapses into a conservative `unidentified` bucket.

Only configure independent quota buckets when they are genuinely independent, authorised and permitted by provider terms. Never rotate accounts, keys or projects to circumvent rate limits, free-tier restrictions or provider policies.

Round-robin cursors, invalid-key suppression, circuit-breaker state and quota cooldowns are process-local to each API instance. They are thread-safe within that instance but reset on instance restart. The monthly budget ledger is database-backed and serialized across instances.

## Paid provider safeguards

Paid use requires owner approval and explicit `AiGateway:AllowPaidProviders=true`, an enabled provider switch, credential `BillingMode=Paid`, verified positive INR pricing, a calculable estimate and available global/per-tenant budget.

Pricing fields per credential are `InputCostPerMillionInr`, `OutputCostPerMillionInr`, optional `ImageCostPerImageInr` and `AudioCostPerMinuteInr`. Paid text/image/document calls require both input and output token prices; page-based input estimates are used for media. A paid audio call requires an audio-per-minute price. Paid PDF calls are blocked if page count cannot be estimated. Paid calls are blocked if the estimate is missing or non-positive.

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
