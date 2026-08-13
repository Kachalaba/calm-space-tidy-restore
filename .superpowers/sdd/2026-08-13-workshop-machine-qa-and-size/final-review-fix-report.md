# Final review fix report

Date: 2026-08-13

Worktree:
`/Users/nikita/.codex/.chatgpt-projects/g-p-6a6370c289748191a83ab20c2f948ca9/CalmSpace/.worktrees/living-workshop`

Reviewed base: `cd777b084af22b9e6d72fac8cdddee6bbe3402f6`

## Outcome

All 8 Important and all 4 Minor findings in
`final-review-findings.md` are closed. No residual product finding is known.
The controller still owns the intentionally deferred whole-project suites and
Android build trio; this wave ran only the required focused fixtures, two
Configure passes, and the stdlib archive-audit suite.

Commits:

- `8fcbad3` — `fix: report archive provenance conservatively`
- `98445e0` — `fix: make workshop transitions durable`
- `ab5fb8b` — `fix: guard Android settings and generated scenes`
- `3ac545a` — `chore: regenerate workshop scene after final fixes`

## Finding-by-finding changes

### Important 1 — workshop analytics producers

Commit: `98445e0`

Files:

- `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeController.cs`
- `Assets/CalmSpace/Tests/PlayMode/WorkshopPresentationPlayModeTests.cs`

`WorkshopHomeController` now receives the existing
`WorkshopAnalyticsSessionState` singleton. A reveal-start event is emitted only
after `PlayRevealAsync` has established real visual playback. Each true replay
is a new start. Reveal completion carries explicit visual-completion provenance
through a persistence retry and emits only after `Applied` retires the exact
fresh head. Missing visuals, faults, foreign/controller cancellation,
`AlreadyApplied`, `Invalid`, lying success with a retained head, and stale
callbacks emit no completion. Memory-viewed emits only for an exact retired
head with `Applied` and `Payload.FirstView == true`.

Producer/cardinality PlayMode coverage now protects interrupted replay (2
starts/1 completion), missing visual (0/0), frame fault (1/0), foreign
cancellation (1/0), persistence retry (1/1), `AlreadyApplied` (1/0), stale
replacement, and the exact memory id/first-view payload.

### Important 2 — preference persist before publish

Commit: `98445e0`

Files:

- `Assets/CalmSpace/Runtime/Demo/DemoThemeService.cs`
- `Assets/CalmSpace/Runtime/UI/DemoExperienceController.cs`
- `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeController.cs`
- `Assets/CalmSpace/Tests/EditMode/EncryptedFileDemoProgressStoreTests.cs`
- `Assets/CalmSpace/Tests/PlayMode/WorkshopHomeNavigationPlayModeTests.cs`

Theme selection now persists before changing `CurrentIndex`, `Current`, or
raising `ThemeChanged`. Both demo and workshop music controls persist the
desired value before calling `SetEnabled`; the post-publish `EnabledChanged`
handler no longer writes the store. Failed writes leave store, disk, service,
label, event count, audio state, and analytics unchanged; one retry publishes
once.

### Important 3 — memory acknowledgement recovery

Commit: `98445e0`

Files:

- `Assets/CalmSpace/Runtime/Demo/DemoProgressRules.cs`
- `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeController.cs`
- `Assets/CalmSpace/Tests/EditMode/WorkshopProfileRulesTests.cs`
- `Assets/CalmSpace/Tests/EditMode/EncryptedFileDemoProgressStoreTests.cs`
- `Assets/CalmSpace/Tests/PlayMode/WorkshopPresentationPlayModeTests.cs`

An already-viewed memory at the exact pending head is repaired by removing only
that head and returning `Applied` with `FirstView == false`; a different head
in front remains untouched. The encrypted V2 repair persists across reload.
Memory-card close and retry explicitly handle all statuses, re-read and compare
the exact persisted head, keep one action to one head, use the calm recovery
surface for retained/failing presentable memories, and never attach a stale
retry to a replacement head. Repair emits no duplicate view analytics.

### Important 4 — no level 8 replay after 8/8

Commit: `98445e0`

Files:

- `Assets/CalmSpace/Runtime/Demo/DemoProgressRules.cs`
- `Assets/CalmSpace/Runtime/UI/DemoExperienceController.cs`
- `Assets/CalmSpace/Tests/EditMode/DemoProgressRulesTests.cs`
- `Assets/CalmSpace/Tests/PlayMode/WorkshopHomeNavigationPlayModeTests.cs`

