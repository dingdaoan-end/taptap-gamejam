# Merge Latest Map and Frame Level Two

**Goal:** Merge GitHub bfc74da into all local waves/fullscreen/name work and limit level-two gameplay to the pictured 25x15-cell region.

**Chosen display policy:** The user chose full 25x15 visibility with small side bars on a 16:9 display. Use a 5:3 camera viewport instead of displaying neighboring regions or stretching the image. Keep HUD readable and construction visibility checks tied to the actual camera viewport.

**Architecture:** Keep the upstream authored Map data. Replace the level-two route-bounds-plus-padding framing with an explicit editable world region, fitted inside the window. Preserve the existing camera transition and restore normal viewport behavior when leaving level two.

## Tasks

- [x] Back up original local files; fast-forward existing Integration worktree to bfc74da and reapply local work without conflict. Main project remains intact until merged result is verified.
- [x] Inspect actual ground/road coordinates against the supplied image; RED actual level-two camera coverage.
- [x] Implement exact 25x15 framing and aspect handling, black side bars, resize/transition/pause/retry behavior. Verify no neighboring map becomes visible and no old-level viewport leaks into level 3.
- [x] Verify merged wave references/routes, geometry preservation, existing later-level lifecycle, and menu/fullscreen behavior; review changes, sync original, build a new playable version.

## Ledger

- Remote bfc74da modifies Map only. Named stash backup plus explicit local-file copies retained in original Artifacts/MergeCamera-20261020.
- Git merged the local eight wave-spawner blocks into the upstream map without conflict. Do not use the previous wave cleanup script's 29c9472 baseline: all future preservation checks must use bfc74da.
- RED: actual old level-two view was 31x23.25. GREEN: center (24.5,.5), extent 25x15; literal expected viewport checks at 1920x1080, 1024x768, 2500x1500 and 1080x1920. Pause/retry and leaving to level 3 passed in real Map Play mode. Logs/level2-camera-final.log; actual world render Artifacts/MergeCamera/level2-actual.png.
- Upstream preservation audit: 1099 blocks, identical IDs; all blocks except eight preexisting local wave edits and the MapUIFlow camera fields match bfc74da exactly. Artifacts/MergeCamera/merge-verification.txt.
- Merged Map static checks: 447 passed plus EnemyAll checks. Real later-wave Play: level 3 defeat/retry, levels 3/4 pause/clear gates/36 and 46 total/victory/next/replay passed. Core harnesses: enemy-wave 169, beat-attack 117. The Play fixture accelerates tempo and freezes actors after checking real arrivals; it does not establish production-speed balance.
- Fresh independent review found no actionable defects; independently confirmed scene preservation and bounds/display fitting. Original project fast-forwarded to bfc74da and 21 local feature files synced with byte comparisons. Original and Integration named stashes retained as backups.
- Original project Windows build succeeded (Logs/build-merged-camera.log). New executable ran for 30 seconds, loaded its menu, reported FullScreenWindow and no startup exceptions (Logs/merged-camera-player-smoke.log). New build folder Builds/乐渡-Windows-20261020-地图视野版; distributable ZIP excludes DoNotShip debug output. No commit or push requested/performed.

## Follow-up: fixed third/fourth level views

The user requested the same framing for levels 3 and 4. Extend the existing fixed frame without changing level 1: level 3 center (24.5,-14.5), level 4 center (-.5,-14.5), shared size 25x15 and the same fit/bars policy. Keep the frame active between levels 2, 3 and 4, unlocking it during transitions and relocking at the new center.

- RED Logs/later-camera-red.log: level 2 passed; actual level 3 was 31x23.25 centered (26,-15), reproducing the unwanted route-bounds framing.
- GREEN Logs/later-camera-green.log: actual Map Play mode visits levels 2/3/4 with exact 25x15 coverage and expected world corners, pauses/retries each, and restores viewport/disables backdrop when flow is disabled. Screen ratio checks also passed. Actual renders saved as Artifacts/MergeCamera/level3-actual.png and level4-actual.png.
- Scene preservation: compared with original project snapshot Artifacts/FixedLaterCamera-20261020, all 1099 block IDs unchanged; only the MapUIFlow size/center fields changed. The former levelTwoViewSize field migrates to fixedLevelViewSize with FormerlySerializedAs.

- Fresh independent review: no actionable correctness findings. Five follow-up files synced back to original with byte equality checks and pre-change backups. Original Windows build succeeded (Logs/build-later-camera.log), output Builds/乐渡-Windows-20261020-全关卡视野版. No push performed.
