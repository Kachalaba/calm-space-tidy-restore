# Living Workshop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the existing eight calm mini-games into one polished, persistent “Old Family Workshop” retention loop with a living illustrated home, family memories, reversible decor, a gentle daily tea ritual, EN/UK/RU localization, and safe optional monetization.

**Architecture:** `LevelCatalog` remains the authority for Addressable levels and progression order. A new `LivingWorkshopCatalog` joins chapter/stage metadata to stable workshop beat IDs. One encrypted V2 profile aggregate owns all mutations and persists before publishing changes. Pure projector/rules classes derive room, album, decor, and daily state; coordinators produce typed actions and launch requests; `DemoExperienceController` retains fades, Addressables, HUD, and active-level ownership. Generated scenes and prefabs remain editor-builder outputs rather than hand-edited sources.

**Tech Stack:** Unity 6000.0.80f1, URP 17.0.4, Addressables 3.1.0, New Input System 1.20.0, UniTask 2.5.10, VContainer 1.18.0, Unity Test Framework 1.6.0, Android API 24+, IL2CPP/ARM64.

## Global Constraints

- Preserve stable level IDs and their catalog order:
  `01-soft-blocks`, `02-pebble-pairs`, `03-tea-drawer`,
  `04-color-shelf`, `05-fastener-tray`, `06-fresh-surface`,
  `07-cabinet-hinge`, `08-dusty-window`.
- Use stable chapter ID `cozy-workshop`; all eight levels become stages `0/8` through `7/8`.
- Preserve encrypted profile filename `player-profile-v1.bin` and associated data `CalmSpace.PlayerProfile.v1`.
- Keep the V1 decoration map frozen:
  `0=soft-fern`, `1=river-stones`, `2=warm-lantern`, `3=clay-vase`.
- A profile mutation is visible only after its encrypted write succeeds.
- A first completion atomically records completion, its one-time token reward, and ordered pending presentation entries.
- Never overwrite or downgrade an authenticated future-version profile.
- Do not add timers, countdowns, lives, fail states, expiring streaks, energy, negative feedback, or forced ads.
- Do not add an in-level rewarded hint button. Rewarded video is allowed only for a specifically selected home-screen decor variant.
- Never show an ad during gameplay, a drag, a transition, a story presentation, or the daily-care interaction.
- The main family story and all eight restoration stages remain free.
- The room must have no steady-state managed allocations in its visible idle loop.
- Use original or publication-safe assets and record provenance in `docs/ASSET_PROVENANCE.md`.
- Do not hand-edit `Assets/CalmSpace/Scenes/Main.unity` or generated level prefabs. Change editor builders, then regenerate.
- Keep direct implementation changes inside `Assets/CalmSpace/`, `ProjectSettings/`, `Packages/`, and project documentation. Preserve unrelated worktree changes.

## Working Commands

Run these once in every implementation shell:

```bash
export CALM_PROJECT="/Users/nikita/.codex/.chatgpt-projects/g-p-6a6370c289748191a83ab20c2f948ca9/CalmSpace"
export CALM_UNITY="/Applications/Unity/Hub/Editor/6000.0.80f1/Unity.app/Contents/MacOS/Unity"
mkdir -p "$CALM_PROJECT/Logs"

calm_edit_test () {
  local calm_fixture="$1"
  local calm_slug="$2"
  "$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
    -runTests -testPlatform EditMode -testFilter "$calm_fixture" \
    -testResults "$CALM_PROJECT/Logs/$calm_slug.xml" \
    -logFile "$CALM_PROJECT/Logs/$calm_slug.log" -quit
}

calm_play_test () {
  local calm_fixture="$1"
  local calm_slug="$2"
  "$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
    -runTests -testPlatform PlayMode -testFilter "$calm_fixture" \
    -testResults "$CALM_PROJECT/Logs/$calm_slug.xml" \
    -logFile "$CALM_PROJECT/Logs/$calm_slug.log" -quit
}
```

Run one EditMode fixture:

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.DemoProgressRulesTests" \
  -testResults "$CALM_PROJECT/Logs/example-progress-editmode.xml" \
  -logFile "$CALM_PROJECT/Logs/example-progress-editmode.log" -quit
```

Run one PlayMode fixture:

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testFilter "CalmSpace.Tests.PlayMode.StartupScenePlayModeTests" \
  -testResults "$CALM_PROJECT/Logs/example-startup-playmode.xml" \
  -logFile "$CALM_PROJECT/Logs/example-startup-playmode.log" -quit
```

Regenerate authored content:

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -executeMethod CalmSpace.Editor.CalmSpaceProjectSetup.ConfigureProject \
  -logFile "$CALM_PROJECT/Logs/configure-project.log" -quit
```

## Execution Preflight

- [ ] Confirm branch `agent/cross-platform-calm-refactor` and preserve unrelated
  changes shown by `git status --short --branch`.
- [ ] Run the complete EditMode and PlayMode commands from Task 14 before the
  first implementation edit.
- [ ] Confirm the current baseline of 118 EditMode and 35 PlayMode tests, or
  record the exact new baseline if the checkout changed after this plan.
- [ ] Confirm Unity imports with no compiler errors and that
  `CalmSpaceProjectSetup.ConfigureProject` is deterministic on a clean diff.
- [ ] Never regenerate while a user-owned Unity scene/prefab edit is unsaved.

## Ownership Map

| Owner | Sole responsibility | Must not own |
|---|---|---|
| `LevelCatalog` | Ordered Addressable levels and chapter/stage lookup | Story, decor, room art |
| `LivingWorkshopCatalog` | Stable beat/story/unlock authoring joined by chapter+stage | Level prefab loading, save state |
| `IDemoProgressStore` | One encrypted aggregate and persist-before-publish commands | UI, animations, Addressables |
| `WorkshopProgressProjector` | Pure derived room/album/daily/recommendation state | Persistence or Unity objects |
| `WorkshopFlowCoordinator` | Typed recommended actions, launch requests, completion commands | `ILevelFlowController` calls |
| `DemoExperienceController` | Fade, Addressable level lifecycle, HUD, completion, current level | Story queue and room-state calculations |
| `WorkshopHomeController` | Home intent, one bottom sheet, entry/exit lifecycle | Profile writes outside typed commands |
| `WorkshopPresentationQueueController` | Persisted room→memory→finale playback | An in-memory authoritative queue |
| `WorkshopRoomPresenter` | Visual masks, hotspot, decor sprites, cancellable reveal | Economy, unlock rules, file writes |
| Album/decor/daily controllers | Their single home surface and explicit player actions | Global navigation or level ownership |
| Editor builders | Generated scene/prefab/catalog/Addressable source of truth | Runtime progression decisions |

## Milestone Gates

1. **Living Spine:** Tasks 1–10. A fresh player can complete eight stages, return to a living room, see one persistent transformation, and receive the correct next action.
2. **Family Layer:** Task 11. Three memories, one album page, the finale cameo, and the kitchen teaser recover correctly after interruption.
3. **Personal Room:** Task 12. Two independent decor slots, Relax Pass boundaries, rewarded decor, and entitled palette/audio behavior are complete.
4. **Gentle Return:** Tasks 13–15. Daily tea, postcard, final validation, Android build, Pixel 8 QA, and public branch/PR handoff are complete.

---

### Task 1: Migrate level authoring from six to eight chapter stages

**Files:**

- Modify: `Assets/CalmSpace/Runtime/Levels/RestorationProgressRules.cs`
- Modify: `Assets/CalmSpace/Runtime/Levels/LevelCatalog.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceDemoLevelBuilder.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/RestorationProgressRulesTests.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/DemoContentAuthoringTests.cs`
- Regenerate: `Assets/CalmSpace/Config/DemoFitting.asset`
- Regenerate: `Assets/CalmSpace/Config/02PebblePairs.asset`
- Regenerate: `Assets/CalmSpace/Config/03TeaDrawer.asset`
- Regenerate: `Assets/CalmSpace/Config/04ColorShelf.asset`
- Regenerate: `Assets/CalmSpace/Config/05FastenerTray.asset`
- Regenerate: `Assets/CalmSpace/Config/06FreshSurface.asset`
- Regenerate: `Assets/CalmSpace/Config/07CabinetHinge.asset`
- Regenerate: `Assets/CalmSpace/Config/08DustyWindow.asset`
- Regenerate: `Assets/CalmSpace/Config/LevelCatalog.asset`

**Consumes / Produces:** Consumes existing `LevelDefinition` metadata and
generated level specs. Produces chapter-scoped validation/lookup and eight
regenerated `cozy-workshop` stages without changing level identity.

**Contracts:**

```csharp
public readonly struct RestorationChapterInfo
{
    public string ChapterId { get; }
    public int FirstCatalogIndex { get; }
    public int StageCount { get; }
}

public static bool TryValidateChapter(
    IReadOnlyList<LevelDefinition> definitions,
    string chapterId,
    out RestorationChapterInfo chapter,
    out int invalidCatalogIndex);

public bool IsRestorationChapterValid(string chapterId);

public bool TryGetRestorationChapter(
    string chapterId,
    out RestorationChapterInfo chapter);

public bool TryFindRestorationStage(
    string chapterId,
    int stageIndex,
    out int levelIndex,
    out LevelCatalogEntry entry);
```

- [ ] **Step 1: Write the failing chapter-scoped tests**

Add cases proving:

- a valid `chapter-a` remains valid when an unrelated `chapter-b` has a gap;
- invalid `chapter-b` fails closed without disabling `chapter-a`;
- duplicate stages, missing stages, mismatched stage counts, and duplicate level IDs fail;
- the generated catalog has exactly eight `cozy-workshop` stages whose stage index equals catalog index.

The authoring expectation is:

```text
01-soft-blocks     cozy-workshop 0/8
02-pebble-pairs    cozy-workshop 1/8
03-tea-drawer      cozy-workshop 2/8
04-color-shelf     cozy-workshop 3/8
05-fastener-tray   cozy-workshop 4/8
06-fresh-surface   cozy-workshop 5/8
07-cabinet-hinge   cozy-workshop 6/8
08-dusty-window    cozy-workshop 7/8
```

- [ ] **Step 2: Verify RED**

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.RestorationProgressRulesTests" \
  -testResults "$CALM_PROJECT/Logs/chapter-rules-red.xml" \
  -logFile "$CALM_PROJECT/Logs/chapter-rules-red.log" -quit
```

Expected: the new chapter-scoped API is absent or the old global validation invalidates both chapters.

- [ ] **Step 3: Implement chapter-scoped validation**

`RestorationProgressRules.TryValidateChapter` must inspect only definitions with the requested chapter ID, require one contiguous sequence from `0` to `stageCount - 1`, and report the catalog index of the first invalid entry. `LevelCatalog` caches validation per requested chapter without allocating in `Update`; remove runtime call-site dependence on global `RestorationMetadataValid`.

- [ ] **Step 4: Update builder metadata and regenerate**

Change only chapter metadata in `LevelSpecs`; preserve level IDs, prefab paths, Addressable addresses, types, and order. Run the configure command from “Working Commands”.

- [ ] **Step 5: Verify GREEN**

Run both fixtures:

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.RestorationProgressRulesTests" \
  -testResults "$CALM_PROJECT/Logs/chapter-rules-green.xml" \
  -logFile "$CALM_PROJECT/Logs/chapter-rules-green.log" -quit
```

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.DemoContentAuthoringTests" \
  -testResults "$CALM_PROJECT/Logs/chapter-authoring-green.xml" \
  -logFile "$CALM_PROJECT/Logs/chapter-authoring-green.log" -quit
```

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime/Levels Assets/CalmSpace/Editor/CalmSpaceDemoLevelBuilder.cs Assets/CalmSpace/Tests/EditMode Assets/CalmSpace/Config
git commit -m "feat: join eight levels into workshop chapter"
```

---

### Task 2: Author the living-workshop content catalog and stable IDs

**Files:**

- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopContentIds.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/LivingWorkshopCatalog.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopCatalogValidator.cs`
- Create: `Assets/CalmSpace/Editor/CalmSpaceWorkshopCatalogBuilder.cs`
- Create: `Assets/CalmSpace/Editor/LivingWorkshopBuildValidator.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/LivingWorkshopCatalogTests.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/DemoContentAuthoringTests.cs`
- Generate: `Assets/CalmSpace/Config/LivingWorkshopCatalog.asset`

