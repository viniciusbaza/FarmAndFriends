# Cercado de animais — contrato backend

Implementação dos planos `docs/plans/2026-09-09-chicken-coop.md`, `docs/plans/2026-09-16-cow-production.md`, `docs/plans/2026-09-23-sheep-wool-production.md`, `docs/plans/2026-09-24-pig-fertilizer.md` e `docs/plans/2026-10-04-peacock-feathers.md`. Espécies `chicken` / `cow` / `sheep` / `pig` / `peacock`, produtos vendáveis `egg` / `milk` / `wool` / `feather`; nomes públicos Galinha / Vaca / Ovelha / Porco / Pavão e Ovos / Leite / Lã / Penas. Estágios `chick` / `calf` / `lamb` / `piglet` / `peachick` / `adult`. Nenhuma flag animal. Todos os endpoints exigem JWT e obtêm o ator de `NameIdentifier`.

| Método/rota | Request | Response HTTP 200 |
| --- | --- | --- |
| GET `/shop/animal-enclosure` | — | `AnimalEnclosureOffer` |
| GET `/shop/animals` | — | `AnimalOffer[]`; `growthDuration` e `productionDuration` em segundos |
| GET `/shop/animal-products` | — | `AnimalProductOffer[]` |
| POST `/shop/buy-animal-enclosure` | corpo vazio | `AnimalOperationResponse` |
| POST `/shop/buy-animal` | `{animalTypeId:"chicken", "cow", "sheep", "pig" ou "peacock"}` | `AnimalOperationResponse` |
| POST `/shop/sell-animal-product` | `{itemId:"egg", "milk", "wool" ou "feather",quantity:integer>0}` | `AnimalOperationResponse` |
| POST `/farms/{farmId}/animals/{animalId}/collect` | `{expectedCycleId:uuid}` | `AnimalOperationResponse` |
| POST `/farms/{farmId}/animals/{animalId}/help` | `{expectedCycleId:uuid}` | `AnimalOperationResponse` |
| POST `/farms/{farmId}/animals/{animalId}/steal` | `{expectedCycleId:uuid}` | `AnimalOperationResponse` |
| GET `/farms/{farmId}/animals/{animalId}/cycles/{cycleId}/helpers` | `?cursor&take` (1–50) | `AnimalHelpersPage`, apenas dono |

Todos POST exigem `Idempotency-Key` UUID não vazio. O fingerprint usa ação e valores semânticos tipados, incluindo item/quantidade na venda. Comprovante do próprio ator é reconhecido antes de estoque/ciclo/participação já consumidos. Chave conflitante retorna 409. Timeout exige recuperar a mesma chave e conteúdo; não criar outra intenção automaticamente.

`AnimalOperationResponse = {operationId,replayed,effect,current}`. `effect` é histórico imutável: ação, timestamp UTC, referências, quantidade efetiva, moedas gastas/ganhas, XP ganho, bônus protegido adicionado. Não representa saldo atual. `current` é a projeção atual autorizada; null para venda sem cercado ou replay após perder amizade. Ações: `buy-animal-enclosure`, `buy-animal`, `collect`, `help`, `steal`, `sell-animal-product`.

GETs existentes `/farms/my` e `/farms/{farmId}` acrescentam `animalState` com `{farmId,enclosure,revision,observedAt}`. Sem cercado: enclosure null/revision 0. Revisão da instalação aumenta em compra de animal, materialização da maturidade, coleta, ajuda e roubo. Projeções são transacionais sob locks; prontidão derivada pode mudar com mesma revision e observedAt posterior. Antes da maturidade cycle null; depois existe somente um ciclo aberto por animal. Primeiro ciclo começa no MaturesAt histórico; coleta inicia próximo ciclo em now. Não há acúmulo offline.

DTOs completos e nullabilidade em [AnimalContracts.cs](AnimalContracts.cs). `helpers` é null para visitantes; dono recebe resumo de até três nomes históricos e cursor. Lista completa permanece consultável depois da coleta. Cada visitante pode receber recompensa uma vez por animal e por ciclo, sem quota compartilhada entre animais ou fazendas. `rewardAvailable` acompanha a elegibilidade atual de `canHelp` e `nextRewardAt` permanece null no backend novo por compatibilidade de contrato, pois somente um novo ciclo do próprio animal reabre a ação. O frontend ainda aceita um prazo não nulo de uma versão anterior apenas para solicitar nova leitura autoritativa durante rollout misto ou rollback. Ajuda que completa bônus parcialmente paga o pacote visitante integral. Perdedor de corrida não registra nenhum efeito.

