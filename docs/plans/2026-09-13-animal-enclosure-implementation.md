# Cercado — execução e validação

Implementação autorizada em 12/set/2026; retomada em 13/set após interrupção por quota da ferramenta. Este registro acompanha o ExecPlan canônico em 2026-09-09-chicken-coop.md. Não substitui as regras de produto.

**Escopo do primeiro commit:** registra a implementação atual do cercado e das quatro espécies, sem declarar a feature finalizada. Balanceamento, observação do fluxo autenticado completo e futuras implementações e ajustes permanecem como trabalho posterior.

**Atualização em 27/set/2026:** as quotas móveis globais e por par abaixo registram a implementação original e foram substituídas. A regra vigente permite uma ajuda recompensada por visitante, animal e ciclo, de forma independente entre galinha, vaca, ovelha e porco. Um novo ciclo reabre a ajuda imediatamente; o teto protegido do dono continua por animal/ciclo.

## Parâmetros provisórios

Escolhidos com economy_designer a partir do nono lote (nível 4, 4.000 moedas), cuidado vegetal (2 moedas/5 XP) e produção vegetal existente. Todos são configuráveis, sem flag própria; não representam balanceamento final.

| Parâmetro | Valor inicial |
| --- | --- |
| Cercado / galinha | 1.000 / 500 moedas comuns; nível 4 |
| Crescimento único / ciclo | 4 h / 12 h |
| Base / venda | 8 ovos / 20 moedas por ovo |
| Reserva | implementação inicial: máximo entre 4 e 75% da base, 6 no padrão; desde 18/set, ciclos novos usam 4 e preservam snapshots anteriores |
| Roubo agregado / individual | implementação inicial 2 / 1; desde 18/set, ciclos novos usam 4 / 1 |
| Limite pessoal de roubo | 1 unidade por visitante, animal e ciclo; sem cooldown compartilhado |
| XP coleta / roubo / compras e venda | 25 / 0 / 0 |
| Ajuda ao dono | +1 ovo protegido até +3 por ciclo |
| Pacote visitante | 2 moedas e 5 XP, integral |
| Quota global móvel de 24 h | 10 ajudas, 20 moedas, 50 XP |
| Quota por par móvel de 24 h | 2 ajudas, 4 moedas, 10 XP |

Na configuração inicial, a primeira produção ficava pronta após 16 h e cada coleta rendia ao dono de 6 a 11 ovos conforme roubo/ajuda, sem expiração. Desde 18/set, novos ciclos rendem de 4 a 11 antes de alterações futuras de balanceamento; ciclos já iniciados conservam seus valores. A renda animal complementa as plantas; valores vegetais atuais não são evidência de economia balanceada.

Monitorar renda por fonte, frequência de coleta, diversidade de ajudantes, concentração por pares, recusas por teto/quota e reserva efetiva. Não foi adicionada quota global de roubo fora do plano. O teto por ciclo limita emissão/retirada, mas não elimina abuso de redes ou contas alternativas.

## Ambiente isolado

Container exclusivo farmfriends-animal-tests-9cfb, PostgreSQL 16, porta local 55439, database farmfriends_animal_test, sem volume do banco do usuário. API de validação em porta separada quando iniciada. Nenhum reset de banco real, commit, push ou deploy autorizado.

## Evidência

**Status: implementado e validado; aguardando avaliação do usuário.** Nenhum commit, push, deploy ou reset do banco real foi realizado.

### Backend e banco

- Build da API e dos testes concluído.
- Suíte completa executada contra o PostgreSQL isolado: **244/244 testes aprovados, zero ignorados**. A suíte animal corresponde a **22/22** desses testes.
- Migration incremental `20260913101836_AddAnimalEnclosure` aplicada no banco isolado; o modelo EF não apresentou alterações pendentes.
- Cobertura executada inclui compra e replay concorrente, marco de nove lotes, crescimento, um ciclo aberto, ajuda/quota/headroom, bônus protegido, roubo/reserva, coleta concorrente, venda idempotente, conservação econômica e ordem de locks com PostgreSQL real.

### Frontend e integração com Phaser

- `npm test`: **113/113 aprovados**, zero ignorados.
- `npm run lint`: aprovado.
- `npm run build`: aprovado. Permanece apenas o aviso não bloqueante de chunks maiores que 500 kB.
- O refresh de carteira, inventário e XP usa respostas absolutas com guards de sessão, requisição e mutação. A recuperação de venda preserva a mesma chave e a mesma quantidade após resultado incerto.
- A cena reconcilia cercado, pintinho, galinha adulta, ninho, estados do ciclo e efeitos sociais a partir do snapshot autoritativo, inclusive depois de resize e reconstrução.

### Validação visual

As capturas em `imgs/animal-enclosure/evidence/` cobrem desktop, mobile, paisagem, nove e 28 lotes, quatro volumes futuros e a compra autenticada do cercado seguida da chegada do pintinho com nove lotes. Após a revisão visual, o cercado passou a usar um paralelogramo alinhado aos lotes, com a porteira embutida em um único lado reto em vez do ápice central em V. A revisão atual aprofundou a face frontal até junto dos lotes e manteve todos os seus postes e trilhos em uma única diagonal. O estilo `enclosure-wood-transparent.png` não contém piso ou gramado próprio: o terreno do cenário ativo aparece por transparência sob toda a estrutura. O cenário `farm-clearing-enclosure-v2.png` mantém livre toda a faixa entre o celeiro e a árvore amarela, com arbustos, flores e pedras realocados para o perímetro. A composição transparente foi inspecionada na fixture real em desktop e mobile com console limpo; as imagens anteriores na pasta de evidências registram as etapas que antecederam essa última troca de ativo. O primeiro teste de resize revelou deslocamento da câmera; a correção foi aplicada e revalidada.

O fluxo autenticado completo de adulto, ajuda, disputa de quota, roubo, coleta, venda e histórico de ajudantes não foi registrado manualmente no navegador. Essas regras foram exercitadas nos testes de integração com PostgreSQL e, quanto à apresentação, na fixture da cena. Antes de release público, é recomendável registrar esse roteiro autenticado ponta a ponta.

### Revisões finais

- `architecture_reviewer`: aprovado, sem achados abertos após as correções de sessão, ordem de locks e resposta atrasada entre fazendas.
- `qa_reviewer`: aprovado, sem achados funcionais bloqueadores.
- `gameplay_reviewer`: experiência aprovada. O único ajuste solicitado foi tornar condicional o texto da recompensa de ajuda quando quota, teto, ciclo ou participação a tornam indisponível; o texto foi corrigido e revalidado.
- `git diff --check`: aprovado.
- Varredura de `imgs/animal-enclosure/`: nenhuma senha, token ou string de conexão. Arquivos auxiliares com identidades descartáveis foram removidos do diretório de evidências.

### Interpretação da corrida de quota

Quota é o limite de recompensas sociais do visitante em uma janela de tempo; headroom é o espaço ainda disponível para aumentar o bônus protegido do dono naquele ciclo. Se duas ajudas disputam a última vaga de qualquer um desses limites, o backend decide tudo na mesma transação. A vencedora registra a ajuda, acrescenta o bônus do dono e paga moedas/XP ao visitante. A perdedora falha sem registrar ajuda, sem consumir quota, sem alterar o bônus e sem pagar recompensa. Um replay do sucesso devolve exatamente o resultado já confirmado, sem pagar novamente.