**Consumes / Produces:** Consumes Task 1 chapter lookup plus stable level
order. Produces the sole workshop authoring catalog, stable content constants,
and runtime/build validation result.

**Stable IDs:**

```text
chapter: cozy-workshop

beats:
cozy-workshop.clear-passage
cozy-workshop.pebble-shelf
cozy-workshop.tea-drawer
cozy-workshop.paint-shelf
cozy-workshop.fastener-tray
cozy-workshop.warm-workbench
cozy-workshop.cabinet-hinge
cozy-workshop.open-window

memories:
family.summer-trail-stones
family.fix-everything
family.open-windows
family.tea-postcard-03

decor slots:
workbench-accent
warm-light

daily care:
family-tea
```

**Contracts:**

```csharp
[Serializable]
public sealed class WorkshopBeatDefinition
{
    public WorkshopBeatDefinition(
        string chapterId,
        string beatId,
        int stageIndex,
        string titleTextKey,
        string resultTextKey,
        int zoneIndex,
        string memoryId,
        string unlockedDecorSlotId,
        bool unlocksDailyCare,
        bool isFinale);

    public string ChapterId { get; }
    public string BeatId { get; }
    public int StageIndex { get; }
    public string TitleTextKey { get; }
    public string ResultTextKey { get; }
    public int ZoneIndex { get; }
    public string MemoryId { get; }
    public string UnlockedDecorSlotId { get; }
    public bool UnlocksDailyCare { get; }
    public bool IsFinale { get; }
    public bool IsValid { get; }
}

public sealed class LivingWorkshopCatalog : ScriptableObject
{
    public int Count { get; }
    public bool TryGetBeat(int index, out WorkshopBeatDefinition beat);
    public bool TryFindBeat(
        string chapterId,
        int stageIndex,
        out WorkshopBeatDefinition beat);
    public bool TryFindBeat(
        string beatId,
        out WorkshopBeatDefinition beat);
}

public enum WorkshopCatalogValidationCode
{
    Valid = 0,
    MissingChapter = 1,
    MissingBeat = 2,
    DuplicateBeatId = 3,
    DuplicateZoneIndex = 4,
    StageGap = 5,
    InvalidMemoryLink = 6,
    MissingFinale = 7,
    MultipleFinales = 8,
    LevelJoinFailed = 9
}

public readonly struct WorkshopCatalogValidationResult
{
    public bool IsValid { get; }
    public WorkshopCatalogValidationCode Code { get; }
    public int InvalidStageIndex { get; }
    public string StableId { get; }
}

public static WorkshopCatalogValidationResult ValidateChapter(
    LevelCatalog levels,
    LivingWorkshopCatalog workshop,
    string chapterId);
```

- [ ] **Step 1: Write failing catalog and join tests**

Cover unique stable IDs, unique zone indices `0–7`, exact memory links at stages `1`, `4`, and `7`, daily unlock at stage `2`, decor unlocks at stages `3` and `5`, and exactly one finale at stage `7`.

- [ ] **Step 2: Verify RED**

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.LivingWorkshopCatalogTests" \
  -testResults "$CALM_PROJECT/Logs/workshop-catalog-red.xml" \
  -logFile "$CALM_PROJECT/Logs/workshop-catalog-red.log" -quit
```

- [ ] **Step 3: Implement catalog and validator**

`WorkshopCatalogValidator.ValidateChapter` must join by `chapterId + stageIndex`; it must not duplicate or infer level order. Return a typed result containing validity, invalid beat/stage, and a diagnostic code. Runtime invalid metadata returns no meta action while the ordinary level catalog remains playable.

- [ ] **Step 4: Add build-time validation**

`LivingWorkshopBuildValidator : IPreprocessBuildWithReport` blocks builds for a missing/duplicate beat, gap, duplicate zone, invalid memory link, absent finale, or failed join. `CalmSpaceProjectSetup` creates and assigns the catalog deterministically.

- [ ] **Step 5: Regenerate and Verify GREEN**

Run configure, then:

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.LivingWorkshopCatalogTests" \
  -testResults "$CALM_PROJECT/Logs/workshop-catalog-green.xml" \
  -logFile "$CALM_PROJECT/Logs/workshop-catalog-green.log" -quit
```

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime/Workshop Assets/CalmSpace/Editor Assets/CalmSpace/Tests/EditMode Assets/CalmSpace/Config/LivingWorkshopCatalog.asset
git commit -m "feat: author living workshop content catalog"
```

---

### Task 3: Define the immutable V2 profile and pure mutation rules

**Files:**

- Create: `Assets/CalmSpace/Runtime/Demo/DemoProfileContracts.cs`
- Create: `Assets/CalmSpace/Runtime/Demo/DemoProgressRules.cs`
- Modify: `Assets/CalmSpace/Runtime/Demo/DemoProgress.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/DemoProgressRulesTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopProfileRulesTests.cs`

**Consumes / Produces:** Consumes frozen stable content IDs and the existing
V1 snapshot behavior. Produces immutable V2 state/commands and pure mutation
rules; persistence remains unchanged until Task 4.

**Core contracts:**

```csharp
public enum ProfileMutationStatus
{
    Applied = 0,
    AlreadyApplied = 1,
    PersistFailed = 2,
    Invalid = 3
}

public readonly struct ProfileMutationResult<TPayload>
{
    public ProfileMutationStatus Status { get; }
    public DemoProgressSnapshot Snapshot { get; }
    public TPayload Payload { get; }
    public bool IsSuccess { get; }
}

public enum PendingPresentationKind
{
    RoomReveal = 0,
    Memory = 1,
    Finale = 2
}

public readonly struct PendingPresentationEntry :
    IEquatable<PendingPresentationEntry>
{
    public PendingPresentationKind Kind { get; }
    public string StableId { get; }
    public bool RequiresExplicitLaunch { get; }

    public static PendingPresentationEntry RoomReveal(string beatId);
    public static PendingPresentationEntry Memory(string memoryId);
    public static PendingPresentationEntry Finale(
        string chapterId,
        bool requiresExplicitLaunch = false);
}

public readonly struct DecorationSelection
{
    public string SlotId { get; }
    public string DecorationId { get; }
}

public enum DecorationGrantSource
{
    Rewarded = 0,
    RelaxPass = 1,
    Migration = 2
}

public readonly struct LevelCompletionMutation
{
    public bool FirstCompletion { get; }
    public int RewardAmount { get; }
    public int TokenBalance { get; }
}

public readonly struct PresentationMutation
{
    public PendingPresentationEntry Presentation { get; }
}

public readonly struct MemoryMutation
{
    public string MemoryId { get; }
    public bool FirstView { get; }
}

public readonly struct DecorationMutation
{
    public string SlotId { get; }
    public string DecorationId { get; }
    public int TokenDelta { get; }
    public int TokenBalance { get; }
}

public readonly struct DailyCareMutation
{
    public string CareId { get; }
    public int UtcDayKey { get; }
    public int RewardAmount { get; }
    public int TokenBalance { get; }
    public int CompletionCount { get; }
    public string UnlockedMemoryId { get; }
}

public readonly struct PreferenceMutation
{
    public bool Changed { get; }
}

public enum ProfileLoadStatus
{
    Missing = 0,
    LoadedV1 = 1,
    LoadedV2 = 2,
    AuthenticationFailed = 3,
    InvalidData = 4,
    IoError = 5,
    UnsupportedVersion = 6
}

public enum ProfileLoadSource
{
    None = 0,
    Primary = 1,
    Temporary = 2,
    Backup = 3,
    LegacyPlayerPrefs = 4,
    Default = 5
}

public readonly struct ProfileInitializationResult
{
    public bool IsReady { get; }
    public ProfileLoadStatus LoadStatus { get; }
    public ProfileLoadSource Source { get; }
    public bool WasMigrated { get; }
    public bool WasPersisted { get; }
    public DemoProgressSnapshot Snapshot { get; }
}
```

`DemoProgressSnapshot` keeps existing level mask, reward mask, token, theme, and music properties, then adds immutable copied collections and helpers:

```csharp
public int SeenRoomRevealCount { get; }
public int ViewedMemoryCount { get; }
public int SeenFinaleCount { get; }
public int PendingPresentationCount { get; }
public int OwnedDecorationCount { get; }
public int DecorationSelectionCount { get; }
public int LastDailyCareUtcDayKey { get; }
public int CompletedDailyCareCount { get; }

public bool HasSeenRoomReveal(string beatId);
public bool HasViewedMemory(string memoryId);
public bool HasSeenFinale(string chapterId);
public bool OwnsDecoration(string decorationId);
public bool TryGetSelectedDecoration(
    string slotId,
    out string decorationId);
public bool TryGetPendingPresentation(
    int index,
    out PendingPresentationEntry entry);
```

Internal arrays must be defensively copied on construction and never returned directly. Unknown stable decoration IDs survive normalization.

**Command contracts consumed by the pure rules:**

```csharp
public readonly struct CompleteLevelCommand
{
    public string LevelId { get; }
    public int LevelIndex { get; }
    public int RewardAmount { get; }
    public int PresentationCount { get; }
    public bool TryGetPresentation(
        int index,
        out PendingPresentationEntry entry);
}
```

- [ ] **Step 1: Write failing pure-rule tests**

Test:

- first completion adds one reward and ordered `RoomReveal → Memory → Finale`;
- repeat completion returns `AlreadyApplied`, adds no tokens, and appends nothing;
- marking a room/finale presentation removes only the matching queue head;
- `MarkMemoryViewed` removes a queued memory and also supports an album first view;
- invalid IDs and out-of-range levels return `Invalid` without changing equality;
- purchase atomically changes balance, ownership, and only the requested slot;
- grant atomically changes ownership and selection without spending tokens;
- same `utcDayKey` cannot grant twice;
- the third daily completion queues `family.tea-postcard-03`;
- unknown owned decoration IDs survive a mutation round trip.

- [ ] **Step 2: Verify RED**

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.WorkshopProfileRulesTests" \
  -testResults "$CALM_PROJECT/Logs/profile-rules-red.xml" \
  -logFile "$CALM_PROJECT/Logs/profile-rules-red.log" -quit
```

- [ ] **Step 3: Implement immutable state and pure rules**

Move `DemoProgressSnapshot` and the pure rules out of the current monolithic
`DemoProgress.cs` into the files listed above. Keep the current
`IDemoProgressStore` and both concrete stores compiling unchanged during this
pure-rules commit; Task 4 replaces the interface atomically with its
implementations. `DemoProgress.cs` retains the old interface and legacy
PlayerPrefs store until that switch.
Keep all array scanning allocation-free and without LINQ. Mutations occur only
on explicit commands, never in `Update`. `MarkPresentationSeen` accepts only
room/finale entries; `MarkMemoryViewed` owns both memory queue removal and
album first-view state. Retain obsolete index-based compatibility projections
only until Task 12 switches every UI call site; do not add a second profile
owner.

- [ ] **Step 4: Verify GREEN**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopProfileRulesTests" "profile-rules-green"
calm_edit_test "CalmSpace.Tests.EditMode.DemoProgressRulesTests" "legacy-progress-green"
```

- [ ] **Step 5: Commit**

```bash
git add Assets/CalmSpace/Runtime/Demo Assets/CalmSpace/Tests/EditMode
git commit -m "feat: define atomic workshop profile rules"
```

---

### Task 4: Implement V2 encrypted loading, V1 migration, and atomic persistence

**Files:**

- Create: `Assets/CalmSpace/Runtime/Demo/IDemoProgressStore.cs`
- Create: `Assets/CalmSpace/Runtime/Demo/ProfilePersistenceContracts.cs`
- Create: `Assets/CalmSpace/Runtime/Demo/SecureProfileCodec.cs`
- Create: `Assets/CalmSpace/Runtime/Demo/LegacyProfileV1Migration.cs`
- Create: `Assets/CalmSpace/Runtime/Demo/DevelopmentProfileFixtureInjector.cs`
- Modify: `Assets/CalmSpace/Runtime/Demo/DemoProgress.cs`
- Modify: `Assets/CalmSpace/Runtime/Demo/EncryptedFileDemoProgressStore.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/EncryptedFileDemoProgressStoreTests.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/DemoProgressStoreMigrationTests.cs`

**Consumes / Produces:** Consumes Task 3 pure state/rules and the existing
authenticated protector. Produces the only writable profile store, typed load
status, atomic commands, V1 migration, and development-only fixture injection.

**Load contracts defined in Task 3 and implemented here:**

```csharp
public enum ProfileLoadStatus
{
    Missing = 0,
    LoadedV1 = 1,
    LoadedV2 = 2,
    AuthenticationFailed = 3,
    InvalidData = 4,
    IoError = 5,
    UnsupportedVersion = 6
}

