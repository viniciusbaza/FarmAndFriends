# Cercado de animais — ExecPlan e decisão consolidada

Data inicial: 2026-09-09. Revisão: 2026-09-12, após avaliação do usuário.
Status: implementação AUTORIZADA pelo usuário e em andamento. Balanceamento final e validação permanecem necessários antes do release.
Veredito recomendado: **Prototype**.
Origem: tarefa `01a08393-cc86-7091-9cfe-a240311ede3d`.

## Autorização e execução — atualização posterior à revisão

O usuário autorizou explicitamente iniciar a implementação seguindo este planejamento e aprovou integralmente a direção visual de `imgs/animal-enclosure/2026-09-12-v1`. Essa autorização substitui todas as menções históricas abaixo a implementação pendente. Inclui backend, migration incremental, React, Phaser, assets originais, testes e documentação. Não inclui commit, publicação, deploy ou reset de dados reais. Parâmetros numéricos provisórios são permitidos para execução e testes; balanceamento final permanece futuro.

Estado inicial revalidado: worktree em 5e837e8; principal limpo em e17cf02. Diferenças entre commits restritas a skills e reorganização de imagens-fonte, sem alterações de aplicação a incorporar. Skills atuais foram lidas diretamente do principal. Planos e propostas visuais já estavam não rastreados. Execução preserva essas entregas e divide ownership entre backend/API.Tests, frontend fora de game, Phaser e assets/documentação sob a tarefa principal.

## Current State — escopo da revisão e evidência

Esta versão substitui integralmente a proposta anterior de galinheiro com várias galinhas. O nome de trabalho é **Cercado de animais**; o caminho histórico deste documento é mantido para preservar referências. Não existe um segundo plano concorrente. Foram recebidas as revisões de game_director, social_designer, economy_designer e technical_planner antes desta consolidação, incluindo as últimas decisões explícitas do usuário.

Somente documentação/análise foi autorizada. Nenhum código de aplicação, migration ou asset foi criado/alterado; não foram executados testes de aplicação, instalação de dependências, commits ou publicação. O documento já estava não rastreado no início desta revisão; demais alterações do projeto devem ser preservadas.

Foram abertas e inspecionadas as referências fornecidas:
- `C:/Users/vinic/AppData/Local/Temp/codex-clipboard-66875720-d375-4440-b5bb-216737085bde.png`.
- `C:/Users/vinic/AppData/Local/Temp/codex-clipboard-2ccc7f18-e4cf-4243-b31b-d09f768dd19b.png`.

As imagens do Colheita Feliz mostram um cercado aberto ao lado dos lotes, espécies compartilhando o espaço e objetos de produção próximos, como ninho e balde. São referências de organização visual; não autorizam copiar assets, interface ou identidade artística. Imagens estáticas não comprovam animações, mas o requisito do usuário é manter animais visíveis e animados. A presença de um animal na referência não aprova sua espécie no catálogo, seu produto ou qualquer mecânica: não presumir carne para porco.

Referências de código abaixo são relativas à raiz do checkout; prefixos API e frontend identificam `FarmAndFriends.Api/` e `FarmAndFriends.Frontend/`. Nomes futuros são propostas.

| Área | Estado real verificado e consequência |
| --- | --- |
| Fazenda | API `Controllers/FarmsController.cs`, `Contracts/Farm/FarmResponse.cs`, `Domain/Entities/Farm.cs`: projeção de lotes e autorização de visita; não há instalação/animal. Adição deve ser compatível. |
| Expansão | API `Domain/Rules/FarmLayoutRules.cs` tem `Evaluate(plots)` e status `Complete`. `LandExpansionService.GetOffer()` pode retornar null por conclusão, flag desligada ou layout inválido. **Ausência de landOffer não comprova expansão concluída.** |
| Ajuda | API `Domain/Services/CropCareService.cs`: TimeProvider, transação, lock de amizade, comprovantes, moedas/XP e replay. Agrupa ajudantes por usuário, apresenta nomes ao dono com lista resumida e captura nome histórico. Reutilizar esses padrões, não seus valores ou cooldowns. |
| Limites de cuidado | `VisitorFarmCareCycle` limita recompensa por visitante/fazenda; não equivale ao orçamento global de ajuda animal entre fazendas e espécies. Cuidado de plantas não aumenta produção. |
| UI de nomes | Frontend `src/components/PlotModal.tsx` mostra “Regada por” com nomes e “e +N”. Reusar linguagem visual, com acesso à lista completa de ajudantes do ciclo selecionado. |
| Roubo | API `TheftService` preserva apenas o último item; `TheftController` usa quota visitante/fazenda/dia. Não satisfaz reserva significativa/teto agregado por ciclo de espécie. `TheftLog` é específico de Plot/Seed. |
| Carteira e venda | API `Inventory`, `InventoryItem`, `ItemType`, `ShopController`: carteira existente; venda de Crop resolve Seed.CropId. Produtos animais precisam de catálogo próprio sem sementes fictícias. |
| Transações | API `Infrastructure/Data/PlayerEconomyLock.cs`: User → Friendship → Farm → Plots → Inventory → itens → comprovantes. Estender ordem, sem inversões. |
| Feed | API `Notification`, `NotificationsController` e frontend `notificationFeed.ts`: janela de sete dias. Nomes/participação do ciclo não dependem do feed. Ajuda/roubo permanecem tipos distintos. |
| Estado e cena | Frontend `useFarm.ts`, `Game.tsx`, `usePhaserGame.ts`, `FarmScene.ts`: snapshot compartilhado, proteção contra respostas antigas, polling/refresh por prazo, eventos farm:change/farm:sync/ui:modal. |
| Layout | `farmLayout.ts`: expansão canônica 7×4 com deslocamento visual; overview em retrato usa bounds dos lotes. Fundo `farm-clearing.png` possui vegetação/cercas, não garante espaço livre para cercado. |
| Banco/testes | `AppDbContext`, migration recente `20260904022050_AddUserAvatar`, testes de care, loja, expansão e múltiplas colheitas. Migration futura incremental, sem reset/squash. Testes existentes não cobrem esta feature. |

## Summary — decisão de produto

**Problema/oportunidade:** tornar conquistas permanentes visíveis e oferecer descoberta social além da prontidão das plantas.

**Por que interessa:** adquirir uma espécie traz um animal real para o cercado compartilhado. Cada filhote cresce uma vez; a diversidade adquirida mostra progresso. O avanço não vem de comprar várias galinhas, recolorações produtivas ou variantes equivalentes.

**Por que voltar:** acompanhar crescimento individual, encontrar o produto de cada espécie esperando coleta e descobrir animais dos amigos. Não há fome, manutenção ou perda por expiração pressionando o retorno.

**Interação social e resultados:** o visitante escolhe uma espécie em produção, ajuda e recebe a recompensa autorizada; o dono recebe bônus protegido e vê os nomes dos ajudantes daquele ciclo. Quando pronta, parte da produção base pode ser roubada dentro de teto agregado. Ajuda anterior e roubo podem coexistir, sempre apresentados separadamente. O dono mantém recompensa significativa; o visitante encontra uma ação positiva mesmo sem roubo.

**Frustração e abuso:** ciclos distintos podem confundir se o painel não identificar espécie/produto. Recompensas sem teto global permitem obter moedas/XP repetidamente alternando espécies/fazendas. O acesso começa na primeira expansão, após adquirir o 9º lote e atingir o nível mínimo. Medir compreensão desse marco. Coleção finita não justifica contornar unicidade nem projetar todo catálogo futuro agora.

