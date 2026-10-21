# Adubo: conclusão imediata e roubo antecipado

## Estado da decisão

Proposta consolidada após análises de game_director, social_designer, economy_designer e technical_planner. Veredito: **Prototype**. Usuário autorizou a mudança de comportamento, confirmou teto de uma hora e escolheu desestimular por balanceamento mantendo o roubo normal. Implementação autorizada nesse escopo; nenhuma taxa ou alteração de XP/quotas está aprovada.

O usuário confirmou uma redução **direta de até uma hora por aplicação**. Quando falta uma hora ou menos, a planta fica pronta no instante confirmado pelo servidor; quando falta mais, conserva o tempo excedente. O preço de duas notas e a produção do porco permanecem hipóteses de balanceamento.

**Decisão de interface em 02/out/2026:** o usuário aprovou a confirmação atual, que informa o consumo de 1 adubo, sem aviso adicional sobre roubo. O papel comunicado do adubo é acelerar a produção. A possibilidade de colheita ou roubo decorre do estado pronto já conhecido no produto e das permissões confirmadas pelo servidor. Esta decisão substitui as exigências anteriores de destacar o roubo no fluxo de adubação; não altera a regra de roubo nem dispensa a sincronização do estado da planta.

## Oportunidade e experiência

- Problema: hoje o adubo adianta a colheita, mas mantém 30 segundos de espera e bloqueia o roubo até o prazo original.
- Interesse: escolher gastar um recurso próprio para concluir um cultivo durante a visita e abrir uma oportunidade social.
- Retorno: produzir adubo com o porco, desenvolver a própria fazenda e encontrar novas oportunidades nas visitas.
- Interação: adubar e roubar continuam ações distintas. Não existe roubo automático, exclusividade ou prioridade para quem adubou.
- Dono: pode colher antes; o novo prazo também antecipa a exposição ao roubo.
- Visitante: consome seu estoque; pode tentar roubar após a confirmação, mas pode perder a oportunidade para o dono ou outro visitante.
- Frustração: gasto sem roubo bem-sucedido, concentração de ataques e perda antes do horário anteriormente esperado.

## Política proposta

1. Uma aplicação por lote/ciclo, com amizade aceita para uso em outra fazenda.
2. Redução efetiva: 3.600 segundos por aplicação, inclusive em ciclos de regeneração.
3. Novo prazo: máximo entre o instante confirmado pelo servidor e o prazo anterior menos a redução.
4. Remover o piso de 30 segundos. Se o efeito cobrir o tempo restante, a produção fica pronta imediatamente.
5. O roubo usa o novo ReadyAt. PreviousReadyAt fica registrado para auditoria e replay.
6. Planta já pronta continua rejeitando adubo sem consumo.
7. Aplicação não muda rendimento, moedas ou XP. Roubo continua separado, sujeito às regras aprovadas.
8. A interface comunica o consumo e a aceleração pelo adubo; os botões de colheita e roubo seguem o estado pronto e as permissões confirmadas pelo servidor, sem aviso adicional de roubo no fluxo de adubação. Notificações dizem "aplicou adubo", sem atribuir intenção de ajuda.

## Decisão econômica

O objetivo "não compensar o investimento" exige distinguir desestímulo por balanceamento de garantia de prejuízo monetário. Não existe câmbio aprovado entre notas e moedas; adubo produzido pelo porco não tem custo monetário marginal. Um preço alto na loja não garante prejuízo.

O usuário escolheu **desestimular pelo balanceamento, mantendo o roubo normal**. Portanto, preservar preço/produção configuráveis nesta implementação, sem prometer prejuízo garantido. A validação do desestímulo é um resultado econômico a medir, não um bloqueio de roubo ou taxa nova.

Em 27/set/2026, esclareceu a potência: **uma aplicação subtrai diretamente uma hora**, concluindo imediatamente quando falta até uma hora. A fórmula intermediária de 25% foi substituída por essa regra. Uma plantação com mais de uma hora restante mantém o tempo excedente.

No catálogo atual, cultivos de poucos minutos podem ser concluídos imediatamente. É uma mudança de potência relevante, inclusive para usos comprados com notas. Em cenário comparável de abóbora intacta, usar um adubo no próprio cultivo pode render mais moedas por adubo do que roubar quatro unidades, mas o roubo pode render mais XP; portanto o desestímulo ainda não está comprovado para progressão.

