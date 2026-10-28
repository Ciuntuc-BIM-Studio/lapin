/**
 * The network as the interface reads it: the engine's catalogs (roles,
 * services, materials, templates), its queries (systems, network, open
 * ports), and what a tool needs from them — the system a role goes in, the
 * port or run under a point.
 */

import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import type { Vec2 } from '../../platform/module';
import { setMep, useMep } from './prefs';

export const SYSTEM_CLASS = 'IfcDistributionSystem';
export const RUN_CLASSES = ['IfcPipeSegment', 'IfcDuctSegment', 'IfcCableSegment'];
/** Every class a node or a run may be, for the tools that pick one and the sections that show one. */
export const PICKABLE = [...RUN_CLASSES, 'IfcBoiler', 'IfcSpaceHeater', 'IfcPipeFitting', 'IfcFlowMeter', 'IfcValve', 'IfcSanitaryTerminal', 'IfcElectricDistributionBoard', 'IfcOutlet', 'IfcLightFixture', 'IfcSwitchingDevice', 'IfcFan', 'IfcAirTerminal'];

export interface RoleInfo {
  id: string; label: string; ifcClass: string; kind: 'source' | 'endpoint' | 'inline' | 'rise'; symbol: string;
  services: string[]; ports: { key: string; service: string; flow: 'in' | 'out' | 'both'; pair: string | null }[];
  size: [number, number, number]; elevation: number; duty: number | null; dutyUnit: string | null;
}
export interface ServiceInfo { id: string; label: string; defaults: Defaults }
export interface Defaults {
  size: number; plane: 'floor' | 'ceiling' | 'wall'; floorElevation: number; ceilingElevation: number; wallElevation: number;
  slope: number; pair: boolean; separation: number; material: string | null; insulation: number; bendRadius: number;
}
export interface SystemInfo { id: string; name: string; service: string; defaults: Defaults; nodes: number; runs: number }
export type RunEnd = { nodeId: string; port: string } | { runId: string; station: number } | null;
export interface NetworkPort { key: string; service: string; flow: string; at: [number, number, number]; taken: boolean }
export interface NetworkNode { id: string; name: string; role: string; storeyId: string | null; ports: NetworkPort[] }
export interface NetworkRun { id: string; name: string; storeyId: string | null; points: [number, number][]; start: RunEnd; end: RunEnd }
export interface NetworkSystem { id: string; name: string; service: string; defaults: Defaults; nodes: NetworkNode[]; runs: NetworkRun[] }
export type OpenItem =
  | { kind: 'port'; id: string; name: string; port: string; at: [number, number, number] }
  | { kind: 'run'; id: string; name: string; end: 'start' | 'end'; at: [number, number] };
/** A material runs are made of (the engine's RunMaterials): its laying rules, when the product has them, come from mep-pro. */
export interface MaterialInfo { id: string; label: string; services: string[]; aliases: string[] }

const catalog = <T,>(name: string): T[] => (useSession.getState().info?.catalogs[`mep.${name}`] as unknown as T[] | undefined) ?? [];
export const roles = (): RoleInfo[] => catalog<RoleInfo>('roles');
export const templates = (): { service: string; source: string; endpoint: string; endpoints: string[] }[] => catalog('templates');
export const services = (): ServiceInfo[] => catalog<ServiceInfo>('services');
export const materials = (): MaterialInfo[] => catalog<MaterialInfo>('materials');
export const materialsFor = (service: string): MaterialInfo[] => materials().filter((m) => m.services.includes(service));
export const roleOf = (node: NodeDto): RoleInfo | null => roles().find((r) => r.id === node.props.role) ?? null;
export const serviceLabel = (id: string): string => services().find((s) => s.id === id)?.label ?? id;

/** A material by id or old name, for its service (else the service's first). */
export function materialOf(service: string, id: string | null | undefined): MaterialInfo | null {
  const all = materials();
  const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();
  const m = id ? all.find((x) => same(x.id, id) || x.aliases.some((a) => same(a, id))) : undefined;
  return m && m.services.includes(service) ? m : m ?? all.find((x) => x.services.includes(service)) ?? null;
}

export const isSystem = (n: NodeDto): boolean => n.type === SYSTEM_CLASS;
export const isRun = (n: NodeDto): boolean => RUN_CLASSES.includes(n.type) && Array.isArray(n.props.points) && typeof n.props.systemId === 'string';
export const isNode = (n: NodeDto): boolean => typeof n.props.role === 'string' && typeof n.props.systemId === 'string' && !isRun(n);
export const systemsOf = (nodes: Map<string, NodeDto>): NodeDto[] => [...nodes.values()].filter(isSystem).sort((a, b) => a.name.localeCompare(b.name));
export const runPoints = (n: NodeDto): Vec2[] =>
  ((n.props.points as unknown as number[][] | undefined) ?? []).filter((p) => Array.isArray(p) && p.length >= 2).map((p) => ({ x: Number(p[0]), y: Number(p[1]) }));

