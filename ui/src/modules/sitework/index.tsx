/**
 * Site works (docs/plan-site-works.md): the temporary works of the site, laid
 * out from the model. Facade scaffolding along a line drawn in plan or round
 * the walls selected; formwork on the floors and beams selected, or on the
 * face picked (in 3D the face under the cursor lights up: a soffit, a beam's
 * side; in plan an edge or the outline). Each from a system type its
 * configurator sets; each created and struck in the execution phase.
 */

import { Building2, Construction, Layers, Map as MapIcon, MousePointerClick, SlidersHorizontal, Table2 } from 'lucide-react';
import type { Grip, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { toPairs } from '../../viewport/gripKit';
import { edgeMiddles, fromPairs, insertVertex, moveVertex, removeVertex } from '../../viewport/contour';
import { notify, useUi } from '../../app/store';
import { activeStorey, engine, execute, useSession } from '../../engine/session';
import { ScaffoldDialog } from './ScaffoldDialog';
import { FormworkDialog } from './FormworkDialog';
import { FormworkSection, ScaffoldSection } from './Sections';
import { useSiteWorks } from './prefs';

const selectedOf = (...classes: string[]): string[] => {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => classes.includes(nodes.get(id)?.type ?? ''));
};

/** The type chosen in the configurator, when it is one for this kind. */
const formworkType = (kind: 'slab' | 'beam' | 'upright'): string | undefined => {
  const id = useSiteWorks.getState().formworkTypeId;
  return id && (useSession.getState().nodes.get(id)?.props.kind ?? 'slab') === kind ? id : undefined;
};

const placed = (r: unknown, what: string) => { if (r) notify(`${what} placed in the execution phase: it shows in views of that phase (Manage → Phases).`); };

/** A scaffold along a line drawn in plan, on its left (flipped in the inspector), 6 m high unless the inspector says otherwise. */
const scaffold: ToolDef = {
  id: 'sitework.scaffold', label: 'Scaffold', icon: Construction, shortcut: 'SC',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => {
    const typeId = useSiteWorks.getState().scaffoldTypeId;
    placed(await ctx.execute('sitework.addScaffold', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(typeId ? { typeId } : {}) }), 'Scaffold');
  },
};

/** A scaffold round the outer faces of the walls selected, up to their tops; it follows them as they change. */
const scaffoldOnWalls: ToolDef = {
  id: 'sitework.scaffoldOnWalls', label: 'Scaffold on walls', icon: Building2, shortcut: 'SW',
  run: () => {
    const wallIds = selectedOf('IfcWall');
    if (wallIds.length === 0) { notify('Select the walls the scaffold goes round (Shift-click for more), then press Scaffold on walls.'); return; }
    const typeId = useSiteWorks.getState().scaffoldTypeId;
    void execute('sitework.scaffoldFromWalls', { wallIds, ...(typeId ? { typeId } : {}) }).then((r) => placed(r, (r as { closed?: boolean } | null)?.closed ? 'A closed scaffold' : 'Scaffold'));
  },
};

/** Formwork on every floor, beam, wall, column and footing selected: all their formable faces. */
const formworkOnSelection: ToolDef = {
  id: 'sitework.formwork', label: 'Formwork', icon: Layers, shortcut: 'FW',
  run: () => {
    const groups: ['slab' | 'beam' | 'upright', string[]][] = [['slab', selectedOf('IfcSlab')], ['beam', selectedOf('IfcBeam')], ['upright', selectedOf('IfcWall', 'IfcColumn', 'IfcFooting')]];
    if (groups.every(([, ids]) => ids.length === 0)) { notify('Select the floors, beams, walls, columns or footings to form, then press Formwork; or use Formwork on face.'); return; }
    void (async () => {
      let r: unknown = null;
      for (const [kind, hostIds] of groups) {
        if (!hostIds.length) continue;
        const typeId = formworkType(kind);
        r = await execute('sitework.addFormwork', { hostIds, ...(typeId ? { typeId } : {}) }) ?? r;
      }
      placed(r, 'Formwork');
    })();
  },
};

interface FaceDto { role: string; label: string }

/** Formwork on the face picked: a floor's soffit or one of its edges, a beam's bottom or one of its sides, a wall's face, a column's or a footing's side. */
const formworkOnFace: ToolDef = {
  id: 'sitework.formworkFace', label: 'Formwork on face', icon: MousePointerClick, shortcut: 'FF',
  gesture: { kind: 'face', types: ['IfcSlab', 'IfcBeam', 'IfcWall', 'IfcColumn', 'IfcFooting'] },
  complete: async ({ entity, face }) => {
    if (!entity || !face) return;
    const f = await engine.call<FaceDto | null>('doc.query', { name: 'core.faceAt', args: { id: entity.id, point: face.point, normal: face.normal } }).catch(() => null);
    if (!f) { notify('No face to form there: pick a floor\'s soffit or edge, a beam\'s bottom or side, a wall\'s face, a column\'s or a footing\'s side.'); return; }
    const upright = ['IfcWall', 'IfcColumn', 'IfcFooting'].includes(entity.type);
    if (f.role === 'Top' || (f.role === 'Bottom' && upright) || (f.role.startsWith('End.') && !upright)) { notify(`The ${f.label.toLowerCase()} is not formed: pick a soffit, an edge, a bottom or a side.`); return; }
    const kind = upright ? 'upright' : entity.type === 'IfcBeam' ? 'beam' : 'slab';
    const typeId = formworkType(kind);
    const r = await execute('sitework.addFormwork', { hostIds: [entity.id], faces: [f.role], ...(typeId ? { typeId } : {}) });
    if (r) notify(`${entity.name} · ${f.label}: formed.`);
  },
};

