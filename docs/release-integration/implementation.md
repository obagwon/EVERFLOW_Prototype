# Release integration implementation

## Existing behavior preserved

- `EFTitleScreen.GameplayScene` already points to `Assets/Scenes/Everflow_Map.unity`.
- The saved title start event, release build target, map ending restart, and return-to-title routes already use the title/map flow. No runtime file, scene, asset, prefab, ProjectSettings, or application version was edited for this change.
- `EFMapWorld.RestartWorld` reloads its current map scene. `ReturnToTitle` loads the existing title scene. Existing play probes exercise title entry, six stages, wheel ending, restart, and title return; these are existing test coverage, not a newly observed Play result.

## Changes

- `EFReleaseSetup.cs`: extract repeatable scene-list construction; preserve utility entries disabled when configuring Release. Expose scene-list validation requiring Title then Map with no duplicate Title/Map; ordinary builds can retain additional enabled utility scenes. Remove legacy MVP fallback from gameplay validation. Validate map nodes, restoration references, gate/side water, nonempty final water segments, and a separate ending camera. P3's authored empty restoration list remains valid.
- `EFReleaseBuild.cs`: ordinary Build/Build And Run now applies the map-specific saved scene reference validation as well as scene ordering, and reports the exhibition map in its guidance. Existing version transaction and explicit Windows player build scenes are preserved.
- `EFReleaseTests.cs`: add scene-list regression checks for repeated setup, source preservation, disabled utility entries, accepted extra ordinary-build scenes, and rejected missing/reordered/legacy/duplicate entries. Add integration tests that temporarily break map references only inside an unsaved preview scene, confirm rejection, and restore/close that preview.

## Verification entry points

- `Tools > EVERFLOW > Release > Run Release Scene List Tests` (`EFReleaseTests.RunSceneListTests`): list-only tests; no project settings changes.
- `Tools > EVERFLOW > Release > Run Release Integration Tests` (`EFReleaseTests.RunIntegration`): existing version tests, scene-list tests, saved title/map validation, and preview negative tests; no Play session or player build.
- `Tools > EVERFLOW > Map > Run Physical and State Tests`: existing Play probe; programmatic ability input and physical traversal, not a human mouse playthrough.

## Implementation-side checks

- `git diff --check` for all three edited Editor files reported no whitespace errors (Git emitted only line-ending normalization warnings).
- Source inspection confirmed current title/build/ending routes and P3 empty restoration policy.
- Unity Play, Unity editor test entry points, and Windows player build were not executed by the implementation agent. An existing Unity project process was preserved. Independent validation results belong in the separate verification report.
