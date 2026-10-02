# Cercado de animais — auditoria antes da implementação

> **Registro histórico; não usar os achados abaixo como especificação vigente.** Em 12/set, após respostas explícitas do usuário, o [ExecPlan](2026-09-09-chicken-coop.md) foi atualizado: F1 resolvido no documento com nove lotes/primeira expansão; F2 corrida resolvida com falha atômica da ação perdedora (replay de sucesso continua sucesso); F3 fingerprint de venda especificado; F4 estratégia de refresh autoritativo/guards/XP sem soma cega especificada; F5 DTOs/erros/revisões/eventos/consumidores detalhados; F6 layout ampliado para aquisição com nove e compras posteriores; F7 substituído pela decisão de feature sempre disponível no release, sem flag própria. Resolução documental não é validação de implementação/runtime. Preserva-se a auditoria original abaixo como evidência do estado anterior; números de linha citados nela referem-se ao snapshot de hash registrado, não à versão atual do plano. Resta avaliar política após teto do bônus do dono e recomendações de produto no plano, sem reabrir a falha de quota já decidida. Nenhum código foi corrigido nesta revisão.

Data: 2026-09-12. Revisão somente leitura de aplicação/plano; este relatório é a única escrita autorizada nesta rodada. Skill aplicada: `C:/Users/vinic/Colheita/.codex/skills/plan-feature/SKILL.md`. Não substitui nem corrige o ExecPlan.

## Summary

**NEEDS DECISION.** Direção arquitetural adequada, mas a compra tem duas condições incompatíveis no documento e a ajuda pode perder remuneração durante uma corrida sem intenção explícita de ajudar gratuitamente. Esses pontos exigem fechamento antes das respectivas implementações. Há também especificações técnicas a completar; números de balanceamento não são necessários para concluir esta revisão.

Nenhuma implementação de animais foi localizada. Não há evidência de regressão de aplicação causada pelos dois commits recentes: eles alteram skills e imagens-fonte, não API/React/Phaser. Os achados abaixo distinguem inconsistências do plano de riscos de integração com código preexistente.

## Current State

| Fonte | Evidência confirmada |
| --- | --- |
| Checkout principal `C:/Users/vinic/Colheita` | HEAD `e17cf02`, status limpo; staged, unstaged e untracked vazios. `git diff --check` e `git diff --cached --check` sem problemas. |
| Commit `e17cf02`, 12/set | Adiciona sete skills e referências/instruções. Apesar do título mencionar crops/animals/economy, o diff não implementa esses sistemas. |
| Commit `2f04785`, 11/set | Reorganiza imagens-fonte para `imgs/tiles`. Não altera imports dos assets usados em `FarmAndFriends.Frontend/src/game/assets`. |
| Worktree `C:/Users/vinic/.codex/worktrees/9cfb/Colheita` | HEAD `5e837e8`, dois commits atrás. Antes deste relatório, somente `docs/plans/2026-09-09-chicken-coop.md` não rastreado; staged/unstaged rastreados vazios. |
| Plano mais recente localizado | [Cercado de animais](2026-09-09-chicken-coop.md), neste worktree, não versionado. LastWriteTime observado: 11/set 01:06:23. SHA256 `9875ED13FAC9647157118D69EA12925EDBE8AF25648B591FD61176C7BDD08683`. O título interno/data não bastam para determinar versão. |
| Busca de sucessores | Nenhum plano de animais encontrado em `docs` do principal ou outro worktree pesquisado; histórico Git disponível de `docs/plans` também não trouxe um. Este é o documento canônico localizado para a auditoria, não uma versão publicada no principal. |

Comparação `git diff 5e837e8..HEAD -- FarmAndFriends.Api FarmAndFriends.Frontend FarmAndFriends.Api.Tests docs AGENTS.md` no principal: vazia. A base de código inspecionada continua igual nesses caminhos. A modificação posterior no plano não está em commit, por isso não há autoria/justificativa Git para reconstruí-la; sua redação explícita mais recente deve ser preservada, não revertida ao histórico da conversa.

