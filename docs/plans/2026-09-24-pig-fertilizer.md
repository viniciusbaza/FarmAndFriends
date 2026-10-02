# Porco e adubo — decisão e ExecPlan

> Revisão de 26/set/2026: a política de tempo e roubo descrita abaixo é histórica. A decisão atual está em [Adubo: conclusão imediata e roubo antecipado](2026-09-26-fertilizer-early-theft.md): teto de uma hora, sem espera mínima de 30 segundos, roubo pelo novo prazo e desestímulo por balanceamento mantendo o roubo normal. Os registros de validação abaixo descrevem a versão original.

## Decisão consolidada

**Prototype**, autorizado pelo pedido de implementação. A oportunidade é fechar o quarto espaço do cercado com uma utilidade visível que melhora o cuidado da própria fazenda e cria uma ação positiva durante visitas. O jogador volta para coletar adubo pronto sem expiração e escolhe um lote em crescimento para antecipar. Um amigo pode gastar o próprio adubo para beneficiar o dono; a ação fica distinta de regar e roubar e não revela prontidão na lista de amigos.

O dono colhe mais cedo, sem perda de rendimento. O visitante ganha uma interação nominal, mas não recebe moedas, XP nem prioridade de roubo pelo uso. Para não criar uma janela de roubo antecipada, a primeira adubação conserva o prazo original como limite mínimo de roubo para aquele ciclo. O adubo do porco não pode ser roubado neste protótipo e não pode ser vendido por moedas. A compra por notas é opcional: o porco continua adquirível com moedas comuns. O risco principal é a compra acelerar repetidamente renda e XP; uma aplicação por lote/ciclo, redução moderada e fonte gratuita limitam esse efeito. Não há acúmulo de produção animal entre ciclos nem perda por ausência.

## Hipóteses econômicas configuráveis

| Parâmetro | Valor inicial para teste |
| --- | ---: |
| Porco / nível mínimo | 2.000 moedas / 7 |
| Crescimento / produção | 8 h / 24 h |
| Adubo base / bônus protegido por ajuda | 4 / +1 até +2 |
| XP por coleta | 25 |
| Loja | 2 notas por adubo |
| Aplicação | 1 por lote e ciclo de colheita |
| Redução | 25% da duração base, até 30 min, mantendo 30 s até ficar pronto |

Esses números não são balanceamento final. Adubo produzido e comprado é o mesmo item, com potência idêntica. Usá-lo não altera yield, moedas ou XP diretamente. Medir adubos produzidos/comprados/usados, uso em visitas, aceleração efetiva, XP e moedas por hora, prontidão após ausência, roubo e janelas de cuidado. Sucesso requer uso frequente do adubo gratuito, visitas úteis sem roubo e ausência de aumento desproporcional de progressão pela compra.

## Contratos e arquitetura

- Reusar `FarmAnimal`, `AnimalProductionCycle`, `AnimalActionCompletion` e `ItemType.AnimalProduct` com espécie `pig`, estágio `piglet` e produto `fertilizer`. Somente o catálogo de produtos vendáveis inclui ovo, leite e lã.
- Expor compra premium em rota própria, com `Idempotency-Key`, débito por `PremiumCurrencyService` e item no inventário; não passar pela compra de itens por moedas.
- `POST /farms/{farmId}/plots/{plotId}/fertilize` aceita identificadores do ciclo esperado, nunca prazo, preço ou recompensa enviados pelo cliente. Responde com operação, replay, prazo anterior/novo e saldo do item. Identidade, amizade, lote, ciclo, estoque, elegibilidade e limite são validados em transação na ordem de locks do projeto.
- Persistir uma conclusão idempotente por aplicação, com prazo original e novo, ator e ciclo. A consulta de roubo usa o prazo original quando o lote foi adubado. Adicionar migration apenas para essa persistência.
- `FarmResponse` inclui a identidade do ciclo necessária ao cliente. React envia intenção, usa a resposta confirmada e recarrega inventário/fazenda; Phaser recebe somente `farm:sync` e apresenta o porco e o novo estágio do lote.
- Compra/uso/adubação registram feedback claro. A aplicação em amigo deve gerar histórico nominal separado de cuidado e roubo, sem spam por repetição idempotente.

## Execução e aceite

1. Backend: catálogo/política do porco, coleta e ajuda; venda exclui adubo; compra por notas com ledger; uso em lote com validação, persistência, preservação da janela de roubo e testes PostgreSQL.
2. React: compra do porco e adubo, inventário, modal de lote próprio/visitado e feedback, tipos e sincronização autoritativa.
3. Phaser: asset RGBA do porco e filhote ou pose jovem, quarto setor do cercado, indicação de pronto no sprite e sincronização idempotente; lote reflete `readyAt` confirmado.
4. Verificar ciclos independentes das quatro espécies, compra premium sem rota por moedas, rejeição de lote vazio/pronto/fora do ciclo sem gasto, replay/concorrência, amizade revogada, venda proibida, estado visual desktop/mobile e migração sem drift.

Não incluir fabricação, efeito em área, aumento de rendimento, compra obrigatória por notas, aplicação repetida no mesmo ciclo ou novas dependências.

## Evidência de implementação — 25/set/2026

- Backend Release compilou sem aviso. A migration `20260925020829_AddFertilizerApplications` foi aplicada ao PostgreSQL isolado `farmfriends_pig_test_20260925`; `has-pending-model-changes` não encontrou diferença. A suíte completa passou **270/270 testes, nenhum ignorado**. Os testes do adubo cobrem compra por notas e replay, uso próprio e em amigo, rejeições sem débito, concorrência, coleta/ajuda do porco, bloqueio de roubo/venda, janela original de roubo, cuidado e projeção do ciclo atual.
- Frontend passou **134/134 testes**, lint e build. O teste visual de cultura confirma que um `readyAt` anterior recebido do servidor adianta a fase sem fabricar prontidão. O atlas do porco tem quatro poses RGBA (adulto parado/andando/pronto e leitão) e é carregado no quarto setor.
- A fixture Phaser foi observada em viewport estreito com porco pronto, produzindo e filhote; cinco sincronizações mantiveram **103 → 103 objetos**. A fixture usa snapshots sintéticos. Ainda não há observação autenticada no navegador do fluxo completo compra → coleta → adubação própria/visita em desktop e mobile; essa é a verificação restante antes de release público.
