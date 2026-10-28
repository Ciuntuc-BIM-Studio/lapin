/** Core: always there — selecting, measuring, the views and how they look, materials and drafting standards. */

import { StoreySection } from './StoreySection';
import { AppWindow, Box, Compass, Copy, CopyCheck, Eye, FlipHorizontal2, Hash, Layers3, MapPin, Move, MousePointer2, Palette, PenTool, Pipette, RotateCw, Ruler, Scissors, Square, SquareSplitHorizontal, Trash2, CloudFog, Link2, History, Layers, SlidersHorizontal } from 'lucide-react';
import { ElevationIcon, MoveToStoreyIcon, SectionIcon } from '../../icons';
import { execute, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import { notify } from '../../app/store';
import { setTypeOf, similarOf } from '../../platform/modules';
import { registerCommand, startCommand, useCad, cadLog, type Step } from '../../viewport/cad/session';
import type { LapinModule } from '../../platform/module';
import { closePane, setTool, splitPane, useUi } from '../../app/store';
import { isDesktop } from '../../engine/files';
import { openViewWindow } from '../../app/windows';
import { MaterialsDialog } from './MaterialsDialog';
import { StandardsDialog } from './StandardsDialog';
import { SheetSection } from './SheetSection';
import { Visibility3D, VisibilityDialog } from './VisibilityDialog';
import { BrowserSetsDialog } from './BrowserSetsDialog';
import { ViewRangeDialog } from './ViewRangeDialog';
import { ObjectStylesDialog } from './ObjectStylesDialog';
import { LocationDialog } from './LocationDialog';
import { CutOrderDialog } from './CutOrderDialog';
import { MoveToStoreyDialog } from './MoveToStoreyDialog';
import { LinksDialog, LinksLook } from './LinksDialog';
import { VersionsDialog } from './VersionsDialog';
import { ExportMeshDialog } from './ExportMeshDialog';
import { PlanStoreysDialog } from './PlanStoreys';
import { ViewSettingsDialog, ViewSettingsSection, ViewStylesDialog } from './ViewSettings';
import { LiteStepDemoDialog } from '../../shell/LiteStepDemoDialog';

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

/** The class's generic fields less the discipline, which the section shows as a choice of the known disciplines. */
const butDiscipline = (cls: string) => () => (useSession.getState().info?.nodeTypes.find((t) => t.type === cls)?.fields ?? []).map((f) => f.key).filter((k) => k !== 'discipline');

/** The discipline of the view in front (its own plan view, the section or elevation): what a view made from it takes. */
const frontDiscipline = (): string | undefined => {
  const s = useUi.getState();
  const id = s.view === 'plan' ? s.planViewId : s.view === 'drawing' ? s.drawingViewId : null;
  const d = id ? useSession.getState().nodes.get(id)?.props.discipline : undefined;
  return typeof d === 'string' && d.trim() ? d : undefined;
};

/** What the core keeps running while the workspace is open: the 3D's visibility, the links' looks. */
function CoreBackground() { return <><Visibility3D /><LinksLook /></>; }

const core: LapinModule = {
  id: 'core',
  name: 'Core',
  summary: 'Project, views, selection and measuring.',
  // The modify tools have no tab of their own: the modelling tabs include them (Architecture, Structure).
  sharedGroups: {
    modify: { label: 'Modify', tools: [], small: [
      { id: 'modify.move', label: 'Move', icon: Move, run: () => void startCommand('MOVE') },
      { id: 'modify.copy', label: 'Copy', icon: Copy, run: () => void startCommand('COPY') },
      { id: 'modify.rotate', label: 'Rotate', icon: RotateCw, run: () => void startCommand('ROTATE') },
      { id: 'modify.mirror', label: 'Mirror', icon: FlipHorizontal2, run: () => void startCommand('MIRROR') },
      { id: 'modify.delete', label: 'Delete', icon: Trash2, run: () => void startCommand('ERASE') },
      { id: 'modify.similar', label: 'Create similar', icon: CopyCheck, shortcut: 'CS', run: createSimilar },
      { id: 'modify.match', label: 'Match type', icon: Pipette, shortcut: 'MA', run: () => void startCommand('MATCHTYPE') },
      { id: 'modify.moveToStorey', label: 'Move to storey', icon: MoveToStoreyIcon, run: () => useUi.setState({ dialog: 'moveToStorey' }) },
    ] },
  },
  tabs: [{
    id: 'manage', label: 'Manage', order: 80,
    groups: [
      { label: 'Settings', tools: [
        { id: 'manage.materials', label: 'Materials', icon: Palette, run: () => useUi.setState({ dialog: 'materials' }) },
        { id: 'manage.standards', label: 'Drafting standards', icon: Hash, run: () => useUi.setState({ dialog: 'standards' }) },
        { id: 'manage.objectStyles', label: 'Object styles', icon: PenTool, run: () => useUi.setState({ dialog: 'objectStyles' }) },
        { id: 'manage.location', label: 'Location', icon: MapPin, run: () => useUi.setState({ dialog: 'location' }) },
        { id: 'manage.cutOrder', label: 'Cut order', icon: Scissors, run: () => useUi.setState({ dialog: 'cutOrder' }) },
        { id: 'manage.viewStyles', label: 'View styles', icon: SlidersHorizontal, run: () => useUi.setState({ dialog: 'viewStyles' }) },
      ] },
      { label: 'Files', tools: [
        { id: 'manage.links', label: 'Links', icon: Link2, run: () => useUi.setState({ dialog: 'links' }) },
        { id: 'manage.versions', label: 'Versions', icon: History, run: () => useUi.setState({ dialog: 'versions' }) },
      ] },
    ],
  }, {
    id: 'view', label: 'View', order: 90,
    groups: [
      { label: 'Views', tools: [
        { id: 'view.3d', label: '3D', icon: Box, run: () => useUi.setState({ view: '3d' }) },
        { id: 'view.plan', label: 'Plan', icon: Square, run: () => useUi.setState({ view: 'plan' }) },
        { id: 'view.section', label: 'Section', icon: SectionIcon, shortcut: 'SE',
          gesture: { kind: 'twoPoints' },
          complete: async ({ points: [a, b] }, ctx) => {
            const r = await ctx.execute('view.create', { kind: 'section', start: [a.x, a.y], end: [b.x, b.y], discipline: frontDiscipline() }) as { id?: string } | null;
            if (r?.id) useUi.setState({ view: 'drawing', drawingViewId: r.id, tool: null });
          } },
        { id: 'view.elevations', label: 'Elevations', icon: ElevationIcon, run: () => void (async () => {
          for (const direction of ['south', 'east', 'north', 'west']) await execute('view.create', { kind: 'elevation', direction, discipline: frontDiscipline() });
        })() },
      ] },
      { label: 'Graphics', tools: [
        { id: 'view.layers', label: 'Visibility', icon: Layers3, shortcut: 'VV', run: () => useUi.setState({ dialog: 'visibility' }) },
        { id: 'view.range', label: 'View range', icon: Ruler, run: () => useUi.setState({ dialog: 'viewRange' }) },
        { id: 'view.settings', label: 'View settings', icon: SlidersHorizontal, run: () => useUi.setState({ dialog: 'viewSettings' }) },
        { id: 'view.storeys', label: 'Storeys shown', icon: Layers, run: () => useUi.setState({ dialog: 'planStoreys' }) },
        { id: 'view.keyPlan', label: 'Key plan', icon: Compass, run: () => useUi.setState((s) => ({ keyPlan: !s.keyPlan })) },
        { id: 'view.split', label: 'Split', icon: SquareSplitHorizontal, run: () => { const s = useUi.getState(); if (s.panes.length > 1) closePane(1); else splitPane('row'); } },
        { id: 'view.window', label: 'New window', icon: AppWindow, run: () => { const s = useUi.getState(); if (isDesktop()) void openViewWindow(s.panes[s.pane]); else notify('A view in a window of its own needs the desktop app.'); } },
        { id: 'view.style', label: 'Visual style', icon: Eye, run: () => useUi.setState((s) => ({ visualStyle: s.visualStyle === 'shaded' ? 'edges' : s.visualStyle === 'edges' ? 'wireframe' : 'shaded' })) },
        { id: 'view.fog', label: 'Fog', icon: CloudFog, run: () => useUi.setState((s) => ({ fog: !s.fog })) },
      ], small: [
        { id: 'view.graphicsVg', label: 'Graphics (VG)', icon: Layers3, shortcut: 'VG', run: () => useUi.setState({ dialog: 'visibility' }) },
      ] },
    ],
  }],
  dialogs: { materials: MaterialsDialog, standards: StandardsDialog, visibility: VisibilityDialog, viewRange: ViewRangeDialog, objectStyles: ObjectStylesDialog, location: LocationDialog, cutOrder: CutOrderDialog, moveToStorey: MoveToStoreyDialog, browserSets: BrowserSetsDialog, links: LinksDialog, versions: VersionsDialog, planStoreys: PlanStoreysDialog, viewSettings: ViewSettingsDialog, viewStyles: ViewStylesDialog, exportMesh: ExportMeshDialog, litestepDemo: LiteStepDemoDialog },
  background: CoreBackground,
  propertySections: [
    { id: 'titleBlock', label: 'Title block', classes: ['LapinSheet'], component: SheetSection, fieldKeys: butDiscipline('LapinSheet') },
    { id: 'storeyLevels', label: 'Levels', classes: ['IfcBuildingStorey'], component: StoreySection, replacesFields: () => true },
    { id: 'viewSettings', label: 'View settings', classes: ['LapinView'], component: ViewSettingsSection, fieldKeys: butDiscipline('LapinView') },
  ],
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    { id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME' },
  ],
};

export default core;