Erros de domínio usam ProblemDetails com `code`, `status`, `detail` e mensagem compreensível:

- 400: `INVALID_REQUEST`, `INVALID_IDEMPOTENCY_KEY`, `INVALID_QUANTITY`.
- 401: JWT ausente/inválido ou claim de identidade inválida.
- 403: `NOT_OWNER`, `NOT_FRIENDS`, `SELF_ACTION`.
- 404: `RESOURCE_NOT_FOUND` (inclui animal/ciclo que não pertence à rota).
- 409: `FIRST_EXPANSION_REQUIRED`, `UNSUPPORTED_FARM_LAYOUT`, `LEVEL_REQUIRED`, `ALREADY_OWNED`, `ENCLOSURE_REQUIRED`, `INSUFFICIENT_FUNDS`, `INSUFFICIENT_STOCK`, `CYCLE_CHANGED`, `NOT_READY`, `NOT_PRODUCING`, `HELP_ALREADY_REGISTERED`, `OWNER_BONUS_LIMIT_REACHED`, `THEFT_ALREADY_REGISTERED`, `THEFT_LIMIT_REACHED`, `IDEMPOTENCY_KEY_REUSED`, `BALANCE_LIMIT`.
- 500: `INVALID_CONFIGURATION` ou falha inesperada, sem dados internos. Falha de transporte/5xx não autoriza assumir rollback.

Inventário mantém forma existente e acrescenta ItemType `AnimalProduct` (enum persistido 4), itemId `egg`, `milk`, `wool` ou `feather`. Notifications acrescenta `AnimalHelped=8`, `AnimalProductStolen=9`, referências opcionais `animalId`, `animalCycleId`, `animalCompletionId`. Não inclui prontidão na lista de amigos. Frontend/integration devem reconhecer `peacock`, `peachick`, `feather` e atualizar inventário/loja/feed, estado de fazenda e refresh absoluto comum de carteira/XP; `effect.xpGained` serve apenas ao feedback. DTOs, rotas, eventos e códigos de erro existentes são preservados. Penas usam `1 pena` / `2 penas` nas mensagens e `do pavão` no roubo.

## Configuração provisória

`Animals` em configuração ASP.NET (override `Animals__NomeDaPropriedade`), validada com ValidateOnStart. Defaults em `Configuration/AnimalEnclosureOptions.cs`; parâmetros escolhidos com economy_designer para protótipo, balanceamento final permanece necessário.