**Economia:** animais permanentes geram renda renovável após custo inicial único. Cada espécie tem produção base própria, sem multiplicador por quantidade de unidades. Plantar deve continuar relevante. Bônus do dono, moedas/XP do visitante e reserva contra roubo são controles separados.

**MVP definido pelo usuário: apenas galinha e ovos.** Adquirir um cercado após adquirir o 9º lote/primeira expansão e atingir o nível mínimo, comprar uma galinha filhote, acompanhar maturidade, coletar/vender ovos, ajudar com recompensa e nomes e roubar com proteção agregada, mantendo tudo na mesma cena. Reservar espaço físico para futura coexistência de **galinha, vaca, ovelha e porco**, uma unidade por tipo. Essa reserva visual não autoriza implementar as três espécies adicionais nem define seus produtos. A instalação/modelo permanece compartilhada, sem criar framework genérico de animais.

**Fora da direção atual:** reprodução/nascimentos, morte, expiração, fome, manutenção/ração obrigatória, compra de duplicatas ou variantes que contornem unicidade, presentes, materiais/projetos cooperativos, transferência/venda de animais e edição livre de instalações. Cão com proteção localizada é horizonte, não parte deste recorte. Não deduzir catálogo/produtos futuros a partir das imagens.

**Escopo técnico:** significativo e transversal: agregado próprio, aquisição condicionada à expansão, ciclos independentes, economia/recompensas, contratos, estado React, mundo Phaser, arte e testes PostgreSQL. Veredito único: **Prototype**, aguardando aprovação de implementação.

## Approved Decisions — decisões explícitas vigentes

- **MVP somente galinha/ovos.** Dimensionar o cercado para galinha, vaca, ovelha e porco coexistirem futuramente, uma unidade por tipo; produtos futuros continuam indefinidos e espécies adicionais não estão autorizadas para implementação.
- Um único cercado aberto compartilhado na mesma cena dos lotes; espécies podem coexistir. Animais reais visíveis/animados, com produtos apresentados próximos de seus alvos.
- **Uma unidade de cada tipo/espécie por fazenda**, permanente. Não usar variantes ou novas instalações para obter duplicatas. Não há compra em quantidade nem lotação baseada em várias galinhas.
- Animais são comprados na loja como filhotes, crescem individualmente uma vez e permanecem adultos. Sem reprodução, morte, manutenção obrigatória ou punição por ausência.
- **Um ciclo produtivo por espécie**, independente das demais. Como há uma unidade de cada tipo, o animal identifica a produção daquela espécie. Não sincronizar ciclos entre espécies.
- Produção pronta espera coleta, sem expirar ou acumular ciclos ilimitados. Coleta reinicia apenas o ciclo da espécie escolhida; roubo não reinicia.
- Ajuda beneficia **também o visitante** com recompensa, além do bônus produtivo do dono. Moedas comuns e XP configuráveis são a proposta de composição, sem valores definitivos.
- Mostrar **nomes dos visitantes que ajudaram**, não somente contador. Ajuda/participação deve referenciar o ciclo da espécie alvo.
- Bônus de produção gerado pela ajuda é **exclusivo e protegido para o dono**; não pode ser roubado. A recompensa do visitante é separada.
- **Ao atingir o teto de bônus do dono, ajuda fica indisponível para novos visitantes naquele ciclo.** Ajuda exige simultaneamente espaço no bônus do dono e quota remunerada do visitante. A intenção é incentivar visitas a outras fazendas; não há recompensa por adicionar amizade nem obrigação de visitar/retribuir.
- Compra do cercado é liberada **após adquirir o 9º lote, junto com a primeira expansão da fazenda**, respeitando também o nível mínimo a balancear. Os novos lotes da grade ampliada podem continuar bloqueados. Espaço é comprado separadamente, não concedido nem incluído na compra do lote.
- Roubo somente de produtos, nunca animais; teto agregado de todos os visitantes por ciclo produtivo e reserva significativa de produção base ao dono.
- Lista de amigos não revela prontidão, direta ou indiretamente; descoberta acontece na visita. Ajuda e roubo são eventos separados.
- Valores de preço, duração, crescimento, base, XP, bônus, quotas e reserva ficam para balanceamento. **24 horas não foi aprovado**, tampouco horário matinal fixo.

## Pending Decisions — recomendações para avaliação

As três dúvidas anteriores — unidade de ciclo, expansão necessária e proteção do bônus — estão resolvidas acima e não são pendências.

| Tema | Recomendação para avaliação antes da implementação; valores antes do release |
| --- | --- |
| Primeiro ciclo | Iniciar em MaturesAt de cada animal, com política inicial persistida na compra. Próximos ciclos começam na coleta individual. |
| Ajuda | Uma participação por amigo/ciclo de espécie, enquanto produz; bônus produtivo limitado por ciclo, sem aceleração. |
| Recompensa visitante | Moedas comuns e XP parametrizados; limite global de ajuda animal por visitante entre todas espécies/fazendas, mais limite por par visitante–dono. |
| Roubo individual | Um roubo bem-sucedido por visitante/ciclo, além do teto agregado; intervalo por par entre ciclos a calibrar/avaliar. |
| Aquisição/venda | Recomendar moeda comum para cercado/animais e venda de produtos sem XP. Compra de animal é unitária, sem quantity. |
| Localização/arte | Testar lateral/superior direita desde a primeira expansão, passando pelas compras posteriores até a fazenda totalmente desbloqueada; posição não aprovada por referência estática. Arte original. |
| Valores e lançamento | Semântica de quotas especificada abaixo; valores de balanceamento antes do release; catálogo do MVP já definido como galinha/ovos. Aprovação de implementação ainda pendente; não bloqueiam conclusão deste plano. |

## Invariants — domínio, aquisição e tempo

Nomes novos propostos: `AnimalEnclosure`, `FarmAnimal`, `AnimalProductionCycle`, `AnimalActionCompletion`. Não modelar animais em Plot/Seed/TheftLog.

- AnimalEnclosure: id, FarmId, aquisição, slot de apresentação, revisão. Um por fazenda.
- FarmAnimal: id, FarmId, AnimalEnclosureId, AnimalTypeId canônico, PurchasedAt, MaturesAt e snapshot inicial da política produtiva. Unicidade `(FarmId, AnimalTypeId)`, também para filhotes. Relação deve garantir fazenda do animal igual à da instalação.
- AnimalProductionCycle: id, FarmAnimalId, StartsAt, ReadyAt, CollectedAt, BaseYield, ProtectedHelpBonus, StolenBaseTotal, reserva/teto/política snapshot. Um ciclo aberto por animal. Não há contagem de adultas participantes ou capacidade artificial.
- AnimalActionCompletion: ator, ação, alvo/ciclo, UUID idempotente, fingerprint, instante, nome capturado, bônus efetivo, moedas/XP efetivos e resultado histórico. Catálogo pequeno apenas com tipos aprovados; não adicionar tipos equivalentes para burlar unicidade.