O catálogo versionado permite que uma abóbora intacta abra vários roubos, somando até 320 moedas e 184 XP por ciclo. São tetos teóricos do catálogo, não ganhos esperados nem valores conferidos na base em execução. Avaliar todas as ações habilitadas e todos os visitantes, não só o primeiro roubo.

- **Desestímulo por balanceamento:** preservar o roubo normal; revisar valor do recurso, potência, produção e retorno total. Não prometer prejuízo garantido. Ajustes de XP, quotas, reserva ou preços precisam de decisão própria.
- **Garantia de prejuízo em moedas:** requer regra adicional explícita, como um custo do roubo antecipado superior ao valor de venda recebido, e decisão sobre XP. A taxa não está aprovada. Não implementar cobrança ou bônus diferente sem aprovação.

Risco social independente: o roubo atual preserva apenas um item, permite repetição no lote e tem quota por visitante/fazenda. Isso impede esvaziamento total, mas não garante uma parcela significativa para o dono sob coordenação. Reserva agregada e repetição são recomendações para decisão, não alterações aprovadas nesta proposta.

## Menor versão jogável e escopo

Conclusão imediata com teto de uma hora, roubo pelo novo prazo, comunicação do consumo e da aceleração, e sincronização autoritativa. Sem novos itens, compra por moedas, roubo automático, conversão de moedas, eventos paralelos ou dependências.

Escopo técnico moderado: backend, textos/estado React, testes e validação do Phaser pelo farm:sync existente. A potência fixa pode concluir um ciclo de duração menor que uma hora no próprio instante inicial; portanto, a validação de ReadyAt >= início do ciclo e o CHECK do banco exigem migration.

## ExecPlan

### 1. Consolidar política

- Resolver significado econômico e confirmar redução direta de uma hora.
- Aprovar a proposta antes de editar a aplicação.
- Preservar riscos, decisões e valores configuráveis neste documento.

### 2. Backend

- FertilizerOptions: redução fixa de 3.600 segundos, piso zero e validação compatível; remover GrowthReductionFraction.
- FertilizerService: clamp em now; manter locks, consumo transacional, amizade, ciclo e idempotência.
- TheftController: remover proteção baseada no prazo original.
- FertilizerApplicationQuery: manter consulta do ciclo atual; remover apenas helper da janela original.
- Manter entidade, índices, PreviousReadyAt e contratos de aplicação/replay.
- GET /shop/fertilizer mantém maxReductionSeconds=3600 e minRemainingSeconds=0. effectPercent perde sentido e deve ser removido de DTO/TS/UI com consumidores e documentação atualizados; implantar backend e frontend coordenados.
- Implementar regra econômica adicional somente se escolhida e detalhada.

### 3. React e Phaser

- Apresentar potência e possível conclusão imediata na oferta do adubo; no PlotModal, confirmar o consumo de 1 adubo e refletir o estado autoritativo após a aplicação, sem aviso específico de roubo.
- Atualizar inventário/fazenda após resposta confirmada; botões seguem o estado autoritativo.
- Verificar recuperação quando o GET posterior falhar; não fabricar prontidão local nem reaplicar prazo de replay antigo.
- Phaser recebe farm:sync existente e mostra o cultivo pronto. Não criar eventos ou regras de domínio no mundo.

### 4. Testes e revisão

- PostgreSQL: redução parcial, igual e maior que restante; exatamente 60 minutos e 58 minutos de qualquer cultura; roubo antes do prazo original; planta pronta sem consumo.
- A redução fixa pode resultar em ReadyAt == CurrentHarvestCycleStartedAt no instante inicial de cultivos curtos. Atualizar regra de ciclo e CHECK persistido com migration, aceitando igualdade, e verificar o desenho de crescimento com duração zero.
- Amizade revogada, falta de estoque, ciclo antigo e chave incompatível sem efeito.
- Duas aplicações concorrentes: um consumo, um registro e uma notificação.
- Adubar versus colher/roubar: saldo e produção consistentes; roubo não garantido.
- Replay após roubo/colheita/regeneração não repete efeito nem altera novo ciclo.
- Regeneração, aplicação pelo dono e por visitante, cuidado/pragas conforme regras atuais.
- Frontend: oferta e feedback, estoque, troca de botões, falha de atualização e farm:sync.
- Navegador autenticado: próprio/amigo; efeito parcial/imediato; lote pronto visualmente; tentativa de roubo; sincronização repetida.
- qa_reviewer, architecture_reviewer e gameplay_reviewer retornam achados; responsáveis corrigem.