Foram lidos AGENTS raiz/API/frontend/Phaser e a skill atual; inspecionados aquisição/expansão, care/theft/economia, DbContext/comprovantes, contratos, estado de fazenda/carteira/XP, loja, bridge/câmera/prazos, testes existentes e rollout. Architecture reviewer revisou backend/persistência; social designer revisou a pendência específica de ajuda. Não se repetiram quatro pareceres de produto completos.

## Invariants

Preservados no desenho: um cercado, uma unidade por tipo/fazenda, MVP galinha/ovos, reserva física para galinha/vaca/ovelha/porco sem implementar essas três, filhote permanente após maturidade, ciclo por espécie, ausência sem expiração/acúmulo, roubo limitado à base com reserva significativa, bônus integralmente protegido, nomes históricos, backend autoritativo e eventos sociais separados. Não forçar animais em Plot/Seed/TheftLog.

Locks propostos, quotas sob User, produção sob Cycle, transação abrangendo carteira/XP/participação/notificação e migration incremental são coerentes. Isso é avaliação do projeto, não comprovação por execução.

## Approved Decisions

Mantêm-se as decisões explícitas listadas no plano, inclusive galinha/ovos como único catálogo MVP e dimensionamento futuro de quatro tipos. **A linha 68 agora registra acesso após a primeira expansão/primeiros nove lotes adquiridos mais nível mínimo.** Essa redação posterior difere do baseline de expansão total da conversa; não foi classificada como bug de produto nem revertida.

Produtos de vaca/ovelha/porco continuam indefinidos. Não há autorização de implementação nesta tarefa. `Prototype` é recomendação de produto, não autorização operacional.

## Pending Decisions

1. Consolidar o requisito de compra conforme a redação mais recente dos nove lotes, eliminando as referências contraditórias a Complete/expansão total. Confirmar precedência com o responsável apenas se a origem da edição não for conhecida; não perguntar novamente sobre todas as decisões antigas.
2. Ajuda sem recompensa é permitida? Se sim, qual intenção explícita permite esse caso e o que acontece quando a quota acaba entre exibição e comando? Recomenda-se não consumir participação em uma ação apresentada como remunerada que não possa pagar.
3. Fechar semântica de janela global/por par, contabilização e pagamento integral/parcial antes da fase de ajuda. Recomendar pacote integral e só pagamentos confirmados consumindo quota. Durações e tetos numéricos ficam para balanceamento.
4. Primeiro ciclo por MaturesAt, moeda de aquisição e limites individuais permanecem recomendações do plano: registrar aceite antes das respectivas fases, sem inventar números.

## Impact by Area

| Área | Resultado da auditoria |
| --- | --- |
| DOMAIN / AUTHORIZATION | Compra contraditória; regra deve reconhecer estado persistido válido e não ausência de oferta. Ajuda tem semântica pendente. |
| DATABASE | Modelo próprio e unicidades adequados; concretizar DDL/índices/constraints e fingerprint de venda antes do código correspondente. |
| API | Rotas propostas existem só no plano; DTOs/erros/consumidores ainda precisam de especificação fechada. |
| FRONTEND_STATE | useFarm protege GETs antigos, mas useInventory não; XP aditivo não deduplica operações. Estratégia para recompensa/replay deve ser explícita. |
| FRONTEND_UI | Nomes por ciclo e alvo inequívoco corretos; ajuda gratuita não pode ser inferida de um clique remunerado. |
| PHASER_WORLD | Bounds atuais centrados nos lotes. Validar aquisição na primeira expansão e continuidade até todos desbloqueados, além de quatro volumes futuros. |
| REACT_PHASER_INTEGRATION | Bridge reutilizável; faltam categorias/gatilhos/cleanup por evento e regra precisa de revisão/ordem de snapshots. |
| TESTING | Cenários amplos no plano; acrescentar casos dos achados e observar resultados persistidos, não apenas status HTTP. |
| OBSERVABILITY | Medir por operationId/ciclo/resultado efetivo, origem de XP/moedas, quota esgotada e replays, sem tokens/segredos. |
| DOCUMENTATION | Plano não versionado e parcialmente contraditório; relatório separado preserva fonte. Atualizar plano depois das decisões, sob autorização própria. |

## Contracts

### F3 — MEDIUM: fingerprint da venda incompleto (antes da fase de venda)

