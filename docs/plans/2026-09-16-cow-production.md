# Vaca e produção de leite — ExecPlan

Implementação solicitada pelo usuário após aprovação de `imgs/animal-enclosure/2026-09-16-cow-v1`. Extensão do cercado existente; não substitui suas regras nem o ciclo da galinha.

## Decisão consolidada

Game director, social designer, economy designer e technical planner avaliaram a extensão: **Prototype**, com implementação autorizada. A bezerra comprada cresce uma vez e permanece como vaca adulta. Uma unidade por espécie/fazenda; sem reprodução, morte, alimentação obrigatória ou acúmulo ilimitado. Produção pronta espera coleta. Cada espécie tem seu ciclo e a coleta inicia apenas seu sucessor.

O jogador ganha uma conquista visível e uma nova expectativa de retorno. A visita revela a vaca, permite ajuda recompensada durante a produção e roubo limitado de leite pronto. Ajuda aumenta bônus protegido do proprietário e registra o nome do visitante. Reserva, ajuda e orçamento agregado de roubo são por animal e ciclo. Desde 27/set/2026, o mesmo visitante pode ajudar uma vez a vaca e uma vez cada outra espécie no ciclo correspondente.

Riscos: emissão combinada de ovos/leite, preferência por roubar leite mais valioso e confusão de alvos na cena. Medir renda por espécie/fonte, intervalo entre coletas, primeira compra/coleta, concentração de interações por par, recusas por quota e compreensão da ação disponível. Nenhuma meta arbitrária antes de uma linha de base.

## Parâmetros provisórios configuráveis

| Parâmetro | Vaca |
| --- | --- |
| Compra / nível | 1.000 moedas / 5 |
| Crescimento único | 8 h |
| Produção | 24 h |
| Base / venda por unidade | 8 leites / 40 moedas |
| Reserva e roubo | novos ciclos: reserva 4, agregado 4, individual 1; ciclos já abertos preservam o snapshot anterior |
| Ajuda | +1 leite protegido, até +3; visitante 2 moedas e 5 XP |
| Coleta / roubo XP | 25 / 0 |

Valores são hipóteses de balanceamento, não compromissos finais. Não alterar parâmetros atuais da galinha nem quotas sociais. Não exigir compra da galinha para comprar a vaca. Compra do cercado continua após nove lotes, junto da primeira expansão.

## Arquitetura e contratos

Reutilizar entidades, serviço, regras, inventário `AnimalProduct`, endpoints e eventos atuais. IDs `cow` e `milk`; estágio jovem `calf`, preservando `chick` e `adult`. Mapas de nomes/ícones no cliente são apenas apresentação. Backend determina preços, recompensas, estoque, permissões e tempos. Phaser só apresenta snapshots confirmados e emite intenção.

Schema já aceita espécies/produtos strings e garante uma espécie por fazenda e um ciclo aberto por animal: nenhuma migration necessária. Preservar leitura de snapshots planos antigos da galinha; criar política efetiva por espécie no mesmo formato. Produto explícito em cada novo ciclo. Manter idempotência e ordem de locks.

## Execução e responsabilidades

1. Backend: opções/config, política por espécie, ofertas, compra, normalização/ciclo, coleta, venda, mensagens e testes PostgreSQL.
2. React: tipos, loja, inventário, modal, venda, feedback e nomes/produtos corretos.
3. Phaser: copiar PNGs aprovados sem alteração raster, registrar atlas JSON/âncoras, bezerra/adulta animadas, baldes vazio/cheio, alvos separados e fixture de duas espécies.
4. Integração: verificar eventos e sincronização, documentação, testes completos e revisões QA/arquitetura/gameplay.

Backend, React e Phaser possuem arquivos distintos. Não adicionar dependências, novas espécies além da vaca, commit, publicação ou reset de banco real.

## Critérios de aceite

- Galinha e vaca coexistem; compra duplicada falha sem segundo débito.
- Leite, bônus, nomes e ciclo da vaca não alteram ovos/ciclo da galinha.
- Bônus protegido não pode ser roubado; reserva do proprietário permanece.
- Cada ajuda recompensada é única por visitante, animal e ciclo; alternar espécies é permitido e um novo ciclo reabre a ação. Roubo segue limite próprio por animal/ciclo.
- Replay e concorrência não duplicam estoque, moeda ou XP.
- Crescimento e políticas já iniciados preservam snapshots diante de configuração nova.
- Loja/modal/inventário/venda distinguem galinha/ovos e vaca/leite.
- Cena mostra animal jovem/adulto e produto correto; sync repetido não duplica objetos.
- Validar desktop/mobile, visita/própria e reduced motion em runtime; distinguir fixture de fluxo autenticado.
- Testes PostgreSQL ignorados não contam como validação.