`GetRecommendedLevel` returns `-1` when no unlocked incomplete level exists.
Both legacy fallback consumers reject `-1`. The PlayMode regression completes
all eight levels, degrades workshop metadata/flow, and confirms no launch and a
current index of `-1`.

### Important 5 — Android version override scope

Commit: `ab5fb8b`

Files:

- `Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs`
- `Assets/CalmSpace/Tests/EditMode/AndroidSigningCleanupTests.cs`

Version name and code are both parsed/validated before either setting mutates.
`AndroidSigningCleanupScope` snapshots both settings when constructed and
restores them through nested `finally` blocks on normal completion or throw;
`Arm()` continues to control signing-secret cleanup only. Tests cover a normal
scope, an armed exception, and valid name plus invalid code. ProjectSettings
remained clean after RED, GREEN, and the final run.

### Important 6 — generated-scene dependency validation

Commits: `ab5fb8b`, `3ac545a`

Files:

- `Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs`
- `Assets/CalmSpace/Tests/EditMode/WorkshopHomeViewTests.cs`
- `Assets/CalmSpace/Scenes/Main.unity`

Generated-scene reuse now checks both controller references, all 19 required
`WorkshopHomeView` references, and all 7 required `WorkshopBottomSheet`
references, in addition to the existing lifetime-scope, audio-bank, and room
checks. Representative saved-scene corruption tests prove repair of
`_roomParent` and `_settingsMusicLabel`; a direct exhaustive regression clears
and restores each of the 28 references individually and requires validation to
reject every null.

### Important 7 — conservative archive provenance

Commit: `8fcbad3`

Files:

- `tools/build/android_archive_audit.py`
- `tools/build/tests/test_android_archive_audit.py`
- `docs/ANDROID_BUILD_SIZE.md`

The release-pair JSON now always reports `sameSource: notEvaluated` unless a
future implementation actually validates provenance. The `<= 1 MiB` result is
reported independently as `container_delta_guard_status`. Tests and docs no
longer describe unrelated inputs as a same-source pass.

### Important 8 — scene repair tests restore tracked state

Commit: `ab5fb8b`

File:

- `Assets/CalmSpace/Tests/EditMode/WorkshopHomeViewTests.cs`

Main-scene tests live in a fixture without the normal view fixture's mutating
`SetUp`. Before opening Main, the scope snapshots exact scene bytes, cloned
Build Settings, and the saved scene setup; it rejects dirty loaded scenes and
mixed saved/untitled setups. Cleanup uses nested `finally` blocks to switch to
a clean scene, restore/import exact Main bytes, restore Build Settings, and
restore the saved scene setup. SHA-256 checks after the corruption RED, GREEN,
and final focused fixtures all matched their respective pre-run baseline.

### Minor 1 — Addressables handle ownership and task retention

Commit: `98445e0`

Files:

- `Assets/CalmSpace/Runtime/Workshop/AddressableWorkshopRoomLoader.cs`
- `Assets/CalmSpace/Tests/EditMode/AddressableWorkshopRoomLoaderTests.cs`

An owned orphan handle is released exactly once before same-target reload.
Matching async completion/unload clears `_inFlight`; the synchronous-completion
assignment race is reconciled without letting an old generation clear a new
load. Tests assert exact start/release/destroy cardinality and retention.

### Minor 2 — PlayMode Await helpers

Commit: `98445e0`

Files:

- `Assets/CalmSpace/Tests/PlayMode/WorkshopHomeNavigationPlayModeTests.cs`
- `Assets/CalmSpace/Tests/PlayMode/WorkshopPresentationPlayModeTests.cs`

Both helpers now require `Succeeded`, observe and propagate fault/cancellation,
and consume generic results once through callbacks. Already-faulted non-generic
and generic regressions protect the behavior.

### Minor 3 — deferred fresh-head inequality race

Commit: `98445e0`

File:

- `Assets/CalmSpace/Tests/PlayMode/WorkshopPresentationPlayModeTests.cs`

The regression begins reveal A, defers its cancellation, replaces the persisted
head with B, then releases A's cancellation. A does not play, retire, or emit
completion for B; the existing exact-equality continuation was retained.

### Minor 4 — Android size documentation

Commit: `8fcbad3`

File:

- `docs/ANDROID_BUILD_SIZE.md`

The earlier numbers are explicitly labelled historical/retained evidence and
the provenance wording matches the conservative audit contract.

## RED evidence

The exact environment used by the Unity commands was:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.0.80f1/Unity.app/Contents/MacOS/Unity
PROJECT=/Users/nikita/.codex/.chatgpt-projects/g-p-6a6370c289748191a83ab20c2f948ca9/CalmSpace/.worktrees/living-workshop
```

Every Test Runner command intentionally omitted `-quit`.

### EditMode REDs

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.WorkshopProfileRulesTests -testResults Logs/final-fix-profile-rules-red.xml -logFile Logs/final-fix-profile-rules-red.log
```

