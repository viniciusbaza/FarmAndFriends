# Local enclosure clearing

Tool: built-in image_gen, transparent output.

## Generation prompt

Use case: precise-object-edit.
Asset type: transparent PNG isometric farm background for a game, same composition and framing as input 1 (1774x887).
Image 1 is the EDIT TARGET and immutable original art/style. Image 2 is ONLY a reference showing where space for the animal enclosure was cleared; do NOT copy its overall rendering or altered texture.
Primary request: create a surgical local edit of Image 1. Clear ONLY the decorative shrub/flower/rock strip to the immediate right of the barn, under the back fence between the barn and the pear tree, continuing to the grass in front of the two fruit trees on the right, to reserve grass for an animal enclosure rendered separately by the game. The grass clearing should be like the free space in image 2. Remove the shrubs, white/orange flowers and rocks in that reserved upper right area. Keep the pear and apple trees on the right in their original exact positions and preserve their original look. Reconstruct the newly exposed back wooden fence where foliage was removed, matching the original wood. Fill exposed ground with the same fine softly painted grass as image 1. Do not draw the animal enclosure itself.
Critical invariants: do NOT redraw, filter, sharpen, recolor or restyle the whole scene. Keep the barn, pond, left-hand side, ALL foreground fence and gates, lower foreground shrubs and flowers, right fruit trees, ground silhouette, fine grass texture, lighting, palette, and all elements outside that localized clearance exactly unchanged from Image 1. Especially keep the entire lower right foreground fence/flower border intact. No scaling, no camera/view change, no new objects. Preserve transparent canvas outside the island. Output landscape 2:1 matching original proportions, ideally 1774x887.

## Integration

- Base: `farm-clearing.png`, untouched.
- Final: `farm-clearing-enclosure-v3.png`, 1774 × 887 RGBA.
- Only a local patch from the generated edit was composited onto the original, with an inward feather. The rear fence, original trees and foreground retain their original pixels outside the edited polygon.
- `validation.json` records the polygon and decoded RGBA comparison: zero exterior pixel differences.
- Scene uses v3 only after enclosure acquisition, through the existing state selection and synchronization.
- Observed the real Phaser fixture with and without enclosure in the in-app browser; `with-enclosure.png` records the acquired state. Build, lint and 158 frontend tests passed.