public enum ProfileLoadSource
{
    None = 0,
    Primary = 1,
    Temporary = 2,
    Backup = 3,
    LegacyPlayerPrefs = 4,
    Default = 5
}

public readonly struct ProfileInitializationResult
{
    public bool IsReady { get; }
    public ProfileLoadStatus LoadStatus { get; }
    public ProfileLoadSource Source { get; }
    public bool WasMigrated { get; }
    public bool WasPersisted { get; }
    public DemoProgressSnapshot Snapshot { get; }
}
```

Use:

- `SecureProfileV1Dto` as an exact decoder for the current schema fields;
- `SecureProfileV2Dto` for V1 fields plus stable ID arrays, slot selections, queue, and daily fields;
- `LegacyProfileV1Migration`, which depends only on frozen constants, never on a mutable ScriptableObject catalog;
- an injectable `IProfileFileSystem`/`SystemProfileFileSystem` seam to deterministically test write and I/O failure.

**Atomic store API introduced in the same compile-safe change:**

```csharp
public interface IDemoProgressStore
{
    event Action<DemoProgressSnapshot> ProgressChanged;
    bool IsInitialized { get; }
    int LevelCount { get; }
    DemoProgressSnapshot Current { get; }

    ProfileInitializationResult Initialize(
        int levelCount,
        string defaultThemeId,
        int completionReward);

    ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
        CompleteLevelCommand command);
    ProfileMutationResult<PresentationMutation> MarkPresentationSeen(
        PendingPresentationEntry presentation);
    ProfileMutationResult<MemoryMutation> MarkMemoryViewed(string memoryId);
    ProfileMutationResult<DecorationMutation> PurchaseAndSelectDecoration(
        string slotId,
        string decorationId,
        int cost);
    ProfileMutationResult<DecorationMutation> GrantAndSelectDecoration(
        string slotId,
        string decorationId,
        DecorationGrantSource source);
    ProfileMutationResult<DecorationMutation> SelectDecoration(
        string slotId,
        string decorationId);
    ProfileMutationResult<DailyCareMutation> CompleteDailyCare(
        string careId,
        int utcDayKey,
        int rewardAmount,
        string unlockedMemoryId);
    ProfileMutationResult<PreferenceMutation> SetSelectedTheme(
        string themeId);
    ProfileMutationResult<PreferenceMutation> SetMusicEnabled(bool enabled);
    bool IsLevelUnlocked(int levelIndex);
    bool IsLevelCompleted(int levelIndex);

    // Transitional V1-index callers removed in Tasks 8 and 12.
    void MarkLevelCompleted(int levelIndex);
    int CompleteLevelAndReward(int levelIndex, int rewardAmount);
    bool TryPurchaseAndSelectDecoration(int decorationIndex, int cost);
    bool TrySelectDecoration(int decorationIndex);
}
```

`PlayerPrefsDemoProgressStore` stops implementing `IDemoProgressStore` and
becomes a legacy import reader. `EncryptedFileDemoProgressStore` implements
the complete API. Transitional index methods map through the frozen V1 table
and never construct workshop presentations; the coordinator replaces those
call sites in Task 8, and Task 12 removes the final index-based decor calls.

V1 defaults after migration:

```text
workbench-accent → soft-fern
warm-light       → linen-shade
```

Each owned V1 bit migrates independently. A selected legacy item selects its mapped slot; the other slot receives its free default.

- [ ] **Step 1: Write failing codec, failure, and migration tests**

Add exact cases:

- V2 encrypted round trip preserves every field and unknown IDs;
- V1 preserves completion, rewards, tokens, theme, music, every individual ownership bit, and selected index;
- completed V1 beats are marked as seen room reveals;
- migrated memories are unlocked by completion but not queued or viewed;
- migrated `8/8` queues only an explicit finale;
- write failure returns `PersistFailed`, leaves `Current` unchanged, and raises no event;
- successful command raises `ProgressChanged` exactly once after file replacement;
- primary authenticated V99 returns `UnsupportedVersion` even with a valid V1 backup;
- corrupt primary plus authenticated V99 temporary returns
  `UnsupportedVersion` without falling through to a valid V1/V2 backup;
- corrupt primary/temporary plus authenticated V99 backup returns
  `UnsupportedVersion`;
- a supported valid primary wins over stale temporary/backup candidates;
- corrupt/authentication-failed candidates fall through in strict
  `primary → temporary → backup` order;
- V99 survives two old-app initializations with unchanged primary and backup bytes;
- the development fixture injector writes authenticated V1/V99 using the
  configured protector/AAD and has no callable release-build path;
- unreadable I/O does not create or persist a default;
- exhausted authentication/invalid candidates may create a default;
- valid temporary/backup recovery republishes through the primary only after successful atomic write.

- [ ] **Step 2: Verify RED**

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "CalmSpace.Tests.EditMode.EncryptedFileDemoProgressStoreTests" \
  -testResults "$CALM_PROJECT/Logs/profile-store-red.xml" \
  -logFile "$CALM_PROJECT/Logs/profile-store-red.log" -quit
```

- [ ] **Step 3: Implement candidate selection and codec**

Evaluate candidates in strict recovery order:

| Primary | Temporary | Backup | Result |
|---|---|---|---|
| valid V1/V2 | any | any | load primary |
| authenticated future | any | any | `UnsupportedVersion` |
| unusable | valid V1/V2 | any | recover temporary |
| unusable | authenticated future | valid older | `UnsupportedVersion` |
| unusable | unusable | valid V1/V2 | recover backup |
| unusable | unusable | authenticated future | `UnsupportedVersion` |
| auth/invalid only | auth/invalid only | auth/invalid only | default allowed |
| I/O error at required candidate | any | any | `IoError`, no persist |

“Unusable” here means missing, authentication failure, or invalid data, not an
authenticated unsupported schema. Inspect the authenticated envelope version
before rolling back to any later candidate. Preserve the current `.tmp` and
`.bak` strategy.

`DevelopmentProfileFixtureInjector` is compiled only under
`DEVELOPMENT_BUILD || UNITY_EDITOR`. Before store initialization, it may read
the Android launch extra `calmspace.profileFixture` and ask
`SecureProfileCodec` plus the active device protector to write an
authenticated `v1` or `v99` fixture. It consumes the request once, logs only
status/checksum, and never logs decrypted data. A release build contains no
callable injector.

- [ ] **Step 4: Implement persist-before-publish commands**

Every command computes a candidate snapshot, encrypts it, writes temporary, rotates backup, replaces primary, then updates `Current` and invokes `ProgressChanged`. `AlreadyApplied` performs no write or event. `PersistFailed` returns the old snapshot.

- [ ] **Step 5: Verify GREEN**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.EncryptedFileDemoProgressStoreTests" "profile-store-green"
calm_edit_test "CalmSpace.Tests.EditMode.DemoProgressStoreMigrationTests" "profile-migration-green"
```

Then run:

```bash
git diff --check
```

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime/Demo Assets/CalmSpace/Tests/EditMode
git commit -m "feat: migrate encrypted profile to workshop v2"
```

---

### Task 5: Add Russian and a complete workshop text catalog

**Files:**

- Modify: `Assets/CalmSpace/Runtime/Demo/DemoLocalization.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopTextCatalog.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopTextService.cs`
- Create: `Assets/CalmSpace/Editor/CalmSpaceWorkshopTextBuilder.cs`
- Create: `Assets/CalmSpace/Editor/WorkshopTextBuildValidator.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/DemoLocalizationTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopTextCatalogTests.cs`
- Generate: `Assets/CalmSpace/Config/WorkshopTextCatalog.asset`

**Consumes / Produces:** Consumes existing shared chrome localization and Task
2 stable text keys. Produces append-compatible RU locale support and complete
EN/UK/RU workshop content lookup with build-time completeness validation.

**Locale compatibility:**

```csharp
public enum DemoLocale
{
    English = 0,
    Ukrainian = 1,
    Russian = 2
}
```

Keep existing stored numeric values unchanged. `ToggleLocale()` cycles `EN → УКР → RU → EN`; system Russian resolves to `Russian`.

**Text contract:**

```csharp
[Serializable]
public sealed class WorkshopTextEntry
{
    public string Key { get; }
    public string English { get; }
    public string Ukrainian { get; }
    public string Russian { get; }
}

public interface IWorkshopTextService
{
    string Get(string key);
}
```

Runtime fallback order is requested locale, English, then the stable key. Log each broken key once.

**Required story copy:**

| Key | English | Ukrainian | Russian |
|---|---|---|---|
| `chapter.cozy-workshop.title` | Old Family Workshop | Стара сімейна майстерня | Старая семейная мастерская |
| `memory.summer-trail.title` | Stones from the summer trail | Камінці з літньої стежки | Камни с летней тропы |
| `memory.summer-trail.body` | We brought a small treasure home from every walk. | З кожної прогулянки ми приносили додому маленький скарб. | С каждой прогулки мы приносили домой маленькое сокровище. |
| `memory.fix-everything.title` | Here, we could fix anything | Тут уміли полагодити все | Здесь умели чинить всё |
| `memory.fix-everything.body` | Measure twice. Be patient. Leave it kinder than you found it. | Двічі відмір. Не поспішай. Залиш річ кращою, ніж вона була. | Дважды отмерь. Не спеши. Оставь вещь лучше, чем она была. |
| `memory.open-windows.title` | Open the windows again | Знову відкрити вікна | Снова открыть окна |
| `memory.open-windows.body` | The room was waiting for voices, tea and summer air. | Кімната чекала на голоси, чай і літнє повітря. | Комната ждала голосов, чая и летнего воздуха. |
| `postcard.family-tea.title` | The family tea card | Листівка сімейного чаю | Открытка семейного чаепития |
| `postcard.family-tea.body` | One spoon for the pot, and time enough to sit together. | Одна ложка для чайника — і час, щоб посидіти разом. | Одна ложка для чайника — и время посидеть вместе. |
| `finale.mom.line-1` | I knew the light would find this room again. | Я знала, що світло знову знайде цю кімнату. | Я знала, что свет снова найдёт эту комнату. |
| `finale.mom.line-2` | Let’s open the kitchen next. Everyone will be here soon. | Далі відкриємо кухню. Скоро всі будуть тут. | Дальше откроем кухню. Скоро все будут здесь. |
| `teaser.kitchen` | Next room: the family kitchen | Наступна кімната: сімейна кухня | Следующая комната: семейная кухня |

**Required task/result copy:**

| Key | English | Ukrainian | Russian |
|---|---|---|---|
| `beat.clear-passage.title` | Clear the passage | Звільнити прохід | Освободить проход |
| `beat.clear-passage.result` | The way to the workbench is clear. | Шлях до верстака вільний. | Путь к верстаку свободен. |
| `beat.pebble-shelf.title` | Arrange the trail stones | Розкласти камінці зі стежки | Разложить камни с тропы |
| `beat.pebble-shelf.result` | Every stone has found its place. | Кожен камінець знайшов своє місце. | Каждый камень нашёл своё место. |
| `beat.tea-drawer.title` | Restore the tea drawer | Відновити чайну шухляду | Восстановить чайный ящик |
| `beat.tea-drawer.result` | Family tea is close at hand again. | Сімейний чай знову поруч. | Семейный чай снова под рукой. |
| `beat.paint-shelf.title` | Bring order to the paint shelf | Навести лад на полиці з фарбами | Навести порядок на полке с красками |
| `beat.paint-shelf.result` | The colors are ready for new ideas. | Фарби готові до нових ідей. | Краски готовы к новым идеям. |
| `beat.fastener-tray.title` | Return the tools | Повернути інструменти на місця | Вернуть инструменты на места |
| `beat.fastener-tray.result` | Everything needed to mend and make is back. | Усе для ремонту й творчості знову на місці. | Всё для ремонта и творчества снова на месте. |
| `beat.warm-workbench.title` | Reveal the warm workbench | Повернути тепло верстаку | Вернуть верстаку тепло |
| `beat.warm-workbench.result` | Warm wood has appeared beneath the dust. | Під пилом знову видно тепле дерево. | Под пылью снова видно тёплое дерево. |
| `beat.cabinet-hinge.title` | Repair the old cabinet | Полагодити стару шафу | Починить старый шкаф |
| `beat.cabinet-hinge.result` | The cabinet closes softly again. | Шафа знову зачиняється тихо. | Шкаф снова закрывается тихо. |
| `beat.open-window.title` | Let the light in | Впустити світло | Впустить свет |
| `beat.open-window.result` | Morning light fills the workshop. | Ранкове світло наповнює майстерню. | Утренний свет наполняет мастерскую. |

