/**
 * The project browser: the levels; the Site (its site and location plans,
 * docs/site-plans.md); then the sets (docs/plan-views-and-joinery.md §1),
 * each with its floor plans, sections, elevations, 3D views, sheets and
 * schedules; "Other" for what no set takes. Views, sheets and levels
 * duplicate from here; "Organize…" edits the sets. No IFC tree: the model is
 * browsed by what you draw on.
 */

import { useState } from 'react';
import { Box, Camera, ChevronDown, ChevronRight, Copy, CopyPlus, FileText, Layers, Map as MapIcon, Plus, Scissors, Settings2, Square, SquareStack, Table2, Trash2 } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { execute, activeStorey, useSession } from '../engine/session';
import { num, storeys } from '../engine/model';
import { projectTitle } from '../engine/files';
import { useCamera } from '../viewport/camera';
import { disciplineOf, groupItems, implicitPlan, itemOf, setsOf, THREE_D, type BrowserItem, type BrowserSet } from './browserSets';
import { IfcTree } from './IfcTree';
import { openContextMenu } from './ContextMenu';
import { toggleSection3d, useSection3d } from '../viewport/section3d';
import { showLevelIn3d } from '../viewport/storeys3dActions';
import { useSectionBox } from '../viewport/sectionBox';
import { createSiteView, SITE_PURPOSES, type SitePurpose } from '../modules/core/SitePlans';

const TAB_KEY = 'lapin.browser.tab';

const level = (e: number) => `${e >= 0 ? '+' : '−'}${Math.abs(e).toFixed(2)}`;
const COLLAPSED_KEY = 'lapin.browser.collapsed';

function loadCollapsed(): Set<string> {
  try { return new Set(JSON.parse(localStorage.getItem(COLLAPSED_KEY) ?? '[]') as string[]); } catch { return new Set(); }
}

/** The camera as it is now, as a saved 3D view keeps it. */
function cameraNow(): Record<string, unknown> {
  const c = useCamera.getState();
  return { azimuth: c.azimuth, pitch: c.pitch, distance: c.distance, target: [c.target.x, c.target.y, c.target.z], ortho: c.ortho };
}

const made = (r: unknown): string | null => (r as { id?: string } | null)?.id ?? null;

