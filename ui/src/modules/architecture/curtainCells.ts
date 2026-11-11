/** A curtain panel type as the engine keeps it (report #133, CurtainPanelSpec): what fills a curtain wall's cell. */
export interface PanelSpec {
  kind: 'glass' | 'solid' | 'framed' | 'empty';
  thickness: number;
  offset: number;
  frameWidth?: number;
  frameDepth?: number;
  inset?: number;
  infill?: 'glass' | 'solid';
  material?: string | null;
  frameMaterial?: string | null;
}

export const PANEL_KINDS: { id: PanelSpec['kind']; label: string }[] = [
  { id: 'glass', label: 'Glass' },
  { id: 'solid', label: 'Solid' },
  { id: 'framed', label: 'Framed infill' },
  { id: 'empty', label: 'Open (no panel)' },
];

/** What to send for a panel type: its kind's own fields only, a material only where one is chosen. */
export function panelJson(p: PanelSpec): Record<string, unknown> {
  const o: Record<string, unknown> = { kind: p.kind };
  if (p.kind === 'empty') return o;
  o.thickness = p.thickness;
  o.offset = p.offset;
  if (p.kind === 'framed') {
    o.frameWidth = p.frameWidth ?? 0.05;
    o.frameDepth = p.frameDepth ?? 0.06;
    o.inset = p.inset ?? 0.01;
    o.infill = p.infill ?? 'glass';
    o.frameMaterial = p.frameMaterial || null;
  }
  o.material = p.material || null;
  return o;
}

/** The keys a reset sends: the cells chosen, and every cell's setting too when all of them are. */
export function clearKeys(keys: readonly string[], chosen: readonly string[]): string[] {
  return [...new Set([...keys, ...chosen])];
}
