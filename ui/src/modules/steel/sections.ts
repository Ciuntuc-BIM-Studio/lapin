/**
 * A section as the engine keeps it (`Section` in the structure module), drawn small: its outline and holes about its
 * box's centre, metres, as the engine builds them (a cold-formed sheet bent sharp, a CHS by 32 sides), and said in a few
 * words. The Steel tab's catalog cards and types dialog draw with it (decision 268).
 */

export interface SectionDto { kind?: string; width?: number; depth?: number; web?: number; flange?: number; lip?: number; name?: string }
export interface P { x: number; y: number }

const ring = (r: number, n = 32): P[] => Array.from({ length: n }, (_, i) => ({ x: r * Math.cos((2 * Math.PI * i) / n), y: r * Math.sin((2 * Math.PI * i) / n) }));

/** A cold-formed section's sheet as a line through its thickness's middle (a C open to the right, a Z's top flange to the right, a Σ's web folded in). */
export function centreline(s: SectionDto): P[] {
  const t = s.web ?? 0, w = s.width ?? 0, d = s.depth ?? 0, lip = s.lip ?? 0;
  const h = t / 2, x = w / 2 - h, y = d / 2 - h, l = lip > h ? d / 2 - lip : null;
  const o: P[] = [];
  if (s.kind === 'C' || s.kind === 'SIGMA') {
    if (l != null) o.push({ x, y: -l });
    o.push({ x, y: -y }, { x: -x, y: -y });
    if (s.kind === 'SIGMA') {
      const f = w / 4;
      o.push({ x: -x, y: -d / 4 - f / 2 }, { x: -x + f, y: -d / 4 + f / 2 }, { x: -x + f, y: d / 4 - f / 2 }, { x: -x, y: d / 4 + f / 2 });
    }
    o.push({ x: -x, y }, { x, y });
    if (l != null) o.push({ x, y: l });
  } else if (s.kind === 'Z') {
    if (l != null) o.push({ x: -x, y: -l });
    o.push({ x: -x, y: -y }, { x: 0, y: -y }, { x: 0, y }, { x, y });
    if (l != null) o.push({ x, y: l });
  }
  return o;
}

/** An open line made a sheet `t` thick about it: each side offset by half, the corners mitred, the ends square. */
export function sheet(line: P[], t: number): P[] {
  if (line.length < 2) return [];
  const unit = (a: P, b: P) => { const l = Math.hypot(b.x - a.x, b.y - a.y) || 1; return { x: (b.x - a.x) / l, y: (b.y - a.y) / l }; };
  const side = (off: number) => line.map((p, i) => {
    const din = i > 0 ? unit(line[i - 1], p) : unit(line[0], line[1]);
    const dout = i < line.length - 1 ? unit(p, line[i + 1]) : din;
    const nx = -din.y - dout.y, ny = din.x + dout.x, nl = Math.hypot(nx, ny) || 1;
    const cos = Math.max((nx / nl) * -din.y + (ny / nl) * din.x, 1e-6);
    return { x: p.x + (nx / nl) * (off / cos), y: p.y + (ny / nl) * (off / cos) };
  });
  return [...side(t / 2), ...side(-t / 2).reverse()];
}

