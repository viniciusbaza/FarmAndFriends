export type ReadTicket = { session: number; requestId: number; mutationEpoch: number }

/** A confirmed command invalidates every earlier read, even before a new GET starts. */
export class AuthoritativeReadGate {
  private requestId = 0
  private mutationEpoch = 0
  begin(session: number): ReadTicket {
    return { session, requestId: ++this.requestId, mutationEpoch: this.mutationEpoch }
  }
  invalidate() { this.mutationEpoch += 1 }
  accepts(ticket: ReadTicket, session: number) {
    return ticket.session === session && ticket.requestId === this.requestId &&
      ticket.mutationEpoch === this.mutationEpoch
  }
}