- `18/19` passed, `1` failed: exact viewed Memory head expected `Applied`, got
  `AlreadyApplied`.

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.DemoProgressRulesTests -testResults Logs/final-fix-demo-rules-red.xml -logFile Logs/final-fix-demo-rules-red.log
```

- `11/12` passed, `1` failed: all-complete recommendation expected `-1`, got
  `7`.

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.EncryptedFileDemoProgressStoreTests -testResults Logs/final-fix-encrypted-store-red.xml -logFile Logs/final-fix-encrypted-store-red.log
```

- `25/27` passed, `2` failed: failed encrypted theme write still returned
  `true`; inconsistent V2 head expected repair `Applied`, got `AlreadyApplied`.

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.AddressableWorkshopRoomLoaderTests -testResults Logs/final-fix-room-loader-red.xml -logFile Logs/final-fix-room-loader-red.log
```

- `6/9` passed, `3` failed: orphan release count expected `1`, got `0`; normal
  and synchronous completed tasks were both retained instead of null.

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.AndroidSigningCleanupTests -testResults Logs/final-fix-signing-red-2.xml -logFile Logs/final-fix-signing-red-2.log
```

- `0/3` passed, `3` failed: normal scope retained `1.0.0-scope`, exception
  retained `temporary-version`, and invalid code partially applied
  `1.0.0-must-not-apply`, each instead of restoring/retaining `1.0.0`.

After a Configure baseline made the marker current:

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.WorkshopHomeViewTests -testResults Logs/final-fix-scene-validation-red.xml -logFile Logs/final-fix-scene-validation-red.log
```

- `9/11` passed, `2` failed: Configure reused Main with null `_roomParent` and
  null `_settingsMusicLabel`. The test scope restored Main and Build Settings;
  their post-RED hashes were unchanged.

### PlayMode REDs

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode -testFilter CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests -testResults Logs/final-fix-presentation-red.xml -logFile Logs/final-fix-presentation-red.log
```