**Compra do cercado:** JWT identifica dono; sob locks de User/Farm/Plots, exigir layout canônico ampliado válido, primeiros nove lotes desbloqueados, nível mínimo e saldo, além de inexistência de cercado. Propor predicado de domínio `HasReachedFirstExpansion`, reutilizando validação/definições de FarmLayoutRules: rejeitar Unsupported, exigir topologia ampliada e desbloqueio dos primeiros InitialPlotCount lotes. Aceitar também estados válidos posteriores. Não exigir status Complete: ele corresponde aos 28 lotes desbloqueados, não ao marco aprovado de nove. A compra do 9º lote já cria a grade ampliada na transação atual; a compra do cercado é outra operação e só pode ocorrer depois desse commit. Flag de expansão desligada ou landOffer null não concedem nem removem esse marco persistido. Comprar cercado não compra animal automaticamente.

**Compra de animal:** inferir fazenda/cercado próprios pelo JWT, validar tipo aprovado, requisitos do catálogo, saldo e ausência daquele tipo, criar uma unidade e debitar atomicamente. Nível/expansão são condições de aquisição; configuração futura não remove propriedade permanente. Duas compras concorrentes do mesmo tipo não geram dois animais.

**Ciclos propostos:** primeiro StartsAt = MaturesAt daquele animal. Compra persiste política inicial e prazo de crescimento para rebalanceamento não reescrever promessa anterior. Normalização autorizada sob locks materializa somente o primeiro ciclo devido, usando esse instante histórico, não a hora da visita. Ausência longa mostra uma produção pronta, sem ciclos offline extras. Cada coleta captura um único now, credita base restante+bônus protegido, fecha ciclo e inicia exatamente um próximo da mesma espécie com política vigente. Maturidade, compra, ajuda ou coleta de outra espécie não alteram esse ciclo.

Ajuda em `StartsAt <= now < ReadyAt`; coleta/roubo a partir de ReadyAt, sujeitos ao servidor. Adulto permanece após coleta. GET pode normalizar após autorização, como precedente das pragas, mas não conceder recompensa por leitura. Não precisa de scheduler para maturidade. Relógio cliente apenas apresenta e solicita refresh.

## Economia, ajuda e reserva por ciclo de espécie

Para cada ciclo da espécie s:

```text
B_s = base inteira da espécie, fixada no início (sem multiplicador de animais)
H_s = amigos distintos com ajuda válida naquele ciclo
A_s = min(tetoBonus_s, H_s × incrementoAjuda_s)
ajudaNovaElegivel exige A_s < tetoBonus_s e quota visitante para pacote inteiro
bonusAdded = min(incrementoAjuda_s, tetoBonus_s - A_s), somente se elegível
R_s = min(B_s, max(reservaAbsoluta_s, ceil(fracaoProtegida_s × B_s)))
T_s = min(tetoRouboAgregado_s, B_s - R_s)
0 <= S_s <= T_s, onde S_s soma TODOS os roubos do ciclo
saldoBase = B_s - S_s
coletaDono = saldoBase + A_s >= R_s + A_s
rouboConcedido <= min(limiteIndividualRestante, T_s-S_s, saldoBase-R_s)
```

A_s é integralmente protegido e nunca amplia T_s. A reserva significativa incide sobre a base mesmo se não houve ajuda. Produções pequenas podem ter T_s=0; não reduzir proteção para obrigar roubo. Conservação no fechamento: ovos/produtos creditados ao dono e visitantes somam B_s+A_s. Uma espécie coletada não zera bônus, nomes, orçamento ou datas de outra.

A ajuda aceita que completa o teto pode adicionar apenas o bônus restante e ainda paga o pacote integral configurado ao visitante. Depois de cheio, nenhuma nova ajuda incrementa H_s, nomes ou recompensas. O teto e incremento usam snapshot do ciclo; relógio, amizade refeita ou alteração de configuração não reabrem espaço. O orçamento de bônus só renova na coleta/novo ciclo da espécie. Validar incremento positivo e teto positivo no catálogo habilitado; valores serão balanceados.

Recompensa visitante é outra fonte: `coinsGained` e `xpGained`, distinta de `ownerBonusAdded`. Nenhuma das três grandezas deriva de cliques, animações ou quantidade de espécies do dono. Configurar valores e limites, sem copiar care vegetal.

**Proposta de proteção contra farming:**
- Uma ajuda por ator/ciclo produtivo, comprovada no banco; nova chave não permite outra participação.
- Janela móvel de recompensa de ajuda animal por ator, atravessando espécies e fazendas. Prever teto de ações e limites de moedas/XP se valores variarem por espécie, além de limite por par visitante–dono.
- Consultar/reservar orçamento sob lock do User; a última recompensa disputada simultaneamente em fazendas diferentes só é concedida dentro do teto. Completions recompensadas sustentam histórico/indexação.
- Coleta, nova espécie, mudança de dia no cliente ou remover/readicionar amizade não reiniciam quotas. Ajudas sem pagamento não prolongam/reiniciam janela.
- Quota visitante e teto de bônus dono são independentes. Regra confirmada pelo usuário: se outra operação consumir a última quota antes desta, retornar conflito sem participação, bônus, moedas, XP ou notificação; atualizar a tela. Não consumir o direito de ajudar nesse ciclo. Nenhuma ajuda remunerada vira gratuita automaticamente.
- Propor pagamento integral do pacote configurado de moedas/XP, sem redução parcial; quotas móveis global e por par devem permitir o pacote inteiro. Somente pagamentos confirmados entram na contagem, no intervalo UTC `(now - duraçãoDaJanela, now]` de cada orçamento. As durações global e por par podem ser distintas, parametrizadas. Capturar now após obter os locks necessários. Falhas/replays não consomem orçamento. A rejeição sem efeitos foi confirmada; pacote integral e contabilização das janelas são especificações recomendadas para a revisão final.
- Não oferecer ajuda gratuita no MVP. Quando a quota visitante acabar, desabilitar ajuda e informar motivo/nextRewardAt autoritativo; prazo local só solicita refresh. Quando bônus do dono estiver cheio, desabilitar ajuda com OWNER_BONUS_LIMIT_REACHED, sem prazo de liberação local: depende da coleta e novo ciclo. Participação remunerada bem-sucedida continua única por ciclo.
- Sob locks de User e Cycle, revalidar ambos os limites antes de qualquer efeito. Na disputa pelo bônus restante, a primeira ajuda válida pode completar o teto; a perdedora retorna 409 OWNER_BONUS_LIMIT_REACHED sem moeda, XP, bônus, nome, participação, contador, quota consumida ou notificação. Replay de operação confirmada é reconhecido antes desses limites consumidos e continua sucesso, sem duplicação.
- Não unir silenciosamente quotas ao cuidado de plantas. Este limite é **global de ajuda animal**, não global de todas recompensas sociais. Simular renda combinada; alterar limites de plantas requer decisão adicional.

Parâmetros futuros: preços/níveis, crescimento e duração por espécie, base/produto/preço de venda, bônus, piso/fração protegidos, tetos de roubo, moedas/XP e quotas móveis. Durações positivas, quantidades inteiras, validação de arredondamento/overflow e combinações. Simular renda conservadora sem ajuda/com roubo máximo, frequência real de coleta e custo da expansão+instalação+animal. Comparar com plantas do mesmo estágio, sem pressupor coleta perfeita. Não inventar produtos/cotações futuros.

## Persistence — concorrência e migration planejada

Propostas `AnimalEnclosureOptions`, catálogo pequeno de tipos, regras puras e `AnimalEnclosureService` concentram domínio. Não criar framework genérico de espécies, tarefas ou eventos.

