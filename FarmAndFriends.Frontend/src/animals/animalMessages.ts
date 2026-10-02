const reasons: Record<string, string> = {
  FIRST_EXPANSION_REQUIRED: 'Complete os 9 lotes iniciais para alcançar a primeira expansão.',
  LEVEL_REQUIRED: 'Alcance o nível indicado para comprar.',
  ALREADY_OWNED: 'Você já comprou este item.',
  INSUFFICIENT_FUNDS: 'Você precisa de mais moedas.',
  ENCLOSURE_REQUIRED: 'Compre o cercado antes de receber animais.',
  NOT_READY: 'A produção ainda está em andamento.',
  NOT_PRODUCING: 'O animal precisa estar produzindo para receber ajuda.',
  HELP_ALREADY_REGISTERED: 'Você já ajudou neste ciclo. Seu cuidado ficou registrado!',
  REWARD_QUOTA_EXHAUSTED: 'Suas recompensas de ajuda estão esgotadas por enquanto.',
  OWNER_BONUS_LIMIT_REACHED: 'O bônus desta produção está completo. Novas ajudas estarão disponíveis no próximo ciclo.',
  THEFT_ALREADY_REGISTERED: 'Você já roubou deste animal neste ciclo.',
  THEFT_LIMIT_REACHED: 'A parte disponível para roubo já acabou.',
  NOT_OWNER: 'Esta ação é reservada ao dono da fazenda.',
  SELF_ACTION: 'Visite um amigo para realizar esta ação.',
  NOT_FRIENDS: 'Esta ação está disponível entre amigos.',
  UNSUPPORTED_FARM_LAYOUT: 'Não foi possível posicionar o cercado nesta fazenda.',
}
export function animalReason(reason: string | null) {
  return reason ? reasons[reason] ?? 'Esta ação não está disponível neste momento.' : null
}
