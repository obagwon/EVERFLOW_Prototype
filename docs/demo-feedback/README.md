# EVERFLOW demo feedback pass — 2026-11-08

Scene: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`.

## Changes

- Added `EFDemoFeedback`, a read-only observer of the existing target, gate and world states. It never calls ability Apply/Undo, moves a device, changes input/camera ownership, writes supply rules or changes colliders.
- Reused the existing ability palette, live orb objects, water impact particle material, Cinemachine finale and HUD. No audio files or AudioMixer assets were found under Assets; twelve deterministic mono PCM clips are synthesized once per Play session. No external downloads, purchased assets or packages.
- BLOOM, FREEZE, FLOW and thaw receive small coloured bursts, expanding rings, orb flight trails and distinct short sound cues. Eight reusable impact slots and eight short-sound voices bound overlapping effects.
- D5's connection socket has amber/moving, green/aligned or supplied, red/misaligned fixed and cyan/internal-ice states. The existing context panel reports alignment, wrong fixation, bypass and blocked water separately. Alignment-ready notice is debounced.
- G/D4 stone motion uses a quiet loop only while the door moves, followed by a one-time latch cue. Water supply, restoration and actual garden arrival each have their own event cue. Nearby running water and the restored wheel have low-volume spatial ambience.
- HUD retains its existing arrangement. A short confirmation uses the existing toast area; it does not add another panel over the reticle. Runtime floating TextMesh test annotations are hidden by default because the HUD already carries their information. Toggle `hideWorldDebugLabels` on the component to retain them.
- Existing camera shots were retained: no new camera controller, forced gameplay camera movement or shake.

## Lifecycle and audio

There are 12 AudioSources: 8 effect voices, 2 gate-motion loops, water and wheel ambience. Audio volumes use the component's `effectsVolume` and `ambientVolume` gain controls; no mixer authoring or project audio settings changes. Pause pauses sources/particles; restart clears rings, bursts and trails, resets observation and restarts the two quiet ambient beds without accumulating sources. Respawn clears long trails and outstanding one-shots.

`pool-check.json` records 12 repeated impact requests with source/particle pool sizes unchanged. `audio-output.json` records nonzero actual AudioSource output RMS. `preservation.json` compares the immediate scene baseline: existing camera, physics, prefab overrides and MonoBehaviour configuration unchanged, one new component block added. The generated sounds are synthesized placeholders and have not received human listening approval as final sound design.

## Validation and recordings

- `runtime-route-alternate.txt`: 28 passing synthetic-input checks, no failures; leaf/C/wheel/ice/alignment/thaw route through garden and Enter restart. This first recording predates the final hiding of duplicate floating labels.
- `continuous-demo.mp4`: ~180.6 s of that actual keyboard/mouse route, with audio. Capture began shortly after Play started; it is not a complete opening cinematic recording.
- `runtime-route-bloom.txt` / `bloom-demo.mp4`: final root/bypass route after label cleanup, **18 checks passed / 0 failures** through garden and Enter restart. Video is 173.17 s with an audio track. Both routes together passed 46 checks.
- `*-play.png`: captures emitted during actual observed state transitions, not staged success screenshots. `bloom.png` is an explicit effect-only preview and is not evidence that its target was solved.
- Existing input route tests have not been rewritten to force passes for feedback. Separate forced-state scene reviews are not used as traversal evidence.
- `lifecycle-final.json`: final pause test passed; 12 audio sources / 8 burst systems remain constant; restart leaves zero burst particles and zero transient rings; exactly 3 orb trails; garden/success state reset.
- Final scene saved with one feedback component, normal Cinemachine Brain enabled, Play stopped, background override off. Current console: zero errors/warnings and no compilation failure. Earlier bridge/Editor history remains in the captured log.
- After the two route recordings, a final cosmetic fix included TextMesh annotations parented under separate scene roots in the hide list. A fresh Play smoke check counted one visible world TextMesh renderer, unchanged 12/8 audio/burst pools and no errors. Thus the check does not certify that every later-created world label is hidden. The route videos precede that final label-scope-only fix; gameplay logic was unchanged and the full routes were not repeated for it.

Scoped visual assessment: palette discipline 3/5 (existing green/cyan/blue); style uniformity 3/5 (small palette-tinted effects); UI intentionality 3/5 (reuse existing panels, remove floating duplicate annotations); feedback visibility improved from sparse/silent to 3/5 (burst/trail/ring and sound at completed transitions). Lighting and character silhouette were outside this pass and remain the existing prototype art. No standalone build, device performance profiling or human audio mix review was performed.

## Files / use / rollback

- `Assets/EverflowMap/Runtime/EFDemoFeedback.cs`: runtime observer, bounded effects/audio and reset handling.
- `Assets/EverflowMap/Runtime/EFV2Guidance.cs`: small adapter for the toast and D5 context state.
- `Assets/EverflowMap/Editor/EFDemoFeedbackTools.cs`: idempotent scene installation.
- `Assets/EverflowMap/Editor/EFDemoReview.cs`: actual-input recording and transition screenshots.
- `Assets/EverflowWater/Feedback/FeedbackLine.mat`: palette-tinted line/trail material.

Open the puzzle scene and Play normally. Inspector `EFDemoFeedback` exposes effect/ambient gains and debug-label visibility. **Tools → EVERFLOW → Demo → Install Feedback** installs or updates the one component. To disable the pass outside Play, remove that component; the HUD adapter falls back to the previous state display when no component exists. `before.unity.snapshot` preserves the pre-pass scene. Do not overwrite subsequent scene edits with the snapshot without reviewing them.

References read: Unity craft-game-floors `vfx.md`, `game-audio.md`, `visual-scorecard.md`; Unity IMGUI workflow; Unity 6000.3 [AudioSource](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource.html) and [ParticleSystem](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.html) API documentation. Existing developer palette/controller were retained; a broad art/mixer/camera redesign was outside this authorized pass.
