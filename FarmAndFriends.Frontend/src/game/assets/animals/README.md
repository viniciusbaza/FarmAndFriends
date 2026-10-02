# Animal assets

Generated using built-in ImageGen from the approved original concept boards in imgs/animal-enclosure/2026-09-12-v1. Originals preserved in Codex generated_images; copies in this directory are the runtime sources. No external game's art was extracted. No CLI fallback or raster postprocessing was used.

- chicken-poses.png: 1536 × 1024, RGBA, actual alpha verified. 4 columns × 2 rows: adult then chick; idle, walk A, walk B, peck. JSON uses equal 384 × 512 frames. Measured foot baselines differ from the requested prompt: adult494, chick382 within row; metadata uses those observed anchors. Adult opaque height approximately392px and chick264px. Keep per-age scale constant when changing pose, including peck. Walk poses are illustrative frames with subtle differences, not motion-capture.
- chicken-nests.png: 755 × 275, RGBA. Two adjacent frames, `nest-empty` (380 × 275) and `nest-eggs` (375 × 275), copied pixel-for-pixel from the approved kit. No historical ground or fence components remain in the runtime atlas.
- Unused complete and aligned enclosure PNG/JSON pairs were removed on 2026-09-16. Their generated originals remain outside the runtime directory; the prompts below record provenance, not loadable assets.
- enclosure-wood-transparent.png: 1774 × 887, RGBA. Current wooden style. The rectangular isometric footprint has extra depth toward the plots. The lower corner pillar remains refined farther left and down along the intersection of the two axes, while the unnecessary intermediate pillar on the left side remains removed and its two rails continue without a seam. The gate recovers the cleaner compact proportions of the earlier multi-post revision: a small centered X-braced leaf between dedicated hinge and latch pillars, followed by a separate two-rail segment up to the corner. The complete interior, perimeter grass, plants and ground rocks use transparent alpha, so the active farm terrain remains visible beneath the structure.
- The front gate is visually closed, a deviation from the prompt's gap; no physical gate-opening mechanic is specified. World input uses its own hit area.
- Earlier kit attempts painted a checkerboard into RGB and were rejected; only the final RGBA kit is consumed.
- Nest egg count is decorative and never sets inventory or collectible quantity.
- cow-poses.png: approved 1536 × 1024 RGBA sheet, copied without raster changes from `imgs/animal-enclosure/2026-09-16-cow-v1/cow-poses-v1.png`. Equal 384 × 512 frames: adult and calf × idle/walk-a/walk-b/graze. JSON records measured hoof pivots per frame; scales are constant per age (idle opaque heights adult346px/calf263px). Approved adult and calf coexist with the chicken in independent scene slots.
- milk-pails.png: approved 1254 × 1254 RGBA image, copied unchanged from the same output folder. Two 627 × 1254 frames named milk-empty/milk-full; measured bottom950, pivot x0.54. Opaque idle height593px is displayed as18 world units. Empty/full is presentation of backend readiness, never item count.
- sheep-poses.png: 1536 × 1024 RGBA, 4 × 2 cells of 384 × 512. Top row is adult with full wool; bottom row is the same adult shorn, each ordered idle/walk-a/walk-b/graze. Generated with built-in ImageGen using local Farm & Friends references, copied from this task's `generated_images` without raster postprocessing. Transparent alpha and per-pose hoof bounds were checked before writing the JSON atlas. Readiness comes exclusively from the accepted cycle snapshot; there is no separate wool object.
- lamb-poses.png: 2172 × 724 RGBA, one row of four 543 × 724 cells ordered idle/walk-a/walk-b/graze. Generated with built-in ImageGen using the adult sheep and local style references; copied unchanged from this task's `generated_images`. It has its own measured hoof pivots and a smaller fixed world scale.
- Scene layout, displayed sizes, visual states and cleanup are owned by Phaser. Backend snapshots determine age/readiness. Pig uses its own catalog, fourth enclosure sector and fertilizer readiness presentation.

## Sheep final production prompts

Adult sheet: Create a Farm & Friends production sprite sheet of one friendly cream-white sheep, matching the approved warm painted isometric animal style in the attached local references. Output a genuinely transparent RGBA PNG, exactly 1536 × 1024, four equal 384 × 512 cells per row and two rows. Top row: full fluffy wool, ready for shearing. Bottom row: the same adult sheep just shorn, with a short smooth coat. In each row show idle, walk A, walk B, graze from left to right. Keep the character's face, proportions, scale and facing direction consistent across all cells, with hooves grounded and generous transparent margins. No separate wool prop, shadows, ground, text, borders or backdrop.

Lamb sheet: Create a matching young lamb production sprite sheet from the approved adult sheep reference in the same Farm & Friends painted style. Output a genuinely transparent RGBA PNG, exactly 2172 × 724, one row of four equal 543 × 724 cells. Show idle, walk A, walk B and graze from left to right, all facing the same lower-right isometric direction. Keep the lamb visibly smaller and younger than the adult while preserving its cream wool, face and hoof styling. Ground the hooves consistently; use generous transparent margins. No adult sheep, wool prop, shadows, ground, text, borders or backdrop.

