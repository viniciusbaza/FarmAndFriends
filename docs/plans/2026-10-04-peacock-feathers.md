# Pavão e penas — ExecPlan

Este plano registra o incremento do pavão. A feature Cercado de Animais permanece em desenvolvimento e ainda terá testes adicionais e análise de gameplay antes de ser considerada finalizada.

## Decisão consolidada e autorização

Prototype aprovado pelo usuário em 4/out/2026, incluindo os assets apresentados
em `imgs/animal-enclosure/2026-10-02-peacock-v1/` e a implementação do quinto
animal no centro do cercado. Os pareceres de direção, social, economia e
planejamento técnico foram consolidados antes das alterações.

O pavão oferece progressão visual e um retorno menos frequente para uma coleta
maior. O proprietário preserva produção pronta sem expiração; visitantes
descobrem o estado ao visitar, ajudam durante produção e podem roubar uma
parcela limitada. A lista de amigos não revela prontidão. Sem reprodução,
alimentação obrigatória, variantes raras ou novos sistemas.

## Regras e configuração inicial

- IDs aditivos: `peacock`, estágio jovem `peachick`, produto vendável `feather`.
- Compra 2.500 moedas / nível 8; crescimento único de 8 h; produção de 72 h.
- Base 6 penas a 150 moedas/pena; coleta base vale 900 moedas, média pontual
  de 300 moedas/dia, como a ovelha.
- Reserva absoluta 4, fração 50%; roubo agregado 2, individual 1; roubo 0 XP.
- Ajuda +1 pena protegida até +3 por ciclo; recompensa 2 moedas e 5 XP;
  coleta 40 XP. Todos os valores são hipóteses configuráveis no servidor.
- Uma ajuda por visitante, animal e ciclo. Cinco animais elegíveis criam cinco
  oportunidades, sem quota compartilhada com outros animais ou plantações.
  Jovens, prontos, bônus cheios e ajuda já registrada continuam inelegíveis.
- Prontidão vem de `cycle.isReady`. Adulto produzindo tem cauda fechada;
  pronto tem leque aberto; coleta confirmada inicia sucessor e fecha o leque;
  roubo parcial mantém o estado pronto. Sem pena solta no mundo.

## Execução e responsabilidades

1. Backend: catálogo/política validada, preço e venda explícitos, snapshot de
   política, nomes/notificações e privacidade. Reutilizar transações, locks,
   idempotência e autorização existentes. Nenhuma migration prevista.
2. React: estágio, nomes, ícones, loja, inventário, venda, modal e feedback de
   pena; somente permissões e recompensas fornecidas pelo servidor.
3. Phaser: copiar atlas aprovados preservando fontes, registrar texturas,
   pivôs e escala fixa por idade; quinto setor central, alvos disjuntos,
   poses/movimento reduzido e reconciliação idempotente. Bridge existente:
   `animal:select`, `farm:sync`, `animal:{collect|help|steal}:done`, `ui:modal`.
4. Revisar integração/arquitetura/gameplay e executar validação abaixo.

## Critérios de aceite e evidência exigida

- Compra única/nível/preço, crescimento, ciclo/política congelada, coleta,
  venda, ajuda, roubo, reserva e bônus protegido, replay e concorrência.
- PostgreSQL isolado com migrations aplicadas: mesmo visitante ajuda os cinco
  animais; total 10 moedas/25 XP; repetir não credita nem acrescenta bônus;
  um animal bloqueado não impede os outros.
- Filhote/adulto/pronto visíveis com os outros quatro animais em desktop e
  mobile. Alvos separados, movimento reduzido, input bloqueado por modal,
  sync repetido sem duplicação, coleta/roubo refletidos corretamente.
- Testes backend/frontend, lint, build e revisão do diff. Distinguir fixture
  sintética de fluxo autenticado e registrar qualquer lacuna real.

## Rollout e acompanhamento

Disponibilizar o cliente que reconhece o pavão junto do catálogo backend;
cliente antigo ignora espécie desconhecida. Após compras persistidas, rollback
para backend antigo não é seguro. Monitorar renda por espécie, atraso da coleta,
ajuda por visitante/fazenda, concentração por pares e reserva preservada sob
roubo; não criar quota social adicional sem evidência de abuso.

## Progresso

- [x] Aprovação visual e autorização de implementação.
- [x] Pareceres consolidados e contratos estabilizados.
- [x] Backend, React e Phaser implementados.
- [x] PostgreSQL, frontend e revisão executados.
- [x] Cena real e fluxo autenticado observados.

## Resultado e evidências de validação

- Backend: 291/291 testes, sem skips, PostgreSQL 16 descartável com migrations
  aplicadas; nenhuma mudança de modelo pendente. Compra, maturidade, snapshots,
  reserva, ajuda, roubo, coleta/venda, concorrência e replay cobertos.
- Frontend: 152/152 na execução final, incluindo regressão da câmera, sem skips;
  lint e build aprovados.
- Chrome headless local, aplicação autenticada real em 1280×800 e 390×844:
  seleção dos cinco animais pelo canvas abriu os modais corretos, incluindo
  eventos de toque via touchscreen no celular. Visitante
  ajudou os cinco por HTTP 200, saldo 100→110 e XP 0→25; nenhuma quota global.
- Prontidão simulada somente no banco descartável: roubo de 1 pena manteve
  leque; dono coletou 6 penas (base restante 5 + bônus protegido 1), fechou leque
  e vendeu 6 por 900 moedas através da loja no celular.
- Fixture da cena Phaser real: três estados em ambos os viewports, ida/retorno,
  sync ×5 mantendo 106→106 objetos, reconstrução mantendo 106→106, modal
  bloqueando seleção. Atlas runtime mantêm PNGs aprovados e transparência real;
  pivôs runtime estabilizados e espelhados na volta.
- A visita autenticada revelou evento de câmera durante reconstrução, antes
  da câmera/layout existirem. Corrigido com guarda do ciclo de vida e modo
  persistido no registry; reprodução final desktop/mobile sem pageerrors.
- A fixture é sintética; a validação de dono/visitante usou API e banco reais
  isolados, com capturas locais ignoradas pelo Git.
- Revisões QA, arquitetura e gameplay não encontraram falha nas regras.
  Melhoria estrutural opcional: reunir posições/alvos em um mapa por espécie
  quando o catálogo crescer novamente. Nenhuma dependência nova ou migration.

Não foram medidos ainda retorno de jogadores, abuso recorrente ou renda
observada em produção. A política econômica continua configurável e o
acompanhamento de rollout continua necessário. Nenhum commit/publicação efetuado.

## Correção de sincronização da compra

A compra aceita no React podia chegar antes de o listener Phaser estar pronto. `FarmSceneSync` retém o último snapshot aceito e reaplica `farm:sync` após `farm:scene:ready`; também envia novas props para cenas ativas. Fazenda ou modo incorretos são ignorados e há cleanup. A fixture com providers React, compra e Phaser reproduziu o caso durante preload em desktop e mobile.

## Arte final aprovada em 13/10/2026

A pasta `imgs/animal-enclosure/2026-10-02-peacock-v1/` é a única fonte visual mantida para o pavão. Ela conserva os PNGs originais e os JSONs com os pivôs aprovados no ciclo v6. O runtime usa cópias exatas desses pares. A/B alternam a cada 520 ms no mesmo lugar, sem patrulha ou espelhamento; movimento reduzido seleciona idle. A prontidão visual continua derivada do snapshot do servidor.
