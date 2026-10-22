# Farm environment assets

Phaser uses `farm-clearing.png` before enclosure acquisition and `farm-clearing-enclosure-v3.png` afterwards. Both are 1774 × 887 RGBA images. Version 3 composites only a local clearing from a built-in ImageGen edit onto the original; every pixel outside the edited polygon remains identical to `farm-clearing.png`. The rear vegetation and foreground border retain the original artwork. Prompt, polygon, pixel comparison and scene screenshots are recorded in [`imgs/animal-enclosure/2026-10-20-local-clearing`](../../../../../imgs/animal-enclosure/2026-10-20-local-clearing/).

## Previous version (v2)

`farm-clearing-enclosure-v2.png` is retained as history. Its broader texture changes motivated v3.

The current edit keeps the yellow-fruit tree at the right boundary and clears the complete placement band between it and the barn. Shrubs, flowers and loose rocks that could appear behind the enclosure rails were moved to outer perimeter areas. This leaves continuous grass for the animal enclosure without cutting the orchard through the enclosure sprite. Barn, pond, paths, plot clearing, outer fence and island silhouette remain invariants.

Current source output: `exec-d360e436-d1d4-4d0c-bb74-7988fd5c1b90.png` under `C:/Users/vinic/.codex/generated_images/01a0a2d9-6666-7212-8b96-c01033599aa4/`. The earlier placement revision is `exec-135a8e0f-1e4b-455a-b6e0-346a439b78ca.png`.

## Final prompt

Edit the production transparent farm-clearing environment and reserve a completely clean grassy placement zone between the barn's right side and the yellow-fruit tree. Remove the white-flower shrub, orange-flower plants, green bushes, loose rocks and all low foliage from that band, relocating a small balanced amount to the far outer perimeter. Keep the yellow-fruit tree, barn, fence, other trees, pond, path, perspective, canvas and transparent corners unchanged. Fill the vacated zone with seamless matching grass and add no enclosure, animals, plots, UI or text.
