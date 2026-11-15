# Continuous waterways across the puzzle map — 2026-11-08

Scene: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`.

All 39 existing water/foam segments have a network assignment. Fifteen renderers own the connected surfaces; the other 24 are visually covered and suppressed. Their GameObjects, original asset references and circuit visibility groups remain intact. No water colliders, ability rules, ice support dimensions, puzzle success rules, build settings or vendor assets were changed.

## Geometry and presentation

| Owner | Continuous coverage |
| --- | --- |
| W_H_to_G_Fall | Highland outlet → upper fall → G supply → G leak fall → S1/S2/S3 bends → Fall2 → S4 → A pond inlet |
| W_H_Pond / W_A_Pond | Tessellated standing pools, gentle bounded ripples |
| W_H_to_D5_Fall | Existing cave ceiling spring → landing → fixed-channel inlet |
| W_D5_Fixed | Freezeable fixed channel, independently controlled |
| W_D5_Fall | Normal supply fall → lower channel → basin inlet |
| W_D5_SpillPool | Misalignment spill → leak fall → floor water → basin |
| W_Bypass_1 | Upper bypass → first drop → second channel → fall → basin inlet |
| W_D2_Basin | Dynamic branch-direction pool; ice stops motion and downstream supply |
| W_D2_Dock | Dock connector and lower dock water |
| W_D2_Wheel | Basin → small-wheel branch |
| W_D2_Exit | Basin → main exit branch |
| W_G_ToWheel | G supply branch → G small wheel |
| W_Exit_Tunnel | Exit → rounded gorge entry → existing downhill gorge → wheel-pool inlet |
| W_R_WheelPool | Restored pool → narrowed island outlet → island waterfall/landing |

The old gorge ends around Y=-17.9 while the old `W_R_WheelFeed` begins at Y=-12.3. Instead of making water rise ~5.6 m, the new route follows the actual downhill gorge and feeds the pool near Y=-17.775. Existing wheel rotation and restoration logic remain unchanged.

The new shared network shader extends the approved S3 prototype approach: continuous distance/travel-time UVs, tangent-oriented normal detail, ballistic falls, edge breakup, impact tint, radial ripples and bounded splash particles. Pool projection reads the existing integrated flow offset, so still water does not acquire a false current and D2 changes direction with its branch. Every animation uses the controller's pauseable local clock/offset. Frozen water uses an ice tint and a stopped phase.

Generated visible geometry after the largest-waterfall lip correction: **19,710 vertices, 36,626 triangles**. Nine impact emitters, each capped at 48 particles. These are counts, not a measured performance guarantee. There is no fluid solver or new package dependency. See `highland-fix/README.md` for the measured cliff-edge correction and matched side views.

## Evidence

- `geometry.txt`: assignment, vertex channels, finite coordinates and no-added-collider validation.
- `runtime-integrated.txt`: actual Unity Play, 28 checks passed / 0 failures; forced circuit states, not traversal.
- `runtime-route-alternate.txt`: **28 checks passed / 0 failures** on the final network, synthetic keyboard/mouse route through A leaf, C, G wheel, D3 ice, D5 alignment, D4 wheel, garden and Enter restart. Recorded route time 145.26 s is an automated traversal, not human first-play time.
- `waterways-review.mp4`: 15.07 s actual Unity Recorder footage: surface → highland → D5 → garden → island fall. Uses forced restored puzzle state and temporary review cameras.
- `surface.png`, `highland.png`, `spill.png`, `bypass.png`, `dock.png`, `gorge.png`, `pond.png`, `garden.png`, `island.png`: actual Play captures. The camera moved for inspection; these are not player traversal evidence.
- `before.unity.snapshot` and `Assets/EverflowWater/Network/BeforeNetwork.asset`: scene/reference baseline before this full-map expansion.
- `offline-preservation.json`: **10 static checks passed** against that immediate scene baseline, including existing physics, camera, non-water MonoBehaviour configuration, prefab transform preservation, local references and network GUIDs.

Restore → Apply was exercised after runtime verification: it restored the approved local S3 prototype, then regenerated the network with exactly 15 owners and 9 impact components. Final scene is saved; the player camera's Cinemachine Brain is enabled, Play stopped and background override restored off.

The older `tools/verify_everflow_water.py` preservation stage uses a baseline from before subsequent project work. It failed on six files already modified before this expansion (`EFV2PuzzlePlayChecks`, `EFV2Plant`, `EFV2PuzzleAssembly`, `EFV2PuzzleWorld`, package manifest and lock). Their filesystem modification timestamps predate the network task. That historical baseline was not rewritten or bypassed to claim a pass; use the immediate-baseline verifier `python tools/verify_water_network.py` for this change.

The review cameras and hidden review HUD are Play-only. Stop restores normal player/Cinemachine control. Whole-game visual quality from every possible viewing angle, other quality levels/APIs, GPU profiling and a standalone player build are not certified by these checks. The original low-poly terrain and channel walls remain; rocks can still be visible through transparent water.

## Rebuild / revert

Outside Play with the puzzle scene open:

- **Tools → EVERFLOW → Water → Network → Apply All Continuous Waterways** regenerates the same 15 mesh assets and one impact root without accumulating objects.
- **Tools → EVERFLOW → Water → Network → Restore Before Network** restores the previous 39 mesh/material/suppression references and the previous prototype spray state.

Use Network menus for this version; the older Prototype/Continuity builders represent earlier presentation stages. The previous FBX-original restore command remains a separate full rollback of the water integration.
