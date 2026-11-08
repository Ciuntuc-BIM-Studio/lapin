/** Core: always there — selecting, measuring, the views and how they look, materials and drafting standards. */

import { StoreySection } from './StoreySection';
import { ImportIfcDialog } from './ImportIfcDialog';
import { toggleDrafts3d, toggleDragMove3d, toggleDxf3d, toggleDrillSelect, toggleGrid, toggleOrbitSelection, toggleThinLines, toggleZoomToCursor, useDisplay } from '../../app/display';
import { measure } from './measure';
import { AppWindow, ListTree, ArrowRightLeft, Bot, Box, BoxSelect, Cable, ClipboardCheck, Cloud, CloudFog, Compass, Copy, CopyCheck, Eye, EyeOff, FileStack, FileText, Filter, FlipHorizontal2, Focus, FolderOpen, Frame, Gauge, Grid3x3, Group, Hash, History, Info, Keyboard, Layers, Layers3, LayoutGrid, Link2, MapPin, MessageSquare, MessageSquareText, Minus, MousePointer2, Move, Orbit, Palette, PanelBottomClose, PanelsTopLeft, PenLine, PenTool, PencilRuler, Pipette, RotateCw, Ruler, Scissors, Send, SlidersHorizontal, Square, SquareDashed, SquareMinus, SquareSplitHorizontal, SunMedium, Trash2, Ungroup, Wand2, ZoomIn } from 'lucide-react';
import { CopyToStoreysIcon, ElevationIcon, MoveToStoreyIcon, ProfileIcon, SectionIcon } from '../../icons';
import { execute, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import { notify } from '../../app/store';
import { setTypeOf, similarOf } from '../../platform/modules';
import { registerCommand, startCommand, useCad, cadLog, type Step } from '../../viewport/cad/session';
import type { LapinModule } from '../../platform/module';
import { voidLabelGrips } from '../../platform/labelParts';
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
import { Visibility3D, VisibilityDialog, openVisibility } from './VisibilityDialog';
import { BrowserSetsDialog } from './BrowserSetsDialog';
import { ViewRangeDialog } from './ViewRangeDialog';
import { ObjectStylesDialog } from './ObjectStylesDialog';
import { LocationDialog } from './LocationDialog';
import { CutOrderDialog } from './CutOrderDialog';
import { MoveToStoreyDialog } from './MoveToStoreyDialog';
import { CopyToStoreysDialog } from './CopyToStoreysDialog';
import { LinksDialog, LinksLook } from './LinksDialog';
import { MapTypesDialog } from './MapTypesDialog';
import { VersionsDialog } from './VersionsDialog';
import { ExportMeshDialog } from './ExportMeshDialog';
import { ProjectFolderDialog } from './cde/ProjectFolderDialog';
import { IssueDialog } from './cde/IssueDialog';
import { CheckDialog } from './cde/CheckDialog';
import { CoordinationDialog } from './coordination/CoordinationDialog';
import { toggleClashMarks, toggleVoids2d } from './coordination/coordination';
import { RemarksDialog } from './remarks/RemarksDialog';
import { DocumentsDialog } from './documents/DocumentsDialog';
import { ProjectInfoDialog } from './documents/ProjectInfoForm';
import { openDocuments } from './documents/documents';
import { TemplatesDialog } from './documents/TemplatesDialog';
import { TitleBlocksDialog } from './titleBlocks/TitleBlocksDialog';
import { RemarksDialog as SheetRemarksDialog } from './cde/pdfRemarks/RemarksDialog';
import { DisplayQualityDialog } from './DisplayQualityDialog';
import { SunDialog } from './SunDialog';
import { ProfilesDialog } from './ProfilesDialog';
import { SectionsDialog } from './SectionsDialog';
import { convertToNative } from './convertTools';
import { GroupSection } from './GroupSection';
import { ExchangeExportDialog } from './ExchangeExportDialog';
import { JgExportDialog } from './JgExportDialog';
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
// Copy to storeys and Move to storey from the command line: their dialogs, on the selection.
registerCommand({ names: ['COPYSTOREY', 'COPYTOSTOREYS', 'COPYSTOREYS'], label: 'Copy to storeys', start: () => { useUi.setState({ dialog: 'copyToStoreys' }); } });
registerCommand({ names: ['MOVESTOREY', 'MOVETOSTOREY'], label: 'Move to storey', start: () => { useUi.setState({ dialog: 'moveToStorey' }); } });

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
      // ArchiCAD's copy to stories (a tester's wish): the selection onto several storeys at once; COPYSTOREY on the command line.
      { id: 'modify.copyToStoreys', label: 'Copy to storeys', icon: CopyToStoreysIcon, run: () => useUi.setState({ dialog: 'copyToStoreys' }) },
      // Groups (docs/groups.md), with Revit's keys.
      { id: 'modify.group', label: 'Create group', icon: Group, shortcut: 'GP', run: () => void groupSelection() },
      { id: 'modify.ungroup', label: 'Ungroup', icon: Ungroup, shortcut: 'UG', run: () => void ungroupSelection() },
      { id: 'modify.editGroup', label: 'Edit group', icon: PencilRuler, shortcut: 'EG', run: editSelectedGroup },
      { id: 'modify.removeFromGroup', label: 'Remove from group', icon: SquareMinus, run: () => void removeFromGroup() },
      // Imported references made Lapin elements, or Studio solids (decision 214).
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
        { id: 'manage.viewFilters', label: 'View filters', icon: Filter, run: () => openVisibility('filters') },
        // The project's 2D profiles: what wall sweeps, reveals and strip footings are swept with (decision 297).
        { id: 'manage.profiles', label: 'Profiles', icon: ProfileIcon, run: () => useUi.setState({ dialog: 'profiles' }) },
      ] },
      { label: 'Files', tools: [
        { id: 'manage.links', label: 'Links', icon: Link2, run: () => useUi.setState({ dialog: 'links' }) },
        { id: 'manage.versions', label: 'Versions', icon: History, run: () => useUi.setState({ dialog: 'versions' }) },
        // The types imports made replaced by the project's own, the choice remembered (docs/spec-ifc.md, Î9).
        { id: 'manage.mapTypes', label: 'Map imported types', icon: ArrowRightLeft, run: () => useUi.setState({ dialog: 'mapTypes' }) },
      ] },
      // The written documents (docs/plan-documents.md): the project's information they read, and the documents from templates.
      { label: 'Documents', tools: [
        { id: 'manage.projectInfo', label: 'Project information…', icon: Info, run: () => useUi.setState({ dialog: 'projectInfo' }) },
        { id: 'manage.documents', label: 'Documents…', icon: FileText, run: () => openDocuments() },
        // The templates by where they are kept (Lapin's, the project's, this computer's), their company style (stage 4).
        { id: 'manage.documentTemplates', label: 'Document templates…', icon: FileStack, run: () => useUi.setState({ dialog: 'documentTemplates' }) },
        // The sheets' title blocks: Lapin's, the project's and this computer's, drawn in the designer with live fields (decision 340).
        { id: 'manage.titleBlocks', label: 'Title blocks…', icon: PanelBottomClose, run: () => useUi.setState({ dialog: 'titleBlocks' }) },
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
      // Openings as solid voids and MEP checked against them (a tester's request, decision 289), run only when asked, as a report (decision 293).
      { label: 'Coordination', tools: [
        { id: 'manage.openingsMep', label: 'Clash check…', icon: SquareDashed, run: () => useUi.setState({ dialog: 'coordination' }) },
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
        // For the eyes only (decision 247): the plan's background grid (F7) and every line one pixel (TL); sheets and PDFs keep their pens.
        { id: 'view.grid', label: 'Grid', icon: Grid3x3, run: toggleGrid },
        { id: 'view.thinLines', label: 'Thin lines', icon: Minus, shortcut: 'TL', run: toggleThinLines },
        // The shapes drafted on plans, sections and reference planes, as lines on their planes in 3D (decision 312).
        { id: 'view.drafts3d', label: 'Drafts in 3D', icon: PenLine, run: toggleDrafts3d },
        // An imported DXF's drawing on its view's plane in 3D (decision 382), on by default; each import and link has its own switch too.
        { id: 'view.dxf3d', label: 'DXF in 3D', icon: PenLine, run: () => { toggleDxf3d(); notify(useDisplay.getState().dxf3d ? 'Imported DXF drawings are shown in 3D on their views\' planes.' : 'Imported DXF drawings are hidden in 3D.'); } },
        // Navigating in 3D (report #89, viewport/navigate3d.ts): the wheel zooms to the point under the cursor, a drag orbits round the selection; kept on this computer.
        { id: 'view.zoomToCursor', label: 'Zoom to cursor', icon: ZoomIn, run: () => { toggleZoomToCursor(); notify(useDisplay.getState().zoomToCursor ? 'The wheel zooms to the point under the cursor.' : 'The wheel zooms to the middle of the 3D view.'); } },
        { id: 'view.orbitSelection', label: 'Orbit around selection', icon: Orbit, run: () => { toggleOrbitSelection(); notify(useDisplay.getState().orbitSelection ? 'The 3D view orbits round the selection, or the point under the cursor.' : 'The 3D view orbits round its middle.'); } },
        // A selected element pressed and dragged in 3D moves, snapped (docs/plan-3d-modelling.md, report #104); off, the drag orbits.
        { id: 'view.dragMove3d', label: 'Drag to move in 3D', icon: Move, run: () => { toggleDragMove3d(); notify(useDisplay.getState().dragMove3d ? 'A selected element dragged in 3D moves, snapped to the others.' : 'A drag in 3D orbits, even on the selection.'); } },
        // Drill-down picking (platform/assemblies.ts): a part picks its whole first, then a level deeper each click; off by default, kept on this computer.
        { id: 'view.drillSelect', label: 'Drill into assemblies', icon: ListTree, run: () => { toggleDrillSelect(); notify(useDisplay.getState().drillSelect ? 'A click picks the whole first, then its parts one level at a time; Esc steps back up.' : 'A click picks the element under the cursor (a group\'s member, its group).'); } },
        // The reference planes' quads in 3D (docs/plan-studio-thick.md); their marks in plans and sections stay.
        { id: 'view.refPlanes', label: 'Reference planes', icon: Frame, run: () => useUi.setState((s) => ({ refPlanes: !s.refPlanes })) },
        // Which sections are marked in the view in front, by layer (docs/sections.md, decision 223).
        { id: 'view.sections', label: 'Sections…', icon: SectionIcon, run: () => useUi.setState({ dialog: 'sections' }) },
        // The reinforcement as tubes in 3D (docs/plan-rebar.md, decision 221): asked of the engine while on, freed when off.
        { id: 'view.rebar3d', label: 'Bars in 3D', icon: Cable, run: () => { void import('../../viewport/rebar3d').then(({ toggleRebar3d }) => toggleRebar3d()); } },
        // The openings as translucent voids in 3D (decision 289): asked of the engine while on. In a plan, section or
        // elevation the view in front draws them itself, crossed and marked, as it prints them (decision 290).
        { id: 'view.openingVoids', label: 'Opening voids', icon: SquareDashed, run: () => {
          const s = useUi.getState();
          if (s.view === '3d') useUi.setState({ openingVoids: !s.openingVoids });
          else void toggleVoids2d();
        } },
        // The clash report's open clashes as red clouds with their numbers in the plan, section or elevation in front (decision 293), on the screen only.
        { id: 'view.clashMarks', label: 'Clash marks', icon: Cloud, run: () => void toggleClashMarks().then((on) => { if (on == null) notify('Open a plan, section or elevation to mark the clashes in.'); }) },
        { id: 'view.displayQuality', label: 'Display quality', icon: Gauge, run: () => useUi.setState({ dialog: 'displayQuality' }) },
        // The project's sun on a day and an hour, lighting the 3D view with its shadows (docs/plan-visualisation.md).
        { id: 'view.sun', label: 'Sun and shadows', icon: SunMedium, run: () => useUi.setState({ dialog: 'sun' }) },
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
        { id: 'app.quickTools', label: 'Quick tools…', icon: LayoutGrid, run: () => useUi.setState({ dialog: 'quickTools' }) },
        // The model assistant (docs/plan-assistant.md): the desktop's only, absent from the web build.
        ...(import.meta.env.VITE_ENGINE !== 'worker' && assistantAvailable()
          ? [{ id: 'app.assistant', label: 'Assistant', icon: Bot, run: () => showPropsTab('assistant') }] : []),
      ] },
    ],
  }],
  // An opening void's label is held by its opening (a roof window's by its roof) where a view draws the voids (decision 292).
  grips: { LapinView: sectionGrips, IfcOpeningElement: voidLabelGrips, IfcRoof: voidLabelGrips },
  references: { LapinView: sectionRefs },
  dialogs: { importIfc: ImportIfcDialog, materials: MaterialsDialog, standards: StandardsDialog, visibility: VisibilityDialog, viewRange: ViewRangeDialog, objectStyles: ObjectStylesDialog, location: LocationDialog, cutOrder: CutOrderDialog, moveToStorey: MoveToStoreyDialog, copyToStoreys: CopyToStoreysDialog, browserSets: BrowserSetsDialog, links: LinksDialog, versions: VersionsDialog, mapTypes: MapTypesDialog, planStoreys: PlanStoreysDialog, viewSettings: ViewSettingsDialog, viewStyles: ViewStylesDialog, exportMesh: ExportMeshDialog, projectFolder: ProjectFolderDialog, cdeIssue: IssueDialog, documents: DocumentsDialog, documentTemplates: TemplatesDialog, titleBlocks: TitleBlocksDialog, projectInfo: ProjectInfoDialog, cdeCheck: CheckDialog, remarks: RemarksDialog, cdeRemarks: SheetRemarksDialog, displayQuality: DisplayQualityDialog, sun: SunDialog, exportExchange: ExchangeExportDialog, exportJg: JgExportDialog, sections: SectionsDialog, coordination: CoordinationDialog, profiles: ProfilesDialog },
  background: CoreBackground,
  propertySections: [
    { id: 'group', label: 'Group', classes: ['IfcGroup'], component: GroupSection },
    { id: 'titleBlock', label: 'Title block', classes: ['LapinSheet'], component: SheetSection, fieldKeys: butDiscipline('LapinSheet') },
    { id: 'storeyLevels', label: 'Levels', classes: ['IfcBuildingStorey'], component: StoreySection, replacesFields: () => true },
    { id: 'viewSettings', label: 'View settings', classes: ['LapinView'], component: ViewSettingsSection, fieldKeys: viewFields },
  ],
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    measure,
  ],
};

export default core;
