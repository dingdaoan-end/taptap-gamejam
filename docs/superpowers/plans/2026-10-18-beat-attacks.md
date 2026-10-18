# Beat Attacks Implementation Plan

> Execute inline with executing-plans in the existing Integration worktree. User requested implementation; keep changes uncommitted and synchronize verified feature files into the original Unity project.

**Goal:** Enemies inside a tower's actual range take damage only on configured background-music beats.

**Architecture:** Keep MusicClock as the only DSP timeline. Add a small configurable repeating beat grid, a tower attack component with final range checks, and enemy health. Put attack on the base Tower prefab so existing Bell/Chord/Duduk variants and newly built towers inherit it. Existing musical phrase controllers remain responsible for the musical layers. Instant hit lines and enemy hit flashes make attack timing visible.

**Tech Stack:** Unity 2022.3.62f3, C#, Physics2D, DSP scheduling, existing prefabs and scenes.

**Spec:** User request in this chat: enemies entering range must be attacked on corresponding background music beats. Default chosen for this first version: every beat, editable per tower as offsets in a repeating four-beat pattern.

## Constraints and review focus

- Preserve the completed 2x2 building mechanism and hidden invalid-placement messages.
- A tower built or activated between beats joins the global grid; no immediate entry damage or independent cooldown.
- Use the same scheduling lead as the musical layers. Recheck live targets at the actual attack, including target death, leaving range, disabled range, and several colliders on one enemy.
- Pause, resume, stop, restart and tower demolition cancel pending attacks. A long stall skips missed attacks rather than bursting damage.
- An empty beat list is a rest. Support fractional offsets. Damage is one target per marked beat; default enemy HP 3 and damage 1 are Inspector settings, not a full balance pass.
- Map has a missing old drum asset GUID; bind the existing verified 19.2-second Lv.1 Drums asset so the 100 BPM/32-beat clock has actual matching background audio. MusicTest already uses that asset.
- Do not add extra attack sounds over existing Bell/Chord music, change map geometry, or push to GitHub.

## Tasks

- [x] 1. Establish RED with real Unity prefab checks: instantiated Tower_Bell must have an attack component and spawned Enemy must have health. Add deterministic timing checks for mid-beat entry, rests, fractional patterns and loop boundaries.
- [x] 2. Implement beat grid, enemy health, shared tower attacks and visible hit feedback. Make detector refresh callable for final range validation. Wire base Tower and Enemy prefabs and repair Map drum reference.
- [x] 3. Verify live Unity damage/target lifecycle and transport behavior, original building checks and existing core/timing suites. Fresh whole-change review; fix concrete findings.
- [x] 4. Compare authored scene/prefab geometry, sync feature files into the original project after conflict checks, and document Inspector controls and verification evidence.

## Ledger

- Baseline e3dcb18 plus the prior uncommitted building feature in both checkouts. Existing MusicGarden and PianoDefense core suites passed before attack implementation.
- Ruling: use the existing isolated worktree and inline execution; no new worktree needed. User asked to implement the feature directly, so no additional design approval round.
- Ruling: beat positions are authored Inspector offsets; no automatic note-onset extraction from WAV. This task establishes beat-synchronized combat, with finer musical score patterns editable later.
- RED: Logs/beat-attacks-red.log exits 1 because an instantiated real Tower_Bell has no attack component.
- First Unity integration run passes damage, range, transport, stalls and prefab inheritance, then exposes an incorrect test assumption: Map intentionally has no preplaced towers. Ruling: verify all three actual scene build-button prefab arguments instead of requiring preplaced towers; this exercises how the player obtains towers without changing the authored layout.
- Initial GREEN: 39 real Unity checks and 15 pure timing checks pass. Real Play smoke passes automatic spawn, three real beat attacks, health changes, feedback creation and death (last hit beat 3). Existing building suite 54 checks passes, both core suites pass, all three original timing entry points rebuilt separately and pass.
- Fresh review finds one Important issue: adding audio lead after every attack skips valid dense fractional marks. New Unity dense-score regression fails for the expected reason in Logs/beat-attacks-dense-red.log. Fix: use lead only for joining/recovery; advance normal attacks to the next future mark without adding lead again.
- Final GREEN: Logs/beat-attacks-final.log exits 0 with 41 passing checks including dense sixteenth-note follow-up attacks; pure timing suite remains 15/15. Review found no other concrete Important/Critical issue.
- Final real Play smoke rerun after the dense-score fix: Logs/beat-attacks-play-final.log exits 0 with BEAT_ATTACK_PLAY_PASS, three actual hits and death at beat 3. Original scene Tilemap/Transform/Camera blocks and prefab Transform/Collider/Sprite/Audio blocks compared verbatim to baseline and preserved.
- Synchronized 23 feature files and patched only the stale Map drum GUID into D:/TuanjieProjects/TapTapGameJamGreybox after checking original files for concurrent edits. File content equality verified; original git diff --check passes. Previous building and hidden-invalid-hint code preserved. No commit or GitHub push performed.
