# Device QA — Pixel 8

Device: Pixel 8 (`shiba`), serial `39131FDJH0005Q`
Package: `com.calmspace.tidyrestore`
Build: `Builds/Android/CalmSpace-Demo-debug.apk`
SHA-256 (APK used for these passes): `a16f0ac0a6bb980bde0b667fe811c555b38c3169e687de099cc435922ba65edf`
Size: 41 142 378 bytes
Installed with: `adb install -r` (no uninstall, no `pm clear`)
Date: 2026-08-02

Two passes were run. The device already carried a profile with all eight levels
completed, so pass A exercised the restored-state and replay paths. With the
owner's explicit approval the saved profile was then deleted (the profile file
only — no uninstall, no `pm clear`) and pass B exercised the real first-run
journey. Screenshots are under `docs/screenshots/device/`.

## Pass A — restored profile (8/8)

| Check | Result | Evidence |
| --- | --- | --- |
| Cold launch shows the illustrated workshop | Pass | `01-cold-launch.png` |
| Permanent state survived a restart (8/8, finale light) | Pass | `01-cold-launch.png` |
| Safe area and touch targets fit the Pixel 8 | Pass | top bar and task card clear of the cutout and gesture bar in every shot |
| Settings sheet opens | Pass | `02-settings.png` |
| UK / RU / EN all switch live | Pass | `02-settings.png`, `05-locale.png`, `06-locale.png` |
| Music toggles | Pass | `07-music-off.png` ("Sound on" → "Sound off") |
| Haptics fire on demand | Pass | "Test haptic feedback" produced device vibration |
| Catalog opens and lists all eight levels | Pass | `08-catalog.png` |
| Level launches from the catalog | Pass | `09-level-01.png` |
| Level is solvable and completes | Pass | `10-level-complete.png` |
| Completion returns home, no auto-next-level | Pass | `10-level-complete.png` shows only Undo / Back home |
| Replay pays out nothing and repeats no reveal | Pass | `11-back-home.png` — still 8/8, Cozy Tokens still 0 |
| No timers, fail states, lives, energy, forced or interstitial ads | Pass | none present in any screen |
| Deferred features stay hidden | Pass | no Album, decor, Daily Care or Relax Pass controls are rendered |
| logcat free of Unity / JNI / Addressables exceptions | Pass | see below |

## Pass B — fresh profile (first run)

The saved profile was removed with the owner's approval:

```bash
adb -s 39131FDJH0005Q shell rm -f \
  /storage/emulated/0/Android/data/com.calmspace.tidyrestore/files/player-profile-v1.bin
```

| Check | Result | Evidence |
| --- | --- | --- |
| Cold launch shows the dusty workshop at 0/8 | Pass | `12-fresh-dirty.png` — grimy window, dull shelves, tumbled boxes, "Clear the passage" |
| Level 1 launches from the home Start action | Pass | `13-first-level.png` |
| First completion pays the reward once | Pass | `14-first-complete.png` — "+15 COZY TOKENS" (absent on the pass A replay) |
| Completion returns home, no auto-next-level | Pass | `14-first-complete.png` shows only Undo / Back home |
| Reveal plays on arrival home | Pass | `15-reveal-start.png` — reveal in flight, primary action suppressed |
| Interaction is suspended during the reveal | Pass | `15-reveal-start.png` has no task title and no Start button |
| The zone changes permanently | Pass | lower-left passage goes from tumbled boxes (`12`) to a neat stack and woven rug (`15`–`18`) |
| Reveal settles and the hotspot advances to the real next level | Pass | `18-reveal-done.png` — "1 / 8 · Arrange the trail stones" |
| Restart keeps the restored zone and does not replay the reveal | Pass | `19-restart-persisted.png` — 1/8, 15 tokens, passage still cleared, no reveal |
| Remaining seven zones stay dusty | Pass | `19-restart-persisted.png` |

## logcat

Pass A captured 4 807 lines, pass B 4 634. Both are free of `AndroidRuntime`,
`FATAL` and `JNI DETECTED` entries, contain zero `E`-level Unity lines, and show
no Addressables load failure. The only exceptions in the buffers come from
unrelated system and third-party packages already running on the phone (Google
Play Services, WhatsApp/Facebook networking) and never from
`com.calmspace.tidyrestore`.

## Re-verification after the review fixes

Independent review found that the demo dead-ended after two levels: a Memory
presentation queued by stage 2 was never drained, so the home stopped offering
a next level. Both device passes above predate that fix, and neither reached
stage 3, which is exactly why they did not catch it.

The fix is covered on desktop by
`WorkshopPresentationPlayModeTests.ProgressionSurvivesTheMemoryBeat`, which
plays stages 1-3 in sequence and fails on the pre-fix code. The shipped APK
(`12ce841f118f92eb37255b5f43c63c80b68770bb57ba8f26bc68cacc9a7744ea`) has **not**
been re-run on the Pixel 8. Before release, repeat the pass B journey through at
least stage 3 and confirm the Start action survives the pebble-shelf beat.