**Required UI/economy copy:**

| Key | English | Ukrainian | Russian |
|---|---|---|---|
| `home.start` | Start | Почати | Начать |
| `home.catalog` | All spaces | Усі простори | Все пространства |
| `home.album` | Family album | Сімейний альбом | Семейный альбом |
| `home.decor` | Make it yours | Зробити по-своєму | Обустроить по-своему |
| `home.settings` | Settings | Налаштування | Настройки |
| `home.daily-care` | Brew family tea | Заварити сімейний чай | Заварить семейный чай |
| `home.view-workshop` | View the workshop | Подивитися майстерню | Посмотреть мастерскую |
| `home.chapter-complete` | The workshop is ready for everyone. | Майстерня готова зустрічати всіх. | Мастерская готова встретить всех. |
| `save.retry` | Retry save | Повторити збереження | Повторить сохранение |
| `save.return-without` | Return without saving | Повернутися без збереження | Вернуться без сохранения |
| `album.title` | Our family album | Наш сімейний альбом | Наш семейный альбом |
| `album.locked` | A memory is still waiting here. | Тут іще чекає спогад. | Здесь ещё ждёт воспоминание. |
| `common.skip` | Skip | Пропустити | Пропустить |
| `common.close` | Close | Закрити | Закрыть |
| `rewarded.unlock` | Watch to unlock this decor | Переглянути й відкрити цей декор | Посмотреть и открыть этот декор |
| `rewarded.unavailable` | This option is resting for now. | Ця можливість поки відпочиває. | Эта возможность пока отдыхает. |
| `rewarded.closed` | Nothing changed. You can try again later. | Нічого не змінилося. Можна спробувати пізніше. | Ничего не изменилось. Можно попробовать позже. |
| `decor.slot.workbench` | Workbench accent | Акцент верстака | Акцент верстака |
| `decor.slot.light` | Warm light | Тепле світло | Тёплый свет |
| `decor.soft-fern` | Soft fern | Ніжна папороть | Нежный папоротник |
| `decor.river-stones` | River stones | Річкове каміння | Речные камни |
| `decor.clay-vase` | Clay vase | Глиняна ваза | Глиняная ваза |
| `decor.moon-glaze-vase` | Moon-glaze vase | Ваза з місячною поливою | Ваза с лунной глазурью |
| `decor.linen-shade` | Linen shade | Лляний абажур | Льняной абажур |
| `decor.warm-lantern` | Warm lantern | Теплий ліхтар | Тёплый фонарь |
| `decor.paper-light` | Paper light | Паперовий світильник | Бумажный светильник |
| `decor.amber-lamp` | Amber workshop lamp | Бурштинова лампа майстерні | Янтарная лампа мастерской |
| `daily.title` | Brew family tea | Заварити сімейний чай | Заварить семейный чай |
| `daily.instruction` | Add the tea, pour gently, then stir three calm circles. | Додай чай, обережно налий воду й зроби три спокійні кола ложкою. | Добавь чай, аккуратно налей воду и сделай ложкой три спокойных круга. |
| `daily.complete` | Tea is ready. A quiet place is waiting. | Чай готовий. На тебе чекає тихе місце. | Чай готов. Тебя ждёт тихое место. |
| `relax-pass.title` | Relax Pass | Relax Pass | Relax Pass |

No text may be embedded in illustrations.

- [ ] **Step 1: Write failing locale and catalog tests**

Enumerate every `DemoTextKey`, every workshop catalog key, and every decoration/title key. Assert non-empty EN/UK/RU values, unique keys, English fallback, and live locale-change notification.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.DemoLocalizationTests" "localization-red"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopTextCatalogTests" "workshop-text-red"
```

Expected: Russian and workshop text contracts are missing.

- [ ] **Step 3: Implement service, exact copy, and validator**

Do not append workshop prose to `DemoTextKey`; keep that enum append-only for shared chrome. `WorkshopTextBuildValidator` blocks release for duplicate or blank translations.

- [ ] **Step 4: Regenerate and Verify GREEN**

Run configure, then:

```bash
calm_edit_test "CalmSpace.Tests.EditMode.DemoLocalizationTests" "localization-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopTextCatalogTests" "workshop-text-green"
```

- [ ] **Step 5: Commit**

```bash
git add Assets/CalmSpace/Runtime/Demo/DemoLocalization.cs Assets/CalmSpace/Runtime/Workshop Assets/CalmSpace/Editor Assets/CalmSpace/Tests/EditMode Assets/CalmSpace/Config/WorkshopTextCatalog.asset
git commit -m "feat: localize workshop in english ukrainian russian"
```

---

### Task 6: Implement the pure projector and flow coordinator

**Files:**

- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopNavigationContracts.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopProgressProjector.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopFlowCoordinator.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopProgressProjectorTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopFlowCoordinatorTests.cs`

**Consumes / Produces:** Consumes level/workshop catalogs and the V2 profile
store. Produces pure projections, closed recommended actions, typed launch
requests, and atomic completion orchestration; never loads a level itself.

**Contracts:**

```csharp
public enum LevelLaunchSource
{
    Workshop = 0,
    Catalog = 1,
    DailyCare = 2
}

public readonly struct LevelLaunchRequest
{
    public string LevelId { get; }
    public int LevelIndex { get; }
    public LevelLaunchSource Source { get; }
    public string ChapterId { get; }
    public string BeatId { get; }
}

public enum WorkshopRecommendedActionKind
{
    StartLevel = 0,
    ShowPendingReveal = 1,
    ShowPendingMemory = 2,
    ShowFinale = 3,
    OpenCompletedWorkshop = 4
}

public readonly struct WorkshopRecommendedAction
{
    public WorkshopRecommendedActionKind Kind { get; }
    public string StableId { get; }
}

public readonly struct WorkshopProgressProjection
{
    public string ChapterId { get; }
    public int RestoredZoneMask { get; }
    public int CompletedBeatCount { get; }
    public int BeatCount { get; }
    public string ActiveHotspotBeatId { get; }
    public string PendingRevealBeatId { get; }
    public bool DailyCareUnlocked { get; }
    public bool IsComplete { get; }
}
```

`WorkshopProgressProjector.GetRecommendedAction` order:

1. first non-explicit pending queue entry;
2. first unlocked incomplete workshop level;
3. `OpenCompletedWorkshop` when the chapter is complete;
4. never auto-repeat level 8.

An explicit migrated finale is not returned on cold start. `GetExplicitWorkshopAction` turns the completed-workshop CTA into `ShowFinale`.

`WorkshopFlowCoordinator`:

```csharp
public interface IWorkshopFlowCoordinator
{
    WorkshopRecommendedAction? GetRecommendedAction();
    WorkshopRecommendedAction? GetExplicitWorkshopAction();
    bool TryCreateLaunchRequest(
        WorkshopRecommendedAction action,
        out LevelLaunchRequest request);
    ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
        string levelId,
        int levelIndex,
        int rewardAmount);
}
```

The coordinator resolves a beat, builds `Room → Memory? → Finale?`, and calls the store. It never receives or invokes `ILevelFlowController`.

- [ ] **Step 1: Write failing projector tests**

Cover:

- fresh profile → stage 1;
- completion with pending reveal → reveal;
- removed reveal → pending memory or next stage;
- 8/8 live queue → finale after room and memory;
- migrated explicit finale → completed-workshop CTA on cold start;
- explicit CTA → finale;
- seen finale and empty queue → completed workshop;
- invalid workshop metadata → no meta action, while catalog lookup still works;
- every completed beat appears immediately in `RestoredZoneMask`, while
  `PendingRevealBeatId` identifies the one temporary before-overlay animation.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopProgressProjectorTests" "workshop-projector-red"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopFlowCoordinatorTests" "workshop-flow-red"
```

- [ ] **Step 3: Implement projector and coordinator**

Use only constructor-injected catalogs/store and pure scans. Do not cache mutable projections or allocate lists per refresh.

- [ ] **Step 4: Verify GREEN**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopProgressProjectorTests" "workshop-projector-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopFlowCoordinatorTests" "workshop-flow-green"
calm_edit_test "CalmSpace.Tests.EditMode.DemoProgressRulesTests" "workshop-progress-regression"
```

- [ ] **Step 5: Commit**

```bash
git add Assets/CalmSpace/Runtime/Workshop Assets/CalmSpace/Tests/EditMode
git commit -m "feat: coordinate workshop progression actions"
```

---

### Task 7: Extend typed product analytics without adding a production sink

**Files:**

- Modify: `Assets/CalmSpace/Runtime/Analytics/ProductAnalytics.cs`
- Create: `Assets/CalmSpace/Runtime/Analytics/WorkshopAnalyticsSessionState.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/ProductAnalyticsTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopAnalyticsTests.cs`

**Consumes / Produces:** Consumes existing safe provider-neutral analytics.
Produces append-only event kinds, typed workshop context, factories, and
session cardinality guards without enabling a production sink.

Append, never renumber, event kinds:

```text
10 workshop_viewed
11 restoration_task_selected
12 restoration_reveal_started
13 restoration_reveal_completed
14 memory_unlocked
15 memory_viewed
16 album_opened
17 workshop_choice_shown
18 decor_slot_opened
19 daily_care_available
20 daily_care_completed
21 chapter_completed
22 next_room_teaser_viewed
23 rewarded_offer_opened
24 rewarded_offer_outcome
25 relax_pass_screen_opened
```

Extend the typed event payload with dedicated `BeatId`, `SlotId`, `VariantId`, `CareId`, `LaunchSource`, and `Outcome` fields. Do not overload the existing `Flag`.

**Producer and cardinality map:**

| Event | Exact producer | Success/cardinality gate |
|---|---|---|
| `workshop_viewed` | `WorkshopHomeController.NotifyVisibleAsync` | hidden→visible transition, once per actual view |
| `restoration_task_selected` | `WorkshopHomeController.HandlePrimaryAction` / `HandleHotspotPressed` | one event only after one `LevelLaunchRequest` is created |
| `level_started` | `DemoExperienceController.LoadLevelCoreAsync` | after successful Addressable bind; copy request `LaunchSource` |
| `restoration_reveal_started` | `WorkshopPresentationQueueController.RunPendingAsync` | immediately before each real playback attempt; repeats after interruption |
| `restoration_reveal_completed` | `WorkshopPresentationQueueController.RunPendingAsync` | only after `MarkPresentationSeen` returns `Applied` |
| `memory_unlocked` | `WorkshopFlowCoordinator.CompleteLevel` | completion result `Applied` with a memory ID |
| `memory_viewed` | `MemoryAlbumController.PresentUnlockAsync` / explicit album open | after successful `MarkMemoryViewed`; include `FirstView` |
| `album_opened` | `MemoryAlbumController.OpenAlbumAsync` | closed→open transition |
| `decor_slot_opened` | `DecorationPanelController.OpenAsync` | one per explicit slot open |
| `workshop_choice_shown` | `DecorationPanelController.OpenAsync` | one slot impression per panel open, not per refresh |
| existing `decoration_selected` | `DecorationPanelController.HandleVariantPressed` | only mutation `Applied`; include `SlotId`, `VariantId`, and source |
| `daily_care_available` | `DailyCareController.RefreshAvailability` | max once per UTC day key per runtime session |
| `daily_care_completed` | `DailyCareController.PlayAsync` | only persisted mutation `Applied` |
| `chapter_completed` | `WorkshopFlowCoordinator.CompleteLevel` | only first completion of finale beat |
| `next_room_teaser_viewed` | `WorkshopFinalePresenter.PlayAsync` | when teaser actually becomes visible |
| `rewarded_offer_opened` | `DecorationPanelController.HandleRewardedPressed` | one accepted explicit offer |
| `rewarded_offer_outcome` | same one-shot rewarded session | exactly one typed outcome for that offer |
| `relax_pass_screen_opened` | `RelaxPassPanelController.OpenAsync` | closed→open; entry hidden with NoOp provider |

