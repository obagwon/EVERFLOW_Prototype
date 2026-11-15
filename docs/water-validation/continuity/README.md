# Leak waterfall continuity — 2026-11-08

Applied to `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`.

- Original S3 ended at world Z=-31.5 while Fall2 began at Z=-30.2: a 1.3 m visible gap.
- Replaced Fall2 and G leak box meshes with open, double-sided curved sheets (1313 vertices each), including rounded entry and landing sections. G landing is forward of the cliff so the lower sheet is not hidden inside opaque terrain.
- UV V stores distance along the sheet. The optional segment `pathFlow` property enables downstream advection, small travelling ripples, soft banks/tail and landing tint in the existing shader. Other segments retain world projection.
- Replaced the two corresponding foam meshes with flat feathered landing patches. No colliders, gameplay rules, activation groups, original FBX assets, or build settings changed.
- Continuous assets are separate `_Continuous.asset` and `_Soft.asset` files. Segment original mesh/material references remain available to the existing restore tool. `before.unity.snapshot` preserves the scene file before this operation (does not include unsaved Editor state).
- Rebuild only in the puzzle scene, outside Play: **Tools > EVERFLOW > Water > Smooth Leak Waterfalls**. Repeated runs update the same four assets and create no objects. The shader generator was updated to retain the new shader features.

Validation: final scene saved; actual Editor Play renders inspected for both falls; both segments wet with advancing local clock; 1313 vertices and distance UVs per ribbon; ShaderUtil reported no shader errors; console ground truth reported no compile failure, zero current errors/warnings. A prior bridge status-write error remains in captured console history. CLI `unity recompile` timed out; explicit AssetDatabase.Refresh subsequently completed imports/domain reloads and the final code executed successfully.

Captures `fall2-after.png` and `g-fall-after.png` use a temporary camera viewpoint during Play, not a player traversal. Stop restored the serialized camera; background override restored off. Whole-game traversal, performance in builds and all camera angles were not tested. This is an animated mesh/shader effect, not a fluid simulation.