Evidência: [plano:141](2026-09-09-chicken-coop.md:141), [rota:163](2026-09-09-chicken-coop.md:163), [testes:244](2026-09-09-chicken-coop.md:244). Fingerprint enumera ação/fazenda/animal/tipo/ciclo, mas venda recebe itemId/quantity, que não aparecem explicitamente nos conflitos testados. A venda atual em [ShopController.cs:243](C:/Users/vinic/Colheita/FarmAndFriends.Api/Controllers/ShopController.cs:243) não oferece idempotência pronta para copiar.

Impacto: seguir apenas a enumeração pode replayar uma venda de quantidade/produto diferentes. Não é prova de bug implementado; é omissão numa fronteira de integridade. Recomendar tabela de fingerprint por comando; venda inclui itemId e quantity canônicos. Testar replay depois de estoque zerado, mesma chave com quantidade/produto diferentes e duas vendas concorrentes do último estoque.

### F5 — MEDIUM: fechar integração de contratos antes de dividir implementação

Evidência: [plano:167](2026-09-09-chicken-coop.md:167), [171](2026-09-09-chicken-coop.md:171), [tabela de eventos:183](2026-09-09-chicken-coop.md:183). Há descrição dos campos e resultado histórico versus atual, mas falta definir incremento/escopo de revision, resposta atual de cada comando e mapeamento HTTP por erro. A tabela de eventos não explicita por linha categoria semântica, gatilho e responsável por cleanup exigidos pela skill atual.

Recomendação técnica: especificar DTOs mínimos, nullabilidade/visibilidade owner/visitor, revisão incrementada em mutações e normalização, tratamento de estado derivado por tempo, erro→HTTP, e uma matriz emissor/receptor/payload/categoria/gatilho/cleanup. Estado projetado por relógio não deve ser descartado apenas porque revision persistida não mudou. Não criar infra genérica para isso.

Consumidores a nomear: FarmResponse/Farm mapper, tipos Farm/Inventory, funções de API, useFarm/FarmProvider e patch de animal/cache da fazenda própria, UserProvider, useInventory, ShopPanel/ShopSellTab/useShop, InventoryPanel, Game/painel de animais, notificationFeed/DTOs e FarmScene. Comprar/vender ovos não pode depender de catálogo de sementes ou Pests.Enabled.

## Persistence

### F1 — HIGH: regra de compra incompatível com a decisão recente (antes da aquisição)

Evidência: [plano:68](2026-09-09-chicken-coop.md:68) versus [97](2026-09-09-chicken-coop.md:97), [218](2026-09-09-chicken-coop.md:218), [233](2026-09-09-chicken-coop.md:233). [FarmLayoutRules.cs:76](C:/Users/vinic/Colheita/FarmAndFriends.Api/Domain/Rules/FarmLayoutRules.cs:76) retorna Complete só para **28 lotes desbloqueados**. [LandExpansionService.cs:257](C:/Users/vinic/Colheita/FarmAndFriends.Api/Domain/Services/LandExpansionService.cs:257) compra o nono e cria 19 bloqueados.

Impacto: exigir Complete bloqueia acesso por mais 19 compras em relação à linha 68. A frase da linha 97 que associa Complete aos nove está tecnicamente incorreta. Recomendação, preservando alteração recente: predicado no domínio de layout suportado e ampliado com primeiros nove desbloqueados (incluindo estados posteriores válidos), sob locks existentes; rejeitar layout inválido e não usar landOffer=null. Harmonizar oferta/progresso, escopo, fases e testes. A revisão não altera esse código/plano.

Manter User com FOR NO KEY UPDATE existente; não introduzir lock tardio no dono em ajuda. Unicidades filtradas por ação permitem ajuda e roubo pelo mesmo ator. Migration aditiva deve preservar enums/dados existentes, relações farm/enclosure/animal e impedir ciclo aberto duplicado. DDL concreta ainda será trabalho autorizado futuro.

## Implementation Sequence

1. Harmonizar gate recente e aprovar semântica de ajuda/remuneração; não escolher essas regras dentro do código.
2. Fechar contratos, fingerprints, revisão/replay, consumos de estado e teste de layout de aquisição/expansão; incorporar os ajustes ao plano em tarefa autorizada.
3. Só então adquirir cercado/galinha, ciclo/coleta/venda, ajuda e roubo em fatias do plano, com contratos estabilizados antes de paralelizar arquivos.
4. Antes de ativar: balanceamento, configuração validada, flags/normalização/replay, migration/testes reais, assets originais e revisão visual.