export const systemsQuery = () => query<SystemInfo[]>('mep.systems');
export const networkQuery = (systemId?: string) => query<NetworkSystem[]>('mep.network', systemId ? { systemId } : {});
export const openPortsQuery = (systemId?: string) => query<OpenItem[]>('mep.openPorts', systemId ? { systemId } : {});
export const fittingsQuery = (systemId: string) => query<{ bends: number; tees: number }>('mep.fittings', { systemId });

/**
 * The system a tool works in, for the services a role takes ("*": any): the
 * current one when it fits, else the first that does, else a new one of the
 * first service named (a run or an any-service fitting with no system at all
 * gets a heating system).
 */
export async function systemFor(wanted: string[]): Promise<string | null> {
  const nodes = useSession.getState().nodes;
  const all = systemsOf(nodes);
  const takes = (s: NodeDto) => wanted.includes('*') || wanted.includes(String(s.props.service));
  const cur = useMep.getState().systemId;
  const current = cur ? nodes.get(cur) : undefined;
  if (current && takes(current)) return current.id;
  const first = all.find(takes);
  if (first) { setMep({ systemId: first.id }); return first.id; }
  const service = wanted.find((w) => w !== '*') ?? 'heating';
  const r = await execute('mep.createSystem', { service }) as { id: string } | null;
  if (r?.id) setMep({ systemId: r.id });
  return r?.id ?? null;
}

/** The service and material of a system, as a new run takes them: its own material, else its service's default (the engine's SystemDefaults). */
export function systemMaterial(systemId: string | null): { service: string; material: string | null } | null {
  const system = systemId ? useSession.getState().nodes.get(systemId) : undefined;
  if (!system) return null;
  const service = String(system.props.service);
  const defaults = system.props.defaults as { material?: string } | undefined;
  return { service, material: defaults?.material ?? services().find((x) => x.id === service)?.defaults.material ?? null };
}

export const dist = (a: Vec2, b: Vec2): number => Math.hypot(a.x - b.x, a.y - b.y);

function distToSegment(p: Vec2, a: Vec2, b: Vec2): number {
  const dx = b.x - a.x, dy = b.y - a.y;
  const l2 = dx * dx + dy * dy;
  const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
  return dist(p, { x: a.x + t * dx, y: a.y + t * dy });
}

/** The run of a system whose centreline passes within `tol` of a point (the nearest). */
export function nearestRun(runs: NetworkRun[], p: Vec2, tol: number, except?: string): NetworkRun | null {
  let best: NetworkRun | null = null, bd = tol;
  for (const r of runs) {
    if (r.id === except) continue;
    const pts = r.points.map(([x, y]) => ({ x, y }));
    for (let i = 0; i + 1 < pts.length; i++) {
      const d = distToSegment(p, pts[i], pts[i + 1]);
      if (d < bd) { bd = d; best = r; }
    }
  }
  return best;
}

/**
 * What a run's end at a point is on: an open port within `tol` ({nodeId,
 * port}), else a run of the system passing there ({runId, at}), else
 * nothing (the end stays open).
 */
export async function endSpec(systemId: string, p: Vec2, tol = 0.3, except?: string): Promise<Record<string, unknown> | undefined> {
  const open = (await openPortsQuery(systemId)) ?? [];
  let best: OpenItem | null = null, bd = tol;
  for (const o of open) {
    if (o.kind !== 'port') continue;
    const d = dist(p, { x: o.at[0], y: o.at[1] });
    if (d < bd) { bd = d; best = o; }
  }
  if (best && best.kind === 'port') return { nodeId: best.id, port: best.port };
  const net = (await networkQuery(systemId))?.[0];
  const run = net ? nearestRun(net.runs, p, tol, except) : null;
  return run ? { runId: run.id, at: [p.x, p.y] } : undefined;
}

/** The end of a run nearer to a point. */
export function nearerEnd(run: NodeDto, p: Vec2): 'start' | 'end' {
  const pts = runPoints(run);
  if (pts.length < 2) return 'end';
  return dist(p, pts[0]) <= dist(p, pts[pts.length - 1]) ? 'start' : 'end';
}

/** A run end as the inspector says it: the node and port, the run and station, or open. */
export function describeEnd(end: RunEnd, nodes: Map<string, NodeDto>): string {
  if (!end) return 'open';
  if ('nodeId' in end) return `${nodes.get(end.nodeId)?.name ?? '?'} · ${end.port}`;
  return `${nodes.get(end.runId)?.name ?? '?'} at ${end.station.toFixed(2)} m`;
}