- Migration incremental futura adiciona modelos/FKs/índices/checks. Único cercado por FarmId, único animal por FarmId+tipo, único ciclo aberto por animal, única coleta por ciclo. Unicidades de ajuda/roubo filtradas por ação permitem ambos no mesmo ciclo/amigo.
- Chave única `(ActorUserId, IdempotencyKey)`. Fingerprints canônicos por comando definidos abaixo; incluem itemId e quantity na venda. Chave igual com conteúdo semântico diferente retorna conflito; conteúdo igual reproduz somente efeito histórico. Revalidar acesso atual antes de retornar projeções privadas frescas. Nunca exigir estoque/ciclo ainda disponível para reconhecer uma operação já concluída.
- Ordem proposta: User → Friendship canônica quando social → Farm → Plots ordenados quando necessários → Enclosure → Animal → Cycle → Inventory → InventoryItems ordenados → completions. Compra do cercado lê/trava lotes; ações animais não precisam travá-los. Não adquirir User do dono depois do animal; bônus permanece no ciclo até coleta.
- Revalidar amizade, propriedade, ciclo esperado, espaço no teto do bônus, quota remunerada e saldo após locks; reler idempotência antes de limites já consumidos por uma operação confirmada. O lock de ciclo serializa ajudas disputando bônus e ladrões disputando base; User serializa quota global do ajudante em fazendas diferentes.
- Transação única inclui quota/recompensa visitante, bônus dono, participação, XP, crédito/débito e notificação. Falha de overflow/crédito/commit não deixa efeitos parciais. Coins usam carteira atual, não novo ledger premium universal.
- Índice de consulta de recompensa por ator+instante e por par quando necessário. Completions mantêm nome histórico/identidade e participações além da janela do feed. Não excluir provas que preservam unicidade/replay.
- Normalização concorrente também respeita unicidade de ciclo; replays históricos não reexecutam comando. Limites preservados após amizade refeita.
- Tipos aditivos de Notification para ajuda/roubo animal, referências opcionais de animal/ciclo/completion, sem reutilizar TheftLog vegetal. Preservar valores antigos dos enums.
- Nenhuma migration é criada/executada nesta tarefa. Fazendas existentes continuam sem cercado/animais, sem concessão, reset ou squash.

## API proposta e contratos

Rotas/nomes são propostas a estabilizar. Todos os comandos exigem JWT e UUID Idempotency-Key; o cliente envia apenas alvo/intenção, nunca preço, usuário, recompensa, prazo ou prova de expansão.

| Rota | Entrada e resposta principais |
| --- | --- |
| GET /shop/animal-enclosure | Oferta: preço, nível mínimo/atual, firstExpansionReached, initialPlotsUnlocked, requiredInitialPlots e requisitos de nível autoritativos, propriedade e motivos de indisponibilidade |
| POST /shop/buy-animal-enclosure | Sem userId nem quantidade; compra única com validação autoritativa, débito e completion |
| GET /shop/animals | Tipos aprovados, preço, crescimento, requisitos e owned/canBuy por tipo; não ligado a Pests.Enabled |
| POST /shop/buy-animal | `{animalTypeId}`; compra UMA unidade na instalação própria, sem quantity |
| POST /farms/{farmId}/animals/{animalId}/collect | `{expectedCycleId}`; crédito do produto e novo ciclo somente desse animal |
| POST /farms/{farmId}/animals/{animalId}/help | `{expectedCycleId}`; ownerBonusAdded, coinsGained, xpGained, helperCount e participação |
| POST /farms/{farmId}/animals/{animalId}/steal | `{expectedCycleId}`; produto/quantidade, saldoBase e orçamento do ciclo |
| POST /shop/sell-animal-product | `{itemId, quantity}`; quantidade aqui é produto em estoque, não animal; crédito autoritativo |

Propor `ItemType.AnimalProduct=4` preservando valores existentes, catálogo de produtos aprovado no servidor e venda independente de Crop/Seed. Atualizar inventário/loja/serialização e consumidores juntos. No MVP, catálogo e operações permitem somente galinha/ovos; vaca, ovelha e porco não ganham entradas compráveis ou produtos por constarem no dimensionamento visual. Não inferir outros pares nem mecânica de carne.

FarmResponse ganha `animalState: AnimalStateEnvelope`, definido na seção Contracts; o envelope contém `enclosure: null | {id, slot, animals}`, revision e observedAt. Esta é a forma canônica, sem revision duplicada dentro da instalação. Cada animal apresenta id, animalTypeId, nome, purchasedAt/maturesAt, estágio autoritativo, ciclo e produto próprios, permissões e motivos. Não há canCollect/ciclo único para todo cercado.

Nomes: recomendar resumo no painel do dono `helpers: [{userId, displayNameAtHelp, helpedAt}]`, com total e consulta paginada por `GET /farms/{farmId}/animals/{animalId}/cycles/{cycleId}/helpers` quando necessário. Permitir ao dono acessar todos os nomes, não somente “e +N”; visitante vê própria participação, sem expor lista de terceiros por padrão. Usar id para unicidade, nome capturado para história. Visibilidade detalhada é proposta de UX; não transformar nomes em ranking. Coleta limpa a lista corrente apenas da espécie e preserva registros históricos.

Resposta distingue operationId/replayed, efeito histórico imutável e estado atual. Não reaplicar saldo/XP antigos de replay nem substituir ciclo novo por snapshot passado. Na ajuda remunerada, quota disputada que impeça pagamento integral retorna conflito sem efeito; não há sucesso com remuneração reduzida. A interface atualiza elegibilidade e explica a indisponibilidade.

ProblemDetails.code para recurso/tipo inválido, falta de amizade/propriedade, primeira expansão ainda não atingida, layout não suportado, nível insuficiente, cercado/animal já adquirido, saldo/estoque, ciclo antigo, não pronto, ajuda repetida, quota de recompensa, teto de bônus do dono atingido (OWNER_BONUS_LIMIT_REACHED), roubo esgotado, chave conflitante e overflow. Quota esgotada rejeita sem efeitos. Nenhum dado produtivo é acrescentado à lista de amigos.

## React, eventos e experiência

Propostas `AnimalEnclosure.ts`, `src/api/animals.ts`, `useAnimalInteraction.ts`, `AnimalEnclosureModal.tsx` e `animalVisuals.ts`. React mantém estado/painéis/HUD, Phaser mundo/input/animações. Reutilizar useFarm/FarmProvider/UserProvider e bridge, sem autoridade paralela.

Tocar no cercado abre painel do animal adquirido; no MVP, apenas galinha. Seleção identifica animal e produto: “Ajudar galinha”, “Coletar ovos”, “Roubar ovos”. A apresentação preserva identificação por espécie para evolução futura, sem abas ou ofertas funcionais das espécies adicionais. Sem botão que reinicie todos os ciclos. Produto próximo pode ser atalho para selecionar o animal correspondente; alvo amplo do cercado e acesso React por teclado/leitor de tela preservam uso mobile.

Mostrar maturidade individual ou prazo produtivo daquela espécie, ajuda prevista e “Ajudado por Ana, Bruno…” com expansão da lista, além do contador. Não agregar nomes de espécies diferentes como se ajudassem o mesmo ciclo. Desabilitar ajuda com “Bônus de produção deste ciclo completo” quando teto dono cheio, ou “Seu limite de recompensa foi atingido” para quota visitante. Não oferecer ajuda apenas para registrar nome. Após falha concorrente, atualizar estado e exibir motivo sem animação de sucesso. Animar jogar alimento somente após ajuda confirmada, sem fome/custo de ração.

