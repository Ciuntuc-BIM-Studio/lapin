/** Core: always there — selecting, measuring, the views and how they look, materials and drafting standards. */

import { Box, Copy, CopyCheck, Eye, FlipHorizontal2, Hash, Layers3, Move, MousePointer2, Palette, Pipette, RotateCw, Ruler, Scissors, Square, SquareStack, Trash2 } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import { notify } from '../../app/store';
import { setTypeOf, similarOf } from '../../platform/modules';
import { registerCommand, startCommand, useCad, cadLog, type Step } from '../../viewport/cad/session';
import type { LapinModule } from '../../platform/module';
import { setTool, useUi } from '../../app/store';
import { MaterialsDialog } from './MaterialsDialog';
import { StandardsDialog } from './StandardsDialog';
import { SheetSection } from './SheetSection';
import { Visibility3D, VisibilityDialog } from './VisibilityDialog';

/** Create Similar: the selected element's tool, set up like it. */
function createSimilar() {
  const id = useUi.getState().selection;
  const node = id ? useSession.getState().nodes.get(id) : undefined;
  const f = node ? similarOf(node.type) : null;
  if (!node || !f) { notify('Select a wall, floor, roof, door or window to make more like it.'); return; }
  f(node);
}

/** MATCHTYPE: a source element's type given to the elements clicked after it (the same class). */
function matchType(): Step {
  const withSource = (sourceId: string): Step => {
    const s = useSession.getState();
    const src = s.nodes.get(sourceId), type = typeOf(s, sourceId);
    if (!src || !type || !setTypeOf(src.type)) { cadLog('That element has no type to give.'); return pick(); }
    const step = (): Step => ({
      prompt: `Click the elements to give „${type.name}” (Enter to finish)`, want: 'select',
      point: async (p) => {
        const id = useCad.getState().host?.pickAny(p);
        const n = id ? useSession.getState().nodes.get(id) : undefined;
        if (!n || n.type !== src.type) cadLog('Pick an element of the same kind.');
        else await setTypeOf(src.type)!([n.id], type.id);
        return step();
      },
      enter: () => null,
    });
    return step();
  };
  const pick = (): Step => ({
    prompt: 'Click the element whose type to give', want: 'select',
    point: (p) => { const id = useCad.getState().host?.pickAny(p); return id ? withSource(id) : pick(); },
  });
  const sel = useUi.getState().selection;
  return sel ? withSource(sel) : pick();
}
registerCommand({ names: ['MATCHTYPE', 'MA'], label: 'Match type', start: matchType });

const core: LapinModule = {
  id: 'core',
  name: 'Core',
  summary: 'Project, views, selection and measuring.',
  tabs: [{
    id: 'modify', label: 'Modify', order: 5,
    groups: [
      { label: 'Modify', tools: [
        { id: 'modify.move', label: 'Move', icon: Move, run: () => void startCommand('MOVE') },
        { id: 'modify.copy', label: 'Copy', icon: Copy, run: () => void startCommand('COPY') },
        { id: 'modify.rotate', label: 'Rotate', icon: RotateCw, run: () => void startCommand('ROTATE') },
        { id: 'modify.mirror', label: 'Mirror', icon: FlipHorizontal2, run: () => void startCommand('MIRROR') },
        { id: 'modify.delete', label: 'Delete', icon: Trash2, run: () => void startCommand('ERASE') },
      ] },
      { label: 'Like this', tools: [
        { id: 'modify.similar', label: 'Create similar', icon: CopyCheck, shortcut: 'CS', run: createSimilar },
        { id: 'modify.match', label: 'Match type', icon: Pipette, shortcut: 'MA', run: () => void startCommand('MATCHTYPE') },
      ] },
    ],
  }, {
    id: 'manage', label: 'Manage', order: 80,
    groups: [
      { label: 'Settings', tools: [
        { id: 'manage.materials', label: 'Materials', icon: Palette, run: () => useUi.setState({ dialog: 'materials' }) },
        { id: 'manage.standards', label: 'Drafting standards', icon: Hash, run: () => useUi.setState({ dialog: 'standards' }) },
      ] },
    ],
  }, {
    id: 'view', label: 'View', order: 90,
    groups: [
      { label: 'Views', tools: [
        { id: 'view.3d', label: '3D', icon: Box, run: () => useUi.setState({ view: '3d' }) },
        { id: 'view.plan', label: 'Plan', icon: Square, run: () => useUi.setState({ view: 'plan' }) },
        { id: 'view.section', label: 'Section', icon: Scissors, shortcut: 'SE',
          gesture: { kind: 'twoPoints' },
          complete: async ({ points: [a, b] }, ctx) => {
            const r = await ctx.execute('view.create', { kind: 'section', start: [a.x, a.y], end: [b.x, b.y] }) as { id?: string } | null;
            if (r?.id) useUi.setState({ view: 'drawing', drawingViewId: r.id, tool: null });
          } },
        { id: 'view.elevations', label: 'Elevations', icon: SquareStack, run: () => void (async () => {
          for (const direction of ['south', 'east', 'north', 'west']) await execute('view.create', { kind: 'elevation', direction });
        })() },
      ] },
      { label: 'Graphics', tools: [
        { id: 'view.layers', label: 'Visibility', icon: Layers3, shortcut: 'VV', run: () => useUi.setState({ dialog: 'visibility' }) },
        { id: 'view.style', label: 'Visual style', icon: Eye, run: () => useUi.setState((s) => ({ visualStyle: s.visualStyle === 'shaded' ? 'edges' : s.visualStyle === 'edges' ? 'wireframe' : 'shaded' })) },
      ], small: [
        { id: 'view.graphicsVg', label: 'Graphics (VG)', icon: Layers3, shortcut: 'VG', run: () => useUi.setState({ dialog: 'visibility' }) },
      ] },
    ],
  }],
  dialogs: { materials: MaterialsDialog, standards: StandardsDialog, visibility: VisibilityDialog },
  background: Visibility3D,
  propertySections: [{ id: 'titleBlock', label: 'Title block', classes: ['LapinSheet'], component: SheetSection }],
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    { id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME' },
  ],
};

export default core;
