# Calm Space — Google Play release checklist

## 1. Lock permanent identifiers

- Confirm package ID: `com.calmspace.tidyrestore`.
- Confirm public game name and developer name.
- Keep `versionName` human-readable, for example `1.0.0`.
- Increase `versionCode` for every Play Console upload.

Package ID and signing decisions must be final before the first store release.

## 2. Configure Play App Signing

Recommended setup:

- Let Google Play manage the app-signing key.
- Create a separate RSA upload key named `calmspace-upload`.
- Store the keystore outside this project.
- Store both passwords only in a password manager.
- Keep two encrypted backups in separate locations.
- Never commit `.jks`, `.keystore`, passwords, or signing property files.

In Unity, configure the upload keystore and use:

`Calm Space > Release > Build Google Play App Bundle`

The release command fails closed when signing, ARM64, IL2CPP, or version
settings are incomplete.

## 3. Configure lifetime Relax Pass

Recommended first product:

- Product ID: `relax_pass_lifetime`
- Type: non-consumable
- Entitlement: suppress every rewarded-video entry point for the owner.
- Hint and room-decor UI should grant the equivalent optional benefit directly
  when Relax Pass is active.

Use Unity IAP and test purchases through a Google Play Internal Testing build.
The Pixel owner's Google account must be added to both Internal Testing and
License Testing.

Do not treat a store/network error as proof that the user does not own Relax
Pass.
Production entitlement verification should validate the purchase token on a
trusted backend; never ship a Play service-account key inside the application.

## 4. Configure ads and privacy

Recommended first integration: direct AdMob without mediation.

Required placements:

- Rewarded hint: `rewarded-hint`
- Rewarded room decor: `rewarded-room-decor`

Do not create interstitial, app-open, banner, or automatic placements. Calm
Space exposes no forced-ad API by design.

Development builds must use Google's official test ad units. Production ad
unit IDs belong in environment-specific configuration, not source code.

Before requesting ads:

- Run the Google UMP consent update on every launch.
- Show the consent form when required.
- Request ads only when UMP reports that ads can be requested.
- Apply the final age-audience and child-directed-treatment settings.
- Keep rewarded video behind an explicit player tap and the existing
  gameplay/drag activity gates.

## 5. Complete Play Console setup

- Create the game under the confirmed package ID.
- Complete developer identity and payments profile.
- Add the privacy-policy URL.
- Complete Data Safety using the final SDK list.
- Complete content rating, target audience, ads declaration, and app-access
  declarations.
- Add store icon, feature graphic, phone screenshots, short description, and
  full description.
- Upload the signed AAB to Internal Testing first.
- Test install, update, purchase, restore, both rewarded placements, consent,
  offline startup, pause/resume, and process restart.
- Promote to a closed track only after the internal checklist passes.

## 6. Device baseline already verified

Pixel 8, Android 17 / API 37, ARM64:

- Cold launch and Addressable level load.
- Three drag and snap interactions.
- Native drag ticks and snap waveform with `USAGE_TOUCH`.
- Pause/resume with vibration cancellation.
- No Unity, VContainer, Addressables, or Android runtime exceptions.
- Static scene sample: approximately 59 FPS and 275 MB PSS.

The player must have Android Touch feedback enabled for touch haptics. The game
must respect this system preference.