| Evento | Payload/direção propostos |
| --- | --- |
| enclosure:click | Phaser → React `{farmId, enclosureId}`, intenção de abrir painel |
| animal:select | Phaser → React `{farmId, animalId}`, opcional para produto/alvo visual; não executa recompensa |
| farm:sync / farm:change | Existentes, Farm ampliado; atualização versus troca preservadas |
| ui:modal | Existente `{source:'animal-enclosure', open}`; bloqueia canvas |
| animal:collect:done | React → Phaser `{operationId, farmId, animalId, cycleId, productId, quantity, xpGained}` |
| animal:help:done | React → Phaser `{operationId, farmId, animalId, cycleId, ownerBonusAdded, coinsGained, xpGained, helperCount}` |
| animal:steal:done | React → Phaser `{operationId, farmId, animalId, cycleId, productId, quantity, xpGained}` |

Refresh considera menor prazo entre maturidades/ciclos de animais e prazos existentes, preservando polling e margem atual. Timer expirado consulta backend; não habilita ação/maturidade localmente. Rejeitar respostas atrasadas por sessão/revisão; aplicar estado atual e deduplicar efeitos por operationId. Compra/ajuda/coleta/venda atualizam carteira/inventário/XP compartilhados sem F5.

Feed nomeia espécie/ação e mantém ajuda/roubo separados, mesmo ator. Bônus e moeda/XP são efetivos; contador/lista corrente vêm das participações do ciclo, não do feed de sete dias. CTA de retorno segue amizade atual, sem revelar prontidão na lista de amigos.

## Assets, layout e Phaser

Um cercado compartilhado aberto, animais identificáveis convivendo, produtos próximos e arte própria. Não criar galinheiros separados para cada espécie. Galinha inicial pode ter ninho/pequeno abrigo, mas não deve ocupar todo espaço como instalação exclusiva. Não adicionar sprites decorativos de espécies não adquiridas.

Planejar chão/sombras, cerca frontal/traseira/entrada, sprites de filhote/adulto e animações andar/ciscar/descansar adequadas ao tipo aprovado, ícone/objeto de produto, ajuda/coleta/roubo distintos. Nenhuma animação ou produto futuro é inferido como regra pela imagem. Definir dimensões de frames, origem nos pés, escala, textura/fallback e movimento reduzido antes de integrar assets originais.

Sprites são indexados por animalId; um por unidade real. Posição/depth/caminhada são apresentação local limitada ao cercado; estágio e disponibilidade vêm do servidor. Não sincronizar cronômetros produtivos pelo comportamento animado. Sync mantém objetos quando seguro; shutdown/troca de fazenda limpa sprites/tweens/timers/listeners. Resultados tardios não animam em outra fazenda.

**Layout ainda não validado em runtime.** A compra é liberada na primeira expansão: a prova deve partir da grade ampliada com nove lotes desbloqueados e 19 bloqueados, com placa de oferta/HUD, e acompanhar as compras seguintes até os 28 desbloqueados. O máximo de 28 é cenário de layout, nunca requisito de compra do cercado. Incluir culturas altas. Validar dois estados: MVP com apenas galinha/ninho e dimensionamento futuro com **galinha, vaca, ovelha e porco simultaneamente**, uma unidade por tipo. Reservar área para os quatro animais, circulação visual e espaços neutros para produtos futuros ainda indefinidos; verificar escala relativa, legibilidade e oclusão entre animais, cerca e objetos. Esses espaços não especificam produtos nem autorizam assets ou implementação das espécies adicionais. Testar também fazendas sem cercado para regressão. Não usar duplicatas para determinar densidade.

Validar lateral/superior direita e ajustar área/fundo se necessário, sempre na mesma cena. Não presumir que a composição da referência cabe no fundo atual. Reservar footprint estável fora do envelope dos lotes, com espaço para produtos/animais sem oclusão permanente. Overview em retrato deve incluir área interativa do cercado; foco/pan/atalho React mantêm alvos legíveis sem reduzir tudo a miniaturas.

Prova visual futura: viewports mobile retrato/paisagem (exemplos QA 360×800 e 800×360), desktop, foco/overview/pan, toque versus arraste, overlay de visita/toast/modal. Verificar os quatro volumes animais previstos com placeholders de dimensionamento rotulados e áreas neutras para produtos, sem criar catálogo funcional ou definir produtos por esse teste. Cercado/produtos não interceptam cliques de lotes; nenhum dos quatro animais previstos fica permanentemente oculto por abrigo/cerca/outro animal. No jogo do MVP, renderizar somente a galinha efetivamente adquirida.

## Implementation Sequence — execução futura

Todas as fases dependem de aprovação explícita de implementação. Não executar agora.

| Fase | Responsável/escopo | Saída e verificação |
| --- | --- | --- |
| 0 — Regras e apresentação | Produto/social/economia/planner; Phaser para prova visual depois de autorizado | Preservar MVP galinha/ovos; fechar regras propostas, posição/layout para quatro tipos futuros e parâmetros candidatos |
| 1 — Aquisição unitária | backend_engineer: modelos/options/regras/DB/compra; frontend_engineer: loja após contrato | Primeira expansão com nove lotes desbloqueados+nível, cercado único, um tipo por fazenda, saldo/replay/concorrência |
| 2 — Ciclo por espécie | Backend: maturidade/ciclo/coleta/produto/venda; frontend: estado/painel/inventário | Primeiro ciclo histórico, coleta independente, nenhuma alteração de outra espécie |
| 3 — Ajuda remunerada | Backend: quota global/par, completion/recompensa/bônus/nomes; frontend: valores/nomes/feedback | Última quota e último bônus concorrentes, um amigo/ciclo, rejeição sem efeitos, crédito único e bônus protegido |
| 4 — Roubo agregado | Backend: orçamento/base/reserva; frontend: ação e feed | Corridas de ladrões/coleta, bônus intocável, eventos separados |
| 5 — Mundo/integração | phaser_engineer: cena/layout/visuais; integration_engineer: partes predefinidas/eventos | Galinha real no cercado, reserva física validada para os quatro tipos, seleção inequívoca, lifecycle/mobile e fluxo sem F5 |
| 6 — Revisão/rollout | qa_reviewer, architecture_reviewer, gameplay_reviewer reportam achados | Testes/revisões de autoridade, concorrência, experiência e documentação |

Contratos e donos de arquivos estabilizados antes de paralelo; Game.tsx, Farm.ts/useFarm e FarmScene.ts com um editor por fase. Revisores não corrigem silenciosamente. Não acrescentar dependências sem necessidade demonstrada.

## Test Strategy — testes e aceitação

Testes de regra com TimeProvider controlado; concorrência com PostgreSQL isolado. PostgresFact/fixture atual depende de configuração de conexão de teste; skips não comprovam locks. Testes com mais de uma espécie usam fixtures técnicas, não aprovação de catálogo futuro.