/** A plan of the active storey in the execution phase, cut a metre up, the props cut, opened: seen from below (the formwork over them as seen), or from above (dashed overhead). */
const formworkPlan = (look: 'up' | 'down'): ToolDef => ({
  id: look === 'up' ? 'sitework.formworkPlan' : 'sitework.formworkPlanAbove', label: look === 'up' ? 'Formwork plan' : 'Formwork plan from above', icon: MapIcon,
  run: () => {
    const storeyId = useUi.getState().storeyId ?? activeStorey()?.id ?? null;
    void execute('sitework.formworkPlan', { look, ...(storeyId ? { storeyId } : {}) }).then((r) => {
      const id = (r as { id?: string } | null)?.id;
      const view = id ? useSession.getState().nodes.get(id) : undefined;
      if (id) useUi.setState({ view: 'plan', storeyId: (view?.props.storeyId as string | undefined) ?? storeyId, planViewId: id, selection: id, selected: [id] });
    });
  },
});

const scaffoldTypes: ToolDef = { id: 'sitework.scaffoldTypes', label: 'Scaffold types…', icon: SlidersHorizontal, run: () => useUi.setState({ dialog: 'scaffoldTypes' }) };
const formworkTypes: ToolDef = { id: 'sitework.formworkTypes', label: 'Formwork types…', icon: SlidersHorizontal, run: () => useUi.setState({ dialog: 'formworkTypes' }) };

const schedule = (kind: 'formwork' | 'scaffold', label: string): ToolDef => ({
  id: `sitework.schedule.${kind}`, label, icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind }).then((r) => { if (r) notify(`${label} made: it is in the project browser, under Schedules.`); }),
});

/** A drawn scaffold's line: its vertices dragged (Ctrl-click takes one out), its segments' middles (Ctrl-click inserts one). */
function lineGrips(n: NodeDto): Grip[] {
  if (Array.isArray(n.props.walls) && n.props.walls.length > 0) return [];
  const pts = fromPairs(n.props.path);
  const o = { pts };
  const save = (next: Vec2[]) => ({ command: 'sitework.updateScaffold', args: { id: n.id, points: toPairs(next) } });
  const grips: Grip[] = pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex' as const, at: p,
    step: (to: Vec2) => save(moveVertex(o, i, to).pts),
    remove: () => (pts.length > 2 ? save((removeVertex(o, i) ?? o).pts) : null),
  }));
  edgeMiddles(pts).slice(0, pts.length - 1).forEach((m, i) => grips.push({ key: `e${i}`, kind: 'edgeMid', at: m, insert: () => save(insertVertex(o, i).pts) }));
  return grips;
}

const lineRefs = (n: NodeDto): RefLine[] => { const pts = fromPairs(n.props.path); return pts.length >= 2 ? [{ points: pts, closed: n.props.closed === true, kind: 'axis' }] : []; };

const sitework: LapinModule = {
  id: 'sitework',
  name: 'Site works',
  summary: 'Facade scaffolding along a line or round the walls, formwork on floors and beams (soffits, edges, bottoms, sides), from configurable systems, temporary in the execution phase.',
  tabs: [{
    id: 'sitework', label: 'Site works', order: 45,
    groups: [
      { label: 'Scaffolding', tools: [scaffold, scaffoldOnWalls], small: [scaffoldTypes] },
      { label: 'Formwork', tools: [formworkOnSelection, formworkOnFace], small: [formworkTypes] },
      { label: 'Drawings', tools: [formworkPlan('up'), schedule('formwork', 'Formwork schedule'), schedule('scaffold', 'Scaffold schedule')], small: [formworkPlan('down')] },
    ],
  }],
  grips: { LapinScaffold: lineGrips },
  references: { LapinScaffold: lineRefs },
  propertySections: [
    { id: 'scaffold', label: 'Scaffold', classes: ['LapinScaffold'], component: ScaffoldSection, replacesFields: () => true },
    { id: 'formwork', label: 'Formwork', classes: ['LapinFormwork'], component: FormworkSection, replacesFields: () => true },
  ],
  dialogs: { scaffoldTypes: ScaffoldDialog, formworkTypes: FormworkDialog },
};

export default sitework;
