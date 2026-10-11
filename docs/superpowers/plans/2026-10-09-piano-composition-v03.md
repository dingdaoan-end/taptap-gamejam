# Piano Composition v0.3 Implementation Plan

> Execution: implement in the current Tuanjie project, preserving the existing uncommitted editable-scene work. The user approved the gameplay design and asset selection on 2026-10-09.

**Goal:** Ship a playable Windows prototype where placing piano towers, choosing attack beats and pitch filters produces music and a Do–Mi–Sol resonance.

**Architecture:** Preserve the pure C# simulation and DSP clock. Add global tower rhythm presets and simultaneous resonance settlement to the simulation. Author a real Canvas, board tiles, instrument prefabs and level configurations in the saved scene; presentation consumes snapshots and never advances gameplay.

**Tech Stack:** Tuanjie 1.10.4 / Unity 2022.3.62t16, C#, uGUI, .NET 8 core checks, Kenney CC0 graphics, attributed Salamander piano samples.

## Constraints and review focus

- Keep scene objects and UI editable in the engine. Keep an openable PianoDefense scene and do not discard the existing scene before preserving it.
- Windows standalone; no new framework, rendering pipeline or paid dependency.
- Attack rhythm uses the global beat, including towers placed mid-round.
- Resonance needs three distinct notes on the same beat; delays living enemies by one beat; never re-emits kill notes.
- Snapshot must copy rhythm and delayed-enemy state; restart/replay cannot leak prior-round state.
- Completing and retrying a lesson must restore its deterministic wave and budget; the final lesson must be winnable with its example layout.
- Assets keep source, license and attribution; packaged sample clips are owned assets, not destroyed as generated clips.

## Tasks

- [ ] 1. Add failing core checks for rhythm presets, global phase, resonance, snapshots and four deterministic lessons. Run `dotnet run --project Tests/PianoDefense.CoreTests` and confirm new checks fail before implementation.
- [ ] 2. Extend `DefenseSimulation.cs`; add `CompositionLessons.cs`; implement rhythm, resonance and lesson waves. Run the complete core checks and simulate example layouts.
- [ ] 3. Download selected Kenney graphics and piano/font assets into a staging folder; import only selected files into `Assets/PianoDefense/Art`; record exact sources and licenses in `THIRD_PARTY_NOTICES.md`.
- [ ] 4. Add saved uGUI hierarchy and controller in `PianoCompositionUI.cs`; extend game lesson/rhythm commands; enhance real board prefabs and effects without mixing rules into the renderer.
- [ ] 5. Add `PianoCompositionBuilder.cs` to migrate and save the scene and build `Builds/PianoDefense-v0.3/NocturnePiano.exe`. Back up the existing scene under Artifacts before migration. Keep the old view available but disabled in the upgraded scene.
- [ ] 6. Run core checks, editor/controller checks and a Windows build. Launch the build, verify placement/selection/rhythm/resonance/retry in the real UI, capture a screenshot and update README with controls and editing instructions.

## Reference use

Brackeys Tower Defense: build/select interaction structure (no copied source). Rhythmify: a shared music-driven clock (no copied source). Unity Royale: card selection and resource hierarchy (no copied source). Kenney UI/Particle packs: selected actual assets. Piano samples: replace the synthetic fallback while preserving DSP scheduling.