| Grupo | Cenários e resultado esperado |
| --- | --- |
| Aquisição do cercado | Antes do 9º lote rejeita; grade ampliada válida com nove, dez e 28 desbloqueados aceita se nível/saldo permitirem. Layout Unsupported rejeita; landOffer null/flag de terra não comprovam marco. Nenhum débito indevido |
| Corrida aquisição | Duas compras cercado e compra versus aquisição do 9º lote; um cercado/débito e resultado serializável |
| Unicidade animal | Mesmo tipo com chaves diferentes/concurrentes, retry, filhote existente, tipo inválido/variante. Uma unidade por fazenda, sem compra em quantidade ou débito duplicado |
| Escopo MVP | Loja/API/jogo permitem somente galinha/ovos; vaca, ovelha e porco não são compráveis, produtivos ou renderizados como propriedade no MVP |
| Crescimento | Filhote cresce uma vez, adulto permanente, primeiro ciclo histórico por animal, config alterada, normalização simultânea/tardia. Nenhum acúmulo offline |
| Independência | Espécies A/B com ritmos distintos; amadurecer/coletar/ajudar/roubar A não altera ciclo, nomes, bônus, prazo ou orçamento de B |
| Ajuda/nomes | Mesmo amigo duas chaves, vários amigos, nomes iguais/renomeados, lista completa/paginada, teto bônus e quota separadas. Identidade única e nomes históricos corretos |
| Recompensa global | Mesmo visitante ajuda simultaneamente espécies/fazendas disputando última quota; limites de ações/moedas/XP/par respeitados, sem reset por coleta/amizade |
| Corrida de quota | Duas ajudas disputam última recompensa: perdedora falha sem participação/bônus/XP/moedas/contador/notificação. Retry da operação vencedora é replay de sucesso, não segunda ajuda. Teto do bônus dono não é quota visitante |
| Roubo/reserva | Vários visitantes concorrentes, T=0, limite individual, roubo versus coleta. Soma <= T, base restante >= R; bônus protegido nunca roubado |
| Fronteiras/corridas | Ajuda exatamente ReadyAt, ajuda/coleta, compra/maturidade, amizade removida, ciclo esperado antigo. Nenhum efeito migra para outro ciclo |
| Idempotência | Mesma chave replay; chave reutilizada em outra espécie/ação/ciclo conflita. Sem duplicar animal, produto, moeda, XP, nome, quota, evento ou animação |
| Falha/conservação | Overflow/crédito/commit/estoque: rollback completo. Dono+visitantes = base+bônus no fechamento, sem duplicar saldo/XP em replay |
| Contratos/segurança | JWT/ownership/farm-animal mismatch, expansão no backend, enum aditivo, plantas/loja existentes preservadas. Lista amigos sem prontidão |
| Cliente | GET atrasado, sessão/troca de visita, timer e retry, quota estimada versus efetiva; HUD/inventário/nomes atualizam sem F5 |
| Mundo | Cercado único, um sprite/tipo adquirido, produtos selecionam alvo certo, móvel/7×4/culturas altas, sync/restart/modal/pan sem vazamentos/interceptação |
| Reserva visual futura | Prova de dimensionamento acomoda galinha, vaca, ovelha e porco juntos, uma unidade por tipo, circulação e áreas de produtos indefinidos, sem perda de legibilidade/oclusão permanente no mobile e com 7×4; não habilita espécies adicionais |
| Compreensão | Jogador distingue animal crescendo, espécie produzindo, ajuda/recompensa/bônus e coleta/roubo; nomes pertencem ao ciclo selecionado |

Comandos de validação futuros: `dotnet build FarmAndFriends.Api/FarmAndFriends.Api.csproj`, `dotnet test FarmAndFriends.Api.Tests/FarmAndFriends.Api.Tests.csproj`; frontend `npm test`, `npm run lint`, `npm run build`. Migration incremental deve ser validada em banco isolado com schema/dados anteriores. Nada disso foi executado nesta revisão documental.

## Métricas, rollout e pendências finais

Medir tempo até primeira expansão/acesso/primeira compra/coleta, compreensão dos requisitos, renda/XP por espécie e fonte social, retorno do investimento e uso de plantas. Medir diversidade de ajudantes, concentração por pares, frequência de quota esgotada, falhas por quota e nomes consultados, parcela base preservada e bônus do dono. Segmentar frequência de jogo sem metas inventadas antes de baseline.

Release: feature sempre disponível desde a primeira versão que a incluir, sem flag própria de animais. Gates normais de expansão, nível, propriedade, amizade e estado permanecem. Migration aditiva antes da aplicação, configuração validada no startup e testes/balanceamento antes do release. Não alterar flags existentes de outros sistemas. Recuperação por correção ou reversão compatível do release, preservando tabelas, animais, ciclos e comprovantes; não apagar progresso nem pausar/reiniciar relógios silenciosamente. Só usar versão anterior que tolere novos campos/enums/dados; caso contrário preferir correção compatível.

- [x] Referências visuais abertas; organização multiespécie entendida sem copiar assets.
- [x] Quatro revisões recebidas e consolidadas, incluindo decisões finais de ciclo/expansão/bônus.
- [x] Substituídos modelo, API, fórmulas, UI, assets, fases e testes do plano anterior.
- [x] Confirmados cercado único, uma unidade por tipo, ciclo por espécie, primeira expansão/nove lotes+nível, nomes/recompensa visitante e bônus protegido.
- [x] Catálogo MVP definido: somente galinha/ovos; reserva física futura para galinha, vaca, ovelha e porco, sem produtos adicionais definidos ou implementação autorizada.
- [x] Ajuda indisponível após teto do bônus do dono; limites independentes e rejeição concorrente sem efeitos especificados.
- [ ] Avaliar integralmente recomendações do plano e validar valores antes do release; aprovação de implementação permanece pendente.
- [ ] Aprovar implementação; realizar prova de layout, código, testes e rollout em tarefa autorizada.

Revisão documental concluída. Confirmados nove lotes/primeira expansão, falha atômica da ajuda perdedora e ausência de flag própria. As escolhas técnicas abaixo ficam especificadas para avaliação integral do usuário. Nenhuma implementação foi autorizada ou executada.

## Contracts — especificação técnica recomendada de 12/set

Esta seção detalha as rotas anteriores; não são endpoints implementados. Todas as leituras autenticadas usam JWT; GET de fazenda continua permitindo dono ou amigo autorizado. Catálogo personalizado só expõe requisitos/propriedade do próprio usuário. Histórico completo de ajudantes é restrito ao dono; visitante recebe sua participação e contagem, não lista de terceiros.

### Requests, resultados e erros

Requests: compra de cercado usa corpo vazio; compra animal `{animalTypeId:string}`; collect/help/steal usam `{expectedCycleId:uuid}`; venda usa `{itemId:string,quantity:integer>0}`. Servidor rejeita tipo/produto fora do MVP. Todos POST exigem Idempotency-Key UUID. Ajuda não possui modo gratuito. Não aceitar preço, XP, timestamp, ator ou quantidade de animais enviados pelo cliente.

