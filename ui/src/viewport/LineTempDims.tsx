/**
 * The temporary dimensions walls, columns and beams share in plan (decision 733; the measuring in lineDims.ts): the storey's
 * lines from the engine (`view.dimLines`), the grid's axes, the choices made on each dimension's dots, and one component
 * that draws a host's dimensions with their values to type, their dots to cycle, and a right click on a dot for the dimension
 * reference's menu ("make this the default", or any of core faces, faces, axis, reference line: Preferences → Grid and snaps
 * keeps it). The tools' bars choose it too (`DimRefChoice`).
 */

import { useEffect, useRef, useState } from 'react';
import type { PlanOverlayProps } from '../platform/module';
import { execute, query, useSession } from '../engine/session';
import { hasModule } from '../platform/brand';
import { setPrefs, usePrefs } from '../app/prefs';
import { axesOf } from '../shell/gridAxes';
import { openContextMenu } from '../shell/ContextMenu';
import { t } from '../i18n';
import { Marks, ROW_PX, type Shown } from './TempDimMarks';
import { DIM_REFS, DIM_REF_LABELS, framePoint, refOfPlace, type AxisLine, type DimLineInfo, type DimPick, type DimRef, type Host, type LineDim } from './lineDims';

interface Answer {
  storeyId: string;
  lines: { id: string; name: string; kind: string; part?: string; start: [number, number]; end: [number, number]; faces: [number, number]; core: [number, number]; reference?: number; joins?: DimLineInfo['joins'] }[];
}

/** The storey's lines from the engine, asked again whenever the model changes; the last answer kept until the next. */
export function useDimLines(storeyId: string | null): DimLineInfo[] | null {
  const nodes = useSession((s) => s.nodes);
  const [lines, setLines] = useState<{ storeyId: string; lines: DimLineInfo[] } | null>(null);
  const asked = useRef(0);
  useEffect(() => {
    if (!storeyId) { setLines(null); return; }
    const n = ++asked.current;
    void query<Answer>('view.dimLines', { storeyId }).then((r) => {
      if (n !== asked.current || !r) return;
      setLines({
        storeyId: r.storeyId,
        lines: r.lines.map((w) => ({
          id: w.id, name: w.name, kind: w.kind, part: w.part, a: { x: w.start[0], y: w.start[1] }, b: { x: w.end[0], y: w.end[1] },
          faces: w.faces, core: w.core, reference: w.reference, joins: w.joins,
        })),
      });
    });
  }, [storeyId, nodes]);
  return lines && lines.storeyId === storeyId ? lines.lines : null;
}

/** The grid axes a plan of this storey shows: its grids' and those of no storey. */
export function useAxes(storeyId: string | null): AxisLine[] {
  const nodes = useSession((s) => s.nodes);
  const out: AxisLine[] = [];
  for (const n of nodes.values()) {
    if (n.type !== 'IfcGrid' || (n.storeyId && n.storeyId !== storeyId)) continue;
    for (const x of axesOf(n.props.axes)) out.push({ id: `${n.id}#${x.tag}`, name: x.tag, a: x.a, b: x.b });
  }
  return out;
}

/** Per element, what the user chose on its dimensions' dots; forgotten for another. */
export function usePicks(who: string): [Record<string, DimPick>, (key: string, p: DimPick) => void] {
  const [picks, setPicks] = useState<{ who: string; p: Record<string, DimPick> }>({ who, p: {} });
  const now = picks.who === who ? picks.p : {};
  return [now, (key, p) => setPicks({ who, p: { ...now, [key]: { ...now[key], ...p } } })];
}

/** The dimension reference the user keeps (Preferences → Grid and snaps). */
export const useDimRef = (): DimRef => usePrefs((s) => s.tempDimRef);
export const setDimRef = (r: DimRef) => setPrefs({ tempDimRef: r });

const DIM_REF_TITLE = 'Where temporary dimensions measure from and to: core face to core face, finish face to finish face, axis to axis, or from the element\'s reference line to the other\'s near face. Kept in Preferences → Grid and snaps.';

