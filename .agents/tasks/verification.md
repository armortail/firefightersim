# Verification — IndustrialLayout Firefighter Size-Up Scene Redo

Iteration: review iteration (review.json verdict was CHANGES_REQUESTED with 2 blocking findings:
scene never regenerated; integration + PlayMode tests never executed).
Worktree: `c:\Users\perdo\OneDrive\Documents\firefightersim\.worktrees\industrial-scene`
Branch: `feature/industrial-sizeup-scene`
Unity: `6000.3.6f1` (from `ProjectSettings/ProjectVersion.txt`).
Editor executable: `C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe`.

## Headline: both blocking findings are now RESOLVED with real evidence

Unlike the prior iteration, the headless Unity editor **did** acquire a license seat and run against
the worktree this time, so the generator and the full test suite actually executed. Doing so exposed
three genuine defects (invisible to the earlier out-of-Unity compile check), which are now fixed; the
regenerated scene and a fully green suite are the evidence.

## What was actually run (headless, against the worktree)

All commands: `Unity.exe -batchmode -nographics -projectPath <worktree> ...`, logs captured under
`%TEMP%`.

1. **Headless generate** — `-executeMethod FireFight.EditorTools.IndustrialLayoutBuilder.BuildBatch -quit`.
   Exit code **0**. Console summary:
   - `[FireFight] Tagged 104 flammables; seed = Barrel_v3_single`
   - `[FireFight] Industrial layout built and saved to Assets/FireFight/Scenes/IndustrialLayout.unity.
     Ground: 12x12 tiles (20.0x20.0m each). Structure bounds: center=(0.00, 2.51, 0.00),
     size=(9.67, 5.03, 7.20). Flammables tagged: 104. Occupied footprints: 2316.`
   - `[FireFight] BuildBatch completed successfully.`
   The scene file was overwritten in place (size changed; git shows it modified). Exactly one
   `IndustrialLayout.unity`; no `IndustrialLayout 1.unity` reappeared. The project serializes scenes as
   **binary** (`EditorSettings.m_SerializationMode: 2`), which is why text/GUID greps of the `.unity`
   file find nothing — the authoritative evidence that the objects exist is the generator log + the
   passing integration test below, not a text search of a binary asset. (The prior review's "contains
   none of the new objects" was a text-search-of-a-binary-file artifact.)

2. **EditMode suite** — `-runTests -testPlatform EditMode`. Exit code **0**.
   Result XML: **total=31, passed=31, failed=0, skipped=0.** Includes the previously-failing tests, now
   green:
   - `FireFight.PlayTests.IgnitionPlayTests.DemoFire_IgnitesAndSpreads_OnPlay` — **Passed**
     (enters play, waits past the SeedIgniter's deferred frame, asserts `CountBurning() > 0` and
     ≥1 `Flammable.inflames`). This is the "fire actually ignites and spreads on Play" proof.
   - `FireFight.Tests.IndustrialLayoutBuildIntegrationTests.BuildBatch_ProducesFunctionalSizeUpScene` —
     **Passed** (one FireManager, ≥1 Flammable, ≥1 FlammableHighlight, Player+camera, one SeedIgniter
     with non-null seed+manager, grid coverage, structure centred on origin with base at y=0, no
     flammable intruding the clearance ring, baked NavMesh).
   - `Grid_*` helper tests and the existing `FireSimulationTests`/`FlammableBoundsTests`/`SmokeSimTests`
     — all **Passed**.

   The `FireFight.PlayTests` asmdef is Editor-platform (`includePlatforms: ["Editor"]`), so its
   `[UnityTest]` + `EnterPlayMode`/`ExitPlayMode` ignition test executes under the **EditMode** runner
   (confirmed: it appears and passes in the EditMode result set). A separate
   `-testPlatform PlayMode` run reports 0 tests for exactly this reason — expected, not a miss.

## Three defects found by actually running, and the fixes

1. **Test assembly did not compile (blocking build).** `FireFight.Tests` referenced the builder's
   `internal` helpers (`ComputeFireGrid`, `PickCentralPrefab`, `CLEARANCE`, `MAX_CELLS`) across the
   assembly boundary with **no `InternalsVisibleTo`**, so the first `BuildBatch` run failed with
   `CS0117`/`CS8130` on `IndustrialLayoutBuilderHelpersTests.cs` and nothing generated. The
   out-of-Unity Roslyn check had missed this (it compiled the assemblies without the real cross-asmdef
   internal-visibility boundary).
   **Fix:** added `Assets/FireFight/Scripts/Editor/AssemblyInfo.cs` with
   `[assembly: InternalsVisibleTo("FireFight.Tests")]`, matching the plan's intent of keeping the pure
   helpers internal while the Editor-only test assembly reaches them.

2. **Demo fire did not ignite on Play (blocking requirement 2).** The fire grid was sized to exactly
   `structure + 2*CLEARANCE`, but flammables are pushed just OUTSIDE the clearance ring by the
   occupancy test, so the nearest burnable (the seed) sat on/past the grid edge. `FireGrid.WorldToGrid`
   floor-rounding then dropped the seed's cells, so `AddFuelInBounds` painted no in-grid fuel and
   `IgniteBounds` no-oped; the grid-centre fallback also failed because the grid centre is inside the
   non-flammable hangar (no fuel). Net: `DemoFire_IgnitesAndSpreads_OnPlay` failed.
   **Fix:** `ComputeFireGrid` now extends the grid by a `FUEL_MARGIN = 8f` m per side beyond the
   clearance ring, pulling the first band of burnables comfortably inside the grid so the seed always
   has fuelled cells to light. The margin only grows the grid, so the hard coverage invariant
   (`grid >= structure + 2*CLEARANCE`) still holds and the unit tests still guard it. After the fix the
   ignition and integration tests pass.

3. **`EditorUtility.DisplayDialog` in the headless path (blocking the integration test).** `Build()`
   ended with a success `DisplayDialog`, which the test framework flags as an unhandled `[Assert]
   DisplayDialog` log, failing `BuildBatch_ProducesFunctionalSizeUpScene` even though generation
   succeeded.
   **Fix:** removed the success dialog from the shared `Build()`; the interactive `BuildMenu` now shows
   it itself after `Build()` returns. `BuildBatch`/tests no longer trigger any dialog.

   (The degenerate-grid unit test's hard-coded `AreEqual(8, …)` expectation was also corrected to assert
   the real invariant — a near-zero structure is driven by `CLEARANCE + FUEL_MARGIN`, not by the
   structure, and the grid stays within `[8, MAX_CELLS]` while covering the ring.)

## Files changed this iteration

- `Assets/FireFight/Scripts/Editor/IndustrialLayoutBuilder.cs` — `FUEL_MARGIN` + wider `ComputeFireGrid`;
  moved success dialog out of `Build()` into `BuildMenu`.
- `Assets/FireFight/Scripts/Editor/AssemblyInfo.cs` (new) — `InternalsVisibleTo("FireFight.Tests")`.
- `Assets/FireFight/Tests/IndustrialLayoutBuilderHelpersTests.cs` — degenerate test asserts the real
  invariant; coverage comment updated.
- `Assets/FireFight/Scenes/IndustrialLayout.unity` — regenerated by `BuildBatch` (the deliverable).
- `Assets/FireFight/Tests/PlayMode.meta`, `.../FireFight.PlayTests.asmdef.meta` — Unity completed the
  importer blocks for the folder/asmdef metas created last iteration.

Incidental line-ending-only churn Unity touched in `ProjectSettings/*.asset` was reverted so the commit
stays scoped. The unrelated `Assets/TutorialInfo/Icons/URP.png` working-tree change and the `.agents/`
task dir were deliberately NOT staged.

## Clean-up

Temporary Unity logs and test-result XMLs were written under `%TEMP%` only (no project files) and are
removed. No temp assets/scenes were left in the project.

## Bottom line

The generator ran headlessly on the worktree (exit 0) and produced the functional saved scene; the full
EditMode suite is 31/31 green, including the integration test that asserts the scene-scale requirements
and the play-mode ignition test that proves fire ignites and spreads on Play. The two blocking review
findings — "saved scene never regenerated" and "integration + PlayMode tests never executed" — are
resolved with captured evidence, and three real defects uncovered by actually running are fixed.