Resultado de POST concluído, inclusive replay: HTTP 200 com `{operationId:uuid,replayed:boolean,effect:AnimalOperationEffect,current:AnimalStateEnvelope|null}`.
- effect imutável: `{action,completedAt,farmId,animalId:uuid|null,cycleId:uuid|null,animalTypeId:string|null,productId:string|null,quantity:number,coinsSpent:number,coinsGained:number,xpGained:number,ownerBonusAdded:number}`. Campos de quantidades sem efeito são zero; referências não aplicáveis são null. Nunca usar effect como saldo atual.
- current: projeção atual autorizada da instalação após comando; null para venda sem instalação ou replay cujo observador perdeu acesso à fazenda. Replay ainda pode devolver seu próprio comprovante sem revelar estado privado atual. Não incluir nomes de terceiros indevidamente.
- AnimalStateEnvelope: `{farmId,enclosure:AnimalEnclosureDto|null,revision:number,observedAt:UTC}`. Revision zero quando instalação nunca existiu; com instalação, incrementa transacionalmente em compra/maturidade materializada/coleta/ajuda/roubo. Replay puro não incrementa. Ler revision e dados numa projeção transacional consistente sob locks de fazenda/instalação, e capturar observedAt para a avaliação temporal dessa projeção. Não carregar dados antigos e depois carimbar revisão/horário novos. observedAt só desempata projeções da mesma revisão; não substitui revision, guard de sessão ou ordem de requests. Mudanças derivadas de tempo podem ocorrer na mesma revision.
- AnimalEnclosureDto: `{id,slot,animals:AnimalDto[]}`. AnimalDto: `{id,animalTypeId,displayName,purchasedAt,maturesAt,stage:'chick'|'adult',cycle:AnimalCycleDto|null,permissions,nextStateChangeAt:UTC|null}`. Antes da primeira maturidade, cycle é null; adulto sem ciclo devido é normalizado antes da projeção autorizada.
- AnimalCycleDto: `{id,productId,startsAt,readyAt,isReady,baseYield,protectedHelpBonus,remainingBase,ownerCollectible,helperCount,viewerHelped,helpers:HelperDto[]|null,helpersNextCursor:string|null}`. Helpers null para visitante; dono recebe resumo e cursor; número total não depende da página/feed.
- permissions: `{canCollect,canHelp,canSteal,collectReason:string|null,helpReason:string|null,stealReason:string|null,rewardAvailable:boolean,coinsReward:number,xpReward:number,nextRewardAt:UTC|null}`. canHelp exige ciclo em produção, amizade/ator válidos, participação ainda não registrada, bônus dono abaixo do teto e quota visitante integral. rewardAvailable descreve somente quota visitante; pode ser true com canHelp=false por OWNER_BONUS_LIMIT_REACHED. Valores de recompensa são do próprio observador, nunca saldo/quota privada do dono. nextRewardAt é prazo da quota visitante, não reabre teto dono. Horário local expirado apenas pede novo GET. Se vários motivos coexistirem, após autenticação/recurso/ciclo e ajuda repetida, priorizar OWNER_BONUS_LIMIT_REACHED sobre REWARD_QUOTA_EXHAUSTED; replay confirmado é resolvido antes das precondições consumidas.
- Catálogo cercado: `{priceCoins,minLevel,currentLevel,firstExpansionReached,initialPlotsUnlocked,requiredInitialPlots,owned,canBuy,reason:string|null}`. Catálogo animais: lista `{animalTypeId,displayName,priceCoins,minLevel,growthDuration,owned,canBuy,reason}`. Apenas galinha comprável; ovos têm catálogo de venda próprio `{itemId,displayName,sellPriceCoins}`.
- GET helpers: `?cursor&take`, limite de página servidor, ordenação estável helpedAt+completionId, cursor opaco, resultado `{items,nextCursor,total}`. HelperDto `{userId,displayNameAtHelp,helpedAt}`. Revalidar dono e correspondência farm/animal/cycle em cada página.

HTTP/ProblemDetails.code: 400 INVALID_REQUEST/INVALID_IDEMPOTENCY_KEY/INVALID_QUANTITY; 401 não autenticado; 403 NOT_OWNER/NOT_FRIENDS/SELF_ACTION; 404 RESOURCE_NOT_FOUND (alvo ausente ou não pertence à rota); 409 FIRST_EXPANSION_REQUIRED/LEVEL_REQUIRED/ALREADY_OWNED/INSUFFICIENT_FUNDS/INSUFFICIENT_STOCK/CYCLE_CHANGED/NOT_READY/HELP_ALREADY_REGISTERED/REWARD_QUOTA_EXHAUSTED/OWNER_BONUS_LIMIT_REACHED/THEFT_LIMIT_REACHED/IDEMPOTENCY_KEY_REUSED/BALANCE_LIMIT; 500 INVALID_CONFIGURATION/erro inesperado sem dados sensíveis. Layout Unsupported falha sem débito, como 409 UNSUPPORTED_FARM_LAYOUT e log de diagnóstico. Erro transiente de transporte/5xx não prova rollback: recuperar mesma operação antes de nova intenção. Elegibilidade atual pode vir em extensão de erro apenas se autorização permitir.

### Fingerprint e recuperação

Chave é vinculada ao ator autenticado; serialização canônica compara valores tipados, não ordem do JSON.

| Comando | Dados semânticos no fingerprint |
| --- | --- |
| buy-animal-enclosure | action + farmId próprio resolvido |
| buy-animal | action + farmId próprio + animalTypeId |
| collect/help/steal | action + farmId + animalId + expectedCycleId |
| sell-animal-product | action + itemId + quantity |
 
Consultar/revalidar comprovante depois dos locks e antes de precondições mutáveis que já foram consumidas, como estoque e ciclo. Resultado confirmado repete sucesso mesmo após estoque zerado ou ciclo seguinte; outra chave do mesmo ator/ciclo em help conflita. Falha antes do commit não grava sucesso nem efeitos. Timeout/resposta perdida após possível commit mantém chave e conteúdo no registro de tentativa do cliente; retry nunca cria outra chave automaticamente. Persistir tentativa pendente por identidade de sessão/usuário sem tokens e limpá-la ao resolver; em troca de conta não reutilizá-la. Se não for possível determinar resultado, mostrar pendência recuperável, não afirmar falha com rollback.

### Consumidores e ordem de estado

Atualizar explicitamente: FarmResponse/mapper/controller; tipos Farm/Inventory e cliente HTTP; useFarm/FarmProvider/farmState; novo hook/painel animal e Game; UserProvider e useInventory; ShopPanel/ShopSellTab/useShop/InventoryPanel; notificações DTO/notificationFeed; FarmScene/usePhaserGame e scheduler time.ts. API antiga de plantas permanece compatível.

Escolha técnica: **todos os produtores de XP concorrentes nesta aplicação usam refresh absoluto comum no UserProvider**, incluindo plantio, colheita, roubo, cuidado, remoção de praga e animais. Substituir nesses consumidores a soma addXp por refresh autoritativo de XP/nível; xpGained permanece apenas no feedback visual. Isso é integração necessária, sem alterar recompensas/regra econômica existentes. Após sucesso/replay, atualizar usuário e carteira pelos GETs autoritativos existentes. Se refresh falhar, mostrar estado econômico pendente e repetir leitura; não transformar comando confirmado em falha ou reaplicar delta.

Em ambos os owners de estado, aplicar guards de identidade da sessão, requestId crescente e mutationEpoch: confirmação invalida leituras iniciadas antes dela e agenda nova leitura; só a última leitura ainda válida pode atualizar. Toda confirmação, inclusive de ações existentes, usa esse mesmo caminho absoluto. Não manter addXp concorrente: invalidar GET em voo não evita duplicação se uma leitura já aplicada incluiu XP de outra ação cuja resposta POST ainda vai chegar. Outros dispositivos convergem por refresh; não presumir que efeito histórico é estado atual.

