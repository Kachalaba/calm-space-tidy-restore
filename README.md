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

- EditMode tests: 47 passed, 0 failed.
- PlayMode tests: 13 passed, 0 failed. Coverage includes all six Addressable
  levels, English/Ukrainian switching, compatible-slot sorting, cancellation,
  slot occupancy, a full touchscreen-to-physics drag through the generated
  level prefab, and the final completion-screen state.
- Android APK and App Bundle: validated, ARM64 + IL2CPP, API 24 minimum and
  target API 36.
- Android API 35 ARM64 emulator: cold launch, theme persistence, sequential
  progression, all five snap levels, the full cleaning level, and 6/6
  completion verified without runtime exceptions.
- Pixel 8: final APK install and launch, automatic Ukrainian locale, portrait
  safe areas, menu, level selection, reverse-order compatible sorting, and
  level completion verified without Unity or Android runtime errors. Native
  `USAGE_TOUCH` haptics were physically verified on the preceding interaction
  build; the haptic bridge is unchanged.

See `docs/PLAY_STORE_RELEASE.md` for signing, monetization, privacy, and Play
Console preparation.

## Implemented foundation

- Production-style portrait home screen, level selection, safe-area handling,
  local progression, completion flow, and three persistent color moods.
- Six demo spaces: Soft Blocks, Pebble Pairs, Tea Drawer, Color Shelf,
  Fastener Tray, and Fresh Surface.
- Persistent English/Ukrainian localization with Ukrainian auto-selection from
  the Android system locale.
- Forgiving compatible-slot sorting in Pebble Pairs and Color Shelf: matching
  objects can fill either free slot in their category, with no timer, penalty,
  or fail state.
- Original 32-second procedural ambient loop generated inside the project,
  with no third-party samples, plus an in-game sound toggle.
- Native Android micro-haptics and snap waveforms with API-level fallbacks.
- Touch and mouse dragging through the New Input System, including pinch
  tracking and exclusive pointer ownership.
- ScriptableObject level catalog with Addressable level prefabs.
- Sorting, cleaning, screw-puzzle, and fitting lifecycle components.
- URP RenderTexture cleaning mask with asynchronous GPU readback and a
  CPU-safe fallback.
- Spatialized pooled ASMR snap audio through an AudioMixer.
- VContainer composition root and UniTask asynchronous flows.
- Ad/IAP policy layer with a 180-second interstitial cooldown, atomic gameplay
  and drag gates, rewarded callbacks, and authenticated local entitlement
  storage.
- EditMode unit tests and PlayMode runtime/startup tests.

## Before a store release

The monetization policy is complete, but the included ad and purchase
providers are deliberately no-op adapters. A real Google Play Billing provider
and selected ad SDK still need to be connected and tested on devices.

Also required for publishing: final icons/screenshots, privacy policy and Data
Safety declarations, production signing, broader device QA, performance
profiling, and a closed-track Google Play test.

## Review and asset provenance

Start with [`docs/LLM_REVIEW_GUIDE.md`](docs/LLM_REVIEW_GUIDE.md) for a focused
architecture, mobile UX, game-design, and release-readiness review. Asset
origins are documented in
[`docs/ASSET_PROVENANCE.md`](docs/ASSET_PROVENANCE.md).

The repository is public for inspection, but no open-source or asset-reuse
license is granted unless a separate `LICENSE` file is added.
