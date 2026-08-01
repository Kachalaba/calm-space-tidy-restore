# Calm Space Product Validation Plan

> **Decision update — 2026-07-30:** the previously proposed three separate hero
> restorations have been replaced by one coherent eight-stage Living Workshop
> vertical slice. The approved product and technical scope is defined in
> `docs/superpowers/specs/2026-07-30-living-workshop-retention-design.md`.
> Evidence gaps and the soft-launch gates in this document remain applicable.

## Commercial decision

Keep the anti-anxiety positioning and test the **Cozy Restoration Workshop**
direction with a limited soft launch. Do not commit to 30 levels, a monthly
subscription, or paid user-acquisition scaling before the core hypothesis has
real retention and monetization evidence.

The repository is a tested vertical slice, not a store-ready business. Its
technical foundation is stronger than the Gemini report assumed:

- Cozy Tokens already connect first-time level completion to decor;
- Android/iOS haptic routing and shared rate limiting already exist;
- cleaner coverage already uses a 64x64 downsample with a ten-frame gate;
- encrypted progression and entitlement stores already exist;
- monetization policy is rewarded-only with a Relax Pass boundary;
- infinite session Undo and anti-anxiety validation already exist.

## What the report got right

The useful product hypothesis is a coherent workshop journey:

1. organize or disassemble an object;
2. clean and polish it;
3. fit the restored parts;
4. place the result into a persistent cozy space.

The project should validate this with a few excellent hero restorations rather
than a large batch of disconnected levels.

## Phase 0 implemented

The existing catalog now contains one authored **Cozy Workshop** journey across
levels 03-08. Each stage remains an independently loaded Addressable level, so
existing save indices, release/unload behavior, Undo ownership, and first-time
Cozy Token rewards remain backward compatible.

Player-facing UI shows:

- localized chapter name;
- current chapter stage;
- actual stage name;
- `Continue restoring` between adjacent stages;
- separate intermediate and chapter-complete copy.

This is deliberately a narrative/product grouping, not yet a seamless
single-object hero restoration.

## Measurement foundation

Provider-neutral product events are instrumented for:

- `session_started`;
- `level_started`;
- `level_completed`;
- `level_abandoned`;
- `undo_used`;
- `decoration_purchased`;
- `decoration_selected`;
- `theme_selected`;
- `music_changed`;
- `locale_changed`.

Every level event includes the stable level id, type, catalog index, restoration
chapter/stage context, and relevant progress or economy values. Development
builds emit inspectable structured logs. Release builds collect nothing until a
consent-aware production analytics adapter is selected.

Analytics failures are isolated after the first provider error and can never
block gameplay.

## Evidence gaps

The Gemini report contains no cited market sources. Treat all CPI, eCPM,
retention, payer-rate, CTR, audience-share, and LTV values as hypotheses.

Its scale table is not a valid unit-economics model: revenue per install changes
with cohort size while the underlying assumptions do not. At its stated base
CPI of $0.75:

- 10,000 installs cost $7,500 versus $920 stated D30 revenue;
- 100,000 installs cost $75,000 versus $52,000 stated D60 revenue;
- the claimed 75-day payback does not follow from the supplied LTV curve.

Store fees, taxes, refunds, consent loss, ad fill, content production, and live
operations are also absent. No subscription decision should use this model.

## Next validation increment

Build the approved **Old Family Workshop** vertical slice:

1. connect all eight existing levels to eight permanent room transformations;
2. make the living workshop the primary home and keep level select secondary;
3. add the family memory layer, two reversible decor slots, one non-expiring
   daily care ritual, and the lifetime Relax Pass boundary;
4. validate the encrypted profile migration, typed product events, EN/UK/RU,
   and the complete flow on Pixel 8.

Implementation is split into independently verified milestones so the core
`level → room reveal → next task` funnel can be measured before family,
personalization, and daily-return layers are added.

## Go / no-go sequence

1. Test 3-5 raw gameplay creatives before buying a large cohort.
2. Add a consent-aware analytics provider, Google Play Billing, and one
   rewarded provider behind the existing interfaces.
3. Run closed testing and verify event completeness, save migration, purchase
   restore, and Data Safety declarations.
4. Acquire a limited, geographically separated cohort.
5. Decide whether to produce 20-30 levels using observed CPI, tutorial
   completion, level 1-3 completion, D1/D7, rewarded opt-in, store-open rate,
   payer conversion, ARPDAU, and crash-free sessions.

Do not add forced interstitials merely to improve short-term ARPDAU. That would
invalidate the product's clearest positioning advantage.

## Decisions intentionally deferred

- production analytics/attribution vendor;
- rewarded ad network or mediation stack;
- price points and regional pricing;
- a second playable room, pet, or seasonal pass;
- 30-level content production;
- paid scaling in Tier-1 markets.

These decisions require behavioral data, content cadence, provider terms,
privacy review, and store configuration that do not yet exist.