- [ ] **Step 1: Write failing factory and cardinality tests**

Verify event names, serialization, required context, every row in the producer
map, no names/free text/device identifiers, one `daily_care_available` per day
key per runtime session, `level_started.LaunchSource`, slot/variant values on
the existing `DecorationSelected` event, and no duplicate first-time events
for `AlreadyApplied`.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.ProductAnalyticsTests" "product-analytics-red"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopAnalyticsTests" "workshop-analytics-red"
```

- [ ] **Step 3: Implement typed factories and session cardinality**

Emit from explicit actions/state transitions only. Never emit from `Refresh`, `Render`, or projection getters. Keep the development sink; do not add Firebase, an ad identifier, or another provider.

- [ ] **Step 4: Verify GREEN**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.ProductAnalyticsTests" "product-analytics-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopAnalyticsTests" "workshop-analytics-green"
```

- [ ] **Step 5: Commit**

```bash
git add Assets/CalmSpace/Runtime/Analytics Assets/CalmSpace/Tests/EditMode
git commit -m "feat: add typed workshop analytics events"
```

---

### Task 8: Replace the old menu with the living-home navigation shell

**Files:**

- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopBottomSheet.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeView.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeController.cs`
- Modify: `Assets/CalmSpace/Runtime/UI/DemoExperienceController.cs`
- Modify: `Assets/CalmSpace/Runtime/Composition/CalmSpaceLifetimeScope.cs`
- Modify: `Assets/CalmSpace/Runtime/Composition/GameBootstrapper.cs`
- Create: `Assets/CalmSpace/Editor/CalmSpaceWorkshopSceneBuilder.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceDemoSceneBuilder.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopHomeViewTests.cs`
- Create: `Assets/CalmSpace/Tests/PlayMode/WorkshopHomeNavigationPlayModeTests.cs`
- Modify: `Assets/CalmSpace/Tests/PlayMode/StartupScenePlayModeTests.cs`
- Regenerate: `Assets/CalmSpace/Scenes/Main.unity`

**Consumes / Produces:** Consumes Task 6 actions/requests, existing level flow,
fade, HUD, locale/music/haptic services, and Task 7 analytics. Produces a
functional living-home shell and compile-safe navigation bridge; unavailable
later-milestone surfaces remain hidden.

**Home contract:**

```csharp
public enum WorkshopHomeEntryReason
{
    ColdStart = 0,
    ReturnFromLevel = 1,
    CatalogBack = 2,
    ExplicitWorkshopView = 3
}

public interface IWorkshopHomeController
{
    event Action<LevelLaunchRequest> LevelLaunchRequested;
    event Action CatalogRequested;
    UniTask InitializeAsync(CancellationToken cancellationToken);
    UniTask PrepareEntryAsync(
        WorkshopHomeEntryReason reason,
        CancellationToken cancellationToken);
    UniTask NotifyVisibleAsync(
        WorkshopHomeEntryReason reason,
        CancellationToken cancellationToken);
    void NotifyHidden();
}
```

`PrepareEntryAsync` runs behind an opaque curtain. `NotifyVisibleAsync` runs after fade-to-clear so a reveal is never hidden.

`DemoExperienceController` adds:

```csharp
public UniTask<bool> PlayLevelAsync(
    LevelLaunchRequest request,
    CancellationToken cancellationToken = default);

public UniTask ReturnToWorkshopAsync(
    WorkshopHomeEntryReason reason,
    CancellationToken cancellationToken = default);
```

It remains the only owner of `_currentLevelIndex`, fade, Addressable level load/unload, HUD binding, undo, and `level_started`. The completion screen’s primary action becomes “Return to workshop”; it does not call `LoadNextLevelAsync`.

- [ ] **Step 0: Wire the exact VContainer ownership graph**

Add serialized `LivingWorkshopCatalog` and `WorkshopTextCatalog` fields beside
the existing catalogs in `CalmSpaceLifetimeScope`, validate them, then register:

```csharp
builder.RegisterInstance(_livingWorkshopCatalog);
builder.RegisterInstance(_workshopTextCatalog);

builder.Register<WorkshopTextService>(
        resolver => new WorkshopTextService(
            _workshopTextCatalog,
            resolver.Resolve<IDemoLocalizationService>()),
        Lifetime.Singleton)
    .As<IWorkshopTextService>();

builder.Register<WorkshopProgressProjector>(
        resolver => new WorkshopProgressProjector(
            _levelCatalog,
            _livingWorkshopCatalog),
        Lifetime.Singleton)
    .As<IWorkshopProgressProjector>();

builder.Register<WorkshopFlowCoordinator>(
        resolver => new WorkshopFlowCoordinator(
            _levelCatalog,
            _livingWorkshopCatalog,
            resolver.Resolve<IDemoProgressStore>(),
            resolver.Resolve<IWorkshopProgressProjector>()),
        Lifetime.Singleton)
    .As<IWorkshopFlowCoordinator>();

builder.RegisterComponentInHierarchy<WorkshopHomeController>()
    .As<IWorkshopHomeController>();
```

`DemoExperienceController.Construct` receives `IWorkshopHomeController` and
`IWorkshopFlowCoordinator`; it subscribes to typed navigation events and
unsubscribes on destroy. No controller resolves dependencies from a service
locator after construction.

- [ ] **Step 1: Write failing home/navigation tests**

Test event subscription cardinality, one open bottom sheet maximum, one active hotspot, typed workshop/catalog launch sources, level lookup by stable ID, and final-stage behavior that does not replay level 8.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopHomeViewTests" "workshop-home-view-red"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopHomeNavigationPlayModeTests" "workshop-home-navigation-red"
```

- [ ] **Step 3: Build the new generated shell**

The room owns the center/background. The top bar contains tokens, album, daily care, and settings. A compact bottom card around `320` reference pixels contains the next task and primary button. Catalog is secondary. Remove the full-screen `CalmSpaceMenuBackground` overlay and the old `1000`-pixel all-in-one settings/decor card. Keep level select, HUD, completion, loading curtain, and safe-area fitting.

The shell exposes capability bindings rather than empty buttons:

- Album is hidden until Task 11 registers `MemoryAlbumController`.
- Decor is hidden until Task 12 registers `DecorationPanelController` and the
  relevant slot is unlocked.
- Daily Care is hidden until Task 13 registers `DailyCareController` and beat
  3 is complete.
- Relax Pass is hidden with the NoOp provider.
- Settings is functional in Task 8 using the existing locale/music/haptics
  services.

`WorkshopHomeViewTests` must fail if a visible button has no subscribed
controller/action. No milestone APK contains a dead or “coming soon” control.

- [ ] **Step 4: Integrate atomic completion outcomes**

- `Applied`: show reward result, emit first-completion events, return home.
- `AlreadyApplied`: show calm replay result without reward/reveal.
- `PersistFailed`: show localized “Retry save” and “Return without saving”; do not update the room or emit successful completion.
- `Invalid`: log once, leave the last stable screen active.

Retry repeats the same stable completion command. Returning without saving changes no profile state.

- [ ] **Step 5: Preserve transition and loading recovery**

An Addressable load failure restores the workshop as the one interactive
screen and exposes a localized Retry action. Cancellation during fade,
room preparation, catalog return, or level unload leaves exactly one active
`CanvasGroup`. No progress changes on a load failure. Existing
`AddressableLevelFlowController` remains the owner of level handles and
release/unload behavior.

- [ ] **Step 6: Regenerate and Verify GREEN**

Run configure, then:

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopHomeViewTests" "workshop-home-view-green"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopHomeNavigationPlayModeTests" "workshop-home-navigation-green"
calm_play_test "CalmSpace.Tests.PlayMode.StartupScenePlayModeTests" "workshop-startup-green"
```

- [ ] **Step 7: Commit**

```bash
git add Assets/CalmSpace/Runtime/UI Assets/CalmSpace/Runtime/Composition Assets/CalmSpace/Editor Assets/CalmSpace/Tests Assets/CalmSpace/Scenes/Main.unity
git commit -m "feat: make workshop the living home screen"
```

---

### Task 9: Create original 2.5D workshop art and the Addressable room presenter

**Required skills for this task:** Read and use `imagegen` before generating or editing visual assets; use `unity-workbench:unity-feature-implementation` for prefab/editor integration.

**Files:**

- Create: `Assets/CalmSpace/Runtime/Workshop/WorkshopRoomVisualState.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/AddressableWorkshopRoomLoader.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopRoomPresenter.cs`
- Modify: `Assets/CalmSpace/Runtime/Composition/CalmSpaceLifetimeScope.cs`
- Create: `Assets/CalmSpace/Editor/CalmSpaceWorkshopAssetBuilder.cs`
- Create: `Assets/CalmSpace/Editor/WorkshopVisualValidator.cs`
- Create: `Assets/CalmSpace/Content/Workshop/CozyWorkshopRoom.prefab`
- Create: `Assets/CalmSpace/UI/Workshop/WorkshopRoom.spriteatlas`
- Create original art under: `Assets/CalmSpace/UI/Workshop/Art/Room/`
- Create original decor art under: `Assets/CalmSpace/UI/Workshop/Art/Decor/`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopVisualAuthoringTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopAddressableAuthoringTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopRoomPresenterTests.cs`
- Modify: `docs/ASSET_PROVENANCE.md`

**Consumes / Produces:** Consumes approved visual direction, Task 6 room
projection, image generation, and Addressables. Produces original aligned art,
one Addressable room prefab/handle owner, and a visual-only presenter.

**Master image prompt:**

```text
Portrait mobile-game environment, 1080x2400 reference composition, an old
family workshop seen as a charming 2.5D paper diorama from a fixed slightly
elevated front view. Hand-painted gouache, visible warm paper and wood grain,
restrained sage, clay and cream palette, gentle morning side light, cozy but
initially dusty and cluttered. Clearly separated zones: lower-left passage
with soft boxes, left pebble shelf, tea drawer, paint shelf, central tool tray,
large wooden workbench, right cabinet with loose hinge, upper-right dusty
window. Leave readable negative space for a compact top bar and bottom task
card. No words, no logos, no UI, no people, no photorealism, no dramatic
damage, no horror, no harsh saturation. Orthographic-feeling geometry,
consistent object scale, publication-ready original mobile game art.
```

Generate one master composition, then use image edits referencing that same master to create aligned cleaned/restored states. Do not generate eight unrelated rooms. Export:

```text
WorkshopBaseDirty.png
Beat01ClearedWalkway.png
Beat02PebbleShelf.png
Beat03TeaDrawer.png
Beat04PaintShelf.png
Beat05ToolTray.png
Beat06WarmWorkbench.png
Beat07RepairedCabinet.png
Beat08ClearWindow.png
FinalSunlight.png
Curtain.png
Plant.png
RadioGlow.png
```

Local restored layers must use transparent canvases aligned to the base. Decor:

```text
WorkbenchAccent/SoftFern.png
WorkbenchAccent/RiverStones.png
WorkbenchAccent/ClayVase.png
WorkbenchAccent/MoonGlazeVase.png
WarmLight/LinenShade.png
WarmLight/WarmLantern.png
WarmLight/PaperLight.png
WarmLight/AmberWorkshopLamp.png
```

**Presenter contract:**

```csharp
public interface IWorkshopRoomLoader : IDisposable
{
    bool IsLoaded { get; }
    WorkshopRoomPresenter Presenter { get; }
    UniTask<WorkshopRoomPresenter> LoadAsync(
        string chapterId,
        Transform parent,
        CancellationToken cancellationToken);
    UniTask UnloadAsync(CancellationToken cancellationToken);
}

public sealed class WorkshopRoomPresenter : MonoBehaviour
{
    public event Action<string> HotspotPressed;
    public string ChapterId { get; }
    public bool IsVisible { get; }
    public bool IsRevealPlaying { get; }
    public void ApplyState(WorkshopRoomVisualState state);
    public void SetVisible(bool visible);
    public void SetInteractionEnabled(bool enabled);
    public bool SetDecoration(string slotId, string variantId);
    public UniTask<WorkshopRevealPlaybackResult> PlayRevealAsync(
        string beatId,
        CancellationToken cancellationToken);
}
```

