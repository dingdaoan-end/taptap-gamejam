# Piano Defense Prototype Implementation Plan

**Goal:** A playable Windows piano-only beat tower-defense round with audible kill notes and a replay of the resulting music.

**Architecture:** A deterministic plain C# simulation owns the beat order, waves, economy and melody matching. A Unity controller schedules synthesized piano voices against DSP time and presents the simulation through a scalable immediate-mode prototype interface. An editor builder creates a dedicated scene and Windows Mono build.

**Tech Stack:** Tuanjie 1.10.4 / 2022.3.62t16, C#, Unity IMGUI, AudioSource.PlayScheduled; dependency-free .NET 8 rule tests.

**Spec:** ../../design/音游塔防_系统设计文档_v1.md, narrowed by the user's request to the most basic version with piano only.

## Global constraints and MVP decisions

- BPM 110; 12 × 7 grid; base HP 10; piano cost 8, range Manhattan 3, attacks once per beat.
- Three fixed horizontal lanes; enemies move one cell each beat. Towers occupy other rows and do not change paths.
- Three 32-beat waves with two 8-beat breaks, followed by clearing the last survivors. All enemies have 1 HP.
- Starting money 24; maximum 8 towers; kill gives 1 coin. Sell refunds 8 before battle or 6 during battle.
- Piano note indexes 0..7 = C4 D4 E4 F4 G4 A4 B4 C5; target melody [0,2,4,7].
- All / solo selected pitch / skip selected pitch filters. Nearest-to-base target first, stable ID tie-break.
- Melody progress is the longest ordered prefix of the target, allowing filler notes. Matched notes must be on strictly increasing beats, no more than 4 beats apart. Each 16-beat phrase has its own match.
- Full phrase: 4 coins and a cadence removes ceil(alive/4) remaining enemies. Cadence victims count as kills but produce no extra notes or recursion.
- Preserve every played kill note for end-of-round replay, including simultaneous notes. Result score and stars use applicable formula terms from the source design.
- Piano timbre is procedurally synthesized placeholder audio. No external samples or music permissions required.
- This increment covers preparation, one night, result and replay. Other instrument classes, daytime exploration, blessings and Boss remain later milestones.
- Work locally on feature/piano-defense. Remote fetch currently fails; base is verified local 0fa34a0. No automatic shared-branch push.

## Task 1 — deterministic rules

Files: Assets/PianoDefense/Runtime/DefenseSimulation.cs, MelodyMatcher.cs; Tests/PianoDefense.CoreTests/Program.cs and project file.

- [ ] Write and run failing behavior tests for melody gaps/chords/restarts; build placement/cost/refund; movement before targeting; filter rules; one death/one note; phrase reward; win and loss.
- [ ] Implement `DefenseSimulation.Step(): BeatResult`, `TryBuild(x,y)`, `Sell(id)`, `SetFilter(id,mode,pitch)`, and `MelodyMatcher.BestPrefix(notes,target,maxGap)`.
- [ ] Run `dotnet run --project Tests/PianoDefense.CoreTests` and check reference-layout outcomes and deterministic replay log.

## Task 2 — audio, interface, lifecycle

Files: Assets/PianoDefense/Runtime/PianoAudio.cs, PianoDefenseGame.cs, PianoDefenseView.cs.

- [ ] Synthesize C4..C5 with fast attacks, decaying partials and faded tails; pool audio voices and schedule with an 80ms lookahead.
- [ ] Start/pause/resume/restart without skipping the pending beat; replay logs by original beat timestamps; discard time debt after a stall instead of bursting notes.
- [ ] Build scalable Chinese interface: beat pulse, tower placement and range, enemy pitch labels, tower filter controls, 16-beat score strip, piano key feedback, result/replay buttons.
- [ ] Manually verify first-time controls and all primary buttons in a visible Windows player.

## Task 3 — integrate, build and deliver

Files: Assets/PianoDefense/Editor/PianoDefenseBuilder.cs, Assets/Scenes/PianoDefense.unity, README.md, .gitignore, ProjectSettings/EditorBuildSettings.asset.

- [ ] Generate the scene with camera/audio listener and game/view components. Put it first in enabled build scenes.
- [ ] Build Windows x64 Mono using the installed editor. Inspect compilation and build results.
- [ ] Open the Windows player; verify setup, active fight, pause/resume and UI readability. Record model tests, build evidence and limitations in the README.
- [ ] Leave the playable build available under Builds/PianoDefense; review changes and report the launch path.