- `21/32` passed, `11` failed. Behavioral failures exposed swallowed Await
  faults; zero producer events on interruption, fault, foreign cancellation,
  persistence retry, and memory view; missing memory recovery for
  `PersistFailed`/retained `AlreadyApplied`; and a stale memory retry mutating a
  replacement. One additional failure (`CompletionShowsTheAuthoredBeatLine`)
  was test-log contamination from the intentionally faulted Await sentinel;
  the regression was corrected to observe the original task on the old-helper
  path before the production GREEN.

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode -testFilter CalmSpace.Tests.PlayMode.WorkshopHomeNavigationPlayModeTests -testResults Logs/final-fix-navigation-red.xml -logFile Logs/final-fix-navigation-red.log
```

- `22/24` passed, `2` failed: an already-faulted Await returned normally and a
  failed music write visibly toggled from `true` to `false`. The degraded 8/8
  consumer regression was added after its focused rules RED and passed with the
  minimal rules fix.

### Archive RED

```sh
python3 -m unittest -v tools.build.tests.test_android_archive_audit.AndroidArchiveAuditTests.test_release_pair_keeps_same_source_not_evaluated_at_guard_boundary > Logs/final-fix-archive-red-2.log 2>&1
```

- `0/1` passed, `1` failed: expected
  `container_delta_guard_status == "pass"`, got missing/`None` under the new
  conservative contract test.

All listed Unity REDs were compile-clean behavioral runs. The earlier
`final-fix-signing-red.log` compile attempt was discarded (this project's NUnit
surface does not expose `NonParallelizable`); `final-fix-signing-red-2.xml` is
the accepted behavioral RED.

## GREEN evidence

### Final focused Unity fixtures

Commands used the same `$UNITY` and `$PROJECT` values above, one Unity process
at a time:

```sh
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.WorkshopProfileRulesTests -testResults Logs/final-fix-profile-rules-final.xml -logFile Logs/final-fix-profile-rules-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.DemoProgressRulesTests -testResults Logs/final-fix-demo-rules-final.xml -logFile Logs/final-fix-demo-rules-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.EncryptedFileDemoProgressStoreTests -testResults Logs/final-fix-encrypted-store-final.xml -logFile Logs/final-fix-encrypted-store-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.AddressableWorkshopRoomLoaderTests -testResults Logs/final-fix-room-loader-final.xml -logFile Logs/final-fix-room-loader-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.AndroidSigningCleanupTests -testResults Logs/final-fix-signing-final.xml -logFile Logs/final-fix-signing-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.WorkshopAnalyticsTests -testResults Logs/final-fix-analytics-green.xml -logFile Logs/final-fix-analytics-green.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.GeneratedWorkshopSceneTests -testResults Logs/final-fix-generated-scene-final.xml -logFile Logs/final-fix-generated-scene-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testFilter CalmSpace.Tests.EditMode.WorkshopHomeViewTests -testResults Logs/final-fix-home-view-final.xml -logFile Logs/final-fix-home-view-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode -testFilter CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests -testResults Logs/final-fix-presentation-final.xml -logFile Logs/final-fix-presentation-final.log
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode -testFilter CalmSpace.Tests.PlayMode.WorkshopHomeNavigationPlayModeTests -testResults Logs/final-fix-navigation-final.xml -logFile Logs/final-fix-navigation-final.log
```

Results:

- `WorkshopProfileRulesTests`: `19/19`, failures `0`, skipped `0`.
- `DemoProgressRulesTests`: `12/12`, failures `0`, skipped `0`.
- `EncryptedFileDemoProgressStoreTests`: `27/27`, failures `0`, skipped `0`.
- `AddressableWorkshopRoomLoaderTests`: `9/9`, failures `0`, skipped `0`.
- `AndroidSigningCleanupTests`: `3/3`, failures `0`, skipped `0`.
- `WorkshopAnalyticsTests`: `4/4`, failures `0`, skipped `0`.
- `GeneratedWorkshopSceneTests`: `6/6`, failures `0`, skipped `0`.
- `WorkshopHomeViewTests`: `6/6`, failures `0`, skipped `0`.
- `WorkshopPresentationPlayModeTests`: `33/33`, failures `0`, skipped `0`.
- `WorkshopHomeNavigationPlayModeTests`: `24/24`, failures `0`, skipped `0`.

### Archive GREEN

```sh
python3 -m unittest -v tools.build.tests.test_android_archive_audit.AndroidArchiveAuditTests.test_release_pair_keeps_same_source_not_evaluated_at_guard_boundary > Logs/final-fix-archive-green.log 2>&1
python3 -m unittest -v tools.build.tests.test_android_archive_audit > Logs/final-fix-archive-suite-final.log 2>&1
```

- Focused: `1/1`, failures `0`.
- Full stdlib fixture: `13/13`, failures `0`, final runtime `0.442s`.

## Configure determinism and generated Main

`CalmSpaceProjectSetup.cs` and other fingerprinted runtime sources changed, so
Configure was run twice with `-quit`:

```sh
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -buildTarget Android -executeMethod CalmSpace.Editor.CalmSpaceProjectSetup.ConfigureProject -logFile Logs/final-fix-configure-pass-1.log
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -buildTarget Android -executeMethod CalmSpace.Editor.CalmSpaceProjectSetup.ConfigureProject -logFile Logs/final-fix-configure-pass-2.log
```

Both passes produced:

- Main marker: `CalmSpace Generated Scene · E25C2FD639BA1FAB`
- `Assets/CalmSpace/Scenes/Main.unity` SHA-256:
  `df931283a241c06687c95bba5f89728d6023ae97e13bf5bc2fed112ad035f8ff`
- `ProjectSettings/EditorBuildSettings.asset` SHA-256:
  `ae158b39643d239199411e0c0b661917f91d8d4406723173673230afc00728a8`

The two `.sha256` evidence files compared equal. Both hashes remained unchanged
after the final scene and view fixtures. The legitimate generated Main is
committed in `3ac545a`; Build Settings and `ProjectSettings.asset` have no diff.

## Cleanup and repository state

- Restored only known builder/test noise after each relevant Unity run:
  `Assets/AddressableAssetsData/link.xml`, its `.meta`, and the eight level
  prefabs (`DemoFitting`, `02PebblePairs` through `08DustyWindow`).
- No ProjectSettings mutation remains.
- No logs, XML results, temporary scenes, secrets, Addressables output, or build
  artifacts were added to a commit.
- `git diff --check` is clean for hand-authored files. Unity-generated Main
  retains Unity's normal serialized empty-value whitespace.
- Unity processes were serialized; Test Runner commands used no `-quit`, while
  Configure commands used `-quit`.
- No full suites, Android build trio, phone/ADB, push, merge, PR update, or
  package change was performed.

## Residual concerns

No residual review finding. Whole-project EditMode/PlayMode suites, build trio,
and final re-review remain deliberately delegated to the controller as required
by the findings contract.
