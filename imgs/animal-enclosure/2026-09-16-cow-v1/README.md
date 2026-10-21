# Vaca — primeira proposta visual

Gerado em 2026-09-16 com a ferramenta integrada ImageGen, sem CLI e sem edição raster posterior. Referências de estilo: `chicken-poses.png` e `enclosure-wood-transparent.png` do jogo atual. Arte original, sem extração de assets de outro jogo.

| Arquivo | Dimensão observada | Conteúdo |
| --- | --- | --- |
| cow-poses-v1.png | 1536 × 1024 | Adulta e bezerra em quatro poses: parada, caminhada A, caminhada B e pastando |
| milk-pails-v1.png | 1254 × 1254 | Balde vazio e balde cheio, lado a lado |

Ambos são PNG RGBA; alpha zero verificado nos cantos e espaços entre objetos. A ferramenta respeitou a dimensão solicitada para a vaca, mas devolveu os baldes em 1254 × 1254, apesar do pedido de 1024 × 1024. Nenhuma imagem foi redimensionada.

Referências aprovadas pelo usuário para implementação. Os PNGs originais foram copiados para `FarmAndFriends.Frontend/src/game/assets/animals/`, com atlas JSON e âncoras medidos para o runtime. As poses de caminhada são ilustrações geradas, usadas na animação de apresentação do cercado. O balde indica o estado confirmado da produção, sem determinar quantidade, preço ou tempo de leite.

## Prompt — vaca e bezerra

Use case: stylized-concept. Asset type: original Farm & Friends cow animation sprite atlas, for visual approval before game integration. Input image 1 chicken atlas is STYLE REFERENCE ONLY for rounded painted character rendering and warm light; ignore all background/glow. Input image 2 transparent wooden enclosure is STYLE and CAMERA reference only. Create ONE transparent RGBA PNG atlas, 1536x1024, arranged exactly 4 columns x 2 rows, each cell 384x512. Row 1 the SAME adult friendly dairy cow, creamy white coat with consistent charcoal-black spots, pink muzzle, warm brown eyes, small cream horns and modest pink udder, rounded believable four-legged body. Row 2 the SAME young female calf with smaller body, softer features, no udder and no prominent horns. Four poses in each row left to right: idle standing on all four hooves; walk A left front hoof advanced with diagonal back hoof moving; walk B alternate diagonal hoof pair advanced; grazing lowered head. Same animal identity, same black patch placement, same body size within each row, same slightly elevated 2:1 isometric three-quarter view facing lower-right, visible top of back, bodies fit horizontally inside cells. Soft upper-left sunlight, soft painted highlights, warm brown outlines, polished casual farming game style compatible with supplied characters and enclosure. Center each animal at x192 within its cell; standing hoof contact line y445 in each cell; leave enough padding, never clip tails ears or hooves. Adult approximately 320px wide and 260px tall; calf approximately 235px wide and 210px tall. Output ONLY eight individual cow cutouts on truly transparent alpha background. No ground, no grass under grazing heads, no hay, no fence, no barn, no shadows or colored glow spanning background, no captions, no labels, no borders, no watermark. Not a concept poster. Transparent spaces around and between silhouettes must be alpha zero.

## Prompt — produção

Use case: stylized-concept. Asset type: original Farm & Friends dairy production two-state game prop atlas for visual approval. Input images: cow atlas for style only; wooden enclosure for painting/material and camera style only. Generate one transparent RGBA PNG 1024x1024. Exactly TWO isolated cutout props arranged side by side in two equal cells 512x1024, no borders or labels. Left cell: empty small galvanized silver milk pail, open oval rim, subtle blue-grey metal facets, warm painted outlines, lowered curved handle, three-quarter elevated isometric view. Right cell: IDENTICAL same milk pail, same silhouette size rim perspective and handle position, visibly filled nearly to the rim with creamy white milk, no splash or overflowing. Place centers x256 and x768, bottom contact y740 in each cell, prop width about 350px and height about 370px, generous clear transparent padding. Match the cow and enclosure's warm rounded painted casual farming game art and upper-left soft light. Both pails must be distinguishable when scaled to 24 pixels. Genuine transparent background, alpha zero everywhere outside the two pails including between the objects and through the handle gaps. No cows, no grass, no floor, no backdrop or baked shadows, no text, no logo, no UI, no checkerboard painted into pixels. This is a proposed world production indicator, not a shop/catalog implementation.

## Originais preservados

- Vaca: `exec-26b4b941-5192-4a6d-a1d6-3f05dc049fa1.png`.
- Baldes: `exec-00493c47-54db-499e-ac96-13d9a9bc611f.png`.
- Pasta de origem: `C:/Users/vinic/.codex/generated_images/01a08393-cc86-7091-9cfe-a240311ede3d/`.

## Limpeza anterior à criação

Removidos do diretório runtime apenas `enclosure-complete.png/.json` e `enclosure-aligned.png/.json`, sem consumidores ativos. Preservados o cercado transparente atual, a galinha e o kit usado pelos dois ninhos. O cenário original continua necessário na tela de login. Os conceitos e evidências em `imgs/animal-enclosure` foram preservados como histórico de aprovação e validação.

Verificação: cinco testes em `animalVisuals.test.ts` e build frontend aprovados. O build mantém o aviso preexistente de chunks maiores que 500 kB.
