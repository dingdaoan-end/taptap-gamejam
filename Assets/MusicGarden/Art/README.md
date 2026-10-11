# Production art v0.4

Generated with the built-in imagegen tool on 2026-10-11. No external UI framework or copied GitHub artwork. Reference concepts remain in docs/art/2026-10-11.

- Actors.png: 1536×1024 RGBA, six 512×512 cells. Top: Piano, ChordPiano, Core. Bottom: Note, FastNote, ChordNote. Alpha preserved; Unity slices into six independent sprites.
- Terrain.png: actual tool output1254×1254, four627×627 cells. Top: Grass, Road. Bottom: Blocker, Pad. Unity slices equal cells using actual image dimensions.
- Square.png: code-generated white8×8 primitive for editable range/UI geometry.

## Final prompt set

Actors: Create one production game sprite sheet for a 2D orthogonal top-down music tower defense, using the attached images only as style/subject references. Exactly1536x1024, regular3-column2-row grid of six512-square cells, no separators, no text, actual transparent background. Each subject entirely within its cell with64 pixels margin, centered consistent foot anchor at80% cell height. Readable hand-painted simplified dark teal night garden walnut brass warm ivory style, slight overhead frontal view, no isometric grid. Top row: one small grand piano tower with raised navy walnut lid and gold circle on circular stone plinth; one upright double keyboard chord piano tower with three amber lights on circular stone plinth; glowing amber crystal musical core with piano-key ring, no surroundings. Bottom row: one round navy ink monster with neutral white luminous center and tiny feet; one faster streamlined navy ink monster swept-back fins and neutral white luminous center; one three-lobed chord ink monster with cyan gold coral three glowing centers. No pitch badges, labels, grass floor, decorative background or mini views. Clean alpha silhouettes. Six independent equally scaled sprites.

Terrain: Generate one production terrain texture atlas for a2D orthogonal square-grid tower defense night garden, using attached kit as style reference. Exactly1024x1024 with exactly2x2 square512x512 cells, cells touch with no gutters, text or labels. Flat straight-down texture view. Top-left: seamless muted teal-green short grass with sparse tiny flecks, no trees or white border. Top-right: seamless warm ivory sandstone square paving, broad smooth readable floor, subtle cracks and texture, fills cell with no grass edges. Bottom-left: dense dark navy-teal hedge canopy with stone flecks, fills cell, clearly impassable and darker than grass, no tall perspective. Bottom-right: muted teal grass with simple circular ivory stone build plinth and thin brass rim,70% cell width, readable at64 pixels. Restrained hand-painted watercolor texture, grass416E68 navy18283D roadD6C6A0 goldBFA367. No perspectives, extra tiles, frames, gradients across atlas or UI.

Audio and Chinese font reuse the existing project licenses documented in THIRD_PARTY_NOTICES.md.