## Sucesso e limites

Medir uso próprio/em amigos, origem do adubo, aplicações seguidas de roubo, moedas e XP totais por aplicação, produção preservada para o dono, concentração no mesmo par e retorno às visitas. Aprovar números após observar esses resultados; não declarar equilíbrio só porque a loja cobra notas.

Aplicações já persistidas também passam a permitir roubo pelo prazo antecipado. O rollback da regra de negócio não apaga aplicações nem desfaz roubos confirmados. O rollback da migration exige colher antes qualquer cultivo ativo cujo prazo seja exatamente igual ao início do ciclo, porque o CHECK anterior proíbe esse estado. Release exige validar o fluxo autenticado; testes sintéticos não substituem essa evidência.

## Critérios de aceite

- Efeito suficiente conclui o cultivo e libera tentativa normal de roubo antes do horário original.
- Efeito insuficiente conserva o bloqueio enquanto o cultivo cresce.
- Consumo único, replay seguro, reserva e quotas aprovadas preservadas.
- Texto, botões e mundo refletem a mesma confirmação do servidor.
- O resultado econômico segue a alternativa explicitamente escolhida, com suas limitações documentadas.

## Evidência histórica da versão intermediária — 26/set/2026

- Backend: teto 3.600 segundos, piso zero, roubo pelo prazo atual e PreviousReadyAt preservado. Redução percentual de 25% mantida; nenhuma migration necessária para essa fórmula.
- PostgreSQL isolado farmfriends_pig_test_20260925: **278/278**, nenhum teste ignorado. Build Release sem erros ou avisos. A base principal não foi alterada.
- Os testes do adubo incluem redução parcial/imediata/exata, caso de 58 minutos com potência suficiente, regeneração, proprietário/visitante, replay e concorrência.
- Frontend: recuperação apenas de leitura após confirmação sem snapshot, texto de conclusão/roubo, estado proveniente de GET confirmado. **8/8 testes focados**, lint e build passaram. Suíte geral **137/142**, com cinco falhas de nomes/quantidades dos animais fora desta alteração.
- Revisão técnica independente somente leitura não encontrou bloqueios de autoridade, locks, replay ou compatibilidade dos refreshes. Agente específico de arquitetura não pôde ser iniciado devido ao limite de threads; o planejador técnico realizou essa revisão e o agente principal conferiu os fluxos.
- Fluxo autenticado no navegador permanece sem observação nesta revisão. Não declarar validação visual completa nem eficácia econômica demonstrada.
- Em 27/set/2026, o usuário esclareceu que a redução é direta de uma hora em qualquer cultura. Os testes e evidências anteriores desta seção pertencem à versão intermediária de 25% e não validam a nova potência.

## Evidência da redução fixa — 27/set/2026

- Backend: redução direta de 3.600 segundos com limite no instante da aplicação; sem piso de espera. Oferta sem `effectPercent`; a prontidão pode coincidir com o início do ciclo.
- Migration `20260928022110_AllowImmediatelyReadyFertilizedPlots` atualiza o CHECK para aceitar essa igualdade. Foi aplicada somente à base PostgreSQL isolada `farmfriends_pig_test_20260925`; a base principal não foi alterada. O modelo EF não tem alterações pendentes.
- Testes PostgreSQL cobrem menos de uma hora, exatamente uma hora, mais de uma hora, ciclos curtos adubados no instante do plantio/recrescimento, prontidão e roubo, replay, concorrência e ausência de consumo em lote já pronto. **281/281 testes backend**, nenhum ignorado; build Release sem erros ou avisos.
- Frontend: contrato atualizado, confirmação sem duração fixa e arte pronta quando `plantedAt == readyAt`. **22/22 testes focados**, lint e build passaram.
- Fluxo autenticado em navegador ainda não foi observado. O desestímulo econômico ao roubo continua uma hipótese a medir, especialmente em XP.