Each beat binding has stable beat ID, restored `CanvasGroup`, temporary
before-overlay `CanvasGroup`, hotspot `Button`, and ambient root. Permanent
room state always comes from completed levels: after restart, a completed zone
is already restored. For the single pending beat, `PlayRevealAsync` briefly
places its before-overlay above that correct restored state and dissolves it.
Cancellation removes the temporary overlay and returns to the completed
persisted room; the pending entry remains, so a later home entry may softly
repeat the effect. Missing visuals return `MissingVisual` and log once.

- [ ] **Step 1: Write failing authoring and presenter tests**

Assert eight unique beat bindings, one hotspot per beat, at least `128×128`
reference pixels on the 1080×2400 Canvas, independent decor slots, ambient
roots disabled while hidden, maximum four materials, valid sprite references,
and correct Addressable address/label. The validator converts through the
configured `CanvasScaler` and also asserts a physical minimum of 48 dp rather
than treating raw pixels as dp.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopVisualAuthoringTests" "workshop-visual-red"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopAddressableAuthoringTests" "workshop-addressables-red"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopRoomPresenterTests" "workshop-room-presenter-red"
```

- [ ] **Step 3: Generate, inspect, and import original art**

Visually inspect every generated image before integration. Import Android textures as ASTC 6×6, mipmaps off, Read/Write off, Clamp/Bilinear. Base max size is 4096; overlays/cameo 2048; story cards 1024.

- [ ] **Step 4: Build prefab and Addressable**

Use address `workshop/cozy-workshop/room` and label `calm-space-workshop-cozy-workshop`. Load once for the chapter, hide during a level, release on app shutdown or chapter replacement. The presenter uses no polling `Update`; short ambient motion uses cancellable tweens/coroutines only while visible.

Register the loader only now that its production type exists:

```csharp
builder.Register<AddressableWorkshopRoomLoader>(
        resolver => new AddressableWorkshopRoomLoader(resolver),
        Lifetime.Singleton)
    .As<IWorkshopRoomLoader>();
```

Inject it into `WorkshopHomeController`. Before this task, the Task 8 shell
uses its fully functional localized task card/hotspot and a static generated
paper-texture room fallback; it exposes no missing loader dependency.

- [ ] **Step 5: Regenerate and Verify GREEN**

Run configure, then:

```bash
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopVisualAuthoringTests" "workshop-visual-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopAddressableAuthoringTests" "workshop-addressables-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopRoomPresenterTests" "workshop-room-presenter-green"
```

Capture a Game-view screenshot at Pixel 8 portrait aspect and inspect it for
the approved paper/gouache direction before committing.

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime/Workshop Assets/CalmSpace/Runtime/UI/Workshop Assets/CalmSpace/Editor Assets/CalmSpace/Content/Workshop Assets/CalmSpace/UI/Workshop docs/ASSET_PROVENANCE.md
git commit -m "feat: add illustrated living workshop room"
```

---

### Task 10: Recover and play the persistent presentation queue

**Files:**

- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopPresentationQueueController.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopStoryFallbackView.cs`
- Modify: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeController.cs`
- Modify: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopRoomPresenter.cs`
- Create: `Assets/CalmSpace/Tests/PlayMode/WorkshopPresentationPlayModeTests.cs`
- Modify: `Assets/CalmSpace/Tests/PlayMode/WorkshopHomeNavigationPlayModeTests.cs`

**Consumes / Produces:** Consumes the persisted queue/store and room presenter.
Produces interruption-safe one-at-a-time playback and a localized story
fallback; never owns an authoritative in-memory queue.

**Contract:**

```csharp
public sealed class WorkshopPresentationQueueController : MonoBehaviour
{
    public bool IsPresenting { get; }
    public void BindRoom(WorkshopRoomPresenter presenter);
    public UniTask RunPendingAsync(
        WorkshopHomeEntryReason entryReason,
        CancellationToken cancellationToken);
    public void Cancel();
}
```

- [ ] **Step 1: Write failing interruption tests**

Cover:

- first completion → return → exactly one reveal after fade;
- replay adds no reveal;
- cancel/kill mid-reveal does not mark seen;
- next entry repeats that reveal once;
- a completed animation calls `MarkPresentationSeen` once;
- persisted failure stops the queue and preserves the entry;
- migrated 8/8 does not auto-run finale on cold start;
- memory/finale entries use the accessible localized fallback before the full
  Family Layer is installed;
- missing art shows a localized fallback dissolve, marks the reveal only after that fallback completes, and does not block home.

- [ ] **Step 2: Verify RED**

```bash
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests" "workshop-presentation-red"
```

- [ ] **Step 3: Implement snapshot-driven playback**

Read the first queue item from the latest store snapshot before every
presentation. Do not maintain a second in-memory queue. Disable room input
while presenting. Room entries use the local reveal; memory/finale entries use
`WorkshopStoryFallbackView`, a retained accessibility fallback with localized
text, Skip, and no embedded art text. Task 11 replaces its primary adapters
with the album/cameo while keeping this fallback for missing Addressables.
After successful playback, persist that one item; continue only after
`Applied` or `AlreadyApplied`. Cancellation never acknowledges an entry.

- [ ] **Step 4: Verify GREEN and milestone gate**

```bash
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests" "workshop-presentation-green"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopHomeNavigationPlayModeTests" "workshop-home-milestone-green"
calm_edit_test "CalmSpace.Tests.EditMode.EncryptedFileDemoProgressStoreTests" "workshop-store-milestone-green"
```

Then run the complete EditMode suite using the Task 14 command. Manually verify
the Living Spine flow through all eight stages in Editor Play mode.

- [ ] **Step 5: Commit**

```bash
git add Assets/CalmSpace/Runtime/UI/Workshop Assets/CalmSpace/Tests
git commit -m "feat: recover workshop room reveals"
```

---

### Task 11: Add three memories, album, finale cameo, and kitchen teaser

**Required skill for visual creation:** Read and use `imagegen`; all edits must reference the approved workshop master so color, paper texture, and family style remain coherent.

**Files:**

- Create: `Assets/CalmSpace/Runtime/Workshop/AddressableWorkshopSpriteLoader.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/MemoryAlbumController.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/MemoryAlbumView.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopMemoryCardView.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopFinalePresenter.cs`
- Modify: `Assets/CalmSpace/Runtime/UI/Workshop/WorkshopPresentationQueueController.cs`
- Modify: `Assets/CalmSpace/Runtime/Composition/CalmSpaceLifetimeScope.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceWorkshopAssetBuilder.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceWorkshopSceneBuilder.cs`
- Create story art under: `Assets/CalmSpace/UI/Workshop/Art/Story/`
- Create: `Assets/CalmSpace/UI/Workshop/WorkshopStory.spriteatlas`
- Create: `Assets/CalmSpace/Tests/PlayMode/MemoryAlbumPlayModeTests.cs`
- Modify: `Assets/CalmSpace/Tests/PlayMode/WorkshopPresentationPlayModeTests.cs`
- Modify: `docs/ASSET_PROVENANCE.md`

**Consumes / Produces:** Consumes pending memory/finale entries, text service,
sprite loader, and home bottom sheets. Produces the album, three memories,
postcard surface, cameo, teaser, and queue adapters with released handles.

**Art outputs:**

```text
MemorySummerTrail.png
MemoryFixEverything.png
MemoryOpenWindows.png
PostcardFamilyTea.png
MomDoorway.png
KitchenTeaser.png
```

The mother cameo is a warm 2D illustration with subtle parallax, lasts 6–10 seconds, has the two localized lines from Task 5, and includes Skip. It must not define the player’s face or name.

**Controllers:**

```csharp
public sealed class MemoryAlbumController
{
    public bool IsOpen { get; }
    public event Action Closed;
    public UniTask OpenAlbumAsync(CancellationToken cancellationToken);
    public UniTask<MemoryCardDismissal> PresentUnlockAsync(
        string memoryId,
        CancellationToken cancellationToken);
    public void Refresh(DemoProgressSnapshot snapshot);
    public void Close();
}

public sealed class WorkshopFinalePresenter
{
    public UniTask<FinalePresentationResult> PlayAsync(
        string chapterId,
        CancellationToken cancellationToken);
    public void HideImmediate();
}
```

- [ ] **Step 1: Write failing family-layer tests**

Test memory unlocks at stages 2/5/8, locked-card privacy, unread badge, Skip/Close as a persisted view, album re-open, live EN/UK/RU refresh, sprite-handle release, cameo cancellation recovery, explicit migrated finale, finale seen only after completion, and one kitchen-teaser analytics event per view.

- [ ] **Step 2: Verify RED**

```bash
calm_play_test "CalmSpace.Tests.PlayMode.MemoryAlbumPlayModeTests" "memory-album-red"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests" "family-presentation-red"
```

- [ ] **Step 3: Generate and inspect coherent story art**

Generate family images as faded gouache/photo-card artifacts that belong to the same house. Avoid photorealistic identifiable people; hands, silhouettes, and distant family figures are acceptable. Record tool/date/prompt summary and original status in provenance.

- [ ] **Step 4: Implement album and queue adapters**

Story illustrations load on demand and release on close. Room remains loaded. `MarkMemoryViewed` persists both queued unlock dismissal and manual first view. The queue order remains `room → memory → finale`, with a separate successful persist after each.

Use these Addressable addresses:

```text
workshop/cozy-workshop/memory/family.summer-trail-stones
workshop/cozy-workshop/memory/family.fix-everything
workshop/cozy-workshop/memory/family.open-windows
workshop/cozy-workshop/memory/family.tea-postcard-03
workshop/cozy-workshop/finale/mom
workshop/cozy-workshop/teaser/kitchen
```

Register the sprite loader as a singleton and the generated album/finale
components through `RegisterComponentInHierarchy`; inject both presenters into
the existing queue controller. The retained textual fallback is used whenever
an Addressable story load returns an error.

- [ ] **Step 5: Verify GREEN**

```bash
calm_play_test "CalmSpace.Tests.PlayMode.MemoryAlbumPlayModeTests" "memory-album-green"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests" "family-presentation-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopTextCatalogTests" "family-text-green"
```

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime/Workshop Assets/CalmSpace/Runtime/UI/Workshop Assets/CalmSpace/Editor Assets/CalmSpace/UI/Workshop Assets/CalmSpace/Tests docs/ASSET_PROVENANCE.md
git commit -m "feat: tell family story through workshop memories"
```

---

### Task 12: Implement two decor slots and Relax Pass/rewarded boundaries

**Files:**

- Modify: `Assets/CalmSpace/Runtime/Demo/DemoDecorationCatalog.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/DecorationInventoryRules.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/DecorationPanelController.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/DecorationPanelView.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/RelaxPassPanelController.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/RelaxPassPanelView.cs`
- Create: `Assets/CalmSpace/Runtime/Monetization/RewardedDecorationGrant.cs`
- Modify: `Assets/CalmSpace/Runtime/Demo/DemoThemeCatalog.cs`
- Modify: `Assets/CalmSpace/Runtime/Demo/DemoThemeService.cs`
- Modify: `Assets/CalmSpace/Runtime/Audio/IBackgroundMusicService.cs`
- Modify: `Assets/CalmSpace/Runtime/Audio/BackgroundMusicController.cs`
- Modify: `Assets/CalmSpace/Runtime/Composition/CalmSpaceLifetimeScope.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceDemoAssetBuilder.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceWorkshopSceneBuilder.cs`
- Regenerate: `Assets/CalmSpace/Config/DemoDecorationCatalog.asset`
- Regenerate: `Assets/CalmSpace/Config/DemoThemeCatalog.asset`
- Generate: `Assets/CalmSpace/Audio/FamilyRadioLoop.wav`
- Modify: `Assets/CalmSpace/Tests/EditMode/DemoDecorationCatalogTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/DecorationInventoryRulesTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/EntitledThemeServiceTests.cs`
- Create: `Assets/CalmSpace/Tests/PlayMode/DecorationPanelPlayModeTests.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/MonetizationManagerTests.cs`