## Character prompt

Use case: stylized-concept game sprite production. Reference image is approved character design. Generate ONE production sprite sheet on GENUINELY TRANSPARENT alpha background, no ivory, no checkerboard painted in image, no shadows baked behind cells. Exactly 4 columns x 2 rows in equal cells, no borders/text. Top row adult cream-white red-comb hen: idle, left-foot forward walk, right-foot forward walk, peck head down. Bottom row yellow chick same poses. Keep EXACT same body size per row, direction facing lower-right elevated isometric, feet contact at the same baseline in each cell, all centered, generous transparent margins. Preserve approved warm rounded painted Farm & Friends design, no other animals/products. Eight separate cutouts total. Canvas 1536x1024, each cell 384x512, feet contact centered at x192 y430 within each cell. Adults body roughly 280 high, chicks 175 high. This is a usable transparent game atlas, not a concept board; background alpha zero. No labels or logos.

## Selected kit prompt

Generate a transparent PNG game sprite atlas, alpha channel required. Use attached approved design ONLY for style: honey wood, rounded painted cartoon. On transparent empty background draw five isolated sprites arranged in two rows: top row diamond grass floor, rear inverted-V fence, front V fence with gate gap. Bottom row empty straw nest and straw nest with white eggs. Each separated with ample transparent space. Fences have no grass floor, interior and rail gaps transparent. Camera 2:1 isometric. No labels, no backdrop, no checker pattern anywhere, no animals. The output will overlay the game scene so every pixel outside the five objects must be transparent, exactly as a cutout sticker sheet.

## Complete enclosure prompt

Extract and refine only the complete empty enclosure from the selected reference. Produce one centered transparent cutout with connected warm rounded posts, two horizontal rails, centered gate, grass and worn-earth interior, flowers, stones and a rear roofed hay shelter. Preserve the elevated isometric perspective. Exclude animals, nests outside the enclosure, text, UI, barn and plots.

## Aligned enclosure prompt

Rebuild the front of the complete enclosure as a true isometric parallelogram aligned to the farm-plot axes. Remove the centered point and place the rectangular gate within one continuous diagonal front fence side, flush with the adjacent rails. Preserve the connected posts, two rails, grassy interior, rear shelter, transparent alpha and established painted style.

## Transparent wooden style prompt

Edit this exact Farm & Friends isometric animal enclosure into a reusable transparent structure asset. Preserve the current isometric geometry, scale, warm wooden posts and rails, straight continuous front fence side, gate position and angle aligned with the plot axes, rear shelter with red tile roof, and hay inside the shelter. Remove every painted ground element: remove the entire green grass floor, worn dirt patch, grassy perimeter lip, flowers, shrubs, plants, and loose ground rocks. Make all interior and exterior spaces between and beneath the fence rails fully transparent alpha, including the whole enclosure floor, so the active farm background terrain shows through naturally. Do not add a replacement floor, shadow plate, lawn border, vegetation, dirt color, or scenery. Keep clean transparent cutout edges around the wooden fence, gate, shelter, and shelter hay. Maintain the established polished casual mobile farming game painted style and the same camera/isometric perspective. Output one RGBA PNG with transparent background, no text, no UI, no extra objects.

## Front alignment refinement prompt

Edit the production transparent wooden enclosure using the annotated runtime screenshot only as an alignment reference. Increase its depth toward the viewer and plots. Move the complete front fence outward so the left corner, gate posts, intermediate post and lower-right corner are collinear on one straight isometric diagonal near the guide line. Remove the recessed bend, keep the rear and shelter approximately fixed, and preserve transparent floor and rail gaps without adding terrain or scenery.

## Pillar geometry refinement prompt

Edit the current production asset using the annotated runtime screenshot as geometric guidance. Move the lower corner pillar outward and left to the precise intersection of the two projected isometric axes, then reconnect both adjacent two-rail runs to that vertex. Remove the marked intermediate pillar from the left sloping side completely and extend both rails continuously through its former position, without stump, seam or gap. Preserve all other structure and fully transparent terrain.

## Gate post restoration prompt

Correct the pillar refinement without changing its isometric vertex. Restore the gate to its compact original width and restore the dedicated latch pillar at its right end. Preserve a separate short two-rail segment between that latch pillar and the relocated corner pillar. The front sequence must remain hinge pillar, compact gate, latch pillar, two-rail segment and geometric corner pillar. Keep only the user-marked pillar 2 removed.

## Coordinate refinement prompt

Move the lower corner pillar near 57% / 90.9% moderately left and slightly down while reconnecting both rail pairs on their isometric axes. Scale the gate leaf near 37.9% / 76.9% down by about 12–15% and shift it left and up so it is centered between the unchanged hinge and latch pillars. Preserve all other structure.

## Compact centered gate prompt

Use the earlier multi-post enclosure only as a visual reference for the preferred gate. Replace only the current front gate assembly with a cleaner compact X-braced leaf, centered between dedicated hinge and latch pillars on the long front side. Keep balanced rail lengths, the separate rail segment to the lower vertex, the current footprint and the previously removed left-side intermediate pillar absent.

