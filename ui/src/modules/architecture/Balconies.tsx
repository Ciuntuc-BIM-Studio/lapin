/**
 * Balconies (a tester's report, #96): Architecture → Balcony (BY) clicked by a wall's face, in the style chosen in the
 * command line's bar (a built-in one — Juliet, cantilevered, stacked on posts, on brackets, hung, rounded, curved, loggia,
 * corner, mezzanine — or the project's own); Balcony by outline drawn; Gallery edge along a line. The configurator,
 * stacking on the storeys above, the schedule, a balcony's properties and its grips (width, depth, along its wall).
 */

import { Layers, Settings2, Table2 } from 'lucide-react';
import { BalconyIcon } from '../../icons';
import type { Grip, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { fromPairs } from '../../viewport/contour';
import { typeOf } from '../../engine/model';
import { outlineGrips, toPairs } from '../../viewport/gripKit';
import { t, useLang } from '../../i18n';

/** A built-in style as the engine's catalogue gives it. */
export type BalconyStyle = { id: string; label: string; description: string; kind: string; attrs: Record<string, unknown> };

export const BALCONY = 'LapinBalcony';
export const BALCONY_TYPE = 'LapinBalconyType';

export function useBalconyStyles(): { builtIn: BalconyStyle[]; own: NodeDto[] } {
  const info = useSession((s) => s.info);
  const nodes = useSession((s) => s.nodes);
  const builtIn = (info?.catalogs['architecture.balconyStyles'] as unknown as BalconyStyle[] | undefined) ?? [];
  // The project's own: its types not made from a built-in one (those are the built-in style itself).
  const own = [...nodes.values()].filter((n) => n.type === BALCONY_TYPE && !n.props.library);
  return { builtIn, own };
}

/** The style a balcony is of: its type's library key (a built-in), or its type (the project's), or none. */
export function styleOf(node: NodeDto): string {
  const type = typeOf(useSession.getState(), node.id);
  if (!type) return '';
  return typeof type.props.library === 'string' ? type.props.library : type.id;
}

/** The styles as options: the project's first, then the built-in ones. */
export function StyleOptions({ own, builtIn }: { own: NodeDto[]; builtIn: BalconyStyle[] }) {
  useLang((s) => s.lang);
  return (
    <>
      {own.length > 0 && <optgroup label={t('Project')}>{own.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}</optgroup>}
      <optgroup label={t('Built-in')}>{builtIn.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}</optgroup>
    </>
  );
}

/** The style the Balcony tools place, in the command line's bar. */
function BalconyStyleOption() {
  const style = useUi((s) => s.balconyStyle);
  const { builtIn, own } = useBalconyStyles();
  return (
    <select className="cad-layer" value={style ?? 'cantilever'} data-testid="balcony-style" title="The balcony's style: click by a wall's face to place it"
      onChange={(e) => useUi.setState({ balconyStyle: e.target.value })}>
      <StyleOptions own={own} builtIn={builtIn} />
    </select>
  );
}

const placed = (r: unknown) => {
  const id = (r as { id?: string } | null)?.id;
  if (id) useUi.setState({ selection: id, selected: [id] });
};

/** A balcony: a click by a wall's face (it hangs there, on the side clicked; a Juliet one on the door clicked by), else where clicked. */
export const balcony: ToolDef = {
  id: 'architecture.balcony', label: 'Balcony', icon: BalconyIcon, shortcut: 'BY',
  gesture: { kind: 'point' },
  options: BalconyStyleOption,
  complete: async ({ points: [p] }, ctx) => {
    const style = useUi.getState().balconyStyle ?? 'cantilever';
    if (style === 'mezzanine') { notify('A gallery edge is drawn along a line: Architecture → Gallery edge.'); return; }
    placed(await ctx.execute('architecture.addBalcony', { storeyId: ctx.storeyId, style, x: p.x, y: p.y }));
  },
};

/** A balcony drawn by its outline: its first edge lies against the building. */
export const balconyOutline: ToolDef = {
  id: 'architecture.balconyOutline', label: 'Balcony by outline', icon: BalconyIcon,
  gesture: { kind: 'polygon' },
  options: BalconyStyleOption,
  complete: async ({ points }, ctx) => {
    const style = useUi.getState().balconyStyle ?? 'cantilever';
    placed(await ctx.execute('architecture.addBalcony', { storeyId: ctx.storeyId, style: style === 'mezzanine' || style === 'loggia' || style === 'corner' ? 'cantilever' : style, contour: points.map((q) => [q.x, q.y]) }));
  },
};

/** A mezzanine's edge over a double-height space: two points along it, the void on the left, the gallery on the right. */
export const galleryEdge: ToolDef = {
  id: 'architecture.galleryEdge', label: 'Gallery edge', icon: BalconyIcon,
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [a, b] }, ctx) => {
    const style = useUi.getState().balconyStyle;
    const own = style && useSession.getState().nodes.get(style)?.props.kind === 'mezzanine' ? style : 'mezzanine';
    placed(await ctx.execute('architecture.addBalcony', { storeyId: ctx.storeyId, style: own, x1: a.x, y1: a.y, x2: b.x, y2: b.y }));
  },
};

const selectedBalconies = () => {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => nodes.get(id)?.type === BALCONY);
};

