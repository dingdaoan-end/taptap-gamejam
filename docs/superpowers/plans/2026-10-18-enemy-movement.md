# Enemy Movement Implementation Plan

> Execute inline in the existing Integration worktree; sync verified changes into the original project. User requested implementation, not a GitHub push.

**Goal:** Make the new Minion art move on the latest Map's authored roads, turn visibly, follow the shared musical tempo, and finish its route safely.

**Architecture:** Reuse MusicClock and EnemyAudioTestSpawner. Add road-cell BFS routing that excludes Obstacle cells, immutable per-enemy path snapshots with distance sampling, and procedural facing/walk presentation for the single-frame sprite. Prepare visible routes/spawners in Map; keep the existing MusicTest route compatible.

**Baseline:** origin/main 8cf3c9c after new sprites and Map Visual Polish. Original main fast-forwarded cleanly. Stale Integration work was first backed up and verified equal to published 3ae6b08, then replaced by the published commits; unrelated local evaluation document restored separately.

## Decisions

- The Minion asset is one static sprite, not an animation strip. Preserve its art/scale and use horizontal facing, subtle bob and sway; no invented animation frames or image edits.
- Movement is continuous along road centers in units per musical beat. Pause freezes movement/pose; live speed changes affect future distance without teleporting previous progress. Long stalls rejoin the correct path position.
- Actual Map has seven connected road components across four regions, rather than four simple routes. Build one visible entry-to-goal route per component, selecting the far entrance and the endpoint nearest Castle. Only the upper-left entry is enabled by default; the other entries remain editable in the scene for later wave/region control.
- Snapshot route geometry and spawn origin for each enemy. Editing an entry or route affects later enemies, never teleports enemies already walking.
- Arrival reports once to the spawner and removes the enemy. Keep existing EnemyHealth and beat attacks working; no new base health/economy/wave progression in this task.
- Do not alter authored ground, roads, rocks, tower sprites, camera framing, or the hidden placement-status UI.

## Tasks

- [x] 1. Inspect latest assets and actual road topology; establish RED that Map has no spawner/route. Add a focused RED for existing enemies changing route when authored waypoints are edited.
- [x] 2. Implement road route preparation, immutable path movement, facing/walk presentation and arrival accounting. Add Map route/spawner setup and attach movement/visual components to the real Enemy prefab.
- [x] 3. Validate turns, multiple segments, route edits, pause/resume, BPM, speed changes, stalls, death/arrival, blocked roads, seven actual routes, newest art preservation, and actual Play-mode walking.
- [x] 4. Run existing building/combat/core checks; fresh review; restore unrelated serialization noise; verify and sync feature files into the original project. Document editor controls.

## Ledger

- Initial RED: Logs/enemy-movement-red.log exits 1: latest Map has no enemy route/spawner. Topology inspection: seven road components; no existing road cell overlaps the rock layer. Clock at (11.87,-7.13); actual Grid uses YXZ swizzle and unit cells.
- Snapshot RED: Logs/enemy-movement-snapshot-red.log exits 1 because changing a waypoint redirects an already spawned enemy.
- First GREEN: 52 movement checks pass, including 263 actual adjacent road points. Initial Play smoke exposed a verification weakness: the editor physics test could exit before restoring simulation mode, and the smoke watched logical distance rather than physical displacement. Fix the test cleanup to restore before Exit, restore the original physics settings, require automatic simulation and verify actual Rigidbody2D movement before accepting the Play result.
- Final real Play with automatic physics and physical displacement passes movement, turns, facing, bob, pause/resume and arrival once (Logs/enemy-movement-play-final.log). Existing regression suites pass: building54, combat41, rocks23 (latest map197 rock cells /3152 footprints), beat core15, MusicGarden core and PianoDefense core.
- Fresh review: stale prepared road geometry is reproduced by moved-entry and off-road-entry RED checks (Logs/enemy-movement-review-red.log). Fix once per due birth batch; skip invalid batches without disabling the spawner, so existing valid enemies are preserved.
- Ruling on review's physics speed-cap concern: not reproduced on this Unity version/project by either real-body edit-mode speed12/.25-beat test or automatic-physics Play at speed120 with per-frame actual-position checks (Logs/enemy-movement-fast-red.log passes). Do not introduce an unproven movement workaround. Retain the stress regressions and the existing long-hitch recovery.
- Final movement GREEN: Logs/enemy-movement-verified.log exits 0 with 55 checks, including new live-entry refresh and invalid-entry skip/preserve regressions. The spawner now prepares the route only when a birth batch is due, skips invalid birth times without a catch-up burst, and preserves existing enemies. A test fixture scheduling issue was corrected by giving Start its initial Update before advancing past the first birth beat.

- Final automatic-physics Play rerun: Logs/enemy-movement-play-verified.log exits 0 with actual Map road movement, bends, facing, walk pose, pause/resume and arrival once; existing combat Play also exits 0 (Logs/enemy-movement-existing-combat-play.log: three beat hits and enemy death).
- Final serialization cleanup verifies authored geometry and existing art preserved; Map contains only additions for editable movement routes, and the original physics settings, MusicTest scene and Tower prefab are unchanged. Read-only saved-scene verification exits 0 with 12 checks (Logs/enemy-movement-saved-scene.log).
- Synced exactly 19 feature files into D:/TuanjieProjects/TapTapGameJamGreybox after confirming its baseline 8cf3c9c was clean. Existing files backed up under Artifacts/EnemyMovement-20261019/pre-sync; source/destination hashes and git diff --check verified. Unrelated evaluation document excluded. Changes remain local and uncommitted; no push requested.