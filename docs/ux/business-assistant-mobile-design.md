# Business Assistant — mobile UX redesign

**Status:** implementation on ux/business-assistant-mobile-redesign; automated visual and real-device acceptance pending.  
**MagicPath prototype:** https://designs.magicpath.ai/v1/glad-city-4489  
**Reference viewport:** 390 × 844 CSS px.

## Current problems

The supplied Android screenshots show an almost full-screen assistant masking the active page, an empty chat with six quick actions, ambiguous first-use model preparation, a press-and-hold microphone that depends on permission and worklet setup timing, successful routes staying hidden behind the open assistant, and a right-anchored panel conflicting with Add Purchase's fixed Review purchase bar.

The live code confirms local Shruti speech recognition and its SHA-256-verified Cache API entry, optional hosted Whisper fallback, constrained navigation intent and the app-shell unsaved-work guard. Those contracts remain unchanged.

## Selected layout

A compact centred navigation sheet with five stable zones:
1. Header, helper line and a 44px close action.
2. Six quick actions arranged in two columns.
3. A scrollable conversation and feedback area.
4. A fixed text/microphone/Go composer.
5. A compact status area for microphone preparation, cache lookup, actual download progress, listening, transcription, errors, retry and privacy disclosure.

Keep Home/Add Purchase visible behind the sheet without a blocking full-screen scrim. Use a bounded panel aligned to the right at desktop widths.

## Voice behaviour and cache

Use tap-to-start/tap-to-stop. Show separate states for starting the microphone, checking the saved model, downloading, listening, transcription, error and ready.

Show determinate progress only after the worker emits a real progress message. Use its existing source/saved metadata:
- source=cache: the cached model is being used.
- source=network and saved=true: the model was downloaded and cached.
- saved=false: this session is ready, but the browser could not retain the model.

Request persistent origin storage on the first microphone gesture where supported. This is best-effort; a browser/device may decline persistence or later clear data. Do not imply that cache eviction is impossible. Do not clear the voice Cache API on logout. Shruti remains primary; the existing optional Whisper fallback and privacy disclosure remain unchanged.

## Navigation and unsaved work

Quick actions, typed commands, voice commands and suggestions share one route helper. The app shell remains authoritative for the unsaved-work confirmation. Accepted navigation closes the assistant and reveals the destination. Declined navigation leaves it open and tells the user that current work is unchanged. The canonical route allowlist remains in force. The assistant still cannot answer business-data questions or execute transactions.

## Add Purchase placement

Centre the sheet on mobile. Place its bottom edge just above the fixed Add Purchase review bar, and move the launcher above the same bar. Account for the global nav height and bottom safe-area inset. On short landscape, compact the panel and make the quick-action row horizontally scrollable so it does not force the sheet outside the viewport. Desktop uses the bounded panel while the Buy review summary remains sticky/in-flow.

## Styling rules

Use existing RBOS theme variables and system font stack; no new UI/font dependency.

| Element | Rule |
|---|---|
| Panel | max 440px, viewport width minus 24px, 23px radius, existing line token and soft shadow |
| Height | min(62dvh, 520px), capped to available visual viewport |
| Header | 15px bold, 12px muted helper; close 44×44px |
| Quick actions | two equal columns; 44px minimum height; 12px radius |
| Conversation | only expanding/scrolling region; 13px message text |
| Composer | 16px input, 48px input/microphone, Go minimum 48px |
| Status | compact; never show a fabricated download percentage |
| Launcher | 52px circle, brand green, safe-area and review-bar clearance |
| Focus | visible 3px info outline, 2px offset |
| Motion | respect reduced-motion |

Responsive profiles: 360, 390 and 412px phones; 768px tablet; 844×390 landscape; 1280px desktop.

## Scope and non-goals

In scope: NavigationAssistant.tsx, navigation-assistant.css, app-shell navigation guard wiring, Add Purchase assistant positioning, voice persistence request and worker metadata feedback, unit/E2E tests and this design contract.

Out of scope: Procurement/OCR/posting/business rules, account/logout semantics, backend endpoints, speech provider order, paid provider enablement, audio privacy rules, LLM allowlist or transaction execution. No backend or database changes.

## Acceptance criteria

- [ ] Compact assistant; Home remains visibly in context.
- [ ] All six shortcuts remain readable and reachable.
- [ ] Tap starts recording; second tap stops; pending microphone startup can be cancelled.
- [ ] Separate microphone startup, model check, actual download, listening and transcription feedback.
- [ ] Cache hits do not show fabricated download percentages.
- [ ] Request persistent storage where supported; disclose failed cache save without promising permanence.
- [ ] Permission denial and worker failure leave typed navigation available with retry.
- [ ] All successful navigation paths close the assistant.
- [ ] Declined protected navigation keeps assistant open and unsaved work safe.
- [ ] Add Purchase assistant stays above the review bar and global nav, including short landscape.
- [ ] No overflow, clipping, fixed-surface collision or unnamed controls.
- [ ] Axe WCAG 2 A/AA passes important states.
- [ ] Android Chrome permission, storage, latency, keyboard and TalkBack are verified on a real device.

## Validation status

The dedicated Playwright test covers the Home viewport matrix, Add Purchase review-bar geometry, typed/suggestion navigation closure, screenshots and Axe WCAG 2 A/AA. CI and Android real-device acceptance remain pending.