## Evidência e estado

**Implementado e validado em 17/set/2026.**

- Backend: build sem avisos/erros com `UseAppHost=false`; suíte completa contra PostgreSQL isolado na porta 55489: **252/252**, zero ignorados. Sete cenários cow cobriam unicidade/compra concorrente, snapshot, isolamento da galinha, conservação/bônus protegido, replay, quotas e o cooldown de roubo compartilhado vigente naquela validação, coleta concorrente e venda do último estoque. O cooldown de roubo foi removido pela decisão posterior registrada abaixo.
- Frontend: **123/123 testes**, lint e build aprovados. Aviso existente de bundle acima de 500 kB permanece não bloqueante.
- PNGs runtime idênticos aos assets aprovados (SHA256); JSON/âncoras e registro de texturas acrescentados, sem edição raster.
- Observação no navegador da fixture da cena real: desktop com duas espécies e balde cheio; sync repetido **98 → 98 objetos**; mobile **390×844**, adulta e bezerra, visita e movimento reduzido. Cliques móveis selecionaram `cow` e `hen` separadamente. A fixture usa snapshots controlados, não chamadas de gameplay.
- Revisões QA, arquitetura e gameplay sem falhas bloqueantes. Ajuste documental solicitado pela arquitetura concluído no contrato backend e documentos runtime.

Não foi registrado um roteiro autenticado completo da vaca no navegador (compra → maturidade → ajuda/roubo → coleta → venda). Regras e concorrência foram executadas em PostgreSQL; apresentação foi observada na fixture. Registrar esse roteiro antes do release público. Valores econômicos continuam provisórios; melhoria futura opcional: explicitar a parcela protegida de ajuda no modal do proprietário.

### Ajuste de reserva e leitura da produção — 18/set/2026

O proprietário aprovou para ciclos novos a divisão base 4/4: quatro unidades mínimas para o dono e até quatro roubáveis, mantendo uma por visitante. Cada ajuda continua adicionando uma unidade exclusivamente à coleta do dono, até três. O painel individual mostra `Disponível para coleta` ao dono e `Produção restante` ao visitante; o valor do visitante é a base remanescente, não uma promessa de roubo. Quando `canSteal` for falso, a interface explica que a parte disponível para roubo acabou sem executar uma chamada previsivelmente inválida. O backend continua revalidando corridas. A lista de amigos não revela prontidão nem quantidade.

**Validação retomada em 21/set/2026.** Build backend Release sem avisos/erros, regras unitárias **6/6** e suíte focada em PostgreSQL isolado **23/23**, sem ignorados. A cobertura confirma a preservação do snapshot histórico 6/2, sucessor 4/4, quatro roubos de uma unidade, coleta de cinco após uma ajuda protegida, projeções `remainingBase = 4`/`ownerCollectible = 5` e bloqueio `THEFT_LIMIT_REACHED`. No frontend, **124/124 testes**, lint e build de produção passaram; permanece apenas o aviso não bloqueante de tamanho do bundle. A notificação de leite também passou a flexionar corretamente `leite`/`leites`, sem expor o termo “protegido”.

### Limite pessoal por animal e ciclo — 21/set/2026

O limite pessoal deixou de ser compartilhado por fazenda. Cada visitante pode roubar uma unidade de cada animal em cada ciclo pronto, enquanto cada ciclo continua aceitando no máximo quatro unidades roubadas e preservando quatro unidades-base para o dono. Roubar da galinha não bloqueia a vaca; um novo ciclo do mesmo animal reabre a oportunidade. `THEFT_ALREADY_REGISTERED` comunica repetição pessoal naquele animal/ciclo, enquanto `THEFT_LIMIT_REACHED` permanece reservado ao esgotamento coletivo. Não existe cooldown temporal adicional de roubo. A mudança reutiliza o índice único `ActorUserId + CycleId + Action`, não altera o schema e não requer migration.

Validação: teste simultâneo de ovo e leite para o mesmo visitante, bloqueio da repetição no mesmo ciclo, novo roubo após iniciar o ciclo seguinte e duas requisições concorrentes no mesmo animal creditando exatamente uma vez; suíte animal PostgreSQL **24/24**, regras **6/6**, frontend **124/124**, lint e builds aprovados.

Nenhum commit, deploy, dependência, migration adicional ou reset de banco real. Após compras persistidas de vaca, backend antigo deixa de operar `cow`/`milk`; eventual rollback deve preservar suporte às espécies já adquiridas.