/** The dimension reference in a tool's bar (Wall, Column, Beam): kept for every temporary dimension. */
export function DimRefChoice() {
  const ref = useDimRef();
  return (
    <label className={`cad-layer${ref !== 'reference' ? ' on' : ''}`} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      <span>Temporary dimensions</span>
      <select aria-label="Temporary dimensions" data-testid="temp-dim-ref" value={ref} onChange={(e) => setDimRef(e.target.value as DimRef)} title={DIM_REF_TITLE}>
        {DIM_REFS.map((r) => <option key={r} value={r}>{DIM_REF_LABELS[r]}</option>)}
      </select>
    </label>
  );
}

/** One host's dimensions as the component draws them: its line, what it measured, and what a typed value does. */
export interface HostDims {
  host: Host;
  dims: LineDim[];
  /** The value typed, for a dimension: the command run (one undo step); null when nothing moves. */
  commit: (d: LineDim, value: number) => Promise<unknown>;
  valueTitle: (d: LineDim) => string;
  ownTitle: string;
}

/** The dimensions of one or more hosts (a column's two lines), with the dimension reference's menu on a dot's right click. */
export function TempDimSet({ testId, className, sets, live, drawing, pick, toScreen, scale, view }:
  { testId: string; className: string; sets: HostDims[]; live: boolean; drawing?: boolean; pick: (key: string, p: DimPick) => void } & Pick<PlanOverlayProps, 'toScreen' | 'scale' | 'view'>) {
  const current = useDimRef();
  // A dot's right click: this dimension's other end made the default for every one, or any of the four.
  const menu = (x: number, y: number, make: DimRef) => openContextMenu(x, y, [
    { label: 'Make this the default', hint: DIM_REF_LABELS[make], onSelect: () => setDimRef(make) },
    ...DIM_REFS.map((r) => ({ label: DIM_REF_LABELS[r], checked: r === current, onSelect: () => setDimRef(r) })),
  ]);
  const viewId = view.id ?? `plan:${view.storeyId}`;
  const items: Shown[] = [];
  for (const { host, dims, commit, valueTitle, ownTitle } of sets) {
    const world = (d: LineDim, s: number, out: number) => d.kind === 'across' ? framePoint(host, s, d.station + out / scale) : framePoint(host, host.faces[1] + out / scale, s);
    for (const d of dims) items.push({
      key: d.key, row: 0, a: d.a, b: d.b, pt: (s, out) => toScreen(world(d, s, out)), steps: 1, value: d.value,
      data: { ref: d.target.kind, what: d.options[d.opt].what, own: d.ownWhat, target: d.target.id },
      refTitle: `${t(d.target.kind === 'axis' ? 'The grid axis' : 'Click: the next reference (near face, far face, core faces, axis)')} · ${d.target.name} · ${t('Right click: make it the default')}`,
      cycleRef: live && d.options.length > 1 ? () => pick(d.key, { opt: d.opt + 1 }) : undefined,
      cycleOwn: live && d.kind === 'across' && d.ownOptions.length > 1 ? () => pick(d.key, { own: d.own + 1 }) : undefined,
      ownTitle, valueTitle: valueTitle(d),
      commit: (v) => commit(d, v),
      onMenu: live ? (p) => menu(p.x, p.y, refOfPlace(d.options[d.opt].what, d.ownWhat)) : undefined,
      permanent: live && hasModule('drafting') ? () => {
        const [a, b, at] = [world(d, d.a, 0), world(d, d.b, 0), world(d, (d.a + d.b) / 2, ROW_PX[0])];
        void execute('drafting.addDimension', { viewId, a: [a.x, a.y], b: [b.x, b.y], at: [at.x, at.y] });
      } : undefined,
    });
  }
  if (!items.length) return null;
  return (
    <g className={`temp-dims ${className}`} data-testid={testId} data-drawing={drawing ? 'true' : undefined}>
      <Marks items={items} live={live} />
    </g>
  );
}