- Cercado 1000 moedas, galinha 500; ambos nível 4; compra do cercado exige grade ampliada válida com nove lotes desbloqueados (e estados posteriores).
- Crescimento 4h, produção 12h, base 8 ovos, venda 20 moedas/ovo, coleta 25 XP, roubo 0 XP.
- Vaca: 1000 moedas/nível 5, crescimento 8h, produção 24h, base 8 leites, venda 40 moedas/unidade. Overrides `CowPriceCoins`, `CowMinLevel`, `CowGrowthSeconds`, `CowProductionSeconds`, `CowBaseYield`, `MilkSellPriceCoins`. Uma unidade por espécie; ciclos independentes, políticas antigas da galinha preservadas. Sem migration adicional.
- Ovelha: 1500 moedas/nível 6, crescimento 8h, produção 48h, base 6 lãs, venda 100 moedas/lã, coleta 40 XP. Overrides `SheepPriceCoins`, `SheepMinLevel`, `SheepGrowthSeconds`, `SheepProductionSeconds`, `SheepBaseYield`, `WoolSellPriceCoins`, `SheepCollectXp`, `SheepOwnerReserveAbsolute`, `SheepOwnerReserveFraction`, `SheepTheftAggregateLimit`, `SheepTheftPerVisitor`, `SheepHelpBonusLimit`, `SheepHelpIncrement`. Na política padrão, reserva 4, roubo agregado 2 e individual 1, ajuda +1 até 3 protegida. A prontidão é apresentada pela pelagem da própria ovelha; roubo parcial não a tosquia. Valores provisórios, sem migration adicional.
- Pavão: 2500 moedas/nível 8, crescimento 8h, produção 72h, base 6 penas, venda 150 moedas/pena, coleta 40 XP. Overrides `PeacockPriceCoins`, `PeacockMinLevel`, `PeacockGrowthSeconds`, `PeacockProductionSeconds`, `PeacockBaseYield`, `FeatherSellPriceCoins`, `PeacockCollectXp`, `PeacockOwnerReserveAbsolute`, `PeacockOwnerReserveFraction`, `PeacockTheftAggregateLimit`, `PeacockTheftPerVisitor`, `PeacockHelpBonusLimit`, `PeacockHelpIncrement`. Na política padrão, reserva 4, roubo agregado 2 e individual 1, ajuda +1 até 3 protegida. Sem expiração ou acúmulo offline. Prontidão usa `cycle.isReady`: leque aberto; coleta confirmada inicia sucessor e fecha o leque, roubo parcial mantém a prontidão. Sem migration adicional. Publicar com cliente compatível; após compras persistidas, backend antigo não reconhece a espécie e não deve receber rollback.
- Participação, reserva, bônus protegido e orçamento agregado de roubo são independentes por animal/ciclo. O mesmo visitante pode ajudar cada animal disponível uma vez no ciclo correspondente.
- Bônus por ajuda 1/teto 3 para galinha, vaca, ovelha e pavão, e teto 2 para o porco; recompensa visitante 2 moedas/5 XP por ajuda confirmada. Cinco animais elegíveis oferecem cinco ajudas ao mesmo visitante, totalizando 10 moedas e 25 XP, sem quota compartilhada com animais, fazendas ou plantações. Quando o teto do animal é atingido, novas ajudas ficam indisponíveis somente nesse animal até o próximo ciclo.
- Novos ciclos usam reserva base max(4,ceil(50%×base)): 4 no padrão de 8; roubo agregado 4 e individual 1. Cada visitante pode roubar uma unidade de cada animal por ciclo; um roubo não bloqueia outras espécies e o ciclo seguinte reabre a oportunidade. Todo bônus de ajuda pertence à coleta do dono e não entra na parcela roubável. Ciclos já abertos preservam seu snapshot anterior.
- Quando a produção está pronta, o dono vê os totais `baseYield`, `protectedHelpBonus` e `ownerCollectible`. Para visitantes, esses três campos permanecem no contrato com valor zero; apenas `remainingBase` é exposto. Esse restante inclui a reserva e não representa quantidade roubável: `canSteal` e sua razão continuam sendo a autoridade da interface, com revalidação transacional no POST.
- Política produtiva/recompensas/reserva/bônus é congelada na compra para o primeiro ciclo e na coleta para ciclos seguintes.

## Persistência e validação

Migration aditiva `20260913101836_AddAnimalEnclosure`: quatro tabelas, índices de unicidade, FKs e checks; três colunas opcionais em Notifications. Nenhuma concessão ou atualização de economia existente. Downgrade removeria progresso animal: rollback de aplicação deve preservar tabelas e usar binário compatível, sem executar Down sobre dados de jogadores.

Locks: User → amizade canônica quando social → Farm → Plots quando compra cercado → Enclosure → Animals → Cycles → Inventory → Items → completions. Farm serializa o agregado; User serializa as mutações da carteira e do XP do visitante. Cuidado vegetal também adquire Farm antes de Plot, evitando inversão com compras e FK do ciclo de cuidado. Recompensa, bônus, inventário, XP, participação e notificação são uma transação.

Cobertura em `Animal*Tests.cs`, `PeacockIntegrationTests.cs` e `FertilizerIntegrationTests.cs`: concorrência, compra/gate, maturidade histórica, policy snapshot, conservação, bônus parcial, ajuda independente nas cinco espécies e por novo ciclo, idempotência/replay sem amizade, venda do último estoque, proteção de nomes, overflow/rollback, ordem de locks e upgrade preservando dados. Execução PostgreSQL requer `CROP_CARE_TEST_CONNECTION` apontando exclusivamente para banco isolado; skips não comprovam essas regras.
