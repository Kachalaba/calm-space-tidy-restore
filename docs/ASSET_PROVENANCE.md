# Asset provenance

This demo does not include third-party stock art, music, or sound samples.

## Generated visual assets

- `Assets/CalmSpace/UI/Art/CalmSpaceMenuBackground.png` — created specifically
  for Calm Space with OpenAI ImageGen on 2026-07-28.
- `Assets/CalmSpace/UI/Art/CalmSpaceAppIcon.png` — created specifically for
  Calm Space with OpenAI ImageGen on 2026-07-28.
- `Assets/CalmSpace/UI/Art/RoundedRectangle.png` — generated as a simple UI
  shape by the project editor tooling.

## Cozy workshop room art

Everything under `Assets/CalmSpace/UI/Workshop/Art/Room/` was created for this
project on 2026-08-02 by the in-repo generator `tools/art/generate_workshop_room.py`
(with its painting library `tools/art/workshop_paint.py`). The generator is
original Python that paints the room from scratch with numpy and Pillow: value
noise, hand-cut masks, gouache colour pooling, cast shadows, paper-cut edge
lips and a paper-tooth finish. It reads no external image, texture, brush, or
model, downloads nothing, and embeds no third-party sample. Regenerating from
source reproduces the committed PNGs byte for byte.

An image-generation model was **not** used for these files. The earlier Task 9
plan assumed a raster image-generation workflow; that tool was not available in
the session that produced this art, so the room was authored procedurally
instead of substituting third-party or unattributable assets.

Structure of the generated set:

- `WorkshopBaseDirty.png` — the single 1080x2400 master painting of the dusty
  room. Every other room file is a local edit of this same master.
- `Beat0N<Name>.png` / `Beat0N<Name>Before.png` — the restored and dusty crops
  of one of eight pairwise-disjoint zone rectangles. The "before" crop is
  byte-identical to that region of the master, which is asserted by the
  generator, so overlays land exactly on what the player already sees.
- `FinalSunlight.png` — the transparent warm finale wash.
- `workshop-room-manifest.json` — the zone rectangles and SHA-256 digests that
  `CalmSpaceWorkshopAssetBuilder` consumes to build the room prefab.

`Assets/CalmSpace/UI/Workshop/WorkshopRoom.spriteatlas` and
`Assets/CalmSpace/Content/Workshop/CozyWorkshopRoom.prefab` are generated from
that art by project-owned editor tooling.

Screenshots under `docs/screenshots/` are renders of this project's own
generated scene, captured by `CalmSpaceRoomScreenshot`.

## Generated audio

- `Assets/CalmSpace/Audio/QuietAtelierLoop.wav` — original procedural ambient
  composition synthesized for this project. It contains no third-party audio
  samples.
- `Assets/CalmSpace/Audio/Tactile/*.wav` — six original placement sounds
  (SoftCloth, WarmWood, RiverStone, GlazedClay, SmallMetal, HeavyBench)
  synthesized by `Assets/CalmSpace/Editor/CalmSpaceTactileAudioBuilder.cs` on
  2026-08-08. Each is a short filtered-noise contact transient shaped by a few
  damped sine partials, written directly to PCM by project-owned editor code.
  They contain no third-party audio sample and regenerate byte-identically
  from source. The pooled audio service picks one at random per placement so
  repeated tidying reads as physical rather than looped.
- `Assets/CalmSpace/Audio/SnapSoft.wav` — original procedural interaction sound
  synthesized for this project. It contains no third-party audio samples.

## Code-generated content

The level geometry, materials, masks, and generated scene/prefab content are
created from project-owned C# editor tooling and shaders. Unity packages remain
subject to their respective upstream licenses.

This provenance note documents origin only. The repository does not grant an
open-source or asset-reuse license unless a separate `LICENSE` file is added.
