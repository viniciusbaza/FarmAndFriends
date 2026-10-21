# Cercado — propostas visuais v1

Propostas para avaliação, sem aprovação de arte ou implementação. Nenhum asset de produção foi substituído.

## Arquivos

| Arquivo | Dimensões | Uso |
| --- | --- | --- |
| 01-farm-composition.png | 1774 × 887 | Composição da fazenda e cercado com uma galinha |
| 02-enclosure-kit-concept.png | 1536 × 1024 | Materiais, cercas, portão, chão, sombra e ninhos |
| 03-chicken-poses-concept.png | 1536 × 1024 | Estudos de filhote e adulta em quatro poses |

Todos os PNGs foram inspecionados visualmente e têm pixels RGB de 24 bits, sem canal alpha. Os fundos são opacos; estas pranchas não são recortes ou atlas utilizáveis no jogo. As poses repetem o mesmo personagem para estudo; não representam vários animais comprados.

## Direção proposta

Madeira clara arredondada, luz quente superior esquerda, cercado aberto de baixa altura, abrigo pequeno, galinha branca com crista vermelha e filhote amarelo. O MVP mostra apenas galinha e ovos. O espaço vazio sugere a reserva para quatro espécies, mas ainda não comprova que os quatro volumes cabem.

O desbloqueio continua após nove lotes, junto da primeira expansão, conforme o plano. A composição gerada não reproduziu corretamente a grade de 7 × 4 com nove lotes desbloqueados e dezenove bloqueados. Ela serve para avaliar aparência, não contagem de lotes, posição final, câmera ou regra de desbloqueio. A quantidade desenhada de ovos é decorativa.

## Manifesto recomendado para uma futura produção

| Asset proposto | Camada e ponto de apoio |
| --- | --- |
| enclosure-ground | Atrás de cercas, ninhos e animais; âncora comum no chão |
| enclosure-fence-rear | Segmentos traseiros separados; mesma referência espacial do chão |
| enclosure-fence-front | Segmentos dianteiros separados para oclusão por profundidade |
| enclosure-gate | Separado, alinhado à abertura e ao caminho |
| animal-shadow | Sob os pés; dimensão independente da pose |
| nest-empty / nest-eggs | Mesma tela e âncora no contato com o chão |
| chicken-chick / chicken-adult | Futuros quadros com pés ancorados na mesma posição |

Os nomes são sugestões, sem contratos adicionados. Exportações futuras devem ter transparência real, margens consistentes e nenhum fundo pintado. Separar cercas dianteiras e traseiras é necessário; a prancha ainda não fornece módulos geometricamente encaixáveis.

## Escala e animação — hipóteses a validar

O código atual usa tile de 64 × 32 e plot visual de 70,4 × 35,2 em farmLayout.ts; a cenoura pronta usa altura de 33 em cropVisuals.ts. Como ponto de partida, testar adulta com aproximadamente 24 unidades de altura e filhote com 14–16. Esses números são proposta visual, não balanceamento nem medidas já aprovadas.

Pode-se iniciar o teste de exportação com quadros de 128 × 128 pixels, mantendo escala de exibição independente da tela. Fixar o contato dos pés, por exemplo próximo a (0,5; 0,85), somente após medir as poses finais. Testar idle, caminhada e bicada; repouso é opcional. Quantidade de quadros e direções dependerão do teste animado.

A prancha não tem quadros alinhados, pivôs medidos ou sequência temporal. Ajustar a perspectiva para a câmera isométrica real. A profundidade deverá acompanhar o contato no chão e as cercas; não usar uma única camada que esconda indevidamente os animais.

A câmera atual considera os lotes. A implementação precisará incluir o cercado nos limites visuais e verificar a primeira expansão e todos os estados até 28 lotes, desktop e mobile. Não houve teste de runtime, encaixe dos quatro animais futuros ou validação de câmera.

## Referências e rastreabilidade

Gerado com a ferramenta integrada de imagens usando farm-clearing.png, plot.png e carrot.png existentes como referências de estilo. As propostas subsequentes também usam a composição e o kit desta série. Prompts completos em prompts.md. Os originais foram preservados na pasta de imagens geradas do Codex.

