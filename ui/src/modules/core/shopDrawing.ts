/**
 * The shop drawing dialog's pure parts (docs/plan-shop-drawings.md §6, decision 581): the choices, their defaults from the
 * element's rule, the sections' stations kept inside the element's frame, and the arguments `drawing.preview` and
 * `drawing.create` take. The engine decides everything else (the paper, the scale, whether it fits).
 */

export const FACES = ['front', 'back', 'top', 'bottom', 'left', 'right'] as const;
export type Face = (typeof FACES)[number];

export const SECTION_ROLES = ['section:x', 'section:y', 'section:z'] as const;
export type SectionRole = (typeof SECTION_ROLES)[number];

export const SCALES = [1, 2, 5, 10, 15, 20, 25, 50, 100] as const;
export const PAPERS = ['A4', 'A3', 'A2', 'A1', 'A0'] as const;
export type Projection = 'iso-e' | 'third-angle';
export type Dims = 'overall' | 'none';
export type Prefer = 'paper' | 'scale';

export interface SectionChoice { role: SectionRole; station: number }

export interface RuleDto { id: string; name: string; views: string[]; iso: boolean; dims: string; sections: { role: string; station: number }[] }

/** What `drawing.rules` answers. The frame's min and max are in the element's own frame (metres). */
export interface RulesDto {
  rule: string; rules: RuleDto[]; drawing: string | null; projection: Projection;
  frame?: { length: number; width: number; height: number; min: [number, number, number]; max: [number, number, number] } | null;
}
export type Frame = NonNullable<RulesDto['frame']>;

export interface Choices {
  rule: string; views: Face[]; iso: boolean; sections: SectionChoice[];
  /** 1:N, or null for the engine's choice. */
  scale: number | null;
  paper: string | null;
  projection: Projection; dims: Dims; prefer: Prefer;
}

const isFace = (v: string): v is Face => (FACES as readonly string[]).includes(v);
const isRole = (v: string): v is SectionRole => (SECTION_ROLES as readonly string[]).includes(v);

/** The axis of a section's role: 0 x (along the part), 1 y (across), 2 z (up). */
export const axisOf = (role: SectionRole): 0 | 1 | 2 => (role === 'section:x' ? 0 : role === 'section:y' ? 1 : 2);

/** The stations a section on an axis may take: the frame's box along it (a plain 0 to the size when the box is not given). */
export function axisRange(frame: Frame | null | undefined, role: SectionRole): [number, number] {
  if (!frame) return [0, 1];
  const a = axisOf(role);
  const size = [frame.length, frame.width, frame.height][a];
  const lo = frame.min?.[a] ?? 0;
  const hi = frame.max?.[a] ?? lo + size;
  return hi > lo ? [lo, hi] : [lo, lo + Math.max(size, 0)];
}

/** A station kept inside its axis (and not a number that is not one). */
export function clampStation(frame: Frame | null | undefined, role: SectionRole, station: number): number {
  const [lo, hi] = axisRange(frame, role);
  return Number.isFinite(station) ? Math.min(hi, Math.max(lo, station)) : (lo + hi) / 2;
}

/** A new section on an axis: in the middle of the part. */
export const newSection = (frame: Frame | null | undefined, role: SectionRole): SectionChoice => ({ role, station: clampStation(frame, role, (axisRange(frame, role)[0] + axisRange(frame, role)[1]) / 2) });

/** The choices a rule makes (its views, its iso, its dimensions, its sections), the project's projection, paper and scale left to the engine. */
export function defaultChoices(r: RulesDto, ruleId: string = r.rule, previous?: Pick<Choices, 'scale' | 'paper' | 'projection' | 'dims' | 'prefer'>): Choices {
  const rule = r.rules.find((x) => x.id === ruleId) ?? r.rules[0];
  const sections = (rule?.sections ?? []).filter((s): s is { role: SectionRole; station: number } => isRole(s.role))
    .map((s) => ({ role: s.role, station: clampStation(r.frame, s.role, s.station) }));
  return {
    rule: rule?.id ?? ruleId,
    views: (rule?.views ?? []).filter(isFace),
    iso: rule?.iso ?? false,
    sections,
    scale: previous?.scale ?? null,
    paper: previous?.paper ?? null,
    projection: previous?.projection ?? r.projection,
    dims: previous?.dims ?? (rule?.dims === 'none' ? 'none' : 'overall'),
    prefer: previous?.prefer ?? 'paper',
  };
}

/** The arguments of `drawing.preview` and `drawing.create`: the ticked faces in their usual order, the sections, null for what the engine chooses. */
export function drawingArgs(element: string, c: Choices, existing?: string | null): Record<string, unknown> {
  const args: Record<string, unknown> = {
    element, rule: c.rule,
    views: FACES.filter((f) => c.views.includes(f)),
    sections: c.sections.map((s) => ({ role: s.role, station: s.station })),
    iso: c.iso, scale: c.scale, paper: c.paper, projection: c.projection, dims: c.dims, prefer: c.prefer,
  };
  if (existing) args.id = existing;
  return args;
}

/** A length in millimetres, as the slider's value shows it. */
export const mm = (metres: number): string => `${Math.round(metres * 1000)} mm`;

/** True when nothing is ticked: there would be no view to put on the sheet. */
export const nothingChosen = (c: Choices): boolean => !c.views.length && !c.iso && !c.sections.length;

/** The element classes a shop drawing is not of: drawings, sheets, annotations, groups and the spatial structure. */
const NOT_DRAWN = new Set(['LapinView', 'LapinSheet', 'LapinSchedule', 'LapinShopDrawing', 'IfcAnnotation', 'IfcGroup', 'IfcGrid', 'IfcBuildingStorey', 'IfcBuilding', 'IfcSite', 'IfcProject']);

/** The one element a shop drawing is made of: the selection when it is a single element of the model. */
export function drawableElement(selected: string[], nodes: Map<string, { type: string }>): string | null {
  if (selected.length !== 1) return null;
  const n = nodes.get(selected[0]);
  return n && !NOT_DRAWN.has(n.type) ? selected[0] : null;
}
