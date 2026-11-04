/**
 * Gas and fuel oil (the tester's request #72, decision 285): what the
 * interface works out itself — which other systems a node can join (a
 * heating system's gas boiler to the gas installation), the arguments of a
 * gas main and of a service connection. The engine does the rest
 * (mep.mainAlong, mep.serviceConnection, mep.joinSystem).
 */

/** A port as the network query gives it. */
export interface PortLike { key: string; service: string; taken: boolean }
/** A system as the session holds it. */
export interface SystemLike { id: string; name: string; service: string }

/**
 * The systems a node may join besides its own: one per service it has an
 * open port for (a gas boiler in the heating system: its gas inlet), of the
 * systems of that service; none when every such port has a run on it.
 */
export function joinTargets(ports: PortLike[], ownService: string, systems: SystemLike[]): SystemLike[] {
  const open = new Set(ports.filter((p) => !p.taken && p.service !== '*' && p.service !== ownService).map((p) => p.service));
  return systems.filter((s) => open.has(s.service));
}

/** What a gas main is made with: along an element picked, or along the points drawn; its offset to the left of the line and its cover. */
export function mainArgs(on: { alongId: string } | { points: { x: number; y: number }[] }, offset: number, depth: number, storeyId?: string): Record<string, unknown> {
  const base: Record<string, unknown> = { offset, depth, ...(storeyId ? { storeyId } : {}) };
  return 'alongId' in on ? { ...base, alongId: on.alongId } : { ...base, points: on.points.map((p) => [p.x, p.y]) };
}
