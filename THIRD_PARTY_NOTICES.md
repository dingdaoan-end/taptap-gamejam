# Third-party assets and references — Piano Composition v0.3

## Included assets

- **Kenney UI Pack 2.0**, Kenney, https://kenney.nl/assets/ui-pack — CC0. Selected grey rectangle/round button PNGs, unchanged; tinted in-game.
- **Kenney Particle Pack**, Kenney, https://kenney.nl/assets/particle-pack — CC0. Selected circle, spark and star PNGs, unchanged.
- **Kenney Tower Defense Kit**, Kenney, https://kenney.nl/assets/tower-defense-kit — CC0. `tower-round-base.fbx` is used as the piano pedestal; materials and orientation changed in the prefab.
- **Salamander Grand Piano v3**, Alexander Holm, https://github.com/sfzinstruments/SalamanderGrandPiano — Creative Commons Attribution 3.0 Unported, https://creativecommons.org/licenses/by/3.0/. Sources: `C4v8.flac`, `D#4v8.flac`, `F#4v8.flac`, `A4v8.flac`, `C5v8.flac`. Modified for this game: mono conversion, pitch resampling for D4/E4/F4/G4/B4, gain adjustment, 2.6-second truncation with a fade, converted to 44.1 kHz / 16-bit WAV. No endorsement is implied.
- **Noto Sans CJK SC**, Noto project, https://github.com/notofonts/noto-cjk — SIL Open Font License 1.1. Unmodified `NotoSansCJKsc-Regular.otf`.

Full licenses are retained under `Assets/PianoDefense/Art/Licenses`. Downloaded 2026-10-09. The shipped game includes this notice, embedded sample attribution on the result screen, and embedded Chinese font.

## Design/code references (no source code or artwork copied)

- Brackeys Tower Defense Tutorial: https://github.com/Brackeys/Tower-Defense-Tutorial — build/select flow reference.
- Rhythmify: https://github.com/Gkxd/Rhythmify — shared beat-driven presentation reference.
- Unity Royale Runtime UI demo: https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo — resource hierarchy and card interaction reference.

Game-specific piano geometry, note enemies, board markers, gameplay, resonance and UI composition were created in this project.
