/** Core: always there — selecting, measuring, the views and how they look, materials and drafting standards. */

import { StoreySection } from './StoreySection';
import { ImportIfcDialog } from './ImportIfcDialog';
import { AppWindow, Bot, Box, BoxSelect, ClipboardCheck, CloudFog, Compass, Copy, CopyCheck, Eye, EyeOff, FlipHorizontal2, Focus, FolderOpen, Frame, Gauge, Group, Hash, History, Keyboard, Layers, Layers3, Link2, MapPin, MessageSquare, MessageSquareText, MousePointer2, Move, Palette, PanelsTopLeft, PencilRuler, PenTool, Pipette, RotateCw, Ruler, Scissors, Send, SlidersHorizontal, Square, SquareMinus, SquareSplitHorizontal, Trash2, Ungroup, Wand2 } from 'lucide-react';
import { ElevationIcon, MoveToStoreyIcon, SectionIcon } from '../../icons';
import { execute, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import { notify } from '../../app/store';
import { setTypeOf, similarOf } from '../../platform/modules';
import { registerCommand, startCommand, useCad, cadLog, type Step } from '../../viewport/cad/session';
import type { LapinModule } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { viewShowsField } from './planeFields';
import { closePane, setTool, splitPane, useUi } from '../../app/store';
import { isDesktop } from '../../engine/files';
import { openViewWindow } from '../../app/windows';
import { assistantAvailable } from '../../assistant/available';
import { showPropsTab } from '../../app/panels';
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
import { ProjectFolderDialog } from './cde/ProjectFolderDialog';
import { IssueDialog } from './cde/IssueDialog';
import { CheckDialog } from './cde/CheckDialog';
import { RemarksDialog } from './remarks/RemarksDialog';
import { RemarksDialog as SheetRemarksDialog } from './cde/pdfRemarks/RemarksDialog';
import { DisplayQualityDialog } from './DisplayQualityDialog';
import { convertToNative } from './convertTools';
import { GroupSection } from './GroupSection';
import { ExchangeExportDialog } from './ExchangeExportDialog';
import { editSelectedGroup, groupSelection, removeFromGroup, ungroupSelection } from './groupTools';
import { toggleRegion3d } from '../../viewport/select3d';
import { hideSelection, isolateSelection, resetTemporary } from '../../viewport/temporary';
import { PlanStoreysDialog } from './PlanStoreys';
import { Storeys3DLooks } from './Storeys3D';
import { setStoreys3d, showAllStoreys3d } from '../../viewport/storeys3dActions';
import { ViewSettingsDialog, ViewSettingsSection, ViewStylesDialog } from './ViewSettings';
import { cutAtPlan, toggleSectionBox } from '../../viewport/sectionBoxTools';
import { frontDiscipline } from './disciplines';
import { sectionGrips, sectionRefs, sectionTools } from './sections';

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
const viewFields = (node: NodeDto) => butDiscipline('LapinView')().filter((k) => viewShowsField(node.props.kind, k));

/** What the core keeps running while the workspace is open: the 3D's visibility and storeys shown, the links' looks. */
function CoreBackground() { return <><Visibility3D /><Storeys3DLooks /><LinksLook /></>; }

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
      // Groups (docs/groups.md), with Revit's keys.
      { id: 'modify.group', label: 'Create group', icon: Group, shortcut: 'GP', run: () => void groupSelection() },
      { id: 'modify.ungroup', label: 'Ungroup', icon: Ungroup, shortcut: 'UG', run: () => void ungroupSelection() },
      { id: 'modify.editGroup', label: 'Edit group', icon: PencilRuler, shortcut: 'EG', run: editSelectedGroup },
      { id: 'modify.removeFromGroup', label: 'Remove from group', icon: SquareMinus, run: () => void removeFromGroup() },
      // Imported references made Lapin elements, or Studio solids (decision 210).
      { id: 'modify.convertToNative', label: 'Convert to native', icon: Wand2, shortcut: 'NV', run: () => void convertToNative() },
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
      // The project folder as a CDE (docs/plan-cde.md): the folder and its naming, the pre-issue checks, the issue.
      { label: 'Issue', tools: [
        { id: 'manage.projectFolder', label: 'Project folder', icon: FolderOpen, run: () => useUi.setState({ dialog: 'projectFolder' }) },
        { id: 'manage.check', label: 'Check', icon: ClipboardCheck, run: () => useUi.setState({ dialog: 'cdeCheck' }) },
        { id: 'manage.issue', label: 'Issue…', icon: Send, run: () => useUi.setState({ dialog: 'cdeIssue' }) },
        // The remarks on the model, exchanged as BCF (docs/plan-cde.md, stage 2).
        { id: 'manage.remarks', label: 'Model remarks…', icon: MessageSquareText, run: () => useUi.setState({ dialog: 'remarks' }) },
        // The reviewers' PDF annotations read back as remarks on the sheets (stage 3).
        { id: 'manage.sheetRemarks', label: 'Sheet remarks…', icon: MessageSquare, run: () => useUi.setState({ dialog: 'cdeRemarks' }) },
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
        ...sectionTools,
        { id: 'view.elevations', label: 'Elevations', icon: ElevationIcon, run: () => void (async () => {
          for (const direction of ['south', 'east', 'north', 'west']) await execute('view.create', { kind: 'elevation', direction, discipline: frontDiscipline() });
        })() },
      ] },
      { label: 'Cut', tools: [
        { id: 'view.sectionBox', label: 'Section box', icon: BoxSelect, shortcut: 'BX', run: toggleSectionBox },
        { id: 'view.cutAtPlan', label: 'Cut at plan', icon: Scissors, run: () => void cutAtPlan() },
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
        // The reference planes' quads in 3D (docs/plan-studio-thick.md); their marks in plans and sections stay.
        { id: 'view.refPlanes', label: 'Reference planes', icon: Frame, run: () => useUi.setState((s) => ({ refPlanes: !s.refPlanes })) },
        { id: 'view.displayQuality', label: 'Display quality', icon: Gauge, run: () => useUi.setState({ dialog: 'displayQuality' }) },
      ] },
      // Selecting and clearing up in 3D (docs/groups.md): a region, and hiding for now, with Revit's keys.
      { label: 'Select', tools: [
        { id: 'view.selectRegion', label: 'Select region', icon: BoxSelect, shortcut: 'SR', run: () => { if (useUi.getState().view !== '3d') useUi.setState({ view: '3d' }); toggleRegion3d(); } },
        { id: 'view.hide', label: 'Hide', icon: EyeOff, shortcut: 'HH', run: hideSelection },
        { id: 'view.isolate', label: 'Isolate', icon: Focus, shortcut: 'HI', run: isolateSelection },
        { id: 'view.unhide', label: 'Show all', icon: Eye, shortcut: 'HR', run: resetTemporary },
      ], small: [
        { id: 'view.graphicsVg', label: 'Graphics (VG)', icon: Layers3, shortcut: 'VG', run: () => useUi.setState({ dialog: 'visibility' }) },
        // The 3D view in front's storeys (report #29): the active storey alone, with those below, or every storey again.
        { id: 'view.3dActiveStorey', label: 'Active storey only in 3D', icon: Layers, run: () => void setStoreys3d({ mode: 'active' }) },
        { id: 'view.3dStoreyAndBelow', label: 'Active storey and below in 3D', icon: Layers, run: () => void setStoreys3d({ mode: 'below' }) },
        { id: 'view.3dAllStoreys', label: 'All storeys in 3D', icon: Layers, run: () => void showAllStoreys3d() },
      ] },
      // The same ids as the app's own commands (app/commands.ts): one entry in the shortcuts, its keys in the tooltip.
      { label: 'Interface', tools: [
        { id: 'app.interface', label: 'Interface…', icon: PanelsTopLeft, run: () => useUi.setState({ dialog: 'interface' }) },
        { id: 'app.shortcuts', label: 'Keyboard shortcuts…', icon: Keyboard, run: () => useUi.setState({ dialog: 'shortcuts' }) },
        // The model assistant (docs/plan-assistant.md): the desktop's only, absent from the web build.
        ...(import.meta.env.VITE_ENGINE !== 'worker' && assistantAvailable()
          ? [{ id: 'app.assistant', label: 'Assistant', icon: Bot, run: () => showPropsTab('assistant') }] : []),
      ] },
    ],
  }],
  grips: { LapinView: sectionGrips },
  references: { LapinView: sectionRefs },
  dialogs: { importIfc: ImportIfcDialog, materials: MaterialsDialog, standards: StandardsDialog, visibility: VisibilityDialog, viewRange: ViewRangeDialog, objectStyles: ObjectStylesDialog, location: LocationDialog, cutOrder: CutOrderDialog, moveToStorey: MoveToStoreyDialog, browserSets: BrowserSetsDialog, links: LinksDialog, versions: VersionsDialog, planStoreys: PlanStoreysDialog, viewSettings: ViewSettingsDialog, viewStyles: ViewStylesDialog, exportMesh: ExportMeshDialog, projectFolder: ProjectFolderDialog, cdeIssue: IssueDialog, cdeCheck: CheckDialog, exportIfc: ExchangeExportDialog, remarks: RemarksDialog, cdeRemarks: SheetRemarksDialog, displayQuality: DisplayQualityDialog, exportExchange: ExchangeExportDialog, exportFrag: ExchangeExportDialog },
  background: CoreBackground,
  propertySections: [
    { id: 'group', label: 'Group', classes: ['IfcGroup'], component: GroupSection },
    { id: 'titleBlock', label: 'Title block', classes: ['LapinSheet'], component: SheetSection, fieldKeys: butDiscipline('LapinSheet') },
    { id: 'storeyLevels', label: 'Levels', classes: ['IfcBuildingStorey'], component: StoreySection, replacesFields: () => true },
    { id: 'viewSettings', label: 'View settings', classes: ['LapinView'], component: ViewSettingsSection, fieldKeys: viewFields },
  ],
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    { id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME' },
  ],
};

export default core;