/** The section's outline and holes about its box's centre. */
export function sectionShape(s: SectionDto): { outline: P[]; holes: P[][] } {
  const w = (s.width ?? 0) / 2, d = (s.depth ?? 0) / 2, tw = s.web ?? 0, tf = s.flange ?? 0;
  switch (s.kind) {
    case 'circle': return { outline: ring(w), holes: [] };
    case 'CHS': return { outline: ring(w), holes: tw > 0 && tw < w ? [ring(w - tw).reverse()] : [] };
    case 'RHS': return {
      outline: [{ x: -w, y: -d }, { x: w, y: -d }, { x: w, y: d }, { x: -w, y: d }],
      holes: tw > 0 && tw < w && tw < d ? [[{ x: -w + tw, y: -d + tw }, { x: -w + tw, y: d - tw }, { x: w - tw, y: d - tw }, { x: w - tw, y: -d + tw }]] : [],
    };
    case 'I': return { outline: [{ x: -w, y: -d }, { x: w, y: -d }, { x: w, y: -d + tf }, { x: tw / 2, y: -d + tf }, { x: tw / 2, y: d - tf }, { x: w, y: d - tf }, { x: w, y: d }, { x: -w, y: d }, { x: -w, y: d - tf }, { x: -tw / 2, y: d - tf }, { x: -tw / 2, y: -d + tf }, { x: -w, y: -d + tf }], holes: [] };
    case 'U': return { outline: [{ x: -w, y: -d }, { x: w, y: -d }, { x: w, y: -d + tf }, { x: -w + tw, y: -d + tf }, { x: -w + tw, y: d - tf }, { x: w, y: d - tf }, { x: w, y: d }, { x: -w, y: d }], holes: [] };
    case 'T': return { outline: [{ x: -tw / 2, y: -d }, { x: tw / 2, y: -d }, { x: tw / 2, y: d - tf }, { x: w, y: d - tf }, { x: w, y: d }, { x: -w, y: d }, { x: -w, y: d - tf }, { x: -tw / 2, y: d - tf }], holes: [] };
    case 'L': return { outline: [{ x: -w, y: -d }, { x: w, y: -d }, { x: w, y: -d + tf }, { x: -w + tw, y: -d + tf }, { x: -w + tw, y: d }, { x: -w, y: d }], holes: [] };
    case 'C': case 'Z': case 'SIGMA': return { outline: sheet(centreline(s), tw), holes: [] };
    default: return { outline: [{ x: -w, y: -d }, { x: w, y: -d }, { x: w, y: d }, { x: -w, y: d }], holes: [] };
  }
}

/** A loop's area (positive counter-clockwise). */
export const signedArea = (pts: P[]): number => pts.reduce((a, p, i) => { const q = pts[(i + 1) % pts.length]; return a + p.x * q.y - q.x * p.y; }, 0) / 2;

/** The section's area: its outline's less its holes' (a CHS's and a circle's exactly). */
export function sectionArea(s: SectionDto): number {
  const D = s.width ?? 0, t = s.web ?? 0;
  if (s.kind === 'circle') return (Math.PI * D * D) / 4;
  if (s.kind === 'CHS') return (Math.PI / 4) * (D * D - (D - 2 * t) * (D - 2 * t));
  const { outline, holes } = sectionShape(s);
  return Math.abs(signedArea(outline)) - holes.reduce((a, h) => a + Math.abs(signedArea(h)), 0);
}

/** How a list names it: its catalogue name, else its kind and size in mm. */
export function sectionLabel(s: SectionDto | undefined): string {
  if (!s) return '—';
  if (s.name) return s.name;
  const mm = (m: number | undefined) => `${Math.round((m ?? 0) * 10000) / 10}`;
  switch (s.kind) {
    case 'circle': return `Ø${mm(s.width)}`;
    case 'rectangle': return `${mm(s.width)}×${mm(s.depth)}`;
    case 'CHS': return `CHS ${mm(s.width)}×${mm(s.web)}`;
    case 'RHS': return `RHS ${mm(s.depth)}×${mm(s.width)}×${mm(s.web)}`;
    case 'SIGMA': return `Σ ${mm(s.depth)}×${mm(s.width)}×${mm(s.web)}`;
    default: return `${s.kind ?? '?'} ${mm(s.depth)}×${mm(s.width)}`;
  }
}

/** Steel's weight per metre, kg/m (7850 kg/m³). */
export const kgPerMetre = (s: SectionDto): number => sectionArea(s) * 7850;

/** An SVG path of the section, fitted in a box `size` wide, y up. */
export function sectionPath(s: SectionDto, size: number, pad = 2): string {
  const { outline, holes } = sectionShape(s);
  const all = [...outline, ...holes.flat()];
  if (!all.length) return '';
  const xs = all.map((p) => p.x), ys = all.map((p) => p.y);
  const span = Math.max(Math.max(...xs) - Math.min(...xs), Math.max(...ys) - Math.min(...ys)) || 1;
  const k = (size - 2 * pad) / span;
  const loop = (pts: P[]) => `M${pts.map((p) => `${(size / 2 + p.x * k).toFixed(2)},${(size / 2 - p.y * k).toFixed(2)}`).join('L')}Z`;
  return [outline, ...holes].map(loop).join('');
}