Fazenda: patch de animal atualiza snapshot ativo e cache próprio correspondente, invalida GETs anteriores e emite farm:sync. Comparar farmId/sessão, depois revision e observedAt; aceitar projeção temporal mais nova na mesma revision, rejeitar inferior. Ordem local requestId também resolve empate. Não comparar relógio cliente com servidor para conceder estado. Nova instalação deve aparecer mesmo sem mudança em plots. Resultados de outra sessão não alteram cena ou carteira.

### Eventos e lifecycle

Payloads da tabela anterior ganham tipos TypeScript nomeados no módulo comum de eventos; nenhum Phaser object passa pelo payload.

| Evento | Categoria/gatilho | Emissor → receptor e cleanup |
| --- | --- | --- |
| enclosure:click | intent; toque válido no cercado | FarmScene → hook React; hook remove listener em unmount/troca; cena remove input ao destruir objeto |
| animal:select | intent; tocar produto/animal selecionável | FarmScene → hook React; mesmo cleanup, valida farmId |
| farm:sync | state-sync; projeção aceita pelo owner | useFarm → FarmScene; scene unregister no shutdown/destroy |
| farm:change | state-sync; troca de fazenda aceita | useFarm → bridge usePhaserGame; remove subscription ao desmontar |
| ui:modal | ui-control; abrir/fechar painel | Game/painel → FarmScene; React envia open:false no cleanup; cena remove listener e restaura bloqueios por source |
| animal:*:done | confirmed-result; novo sucesso confirmado, uma vez por operationId na sessão | hook animal → FarmScene; scene remove listeners no shutdown; ignorar replay para animação e fazenda diferente |
 
Aplicar snapshot confirmado antes do efeito, mantendo cicloId do efeito histórico para não animar produto de outro ciclo. Em replay recuperado, atualizar estado sem repetir efeito. Scene restart por compra de terra recria animais do snapshot e limpa listeners/tweens anteriores; quando apenas enclosure muda, reconciliar objetos/bounds sem depender do teste de topologia de plots.

## Rollout and Rollback — resolução de 12/set

Sem flag própria e sem etapa extra de ativação: disponível desde primeiro release que inclui a feature. Configurações numéricas validadas com ValidateOnStart antes do release; migração aditiva aplicada antes da aplicação. Normalização GET autorizada continua funcionando por maturidade independentemente de flags de plantas/terras. Falhas técnicas retornam erros e preservam transações, não simulam desativação de gameplay. Reversão exige binário compatível com novos enums/tabelas ou correção posterior; nunca apagar dados ou comprovantes. Nenhuma configuração/infra foi alterada nesta tarefa.

## Acceptance Criteria — complementos da revisão

- Compra cercado em grade ampliada válida com nove desbloqueados, e nos estados posteriores; tentativa concorrente com o 9º lote lineariza antes (rejeita) ou depois (aceita se demais requisitos), sem concessão automática.
- Corrida pela última quota: só vencedora paga/registra; perdedora sem todos os efeitos e com direito do ciclo preservado. Replay vencedora retorna sucesso único. Outra chave mesma ajuda já concluída conflita.
- Corrida pelo bônus restante: duas pessoas elegíveis disputam teto; vencedora recebe pacote integral e adiciona min(incremento, restante), perdedora recebe OWNER_BONUS_LIMIT_REACHED sem todos os efeitos nem consumo de quota. Teto já cheio rejeita qualquer novo visitante; replay vencedor continua sucesso. Coleta de outra espécie, tempo, amizade e configuração não renovam teto; coleta da espécie inicia novo orçamento. DTO/UI devem distinguir quota disponível de ajuda indisponível pelo teto dono.
- Venda: mesma chave com outro itemId/quantity conflita; replay depois de estoque zerado recupera crédito original; duas vendas do último estoque não negativam.
- Carteira: GET antigo que termina após confirmação não desfaz saldo; XP já recebido em refresh não é somado de novo no replay; logout/login não recebe resposta da conta anterior; refresh falho não desfaz sucesso confirmado.
- XP cruzado: plantio comita, GET disparado por ação animal já incorpora esse XP, resposta do plantio chega depois; resultado final contém o prêmio uma única vez. Cobrir também demais ações de XP e duas confirmações fora de ordem. Guards sem abandonar soma concorrente não satisfazem esse teste.
- Revision igual com observedAt mais recente aceita prontidão derivada; compra de cercado sem mudança de plots cria visual/bounds; compras posteriores de lotes não removem animais.
- Layout cobre primeira expansão/placa e todos os desbloqueios até 28, mobile/foco/overview e quatro volumes futuros. Somente galinha real no MVP.
- Não há flag animal nos contratos/opções/rollout; flags existentes não são alteradas. Configuração inválida impede release saudável, sem valores inventados nesta revisão.

## Impact by Area

DOMAIN/DATABASE/AUTHORIZATION: gate primeira expansão, unicidade, ciclos, quotas e transações; API: DTOs/fingerprints/erros explícitos; FRONTEND_STATE: refresh absoluto comum, guards e replay; FRONTEND_UI: oferta/nome/recompensa; PHASER_WORLD e REACT_PHASER_INTEGRATION: criação/reconciliação do cercado, bounds e eventos; TESTING: concorrência/estado final; OBSERVABILITY: operationId, resultado e origem econômica sem segredos; DOCUMENTATION: auditoria histórica e este plano vigente.

## Risks

Riscos tratados: compra bloqueada até 28 indevidamente, ajuda perdida sem pagamento, venda replayada com outra quantidade, XP duplicado por confirmação tardia, GET antigo revertendo carteira, criação de cercado sem atualizar câmera e rollback incompatível. Testes correspondentes estão especificados acima. Balancear o número de ajudas até o teto para incentivar descoberta sem esgotar oportunidades cedo demais; medir diversidade de fazendas visitadas e recusas por teto, sem prêmio por adicionar amigos ou obrigação social. Regra de indisponibilidade após teto está decidida e não será reaberta por esse balanceamento.

## Responsible Agents

Backend, frontend, Phaser, integração e revisores seguem os responsáveis da tabela de fases. Architecture reviewer revisou as recomendações técnicas desta atualização; foi incorporada sua correção de XP entre ações existentes e novas. Não foi repetida rodada de quatro pareceres. Revisores não alteraram código. Esta atualização só modifica documentos no worktree.

## Status

READY FOR IMPLEMENTATION quanto à especificação consolidada: ajuda bloqueada após teto do bônus do dono, quota visitante independente e ambas as corridas rejeitam perdedoras sem efeitos. Não resta a pendência de ajuda após teto. Isso não autoriza implementar: o usuário ainda avaliará o plano integral. Recomendações técnicas/produto documentadas ficam submetidas a essa aprovação; balanceamento e prova visual são entregas futuras antes do release. Nenhum código, migration, teste de runtime ou asset foi produzido nesta revisão.
# Atualização de regra — 27/set/2026

A decisão posterior substitui as quotas móveis globais e por par descritas historicamente neste plano. A ajuda recompensada agora é limitada a uma participação por visitante, animal e ciclo. O mesmo visitante pode ajudar cada uma das quatro espécies no respectivo ciclo; um novo ciclo do animal reabre a ajuda imediatamente. `rewardAvailable` acompanha `canHelp`, `nextRewardAt` permanece null e `REWARD_QUOTA_EXHAUSTED` não faz mais parte do fluxo animal. O teto protegido do dono continua independente por animal e ciclo.

