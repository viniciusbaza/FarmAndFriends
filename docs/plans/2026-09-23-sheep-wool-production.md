# Ovelha e produção de lã — ExecPlan

## Decisão consolidada

**Prototype**, solicitado para implementação como terceiro volume do cercado. A ovelha traz progressão visível e um retorno menos frequente, com uma tosquia de maior valor por coleta. A produção pronta espera o proprietário sem expirar. Uma fazenda pode ter uma ovelha, com um ciclo aberto próprio, independente de galinha e vaca.

Ovelha jovem (`lamb`) cresce uma vez. A adulta aparece com pelo curto enquanto produz e com lã farta quando o ciclo está pronto. Somente a **tosquia/coleta confirmada pelo backend** inicia o sucessor e devolve a aparência de pelo curto. Roubo parcial retira uma porção de lã, sem tosquiar o animal. A cena não mostra ninho, balde ou outro objeto secundário para a ovelha.

Visitas preservam ajuda durante a produção, com bônus protegido para o dono e crédito nominal ao visitante, e roubo limitado quando pronta. A lista de amigos não revela prontidão. O modal distingue ajudar, roubar lã e tosquiar. Sem reprodução, alimentação obrigatória, crafting, expiração nem acúmulo de ciclos.

## Economia provisória e configurável

| Parâmetro | Hipótese inicial |
| --- | ---: |
| Compra / nível | 1.500 moedas / 6 |
| Crescimento único | 8 h |
| Produção | 48 h |
| Base / preço de venda | 6 lãs / 100 moedas por lã |
| Reserva / roubo agregado / por visitante | 4 / 2 / 1 |
| Ajuda | +1 lã protegida, até +3; uma recompensa por visitante, ovelha e ciclo, independente das outras espécies |
| XP da tosquia / roubo | 40 / 0 |

Esses valores são um ponto de teste, não balanceamento final. Em 48 horas, o pico base é 600 moedas; duas coletas pontuais de vaca somam 640 moedas. Com roubo máximo, o dono conserva ao menos 4 lãs base; bônus de ajuda permanece intocável. Medir renda efetiva por espécie, atraso entre prontidão e coleta, concentração de roubos por par, taxa de ajuda e retorno após ausência antes de fixar números. O incentivo para roubar de muitas fazendas é risco residual a acompanhar sem criar limite novo nesta extensão.

## Contratos e arquitetura

Reutilizar o modelo, transações, snapshots, idempotência e endpoints do cercado. IDs novos: espécie `sheep`, estágio jovem `lamb`, produto `wool`. O backend define preço, duração, yield, reserva, permissões, XP e venda. React mantém estado/ações e aplica respostas confirmadas. Phaser recebe snapshots e controla apenas a aparência e o clique. A prontidão visual deriva de `cycle.isReady`; o campo não prova permissão de roubo.

O schema atual armazena espécies/produtos como strings e já garante unicidade por espécie/fazenda e um ciclo aberto por animal. Nenhuma migration é prevista. Os snapshots de galinha e vaca permanecem legíveis e imutáveis. O deploy do backend deve reconhecer `sheep/wool` antes da disponibilização da compra no cliente; após compras persistidas, rollback para backend que desconhece a espécie não é seguro.

## Execução

1. Backend: acrescentar catálogo, opções validadas e política por espécie; cobrir compra, crescimento, ciclo, ajuda, roubo, tosquia, venda, replay e concorrência em PostgreSQL isolado.
2. React: acrescentar nomes, estágio, produto, loja, inventário, venda, modal e feedback específicos; usar somente permissões e quantidades do servidor.
3. Phaser: criar atlas RGBA de adulta com pelagem curta/pronta e filhote, registrar texturas/pivôs, ocupar terceiro setor do cercado sem objeto secundário e sincronizar de modo idempotente.
4. Integrar e revisar: documentar eventos/contratos, executar testes backend/frontend e observar cena real em desktop/mobile, fazenda própria/visita e movimento reduzido. Fixture visual não substitui fluxo autenticado de compra até venda.

## Critérios de aceite

- Compra única de ovelha e ciclos de três espécies independentes; ausência não destrói lã pronta.
- Lã e bônus protegido conservados sob roubo e concorrência; replay não duplica estoque, moeda ou XP.
- Tosquia do dono inicia novo ciclo e mostra pelo curto apenas após confirmação; roubo parcial mantém lã farta.
- Loja, modal, histórico, inventário e venda nomeiam ovelha/lã e distinguem tosquia de roubo.
- Cena mostra filhote, adulta curta e adulta lanosa no mesmo cercado, sem objeto secundário da ovelha; sync repetido não duplica objetos.
- Testes PostgreSQL realmente executados e validação visual observada têm evidência própria.

## Evidência de implementação — 23/set/2026

- Backend Release compilado sem avisos/erros. As migrations existentes foram aplicadas em PostgreSQL 16 descartável e isolado; `dotnet ef migrations has-pending-model-changes` não encontrou drift. Suíte backend completa: **260/260 testes aprovados, nenhum ignorado**. Cinco testes focados da ovelha cobrem compra concorrente, snapshot, coexistência das três espécies, reserva, ajuda, roubo, replay, tosa e venda.
- Frontend: **128/128 testes**, lint e build aprovados. O Vite mantém o aviso não bloqueante de chunk acima de 500 kB.
- Fixture da cena real observada em desktop e viewport estreito (319×819), própria e visita, movimento normal e reduzido. A ovelha lanosa, a de pelo curto e o cordeiro ficaram visíveis no terceiro setor, sem produto secundário. Toque na ovelha em mobile/visita enviou `animal:select: sheep`. Tosquia da fixture exibiu `Tosquia` e mudou para pelo curto; sincronização ×5 manteve **100 → 100** objetos.
- A fixture usa snapshots controlados. O roteiro autenticado no navegador (compra → crescimento → ajuda/roubo → tosa → venda) ainda não foi executado; é a verificação restante antes de release público.