/** `show`: a folded browser opened over the view on one of its parts (the rail's icons): the project, its levels alone, or the IFC tree. */
export function ProjectBrowser({ show }: { show?: 'project' | 'levels' | 'ifc' } = {}) {
  const view = useUi((s) => s.view);
  useUi((s) => s.storeyId);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const doc = useSession((s) => s.doc);
  const ui = useUi();
  const levels = storeys({ nodes, relations }).slice().reverse();
  const active = activeStorey();
  const [collapsed, setCollapsed] = useState<Set<string>>(loadCollapsed);
  const [kept, setTab] = useState<'project' | 'ifc'>(() => { try { return localStorage.getItem(TAB_KEY) === 'ifc' ? 'ifc' : 'project'; } catch { return 'project'; } });
  const [shown, setShown] = useState(show);
  const tab = shown === 'ifc' ? 'ifc' : shown ? 'project' : kept;
  const levelsOnly = shown === 'levels';
  const pickTab = (t: 'project' | 'ifc') => { setShown(undefined); setTab(t); try { localStorage.setItem(TAB_KEY, t); } catch { /* a convenience only */ } };
  const toggle = (id: string) => {
    const next = new Set(collapsed);
    if (next.has(id)) next.delete(id); else next.add(id);
    setCollapsed(next);
    try { localStorage.setItem(COLLAPSED_KEY, JSON.stringify([...next])); } catch { /* a convenience only */ }
  };

  const sets = setsOf(nodes);
  const items: BrowserItem[] = [...levels.map(implicitPlan), THREE_D, ...[...nodes.values()].map(itemOf).filter((i): i is BrowserItem => i !== null)];
  // The site's drawings are the Site's, not the sets'.
  const siteItems = items.filter((i) => i.kind === 'site');
  const groups = groupItems(sets, items.filter((i) => i.kind !== 'site'));

  // A plan's own view is selected too, so its properties (discipline, style, range, storeys shown, reference level) come to the panel; a level's default plan has no entity to select.
  const showPlan = (id: string, planViewId: string | null = null) => useUi.setState({ view: 'plan', storeyId: id, planViewId, selection: planViewId, selected: planViewId ? [planViewId] : [] });
  const showDrawing = (id: string) => useUi.setState({ view: 'drawing', drawingViewId: id, selection: id, selected: [id] });
  const show3d = (id: string | null) => useUi.setState({ view: '3d', view3dId: id, ...(id ? { selection: id, selected: [id] } : {}) });
  const showSheet = (id: string) => useUi.setState({ view: 'sheet', sheetId: id, selection: id, selected: [id] });
  const showSchedule = (id: string) => useUi.setState({ view: 'schedule', scheduleId: id, selection: id, selected: [id] });
  const remove = (id: string) => void execute('element.delete', { id }).then(() => {
    const s = useUi.getState();
    if (s.drawingViewId === id || s.planViewId === id) useUi.setState({ view: 'plan', drawingViewId: null, planViewId: null });
    if (s.view3dId === id) useUi.setState({ view3dId: null });
    if (s.sheetId === id) useUi.setState({ view: 'plan', sheetId: null });
    if (s.scheduleId === id) useUi.setState({ view: 'plan', scheduleId: null });
  });
  const duplicateView = (item: BrowserItem, withDetailing: boolean) => void execute('view.duplicate', { id: item.id, withDetailing }).then((r) => {
    const id = made(r);
    if (!id) return;
    if (item.kind === 'plan' || item.kind === 'site') showPlan(item.storeyId ?? active?.id ?? '', id);
    else if (item.kind === '3d') show3d(id);
    else showDrawing(id);
  });
  const duplicateSheet = (id: string) => void execute('sheet.duplicate', { id }).then((r) => { const n = made(r); if (n) showSheet(n); });
  const duplicateLevel = (id: string, withContents: boolean) => void execute('storey.duplicate', { id, withContents }).then((r) => { const n = made(r); if (n) useUi.setState({ storeyId: n, selection: n }); });
  const save3d = (fromId: string) => void execute('view.duplicate', { id: fromId }).then(async (r) => {
    const id = made(r);
    if (!id) return;
    await execute('view.setCamera', { id, camera: cameraNow() });
    // The section box as it is now goes with the camera.
    const box = useSectionBox.getState().box;
    if (box) await execute('view.setSectionBox', { id, box });
    show3d(id);
  });
  const updateCamera = (id: string) => void execute('view.setCamera', { id, camera: cameraNow() }).then(() => notify('The view keeps the camera as it is now.'));
  const addElevations = async (discipline: string) => {
    for (const direction of ['south', 'east', 'north', 'west']) await execute('view.create', { kind: 'elevation', direction, discipline });
  };

  const isPlanOn = (item: BrowserItem) => view === 'plan' && active?.id === item.storeyId && (item.node ? ui.planViewId === item.id : !ui.planViewId);
  // A section's or an elevation's right-click: open it, or show it in 3D (the model cut by it, its drawing on the cut; again: stop).
  const drawingMenu = (item: BrowserItem) => (e: React.MouseEvent) => {
    e.preventDefault();
    const on = useSection3d.getState().viewId === item.id;
    openContextMenu(e.clientX, e.clientY, [
      { label: 'Open', icon: item.kind === 'section' ? <Scissors size={13} /> : <SquareStack size={13} />, onSelect: () => showDrawing(item.id) },
      { label: 'Show in 3D', icon: <Box size={13} />, checked: on, onSelect: () => { toggleSection3d(item.id); if (!on && useUi.getState().view !== '3d') useUi.setState({ view: '3d' }); } },
    ]);
  };
  // A level's right-click: the 3D in front shows it alone, or with the levels below (report #29); it becomes the active level, which the 3D then follows.
  const levelMenu = (id: string) => (e: React.MouseEvent) => {
    e.preventDefault();
    openContextMenu(e.clientX, e.clientY, [
      { label: 'Show only this level in 3D', icon: <Layers size={13} />, onSelect: () => void showLevelIn3d(id, 'active') },
      { label: 'Show this level and below in 3D', icon: <Layers size={13} />, onSelect: () => void showLevelIn3d(id, 'below') },
    ]);
  };
  const Row = ({ item, on, open, children }: { item: BrowserItem; on: boolean; open: () => void; children?: React.ReactNode }) => (
    <span className="tree-row">
      <button type="button" className={`tree-item${on ? ' on' : ''}`} onClick={open} onContextMenu={item.kind === 'section' || item.kind === 'elevation' ? drawingMenu(item) : undefined}>{item.kind === 'sheet' ? `${String(item.node?.props.number ?? '')} · ${item.name}` : item.name}</button>
      {children}
      {item.node && <button type="button" className="icon mini" title={item.kind === 'sheet' ? 'Delete the sheet' : item.kind === 'schedule' ? 'Delete the schedule' : 'Delete the view'} onClick={() => remove(item.id)}><Trash2 size={11} /></button>}
    </span>
  );
  const Dup = ({ item }: { item: BrowserItem }) => (
    <>
      <button type="button" className="icon mini" title="Duplicate the view (its settings)" onClick={() => duplicateView(item, false)}><Copy size={11} /></button>
      {item.kind !== '3d' && <button type="button" className="icon mini" title="Duplicate with detailing (dimensions, texts, line edits)" onClick={() => duplicateView(item, true)}><CopyPlus size={11} /></button>}
    </>
  );

  const Kind = ({ id, icon, label, list, plus, children }: { id: string; icon: React.ReactNode; label: string; list: BrowserItem[]; plus?: React.ReactNode; children: (item: BrowserItem) => React.ReactNode }) => {
    const closed = collapsed.has(id);
    return (
      <div className="tree-group">
        <div className="tree-label">
          <button type="button" className="icon mini fold" title={closed ? 'Expand' : 'Collapse'} onClick={() => toggle(id)}>{closed ? <ChevronRight size={12} /> : <ChevronDown size={12} />}</button>
          {icon}{label}<span className="count">{list.length}</span>{plus}
        </div>
        {!closed && list.map((item) => <span key={item.id}>{children(item)}</span>)}
      </div>
    );
  };

  const SetGroup = ({ set, list }: { set: BrowserSet | null; list: BrowserItem[] }) => {
    const id = set?.id ?? 'other';
    const closed = collapsed.has('set:' + id);
    const discipline = disciplineOf(set);
    const of = (kind: BrowserItem['kind']) => list.filter((i) => i.kind === kind);
    return (
      <div className={`tree-set${closed ? ' closed' : ''}`}>
        <div className="tree-label set">
          <button type="button" className="icon mini fold" title={closed ? 'Expand' : 'Collapse'} onClick={() => toggle('set:' + id)}>{closed ? <ChevronRight size={12} /> : <ChevronDown size={12} />}</button>
          <span className="set-name">{set?.name ?? 'Other'}</span><span className="count">{list.length}</span>
        </div>
        {!closed && <>
          <Kind id={`${id}:plans`} icon={<Square size={13} />} label="Floor plans" list={of('plan')}
            plus={set && active && <button type="button" className="icon mini" title="A foundation plan of this level" onClick={() => void execute('view.create', { kind: 'plan', storeyId: active.id, foundation: true, name: `${active.name} – foundations`, discipline }).then((r) => { const n = made(r); if (n) showPlan(active.id, n); })}><Plus size={12} /></button>}>
            {(item) => <Row item={item} on={isPlanOn(item)} open={() => showPlan(item.storeyId ?? '', item.node ? item.id : null)}><Dup item={item} /></Row>}
          </Kind>
          <Kind id={`${id}:sections`} icon={<Scissors size={13} />} label="Sections" list={of('section')}
            plus={set && <button type="button" className="icon mini" title="Draw a section line in the plan" onClick={() => useUi.setState({ view: 'plan', tool: 'view.section' })}><Plus size={12} /></button>}>
            {(item) => <Row item={item} on={view === 'drawing' && ui.drawingViewId === item.id} open={() => showDrawing(item.id)}><Dup item={item} /></Row>}
          </Kind>
          <Kind id={`${id}:elevations`} icon={<SquareStack size={13} />} label="Elevations" list={of('elevation')}
            plus={set && of('elevation').length === 0 && <button type="button" className="icon mini" title="The four elevations" onClick={() => void addElevations(discipline)}><Plus size={12} /></button>}>
            {(item) => <Row item={item} on={view === 'drawing' && ui.drawingViewId === item.id} open={() => showDrawing(item.id)}><Dup item={item} /></Row>}
          </Kind>
          <Kind id={`${id}:3d`} icon={<Box size={13} />} label="3D views" list={of('3d')}>
            {(item) => (
              <Row item={item} on={view === '3d' && (ui.view3dId ?? '3d') === item.id} open={() => show3d(item.node ? item.id : null)}>
                <button type="button" className="icon mini" title="Save the camera as a 3D view" onClick={() => save3d(item.id)}><Copy size={11} /></button>
                {item.node && <button type="button" className="icon mini" title="Keep the camera as it is now" onClick={() => updateCamera(item.id)}><Camera size={11} /></button>}
              </Row>
            )}
          </Kind>
          <Kind id={`${id}:sheets`} icon={<FileText size={13} />} label="Sheets" list={of('sheet')}
            plus={set && <button type="button" className="icon mini" title="New sheet" onClick={() => void execute('sheet.create', { name: 'Sheet', paper: 'A3', discipline }).then((r) => { const n = made(r); if (n) showSheet(n); })}><Plus size={12} /></button>}>
            {(item) => (
              <Row item={item} on={view === 'sheet' && ui.sheetId === item.id} open={() => showSheet(item.id)}>
                <button type="button" className="icon mini" title="Duplicate the sheet (paper and title block, without its views)" onClick={() => duplicateSheet(item.id)}><Copy size={11} /></button>
              </Row>
            )}
          </Kind>
          <Kind id={`${id}:schedules`} icon={<Table2 size={13} />} label="Schedules" list={of('schedule')}
            plus={set && (
              <select className="mini-select" value="" title="New schedule" onChange={(e) => { const kind = e.target.value; if (kind) void execute('architecture.createSchedule', { kind }).then((r) => { const n = made(r); if (n) showSchedule(n); }); }}>
                <option value="">+</option><option value="all">Joinery</option><option value="parcel">Parcel coordinates</option><option value="door">Doors</option><option value="window">Windows</option><option value="lintel">Lintels</option><option value="rooms">Rooms</option><option value="planting">Planting</option>
              </select>
            )}>
            {(item) => <Row item={item} on={view === 'schedule' && ui.scheduleId === item.id} open={() => showSchedule(item.id)} />}
          </Kind>
        </>}
      </div>
    );
  };

  return (
    <nav className="panel browser" aria-label="Project browser">
      <div className="panel-head">
        <span className="eyebrow">Project</span>
        <span className="panel-title">{projectTitle(doc?.path)}{doc?.dirty ? ' •' : ''}</span>
        <button type="button" className="icon mini" title="Organize the browser (sets)" onClick={() => useUi.setState({ dialog: 'browserSets' })}><Settings2 size={13} /></button>
        <div className="browser-tabs" role="tablist">
          <button type="button" role="tab" className={`browser-tab${tab === 'project' ? ' on' : ''}`} aria-selected={tab === 'project'} onClick={() => pickTab('project')}>Project</button>
          <button type="button" role="tab" className={`browser-tab${tab === 'ifc' ? ' on' : ''}`} aria-selected={tab === 'ifc'} onClick={() => pickTab('ifc')}>IFC</button>
        </div>
      </div>
      {tab === 'ifc' && <IfcTree />}
      {tab === 'project' && <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Layers size={13} />Levels<span className="count">{levels.length}</span>
          <button type="button" className="icon mini" title="New level above" onClick={() => void execute('storey.create', {})}><Plus size={12} /></button>
        </div>
        {levels.map((s) => (
          <span key={s.id} className="tree-row">
            <button type="button" className={`tree-item${active?.id === s.id ? ' on' : ''}`} onClick={() => useUi.setState({ storeyId: s.id, selection: s.id })} onContextMenu={levelMenu(s.id)}>
              {s.name} · {level(num(s, 'elevation'))}
            </button>
            <button type="button" className="icon mini" title="Duplicate the level with everything on it (Alt: an empty level)" onClick={(e) => duplicateLevel(s.id, !e.altKey)}><Copy size={11} /></button>
          </span>
        ))}
      </div>}
      {tab === 'project' && !levelsOnly && (
        <div className="tree-group" data-testid="browser-site">
          <div className="tree-label">
            <button type="button" className="icon mini fold" title={collapsed.has('site') ? 'Expand' : 'Collapse'} onClick={() => toggle('site')}>{collapsed.has('site') ? <ChevronRight size={12} /> : <ChevronDown size={12} />}</button>
            <MapIcon size={13} />Site<span className="count">{siteItems.length}</span>
            <select className="mini-select" value="" title="New site plan or location plan" aria-label="New site drawing"
              onChange={(e) => { const p = e.target.value as SitePurpose; if (p) void createSiteView(p).then((id) => { const n = id ? nodes.get(id) ?? useSession.getState().nodes.get(id) : undefined; if (id) showPlan(String(n?.props.storeyId ?? active?.id ?? ''), id); }); }}>
              <option value="">+</option>
              {SITE_PURPOSES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
            </select>
          </div>
          {!collapsed.has('site') && siteItems.map((item) => (
            <span key={item.id}><Row item={item} on={isPlanOn(item)} open={() => showPlan(item.storeyId ?? '', item.id)}><Dup item={item} /></Row></span>
          ))}
          {!collapsed.has('site') && siteItems.length === 0 && <p className="muted small pad">No site drawings yet: + makes a site plan or a location plan.</p>}
        </div>
      )}
      {tab === 'project' && !levelsOnly && groups.map((g) => <SetGroup key={g.set?.id ?? 'other'} set={g.set} list={g.items} />)}
    </nav>
  );
}
