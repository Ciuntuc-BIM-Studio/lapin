/**
 * The steel connections' own logic (decision 273), kept apart from their panel so it is tested: which parameters a kind
 * of connection shows, the bolts' sizes offered, and its parts and welds as the panel lists them.
 */

export type NodeKind = 'base' | 'eaves' | 'apex' | 'splice' | 'post';

/** A connection's parameters as the engine keeps them (each given, or left to the profiles: `NodeParams`). */
export interface NodeParamsDto {
  plate?: number; bolt?: number; grade?: string; rows?: number; cols?: number; edge?: number; pitch?: number; gauge?: number;
  extension?: number; stiffeners?: boolean; base?: 'pinned' | 'fixed'; anchor?: 'straight' | 'hooked' | 'plate'; anchors?: number;
  anchorLength?: number; grout?: number; gap?: number;
}

/** What `steel.node` says of a connection. */
export interface NodeInfo {
  kind: NodeKind; label: string;
  members: { id: string; end?: 'start' | 'end' | null; name?: string }[];
  params: NodeParamsDto;
  spec: Required<NodeParamsDto>;
  parts: { part: string; size: string; grade: string; count: number }[];
  welds: { throat: number; length: number; count: number }[];
  weight: number; own: boolean; problems: string[];
  footing?: { id: string; name: string };
}

export type NodeField = keyof NodeParamsDto;

/** The parameters a kind of connection shows, in order (a base's ribs only when fixed). */
export function nodeFields(kind: NodeKind, base?: string): NodeField[] {
  switch (kind) {
    case 'base': return ['base', 'plate', 'anchor', 'anchors', 'bolt', 'grade', 'anchorLength', 'grout', 'edge', 'gauge', 'pitch', ...(base === 'fixed' ? ['stiffeners' as const] : [])];
    case 'eaves': return ['plate', 'bolt', 'grade', 'rows', 'cols', 'edge', 'pitch', 'gauge', 'extension', 'stiffeners'];
    case 'apex':
    case 'splice': return ['plate', 'bolt', 'grade', 'rows', 'cols', 'edge', 'pitch', 'gauge', 'extension'];
    case 'post': return ['plate', 'bolt', 'grade', 'rows', 'edge', 'pitch', 'gap'];
  }
}

/** The bolts' and anchors' nominal diameters offered, metres. */
export const BOLT_SIZES = [0.012, 0.016, 0.02, 0.024, 0.027, 0.03, 0.036];
export const GRADES = ['4.6', '5.6', '8.8', '10.9'];

/** A diameter as a bolt is named: M20. */
export const boltLabel = (d: number) => `M${Math.round(d * 1000)}`;

/** The anchors a base takes: two or four pinned, four to eight fixed. */
export const anchorCounts = (base: string | undefined) => (base === 'fixed' ? [4, 6, 8] : [2, 4]);

/** The parts as the panel lists them: "4 × Bolt M20×60 8.8", plates first, then bolts, anchors and their plates. */
export function partsRows(parts: NodeInfo['parts']): { part: string; text: string; count: number }[] {
  const order = ['Plate', 'Bolt', 'Anchor', 'Anchor plate'];
  return [...parts]
    .sort((a, b) => order.indexOf(a.part) - order.indexOf(b.part) || a.size.localeCompare(b.size))
    .map((p) => ({ part: p.part, text: `${p.size}${p.grade ? ` · ${p.grade}` : ''}`, count: p.count }));
}

/** The welds by throat: "a5 · 3.20 m". */
export const weldsText = (welds: NodeInfo['welds']) =>
  welds.map((w) => `a${Math.round(w.throat * 1000)} · ${w.length.toFixed(2)} m`).join(', ');
