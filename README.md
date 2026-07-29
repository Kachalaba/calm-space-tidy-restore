# Calm Space: Tidy & Restore

Android hybrid-casual antistress game demo built with Unity 6000.0.80f1.
The project uses one persistent scene and loads gameplay prefabs through
Addressables.

Public repository:
[Kachalaba/calm-space-tidy-restore](https://github.com/Kachalaba/calm-space-tidy-restore)

## Open and play

1. Open this folder from Unity Hub.
2. Open `Assets/CalmSpace/Scenes/Main.unity`.
3. Press Play.
4. Choose a mood and start from the home screen or level selection. The
   language button switches between English and Ukrainian.

The menu command `Calm Space > Configure Project` can safely regenerate the
demo scene and generated assets. It preserves any custom Android keystore
configuration.

## Android build

For a directly installable development APK, use
`Calm Space > Development > Build Android Test APK`. The file is written to:

`Builds/Android/CalmSpace-Demo-debug.apk`

Use `Calm Space > Development > Build Android Test App Bundle` while Android
is the active build target. The test bundle is written to:

`Builds/Android/CalmSpace-Tidy-Restore.aab`

The current local bundle uses the default development signing setup. For a
store build, configure the Play App Signing upload key in Unity and use
`Calm Space > Release > Build Google Play App Bundle`. This release command
fails closed when the keystore, alias, passwords, ARM64, IL2CPP, or version
settings are invalid. It writes:

`Builds/Android/CalmSpace-Tidy-Restore-release.aab`

## Verified baseline

- EditMode tests: 109 passed, 0 failed.
- PlayMode tests: 35 passed, 0 failed. Coverage includes all eight Addressable
  levels, English/Ukrainian switching, compatible-slot sorting, cancellation,
  slot occupancy, a full touchscreen-to-physics drag through the generated
  level prefab, hold-to-unscrew with panel release, cleaning-stage
  sequencing and rollback, feedback-failure resilience, platform haptic
  selection and rate limiting, encrypted profile recovery, rewarded-only
  monetization gates, transition-curtain cancellation, the localized screw
  instruction, the final completion-screen state, the contained home
  collection artwork, exact single-item decoration purchases, and persistence
  across scene reloads.
- Android APK and App Bundle: validated, ARM64 + IL2CPP, API 24 minimum and
  target API 36.
- Android API 35 ARM64 emulator: cold launch, theme persistence, sequential
  progression, all five snap levels, the full cleaning level, and 6/6
  completion verified without runtime exceptions.
- Pixel 8: the eight-level gameplay set has been played end to end across the
  current implementation cycle. Automatic Ukrainian locale, portrait safe
  areas, the home/decor flow, eight-card level select, compatible sorting,
  hold-to-unscrew, layered reveal, and debris → sponge → squeegee sequencing
  were exercised without Unity or Android runtime crashes. The current APK
  was also cold-started, backgrounded/resumed, and checked through a mixed
  cleaning placement → Undo flow; the item and 17% progress both returned to
  their exact prior state. A gameplay memory snapshot measured 294 MB PSS on
  Android 17 / API 37. Native `USAGE_TOUCH` haptics were physically verified
  on the device.

See `docs/PLAY_STORE_RELEASE.md` for signing, monetization, privacy, and Play
Console preparation.

## Implemented foundation

- Production-style portrait home screen, level selection, safe-area handling,
  local progression, completion flow, and three persistent color moods.
- Illustrated home screen with a contained four-item calm collection and
  distinct Fern, Stones, Lantern, and Vase silhouettes. First completion of
  each level grants 15 Cozy Tokens exactly once; tokens can unlock River
  Stones, Warm Lantern, and Clay Vase, while Soft Fern is included. Existing
  version-1 progress migrates automatically and receives any earned rewards
  only once.
- Eight demo spaces: Soft Blocks, Pebble Pairs, Tea Drawer, Color Shelf,
  Fastener Tray, Fresh Surface, Cabinet Hinge, and Dusty Window.
- Hold-to-unscrew fasteners: a screw turns only while a finger rests on it,
  ticks once per whole turn, and never rewinds when the finger lifts. A panel
  keeps its colliders off until its last screw is out, so taps fall through to
  the screws and the panel cannot be dragged early. Placing a freed panel
  uncovers the layer beneath it.
- Multi-pass cleaning: a level can run an ordered set of stages, each with its
  own mask, brush profile, and implement. Debris is cleared by hand before the
  implement applies, finished passes stay on screen, and the HUD reports the
  active tool, stage, and coverage.
- Persistent English/Ukrainian localization with Ukrainian auto-selection from
  the Android system locale.
- Forgiving compatible-slot sorting in Pebble Pairs and Color Shelf: matching
  objects can fill either free slot in their category, with no timer, penalty,
  or fail state.
- Original 32-second procedural ambient loop generated inside the project,
  with no third-party samples, plus an in-game sound toggle.
- Native Android micro-haptics and snap waveforms plus iOS impact feedback,
  with shared rate limiting and graceful platform fallbacks.
- Touch and mouse dragging through the New Input System, including pinch
  tracking, exclusive pointer ownership, and an unbounded per-level Undo
  history for placements and screw holds.
- ScriptableObject level catalog with Addressable level prefabs.
- Sorting, cleaning, screw-puzzle, and fitting lifecycle components. Levels
  report progress across every kind of work they contain, not just placed
  items.
- URP RenderTexture cleaning mask with a 64×64 coverage pass, frame-skipped
  asynchronous GPU readback, and a CPU-safe fallback.
- Spatialized pooled ASMR snap audio through an AudioMixer.
- VContainer composition root and UniTask asynchronous flows.
- Rewarded-only ad/IAP policy with no interstitial API, atomic gameplay and
  drag gates, explicit hint/decor rewards, Relax Pass state, and
  authenticated local entitlement storage.
- Authenticated encrypted profile storage for level unlocks, Cozy Tokens,
  themes, music, and room inventory, including one-time migration and
  tamper-safe backup recovery.
- Build-time Anti-Anxiety validation that rejects timers, fail/lose
  contracts, forced-ad formats, missing Undo integration, and expensive
  cleaner coverage settings.
- EditMode unit tests and PlayMode runtime/startup tests.

## Before a store release

The monetization policy is complete, but the included rewarded-ad and Relax
Pass purchase providers are deliberately no-op adapters. A real Google Play
Billing provider and selected rewarded-ad SDK still need to be connected and
tested on devices. The architecture intentionally has no forced or
interstitial ad path.

Also required for publishing: final icons/screenshots, privacy policy and Data
Safety declarations, production signing, broader device QA, performance
profiling, and a closed-track Google Play test. The iOS native bridge is
implemented behind compile guards, but still requires an Xcode export and
physical iPhone validation before an iOS release.

## Review and asset provenance

Start with [`docs/LLM_REVIEW_GUIDE.md`](docs/LLM_REVIEW_GUIDE.md) for a focused
architecture, mobile UX, game-design, and release-readiness review. Asset
origins are documented in
[`docs/ASSET_PROVENANCE.md`](docs/ASSET_PROVENANCE.md).

The repository is public for inspection, but no open-source or asset-reuse
license is granted unless a separate `LICENSE` file is added.