**Consumes / Produces:** Consumes V2 atomic decor commands, monetization
snapshot/provider interfaces, room slots, theme/music services, and analytics.
Produces two independent inventory surfaces, one-shot rewarded grant,
entitlement-gated Relax content, and original Family Radio audio.

**Catalog:**

```text
workbench-accent:
soft-fern             0 tokens   free
river-stones         20 tokens   standard/rewarded
clay-vase            60 tokens   standard/rewarded
moon-glaze-vase       Relax Pass only

warm-light:
linen-shade           0 tokens   free
warm-lantern         40 tokens   standard/rewarded
paper-light          60 tokens   standard/rewarded
amber-workshop-lamp   Relax Pass only
```

`DemoDecorationDefinition` gains `SlotId`, localization key, preview sprite, `RequiredEntitlement`, and `AllowsRewarded`. Validation requires one free standard option per slot and forbids rewarded unlock for Relax Pass variants.

- [ ] **Step 1: Write failing inventory, entitlement, and UI tests**

Cover independent selections, insufficient balance, immediate encrypted save,
free defaults, legacy mappings, unknown-ID preservation, NoOp CTA hiding,
exact captured rewarded ID, one grant for one completed/reward-earned outcome,
no grant on close/failure, and background hotspot blocking while the sheet is
open.

Add explicit entitlement cases:

- desired `evening-linen` plus `Unknown/Checking/ProviderUnavailable` applies
  the free visual/audio fallback without rewriting the desired profile ID;
- transition to `VerifiedEntitled` applies Evening Linen and Family Radio;
- transition to `VerifiedNotEntitled` persists the free default exactly once;
- a failed fallback persist leaves the prior desired ID and visible free
  fallback, then permits retry;
- gated palettes, audio variations, and Relax decor never appear in ordinary
  available lists before `VerifiedEntitled`;
- NoOp hides the Relax Pass entry; a configured provider may open the
  information/restore panel, which emits `relax_pass_screen_opened`;
- Relax Pass never changes chapter/level/story unlock state.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.DemoDecorationCatalogTests" "decor-catalog-red"
calm_edit_test "CalmSpace.Tests.EditMode.DecorationInventoryRulesTests" "decor-rules-red"
calm_edit_test "CalmSpace.Tests.EditMode.EntitledThemeServiceTests" "entitled-theme-red"
calm_edit_test "CalmSpace.Tests.EditMode.MonetizationManagerTests" "decor-monetization-red"
calm_play_test "CalmSpace.Tests.PlayMode.DecorationPanelPlayModeTests" "decor-panel-red"
```

- [ ] **Step 3: Implement slot-aware inventory UI**

UI renders from `ProgressChanged`; do not optimistically select. Opening a slot emits one impression. A rewarded request captures the exact stable ID in a one-shot object; only `Completed + RewardEarned` invokes `GrantAndSelectDecoration(slotId, decorationId, Rewarded)`. Ignore `ExtraRoomDecor.Amount` as an index.

Register `DecorationPanelController` and `RelaxPassPanelController` from the
generated scene and inject `IDemoProgressStore`, `IMonetizationManager`,
`IDemoThemeService`, `IBackgroundMusicService`, text, analytics, and room
presenter explicitly. The Home capability binding becomes visible only after
these registrations exist.

- [ ] **Step 4: Add entitlement-aware theme/audio**

Add palette `evening-linen` with `RequiredEntitlement=RelaxPass` and `MusicVariationId=family-radio`. Generate an original seamless Family Radio loop in `CalmSpaceDemoAssetBuilder`; preserve `musicEnabled` separately.

Extend the services with explicit desired/applied state:

```csharp
public enum ContentEntitlement
{
    None = 0,
    RelaxPass = 1
}

public interface IDemoThemeService
{
    string DesiredThemeId { get; }
    ThemePalette Current { get; }
    void ApplyEntitlement(MonetizationSnapshot snapshot);
}

public interface IBackgroundMusicService
{
    string CurrentVariationId { get; }
    bool TrySetVariation(string variationId);
}
```

`ThemePalette` carries `RequiredEntitlement` and `MusicVariationId`.
`ApplyEntitlement` is called from initialization and
`IMonetizationManager.AvailabilityChanged`; it is the only place that may
translate desired gated state into a free applied fallback.

- Before entitlement verification: render the free fallback without rewriting desired `evening-linen`.
- `VerifiedEntitled`: apply Evening Linen and Family Radio.
- `VerifiedNotEntitled`: atomically persist the free default.
- Provider unavailable or NoOp: hide purchase/reward CTAs; do not display fake active store controls.

- [ ] **Step 5: Verify GREEN**

Run configure, then:

```bash
calm_edit_test "CalmSpace.Tests.EditMode.DemoDecorationCatalogTests" "decor-catalog-green"
calm_edit_test "CalmSpace.Tests.EditMode.DecorationInventoryRulesTests" "decor-rules-green"
calm_edit_test "CalmSpace.Tests.EditMode.EntitledThemeServiceTests" "entitled-theme-green"
calm_edit_test "CalmSpace.Tests.EditMode.MonetizationManagerTests" "decor-monetization-green"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopRoomPresenterTests" "decor-room-green"
calm_play_test "CalmSpace.Tests.PlayMode.DecorationPanelPlayModeTests" "decor-panel-green"
calm_play_test "CalmSpace.Tests.PlayMode.StartupScenePlayModeTests" "decor-startup-green"
```

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime Assets/CalmSpace/Editor Assets/CalmSpace/Config Assets/CalmSpace/Audio Assets/CalmSpace/Tests
git commit -m "feat: personalize workshop with calm decor"
```

---

### Task 13: Add the gentle daily family-tea ritual and postcard

**Files:**

- Create: `Assets/CalmSpace/Runtime/Core/UtcClock.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/DailyCareRules.cs`
- Create: `Assets/CalmSpace/Runtime/Workshop/DailyCareDefinition.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/DailyCareController.cs`
- Create: `Assets/CalmSpace/Runtime/UI/Workshop/TeaRitualPresenter.cs`
- Create: `Assets/CalmSpace/Content/Workshop/FamilyTeaRitual.prefab`
- Create daily art under: `Assets/CalmSpace/UI/Workshop/Art/Daily/`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceWorkshopAssetBuilder.cs`
- Modify: `Assets/CalmSpace/Editor/CalmSpaceWorkshopSceneBuilder.cs`
- Modify: `Assets/CalmSpace/Runtime/Composition/CalmSpaceLifetimeScope.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/DailyCareRulesTests.cs`
- Create: `Assets/CalmSpace/Tests/PlayMode/DailyCareFlowPlayModeTests.cs`
- Modify: `docs/ASSET_PROVENANCE.md`

**Consumes / Produces:** Consumes stage-3 completion, UTC clock, presentation
activity leases, V2 daily command, and story queue. Produces one Addressable
no-fail tea ritual, +5 idempotent reward, total care count, and postcard 3.

**Clock:**

```csharp
public interface IUtcClock
{
    DateTime UtcNow { get; }
}

public static int ToUtcDayKey(DateTime utc)
{
    return (utc.Year * 10000) + (utc.Month * 100) + utc.Day;
}
```

**Daily rules:**

- unlock after stage 3 (`03-tea-drawer`);
- first care is available immediately;
- after completion, the next is available only for a later UTC day key;
- same/lower key never duplicates a reward and never removes tokens/count;
- one atomic completion grants 5 Cozy Tokens and increments total care count;
- third completion queues `family.tea-postcard-03`;
- an unfinished ritual remains available after date/app changes;
- no countdown, expiry copy, resettable streak, or deadline.

**Interaction:** a calm modal sequence—place tea tin, add one spoon, pour water, then make three gentle stir arcs. Progress is event-driven; elapsed time is never a success condition. The expected human pace is 45–90 seconds.

- [ ] **Step 1: Write failing pure and flow tests**

Test locked/unlocked state, first availability, same-day idempotency, forward-day availability, backward-clock non-punishment, cancel without reward, exactly +5, third-care postcard, no input/ad overlap, and absence of timer/streak UI.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.DailyCareRulesTests" "daily-care-rules-red"
calm_play_test "CalmSpace.Tests.PlayMode.DailyCareFlowPlayModeTests" "daily-care-flow-red"
```

- [ ] **Step 3: Implement clock, rules, and modal**

Acquire a gameplay presentation lease for the entire ritual so ads cannot start. Require an empty level root. Disable home/hotspot input. Release all Addressable handles and leases on close/cancel. UI updates only after a successful profile persist.

Register `SystemUtcClock` as `IUtcClock` singleton and the generated
`DailyCareController` component in `CalmSpaceLifetimeScope`; inject the
profile store, workshop catalog, activity coordinator, analytics, and
presentation queue. Do not route daily care through `ILevelFlowController`.

- [ ] **Step 4: Generate/import daily art and configure Addressable**

Create `TeaRitualBackground.png`, `TeaTin.png`, `Spoon.png`, `Cup.png`, `Kettle.png`, and `Steam.png` in the established paper/gouache style. Address:
`workshop/cozy-workshop/daily/family-tea`; use the workshop label.

- [ ] **Step 5: Verify GREEN**

Run configure, then:

```bash
calm_edit_test "CalmSpace.Tests.EditMode.DailyCareRulesTests" "daily-care-rules-green"
calm_edit_test "CalmSpace.Tests.EditMode.MonetizationManagerTests" "daily-care-monetization-green"
calm_play_test "CalmSpace.Tests.PlayMode.DailyCareFlowPlayModeTests" "daily-care-flow-green"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopPresentationPlayModeTests" "daily-care-presentation-green"
```

- [ ] **Step 6: Commit**

```bash
git add Assets/CalmSpace/Runtime Assets/CalmSpace/Content/Workshop Assets/CalmSpace/UI/Workshop Assets/CalmSpace/Editor Assets/CalmSpace/Tests docs/ASSET_PROVENANCE.md
git commit -m "feat: add gentle family tea return ritual"
```

---

### Task 14: Enforce anti-anxiety, authoring, performance, and integration gates

**Files:**

- Modify: `Assets/CalmSpace/Editor/AntiAnxietyProjectValidator.cs`
- Create: `Assets/CalmSpace/Editor/LivingWorkshopReleaseValidator.cs`
- Modify: `Assets/CalmSpace/Tests/EditMode/AntiAnxietyProjectValidatorTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/LivingWorkshopReleaseValidatorTests.cs`
- Create: `Assets/CalmSpace/Tests/EditMode/WorkshopAllocationTests.cs`
- Create: `Assets/CalmSpace/Tests/PlayMode/WorkshopIdlePerformancePlayModeTests.cs`
- Create: `Assets/CalmSpace/Runtime/Diagnostics/WorkshopPerformanceProbe.cs`
- Modify: `Assets/CalmSpace/Tests/PlayMode/StartupScenePlayModeTests.cs`
- Delete after all references move: `Assets/CalmSpace/Runtime/UI/DemoRoomPresenter.cs`
- Delete after replacement: `Assets/CalmSpace/Tests/EditMode/DemoRoomPresenterTests.cs`
- Modify: `docs/PRODUCT_VALIDATION_PLAN.md`
- Modify: `docs/PLAY_STORE_RELEASE.md`
- Modify: `docs/LLM_REVIEW_GUIDE.md`
- Modify: `docs/ASSET_PROVENANCE.md`

**Consumes / Produces:** Consumes every prior milestone plus current validators
and test harnesses. Produces release-blocking anti-anxiety/authoring gates,
attributed allocation evidence, cleaned obsolete code, and honest validation
documentation.

- [ ] **Step 1: Write failing release-gate tests**

The validator must reject:

- invalid chapter/catalog/text joins;
- missing free decor defaults;
- timer/countdown/fail/lose/energy/lives/streak-reset contracts in workshop/core level UI;
- any interstitial provider or call site;
- any rewarded CTA inside HUD/level/completion;
- workshop art missing a provenance record or expected non-text asset label;
- missing Addressable room/daily/story assets;
- more than four room materials;
- missing EN/UK/RU;
- a generated Home still using `CalmSpaceMenuBackground`;
- a completion primary action still invoking “next level”.

- [ ] **Step 2: Verify RED**