/** The configurator on the balcony selected. */
export const balconyConfigurator: ToolDef = {
  id: 'architecture.balconyConfigurator', label: 'Balcony configurator', icon: Settings2,
  run: () => {
    const [id] = selectedBalconies();
    if (!id) { notify('Select a balcony first, then open the balcony configurator.'); return; }
    useUi.setState({ dialog: 'balcony', balconyId: id });
  },
};

/** The balconies selected, copied onto every storey above theirs (Copy to storeys), one step. */
export const stackBalconies: ToolDef = {
  id: 'architecture.stackBalconies', label: 'Stack on storeys above', icon: Layers,
  run: () => {
    const ids = selectedBalconies();
    if (!ids.length) { notify('Select the balconies to stack on the storeys above.'); return; }
    void execute('architecture.stackBalcony', { ids }).then((r) => {
      const n = ((r as { ids?: string[] } | null)?.ids ?? []).length;
      if (!useSession.getState().error) notify(n ? 'The balconies are copied onto the storeys above.' : 'The storeys above already have these balconies.');
    });
  },
};

export const balconySchedule: ToolDef = {
  id: 'architecture.balconySchedule', label: 'Balcony schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'balconies' }).then((r) => { if (r) notify('Balcony schedule made: it is in the project browser, under Schedules.'); }),
};

/** A balcony in the inspector: its style (the fields below are its own), the configurator and stacking. */
export function BalconySection({ node }: { node: NodeDto }) {
  const { builtIn, own } = useBalconyStyles();
  useSession((s) => s.relations);
  const current = styleOf(node);
  return (
    <div className="layers" data-testid="balcony-section">
      <label className="type-pick">
        <span className="eyebrow">Style</span>
        <span className="row">
          <select value={current} disabled={!node.editable} onChange={(e) => void execute('architecture.updateBalcony', { id: node.id, style: e.target.value })}>
            {!current && <option value="">By hand</option>}
            <StyleOptions own={own} builtIn={builtIn} />
          </select>
          <button type="button" className="icon" title="Balcony configurator…" disabled={!node.editable} onClick={() => useUi.setState({ dialog: 'balcony', balconyId: node.id })}><Settings2 size={14} /></button>
        </span>
      </label>
      <button type="button" className="btn small" disabled={!node.editable} title="Copy it onto every storey above its own (Copy to storeys)"
        onClick={() => void execute('architecture.stackBalcony', { ids: [node.id] })}><Layers size={13} /> Stack on storeys above</button>
    </div>
  );
}

type Frame = { at: [number, number]; u: [number, number]; v: [number, number]; hosted: boolean };

/**
 * A balcony's grips: its middle on the wall slides it along the wall (or moves it), the front's middle sets its depth
 * (a loggia's: how deep the recess), the sides' middles its width (both sides alike); a drawn one by its outline.
 */
export function balconyGrips(node: NodeDto): Grip[] {
  const pts = fromPairs(node.props.contour);
  if (pts.length >= 3) return outlineGrips({ pts }, (o) => ({ command: 'architecture.updateBalcony', args: { id: node.id, contour: toPairs(o.pts) } }));
  const f = node.props.balconyFrame as Frame | undefined;
  if (!f) return [];
  const at = { x: f.at[0], y: f.at[1] };
  const u = { x: f.u[0], y: f.u[1] };
  const v = { x: f.v[0], y: f.v[1] };
  const P = (a: number, b: number): Vec2 => ({ x: at.x + u.x * a + v.x * b, y: at.y + u.y * a + v.y * b });
  const local = (q: Vec2) => ({ a: (q.x - at.x) * u.x + (q.y - at.y) * u.y, b: (q.x - at.x) * v.x + (q.y - at.y) * v.y });
  const w = Number(node.props.width ?? 3);
  const d = Number(node.props.depth ?? 1.5);
  const kind = String(node.props.kind ?? '');
  const sign = kind === 'loggia' || kind === 'mezzanine' ? -1 : 1;
  const set = (attrs: Record<string, unknown>) => ({ command: 'element.update', args: { ids: [node.id], attrs } });
  const round = (x: number) => Math.round(x * 1000) / 1000;
  return [
    { key: 'along', kind: 'centre', at, title: f.hosted ? 'Drag along the wall' : 'Drag: move the balcony', step: (to) => ({ command: 'architecture.updateBalcony', args: { id: node.id, x: to.x, y: to.y } }) },
    { key: 'depth', kind: 'edgeMid', at: P(0, sign * d), title: kind === 'loggia' ? 'Drag: how deep the recess goes' : 'Drag: the depth', step: (to) => { const b = sign * local(to).b; return b > 0.05 ? set({ depth: round(b) }) : null; } },
    { key: 'w+', kind: 'edgeMid', at: P(w / 2, sign * d / 2), title: 'Drag: the width (both sides)', step: (to) => { const a = Math.abs(local(to).a) * 2; return a >= 0.3 ? set({ width: round(a) }) : null; } },
    { key: 'w-', kind: 'edgeMid', at: P(-w / 2, sign * d / 2), title: 'Drag: the width (both sides)', step: (to) => { const a = Math.abs(local(to).a) * 2; return a >= 0.3 ? set({ width: round(a) }) : null; } },
  ];
}
