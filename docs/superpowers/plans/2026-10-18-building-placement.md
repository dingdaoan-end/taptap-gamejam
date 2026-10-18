# 2×2 Building Placement Implementation Plan

> Execute inline with the executing-plans skill, using the existing isolated worktree. Keep game changes uncommitted and copy verified files into the user's original project.

**Goal:** Enforce all five user construction requirements through the existing build menu, including full camera visibility and same-level 2×2 ground.

**Architecture:** BuildPlacementValidator reads real Tilemaps, camera projection, tower attack colliders and occupied objects. BuildSelection previews/selects a grid footprint. BuildSlot revalidates at construction time so UI and direct calls share the same gate. Existing tower prefabs, section bindings and demolition remain in use.

**Tech:** Unity 2022.3.62f3, C#, Tilemap, Physics2D, existing Canvas.

## Binding requirements and decisions

- Every one of the four cells must be a complete tile on the same Ground level 1/2/3/4 (user confirmed).
- Check full cell corners against the actual camera viewport, not cell centers or renderer visibility.
- Any intersection of the footprint with an existing tower's attack collider blocks construction; do not compare the new tower's attack range to old ranges.
- Ground descendant obstacle sprites/colliders/tilemaps, explicit obstacles and buildings occupy space. Empty BuildSlot markers and the ground surface collider do not.
- Existing tower footprints remain occupied regardless of sound/target state; removing a tower releases space immediately.
- Keep the existing menu and section music context. Support free ground placement, not only the authored slot markers.
- Legacy MusicTest Ground is renamed Ground level1; Map retains its four authored ground layers. Do not alter map artwork or camera layout.
- Boundary contact with an attack range is conservatively blocked. Viewport edges are accepted with a small floating point tolerance, but no partial tile outside the view is accepted.

## Tasks

- [x] 1. Establish RED: editor check calls the existing BuildSlot.TryBuild on non-ground and proves that the current code incorrectly builds.
- [x] 2. Add validator, footprint geometry and obstacle marker; integrate guard into BuildSlot and free-placement preview/menu into BuildSelection.
- [x] 3. Add editor installer and real Unity integration checks: each of the five gates, mixed levels, non-default grid swizzle, attack-range intersection, offscreen tile corners, stale selection, overlapping placements, demolition and rebuilt eligibility.
- [x] 4. Run Unity batch compilation/checks on both actual scenes, run existing core/timing checks, inspect changes, then synchronize only the feature files into the original project after verifying its baseline has not changed.

## Ledger

- Baseline: e3dcb18; original project clean; Unity not running at initial inspection. Existing linked worktree reused; no new checkout needed.
- Use ALLUSERSPROFILE=ProgramData for editor child processes: the host omits that variable and UPM otherwise fails before loading the project.
- RED: Logs/building-red.log exits 1 with BUILD_PLACEMENT_RED: construction on non-Ground must be rejected.
- GREEN: Logs/building-green.log exits 0, 54 checks pass, both scenes configured; legal sites in current authored cameras: Map 125, MusicTest 110.
- Semantic scene comparison confirms every existing Tilemap cell/index, transform and Camera serialized block is unchanged. Scene UI/validator references added; MusicTest Ground renamed as planned.
- Final Unity run: Logs/building-final.log exits 0 with BUILD_PLACEMENT_CHECK_SUCCESS checks=54. Map has 125 legal footprints, MusicTest has 110 in the authored camera views.
- Existing MusicGarden.CoreTests and PianoDefense.CoreTests pass; standalone MusicClockTests, ChordPhraseTimingTests and EnemyBeatTimingTests each rebuilt with their explicit entry point and passed.
- Fresh independent code review found no Important/Critical findings. Its minor scene-save animation-speed churn finding was corrected: all 9 Map and 5 MusicTest Tilemap serialized components restored verbatim to the authored baseline in both checkouts.
- Feature files synchronized into D:/TuanjieProjects/TapTapGameJamGreybox after baseline verification. Original Unity runtime screenshot shows the green 2x2 preview, existing build menu and Chinese status text; no compilation/runtime exceptions found in its preview log. Changes remain uncommitted and unpushed.