```bash
calm_edit_test "CalmSpace.Tests.EditMode.LivingWorkshopReleaseValidatorTests" "workshop-release-validator-red"
calm_edit_test "CalmSpace.Tests.EditMode.AntiAnxietyProjectValidatorTests" "workshop-anti-anxiety-red"
calm_edit_test "CalmSpace.Tests.EditMode.WorkshopAllocationTests" "workshop-allocation-red"
calm_play_test "CalmSpace.Tests.PlayMode.WorkshopIdlePerformancePlayModeTests" "workshop-idle-performance-red"
```

- [ ] **Step 3: Implement validators and remove obsolete room code**

Validators should inspect serialized assets, scene bindings, Addressable settings, and relevant type/field contracts. Avoid brittle scans of comments; scan player-facing text keys and serialized components.

- [ ] **Step 4: Enforce accessibility behavior**

Dynamic fonts, wrapping, and safe-area bounds must work on every Home, Album,
Decor, Daily, fallback, and Cameo text surface. Every interactive target is at
least 48 dp, has a non-color cue, and is reachable with sound disabled.
Important audio/haptic feedback has a visual counterpart. Existing music and
haptic disable controls remain available in Settings and persist.

- [ ] **Step 5: Perform the text-in-art visual gate**

Open every Room, Decor, Story, and Daily source image at original resolution
and confirm that it contains no player-facing words. Run OCR as a secondary
warning pass, but require visual approval because OCR cannot reliably prove
absence. Record this checklist in `docs/ASSET_PROVENANCE.md`; do not pretend a
serialized Unity validator can inspect semantic pixel content.

- [ ] **Step 6: Add attributed allocation gates**

`WorkshopAllocationTests` warms and then invokes the pure projector/rules and
explicit `ApplyState`/`Render` paths 1,000 times while measuring
`GC.GetAllocatedBytesForCurrentThread`; each named workshop path must report
zero additional managed bytes.

`WorkshopIdlePerformancePlayModeTests` runs two isolated player harnesses:
an empty UI baseline and the warmed Workshop Home. Sample 300 frames with
`ProfilerRecorder`; require no `GC.CollectionCount` change and require the
workshop median/p95 allocation values to be no worse than the baseline. This
global recorder is a regression signal, not attribution proof.

`WorkshopPerformanceProbe`, compiled only for Editor/Development builds, wraps
home projection, render, room ambient, and reveal code in named
`ProfilerMarker`s and emits one machine-readable summary when explicitly
enabled. It is absent from release behavior. Also assert ambient work stops
while hidden and no workshop component owns an idle polling `Update`.

- [ ] **Step 7: Verify GREEN with full desktop validation**

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -executeMethod CalmSpace.Editor.CalmSpaceProjectSetup.ConfigureProject \
  -logFile "$CALM_PROJECT/Logs/final-configure.log" -quit
```

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform EditMode \
  -testResults "$CALM_PROJECT/Logs/final-editmode.xml" \
  -logFile "$CALM_PROJECT/Logs/final-editmode.log" -quit
```

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "$CALM_PROJECT/Logs/final-playmode.xml" \
  -logFile "$CALM_PROJECT/Logs/final-playmode.log" -quit
```

Expected: all tests pass, no compiler errors, no leaked Addressable-handle warnings, no build-validator errors.

- [ ] **Step 8: Perform focused code review**

```bash
rg -n "TODO|FIXME|TBD|NotImplementedException|Interstitial|Countdown|Lose|FailState" Assets/CalmSpace docs
git diff --check
git status --short
```

Resolve production placeholders. Confirm `InstantSolutionHint` exists only as an unused extension enum and no UI/call site exposes it.

- [ ] **Step 9: Update validation documentation**

Document the implemented funnel, exact event definitions, disabled production providers, asset provenance, test counts, and remaining Play Store work without claiming D1/D7 or revenue proof.

- [ ] **Step 10: Commit**

```bash
git add Assets/CalmSpace docs
git commit -m "test: gate living workshop release quality"
```

---

### Task 15: Build, validate on Pixel 8, and publish the review branch

**Required skills:** Read and use `unity-workbench:unity-build-validation`, `game-studio:game-playtest`, and `test-android-apps:android-performance`.

**Files:**

- Build: `Builds/Android/CalmSpace-Demo-debug.apk`
- Record: `docs/DEVICE_QA_PIXEL_8.md`
- Update: `docs/LLM_REVIEW_GUIDE.md`
- Update public branch: `agent/cross-platform-calm-refactor`
- Update public PR: `https://github.com/Kachalaba/calm-space-tidy-restore/pull/2`

**Consumes / Produces:** Consumes the fully green project, Development APK
builder, Pixel 8, ADB, and public GitHub branch. Produces APK checksum,
migration/clean-install/performance evidence, device QA document, and a
review-ready public PR without merging it.

- [ ] **Step 1: Build the Android APK**

```bash
"$CALM_UNITY" -batchmode -projectPath "$CALM_PROJECT" \
  -buildTarget Android \
  -executeMethod CalmSpace.Editor.CalmSpaceProjectSetup.BuildAndroidTestApk \
  -logFile "$CALM_PROJECT/Logs/android-build.log" -quit
```

Verify:

```bash
shasum -a 256 "$CALM_PROJECT/Builds/Android/CalmSpace-Demo-debug.apk"
```

- [ ] **Step 2: Verify the Pixel and preserve the migration fixture**

```bash
adb devices -l
adb -s 39131FDJH0005Q shell getprop ro.product.model
```

Expected model: Pixel 8. Before any clean-install test, preserve the currently installed debug profile as a migration fixture when `run-as` access is available. Record whether the fixture is V1 or V2 and its checksum; never commit decrypted player data.

- [ ] **Step 3: Run the migration-preserving upgrade sequence**

```bash
mkdir -p "$CALM_PROJECT/Builds/Android/DeviceFixtures"
adb -s 39131FDJH0005Q exec-out run-as com.calmspace.tidyrestore \
  tar -cf - files/player-profile-v1.bin \
  > "$CALM_PROJECT/Builds/Android/DeviceFixtures/pixel8-preupgrade-profile.tar"
shasum -a 256 "$CALM_PROJECT/Builds/Android/DeviceFixtures/pixel8-preupgrade-profile.tar"
adb -s 39131FDJH0005Q logcat -c
adb -s 39131FDJH0005Q install -r "$CALM_PROJECT/Builds/Android/CalmSpace-Demo-debug.apk"
adb -s 39131FDJH0005Q shell am force-stop com.calmspace.tidyrestore
adb -s 39131FDJH0005Q shell monkey -p com.calmspace.tidyrestore -c android.intent.category.LAUNCHER 1
```

Confirm the device log reports `LoadedV1`, `WasMigrated=true`, and a successful
V2 persist. Force-stop and launch again; confirm `LoadedV2` with the same
progress/tokens/theme/music/decor. This is the non-destructive upgrade path;
do not uninstall before it passes.

- [ ] **Step 4: Validate authenticated future-schema preservation**

The APK is a Development build, so ask the in-app fixture injector to create
V99 through the Pixel’s own Keystore and the production AAD:

```bash
adb -s 39131FDJH0005Q shell am start -S -W \
  -a android.intent.action.MAIN \
  -c android.intent.category.LAUNCHER \
  -p com.calmspace.tidyrestore \
  --es calmspace.profileFixture v99
adb -s 39131FDJH0005Q exec-out run-as com.calmspace.tidyrestore \
  sha256sum files/player-profile-v1.bin files/player-profile-v1.bin.bak
adb -s 39131FDJH0005Q shell am force-stop com.calmspace.tidyrestore
adb -s 39131FDJH0005Q shell monkey -p com.calmspace.tidyrestore -c android.intent.category.LAUNCHER 1
adb -s 39131FDJH0005Q shell am force-stop com.calmspace.tidyrestore
adb -s 39131FDJH0005Q shell monkey -p com.calmspace.tidyrestore -c android.intent.category.LAUNCHER 1
adb -s 39131FDJH0005Q exec-out run-as com.calmspace.tidyrestore \
  sha256sum files/player-profile-v1.bin files/player-profile-v1.bin.bak
```

Both launches must report `UnsupportedVersion`; both checksum pairs must be
identical. Never copy a desktop-encrypted fixture into the Android sandbox.

- [ ] **Step 5: Run the destructive clean-install sequence**

The migration evidence is now preserved. Explicitly record that the next
command clears Calm Space app data and its development Keystore entry, then:

```bash
adb -s 39131FDJH0005Q uninstall com.calmspace.tidyrestore
adb -s 39131FDJH0005Q install "$CALM_PROJECT/Builds/Android/CalmSpace-Demo-debug.apk"
adb -s 39131FDJH0005Q logcat -c
adb -s 39131FDJH0005Q shell monkey -p com.calmspace.tidyrestore -c android.intent.category.LAUNCHER 1
```

Confirm a default V2 profile and the dirty workshop. Query and record the
phone’s Wi-Fi/mobile-data state, disable both services, cold-start once, then
restore each service to its recorded state. Offline launch must preserve the
same room/profile and show no provider error modal.

- [ ] **Step 6: Complete the device playtest**

On the Pixel 8 verify:

- all eight stages;
- one-time rewards and no replay reveal;
- room → memory → finale ordering;
- migrated explicit finale CTA;
- two independent decor slots and persistence;
- NoOp purchase/reward controls hidden;
- daily tea cancel/success/next UTC day/backward clock behavior;
- postcard at care 3;
- EN/UK/RU on open Home/Album/Decor/Cameo;
- safe area, touch targets, haptics, ASMR, music transitions;
- no Unity/JNI/Addressables exceptions;
- the development performance probe reports zero attributed bytes for warmed
  workshop projection/render methods and the idle-player harness remains at
  its recorded baseline.

Capture screenshots for dirty room, stage 4, completed room, album, decor sheet, Russian UI, and Ukrainian UI. Record objective outcomes in `docs/DEVICE_QA_PIXEL_8.md`.

- [ ] **Step 7: Capture the on-device performance probe**

```bash
adb -s 39131FDJH0005Q logcat -c
adb -s 39131FDJH0005Q shell am start -S -W \
  -a android.intent.action.MAIN \
  -c android.intent.category.LAUNCHER \
  -p com.calmspace.tidyrestore \
  --es calmspace.performanceProbe workshop-home
adb -s 39131FDJH0005Q logcat -d -s Unity
```

Exercise the visible Home for at least 300 post-warm-up frames. Save the
machine-readable `CALMSPACE_HOME_PERF` summary and inspect named profiler
markers; fail the gate for attributed allocations, collections, or recurring
Addressable/JNI errors.

- [ ] **Step 8: Re-run final tests after device fixes**

Repeat the full EditMode/PlayMode commands from Task 14 and rebuild if any source or generated asset changed.

- [ ] **Step 9: Commit QA evidence**

```bash
git add docs/DEVICE_QA_PIXEL_8.md docs/LLM_REVIEW_GUIDE.md
git commit -m "docs: record pixel 8 workshop validation"
```

- [ ] **Step 10: Push and update the public PR**

```bash
git status --short --branch
git push origin agent/cross-platform-calm-refactor
gh pr view 2 --repo Kachalaba/calm-space-tidy-restore
```

Update PR #2 with the four milestone summary, exact test counts, APK checksum, Pixel 8 evidence, known NoOp monetization limitation, and review paths. Mark ready for review only after every required gate above is green. Do not merge without a separate user instruction.

## Final Definition of Done

- The first screen is the living illustrated workshop, not the old menu/card.
- Eight existing levels form one persistent restoration chapter without changing stable level IDs.
- A first completion persists reward and queue atomically; replay never duplicates them.
- Kill/restart safely resumes at most one pending room, memory, or finale item.
- Old V1 profiles migrate; authenticated future versions are never overwritten or downgraded.
- Three memories, album, cameo, kitchen teaser, two decor slots, and daily tea are complete in EN/UK/RU.
- No timer, fail state, forced ad, energy, expiring streak, or in-level rewarded CTA exists.
- Relax Pass content is entitlement-gated and NoOp providers show no fake store controls.
- Full EditMode/PlayMode suites pass, Android APK builds, and Pixel 8 QA is documented.
- The public GitHub branch and PR contain the complete reviewable demo; merge remains user-controlled.
