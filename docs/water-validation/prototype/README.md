# S3 unified water prototype — 2026-11-08

Only `Everflow_Map_v2_test_with_puzzle` S3 → Fall2 → S4 was prototyped. G and all other water geometry/material assignments retain their previous presentation.

The S3 renderer now owns one connected 4025-vertex / 7680-triangle surface from Z=-44 at Y=3.225 to Z=-26 at Y=.225. The falling section follows a ballistic trajectory with 2.4 m/s horizontal launch velocity and gravity 9.81. Its landing is around Z=-29.62. The former S3/Fall2/S4 overlapping geometry is replaced visually by this one surface. The three redundant Fall2/S4/foam renderers are suppressed by presentation flags; their GameObjects, original meshes and puzzle ownership remain intact.

The dedicated shader uses continuous UV travel time, tangent-aligned normal detail, one body tint, gradual fall detail, impact turbulence and radial ripples. A small particle system adds impact droplets. It follows the existing wet/frozen/pause states; it does not write gameplay state. This is authored geometry and shader animation, not a fluid solver. Existing project-wide water material/shader are not replaced.

New assets: `Assets/EverflowWater/Prototype/`, two prototype shaders; `EFLeakPrototypeTools`, `EFLeakPrototypeBackup` and `EFLeakPrototypeSplash`. The generic integration gained optional material override and presentation suppression fields (default off/null).

## Review

- `S3-water-review.mp4`: 9.2 s actual Unity Recorder footage, front → side → top. HUD hidden and camera moved only in Play for review. Stop restored the player's camera and HUD.
- `before-*` / `after-*`: front, side, top at matching camera placements; footage is a visual inspection, not a player traversal.
- The first side camera was obstructed by the bridge. The final side/top cameras use the other bank; opaque terrain was never hidden to make the result look better.
- Former opaque terrain intersections at the lip/landing were reduced by moving the ballistic trajectory forward. Dark tinted rock may still be visible through transparent water.

## Checks performed

- Unity compiled and ran the final prototype; ShaderUtil reported no shader errors.
- All generated vertices finite, UV travel coordinate strictly increases down the path.
- Exactly one of the four original water renderers visible during Play; one impact spray component, 13 live particles at the sampled frame.
- Existing G material override remains null.
- Repeated Apply retains a single spray. Restore then Apply passed, and final scene is saved with the prototype applied.
- Final runtime console inspection showed no current errors or compile failure. Pre-existing bridge/Editor warning history remains. A prototype mesh filename/name mismatch warning was corrected.
- Background override restored off, Cinemachine Brain restored enabled, Play stopped.

Not tested: whole-game completion, all viewing angles, player builds, GPU profiling or other quality levels. The connection to other existing water at the outer S3/S4 boundaries still uses the previous neighbouring presentation; this prototype evaluates one fall only.

## Revert or reapply

Outside Play mode with the puzzle scene open:

- **Tools → EVERFLOW → Water → Prototype → Restore S3 Before Prototype** restores saved mesh/material/suppression references and removes this spray.
- **Tools → EVERFLOW → Water → Prototype → Apply S3 Unified Water** reapplies it.

`S3_Before.asset` preserves stable baseline asset references. `before.unity.snapshot` is the scene file before the prototype. Both are retained for review.