Nenhuma fase de implementação começou nesta auditoria.

## Test Strategy

Não foram executados build, testes de aplicação, migrations ou testes visuais/runtime. Auditoria estática de código, testes e Git; não há comprovação de locks sob concorrência nesta rodada. [CropCareConcurrencyTests.cs:23](C:/Users/vinic/Colheita/FarmAndFriends.Api.Tests/Integration/CropCareConcurrencyTests.cs:23) mostra dependência de configuração PostgreSQL; skips não equivalem a sucesso funcional.

Acrescentar ao plano:
- Compra com grade expandida e nove desbloqueados, depois dez e 28; inválido/flag desligada; corrida com compra do nono. Não reduzir essa matriz a Complete.
- Recompensa exibida → outra ajuda consome última quota → ação original rejeitada sem participação/bônus/XP/moedas/notificação, se adotada recomendação. Ajuda gratuita só com intenção aprovada; fingerprint cobre essa intenção.
- Janela/par e pacote integral, ações simultâneas em fazendas diferentes, replays após janela/ciclo e estoque zerado de venda.
- GET inventário antigo atrasado após crédito, logout/login em outra conta, replay depois de refresh que já contém XP e duas operações distintas fora de ordem.
- Normalização de maturidade/readyAt sem alteração de revisão persistida, aquisição que adiciona cercado sem mudar topologia de lotes, compra de terra que reinicia cena após cercado existente.
- Flag desligada antes/depois da maturidade, GET, venda, replay e reativação tardia sem ciclos extras.

## Risks

### F2 — HIGH: ajuda remunerada pode virar gratuita e consumir participação (decisão antes da ajuda)

Evidência: [plano:65](2026-09-09-chicken-coop.md:65), [82](2026-09-09-chicken-coop.md:82), [130](2026-09-09-chicken-coop.md:130), [131](2026-09-09-chicken-coop.md:131), [161](2026-09-09-chicken-coop.md:161) e [171](2026-09-09-chicken-coop.md:171). O plano permite zero após corrida e consumo definitivo, enquanto chama ajuda gratuita de voluntária. O request só tem expectedCycleId.

Impacto: mostrar a perda depois da ação não registra a escolha de ajudar sem remuneração. Social designer recomenda rejeitar sem efeitos se a ação remunerada não puder pagar; se ajuda gratuita for aprovada, disponibilizar intenção distinta explícita, parte do fingerprint, com nova operação. Escopo/janela/contagem e pagamento integral/parcial são regras anteriores ao código, não apenas números a acertar antes da ativação.

### F4 — MEDIUM: invalidar inventário e somar XP não bastam para consistência (antes de integrar recompensas)

Evidência: [plano:193](2026-09-09-chicken-coop.md:193) promete atualização/replay sem duplicação. [useInventory.ts:9](C:/Users/vinic/Colheita/FarmAndFriends.Frontend/src/inventory/useInventory.ts:9) aplica qualquer GET concluído, sem request-id/sessão. [UserProvider.tsx:63](C:/Users/vinic/Colheita/FarmAndFriends.Frontend/src/user/UserProvider.tsx:63) addXp soma delta sem operationId. [useFarm.ts:129](C:/Users/vinic/Colheita/FarmAndFriends.Frontend/src/farm/useFarm.ts:129) invalida GETs ao patch de lote, mas isso não se aplica automaticamente à carteira ou a patch de animal.

Impacto concreto de integração: refresh antigo termina depois do novo e mostra saldo anterior; dedupe só em memória pode somar XP já presente num refresh anterior ao replay. São limitações preexistentes relevantes, não regressões dos commits recentes. Recomendar guarda de sessão/ordem para inventário e estratégia explícita de XP atual autoritativo em replay/resultado incerto (ou mecanismo equivalente demonstrado). Evitar soma cega; mapear patch/cache próprio de animal. Não corrigir unrelated state nesta auditoria.

### F6 — MEDIUM: prova visual deve acompanhar gate novo (antes de fechar assets/layout)

