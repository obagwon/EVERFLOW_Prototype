# Largest waterfall lip correction — 2026-11-08

The G leak waterfall previously started its descent at world Z=-57.65, while the measured cliff edge near Y=15 lies at Z=-60.38..-60.17 across the water's width. This left roughly 2.5 m of unsupported horizontal water beyond the cliff.

The highland route now leaves the lip at `(-45, 15.225, -60.22)` and follows the same gravity-based trajectory generator as the smaller falls, landing at `(-45, 3.225, -56.7)`. Its initial horizontal speed is approximately 2.25 m/s and downward speed increases with gravity 9.81 m/s². The lower stream reconnects to the existing S1 channel, and the landing spray/impact follows the new contact point. Rebuilding the entire network uses this corrected route too.

Only the highland continuous mesh and its existing landing metadata/spray placement were rebuilt. The 15 other pre-existing Network assets in `other-meshes-before.json` hash identically afterward (`other-meshes-preservation.json`). Gameplay, colliders, other waterfalls, materials and shaders were not changed by this correction.

Validation in actual Unity Play:

- Matched side captures: `before-side.png` / `after-side.png`; front: `after-front.png`.
- `largest-waterfall-review.mp4`: actual side/front recording, temporary review cameras only.
- 1156 falling-sheet vertices sampled against terrain collision: minimum forward clearance **0.43285 m**, no cliff penetration. See `clearance.json`.
- Frame progression confirmed; ShaderUtil reported no shader errors and current runtime console errors were zero.
- Scene-reference/static preservation checks use the network verifier. Full puzzle traversal was already checked for the network version and was not repeated for this geometry-only correction.

`Assets/EverflowWater/Network/HighlandBeforeLipFix.asset` and `before.unity.snapshot` preserve the immediately previous shape/state. Menu: **Tools → EVERFLOW → Water → Network → Refine Largest Waterfall Lip**. The normal full Network Apply also retains this fix.