## Source images

Characters: exec-e01e225c-879a-4d0d-adff-c8f52d33920c.png.
Kit: exec-e28680b2-a19a-41e8-bd6f-8be055bf688a.png.
Complete enclosure: exec-00265277-4fa4-4ed2-9bd0-da48a097e013.png, derived from the user-selected `exec-b54a06a0-11f5-461c-bb92-25144b7179ac.png` composition and `exec-ff5d83bd-7895-420e-a81f-b059d44761b1.png` fence reference.
Aligned enclosure: exec-dccc35c0-07ce-44e7-a4ae-8ba42e4d6096.png, derived from the current complete enclosure and the annotated runtime alignment reference.
Transparent wooden style: exec-2539582c-3697-4c68-ba82-31fbd1aad94c.png, derived from the aligned enclosure after the user requested terrain-independent presentation.
Current front-aligned revision: exec-3329a69e-86f3-491c-979f-5bc9aac8bbc0.png, derived from the transparent style and the user's annotated runtime guide.
Rejected over-removal revision: exec-02823f52-8d9d-4c10-bb78-b73d189f02bd.png; it removed the requested pillar but stretched the gate by also omitting its closing post.
Current pillar-corrected revision: exec-afb26d3b-e8be-436b-a19e-01c44745fcef.png, which restores the compact gate, latch pillar and following rail segment while keeping only the user-marked pillar 2 removed.
Current coordinate-refined revision: exec-be45c4e0-c538-4e57-86cc-c17824af3e6f.png, which moves the lower vertex left/down and reduces and recenters the gate leaf.
Current compact-gate revision: exec-3d1a61dd-59cd-4ea3-ac57-3ec036d89632.png, based on the preferred earlier gate proportions while preserving the final footprint.
Both originate under C:/Users/vinic/.codex/generated_images/01a088b2-6cde-7c62-9e97-71005b3a5844/.

## Pig walking extension — 26 September 2026 (superseded)

`pig-walk-poses.png` was generated by the built-in ImageGen from the local `pig-poses.png` reference, copied without raster processing. RGBA alpha was verified (966,661 fully transparent pixels). The 1536×1024 sheet contains four original 768×512 cells: top row piglet walk A/B with opposite strides; bottom row happy adult ready walk A/B with the same green leaf indication. This intermediate attempt mixed the original idle/producing atlas with a separate walking sheet; both are now reference assets only and are not loaded in the scene. Runtime uses one fixed scale per age across A/B, so alternating hooves do not resize the torso; measured centers and hoof pivots keep the animal on the ground. The revised art makes the near foreleg bend backward in A and extend in B, with opposite far-leg poses and a corresponding rear-leg stride. Atlas-only trimming removes the bottom 12 pixels from both piglet frames to exclude a neighboring leaf tip, preserving the full source coordinate system for origins.

Prompt intent: keep the approved Farm & Friends rounded pink isometric pig design, transparent background, two alternating leg positions for the piglet and for the adult ready pig, with the existing happy face and green leaf on both adult strides; preserve idle poses in the separate original atlas. No scenery, text or additional props.
## Unified pig cycle atlas — 26 September 2026

`pig-cycle-poses.png` is the only pig texture loaded by the scene. It was generated with built-in ImageGen from the previous pig references, then edited with ImageGen to exaggerate the opposite B stride while preserving faces and torsos. Actual output is 1254×1254 RGBA, not the requested 1536×1536; metadata uses nine real 418×418 cells. Rows: piglet, producing adult, ready adult. Columns: idle, walk A, walk B. Near forelegs are planted in A and lifted back in B; far forelegs and hind legs provide the opposite stride. Both adult ready strides and idle preserve the green leaf.

Alpha was inspected (888,454 fully transparent pixels). Each row shares a stable torso pivot x0.54 and a fixed support baseline: piglet371, adult370, ready adult356. Baselines are not changed to follow the most extended hoof. Idle silhouette heights set one scale per age (piglet23/283, adult32/330); every idle/A/B frame of that age uses that scale. Face and torso alignment was inspected across columns; no per-frame height normalization or texture swap occurs during the gait. Earlier `pig-poses` and `pig-walk-poses` files are preserved only as art references.

Prompt intent: one coherent sheet containing all three pig states, three equal cells per row, stable body/head position and scale within each row, consistent isometric camera, alternating near/far foreleg and hind-leg support, and the same happy ready face and leaf. The column-B correction specifically lifts and bends the near foreleg backward while the far foreleg is planted forward, with the opposite hind-leg support. No scenery, text or products outside the ready leaf.
Final near-foreleg orientation refinement: ImageGen edited only the near foreleg in the third column for all three states. The shin and hoof point downward like the other legs, with the hoof front/split visible rather than the sole facing the viewer. The receded stride, faces, torso anchors and ready leaf remain. Dimensions stayed 1254×1254; existing atlas coordinates, scales and pivots remain valid.