Evidência: [plano:205](2026-09-09-chicken-coop.md:205) ainda parte de compra com expansão completa. [FarmScene.ts:397](C:/Users/vinic/Colheita/FarmAndFriends.Frontend/src/game/scenes/FarmScene.ts:397) enquadra lotes no retrato; [1368](C:/Users/vinic/Colheita/FarmAndFriends.Frontend/src/game/scenes/FarmScene.ts:1368) reinicia por mudança de topologia/input dos lotes, não por surgimento de cercado.

Recomendar testar chegada do cercado com nove desbloqueados/19 bloqueados, placa de oferta e compras seguintes até 28, além do dimensionamento de quatro volumes futuros. Adicionar/criar cercado e recalcular bounds quando só animalEnclosure muda. Garantir rebuild de terreno não perca animais/listeners nem deixe modal bloqueando cena. Não basta testar fazenda final; nenhum layout foi observado funcionando nesta auditoria.

Refinamentos não bloqueantes: nome do arquivo ainda chicken-coop pode ser mantido; conversão formal do plano para as seções da skill pode ocorrer na próxima revisão, sem confundir estilo documental com bug de domínio. Telemetria e texto exato de feedback podem ser detalhados sem números econômicos agora.

## Rollout and Rollback

### F7 — MEDIUM: flag versus GET que persiste ciclo (antes de ativar)

Evidência: [plano:103](2026-09-09-chicken-coop.md:103) permite normalização persistente em GET; [258](2026-09-09-chicken-coop.md:258) desativa mutações e mantém leitura. Falta definir o comportamento do animal que amadurece desativado, incluindo replay.

Recomendação: matriz explícita ligada/desligada para compra, GET/normalização, coleta, ajuda, roubo, venda e replay; preservar promessa temporal sem acúmulo. Definir rejeição versus leitura derivada/materialização e efeitos permitidos. Seguir ValidateOnStart existente em Program para parâmetros, conferir JSON/ambiente/binding e nunca dar autorização por clock cliente. Não é necessário escolher valores nesta auditoria.

Migration aditiva primeiro; ativação só após PostgreSQL real e QA visual. Desativar preservando propriedade/ciclos; não remover tabelas com progresso. Não foi aplicado rollback ou modificado ambiente.

## Acceptance Criteria

Para declarar o plano pronto: gate com interpretação única e testes nos estados corretos; comportamento da ajuda/pacote/quotas aprovado; fingerprints de todos os comandos; respostas/revisões/replay e eventos com donos claros; caminho de carteira/XP sem respostas antigas; prova de layout planejada desde aquisição, não só final. Balanceamento pode permanecer parametrizado até ativação, sem deixar regras de contrato indefinidas.

Para ativação futura: testes PostgreSQL sem skips relevantes, conservação base+bônus, reserva inviolável, transações e retries, compatibilidade das plantas, inventário/XP sem F5, modal/lifecycle/câmera e somente galinha/ovos funcionais. Este relatório não afirma que tais critérios passaram.

## Responsible Agents

- Principal: comparação de fontes, skill/AGENTS, frontend/estado/eventos/prazos/loja/câmera/testes e consolidação; somente este relatório.
- architecture_reviewer: revisão independente de domínio/API/transações/persistência/config/rollout, achados F1/F3/F7.
- social_designer: revisão limitada à lacuna de consentimento/quota remunerada, achado F2; sem reabrir MVP ou bônus protegido.
- Em trabalho futuro autorizado: technical_planner consolida contratos; backend/frontend/Phaser implementam suas áreas; economy_designer calibra valores após semânticas; QA/arquitetura/gameplay verificam resultados.

## Status

**NEEDS DECISION**, com ajustes técnicos obrigatórios antes das fases afetadas. Não BLOCKED por ferramenta, nem investigação de implementação incompleta disfarçada de aprovação: a auditoria estática solicitada foi concluída, mas execução/runtime/arte não foram verificados. Não há autorização de código/commit/migration/instalação nesta tarefa.

Nenhum plano principal foi sobrescrito; o ExecPlan localizado também permaneceu intacto. Nenhuma memória persistente foi atualizada. Relatório é snapshot dos caminhos/hashes acima; edições posteriores exigem revalidar os trechos afetados.
