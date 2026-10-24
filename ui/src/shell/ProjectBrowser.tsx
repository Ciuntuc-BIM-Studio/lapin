/** The project browser: the levels, the floor plans (one per level, and any others), sections, elevations, the 3D view; sheets and schedules come with M6. No IFC tree: the model is browsed by what you draw on. */

import { Box, ChevronDown, FileText, Layers, Plus, Scissors, Square, SquareStack, Table2, Trash2 } from 'lucide-react';
import { useUi } from '../app/store';
import { execute, activeStorey, useSession } from '../engine/session';
import { num, storeys } from '../engine/model';
import { projectTitle } from '../engine/files';

const level = (e: number) => `${e >= 0 ? '+' : '−'}${Math.abs(e).toFixed(2)}`;

export function ProjectBrowser() {
  const view = useUi((s) => s.view);
  useUi((s) => s.storeyId);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const doc = useSession((s) => s.doc);
  const levels = storeys({ nodes, relations }).slice().reverse();
  const active = activeStorey();
  const ui = useUi();
  const showPlan = (id: string, planViewId: string | null = null) => useUi.setState({ view: 'plan', storeyId: id, planViewId });
  const views = [...nodes.values()].filter((n) => n.type === 'LapinView');
  const plans = views.filter((v) => v.props.kind === 'plan');
  const sections = views.filter((v) => v.props.kind === 'section');
  const elevations = views.filter((v) => v.props.kind === 'elevation');
  const sheets = [...nodes.values()].filter((n) => n.type === 'LapinSheet');
  const schedules = [...nodes.values()].filter((n) => n.type === 'LapinSchedule');
  const showDrawing = (id: string) => useUi.setState({ view: 'drawing', drawingViewId: id, selection: id, selected: [id] });
  const remove = (id: string) => void execute('element.delete', { id }).then(() => { if (useUi.getState().drawingViewId === id) useUi.setState({ view: 'plan', drawingViewId: null }); });
  const addElevations = async () => {
    for (const direction of ['south', 'east', 'north', 'west']) await execute('view.create', { kind: 'elevation', direction });
  };
  return (
    <nav className="panel browser" aria-label="Project browser">
      <div className="panel-head"><span className="eyebrow">Project</span><span className="panel-title">{projectTitle(doc?.path)}{doc?.dirty ? ' •' : ''}</span></div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Layers size={13} />Levels<span className="count">{levels.length}</span>
          <button type="button" className="icon mini" title="New level above" onClick={() => void execute('storey.create', {})}><Plus size={12} /></button>
        </div>
        {levels.map((s) => (
          <button type="button" key={s.id} className={`tree-item${active?.id === s.id ? ' on' : ''}`} onClick={() => useUi.setState({ storeyId: s.id, selection: s.id })}>
            {s.name} · {level(num(s, 'elevation'))}
          </button>
        ))}
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Square size={13} />Floor plans<span className="count">{levels.length + plans.length}</span>
          {active && <button type="button" className="icon mini" title="A foundation plan of this level" onClick={() => void execute('view.create', { kind: 'plan', storeyId: active.id, foundation: true, name: `${active.name} – foundations` })}><Plus size={12} /></button>}
        </div>
        {levels.map((s) => (
          <button type="button" key={s.id} className={`tree-item${view === 'plan' && active?.id === s.id && !ui.planViewId ? ' on' : ''}`} onClick={() => showPlan(s.id)}>{s.name}</button>
        ))}
        {plans.map((p) => (
          <span key={p.id} className="tree-row">
            <button type="button" className={`tree-item${view === 'plan' && ui.planViewId === p.id ? ' on' : ''}`} onClick={() => { showPlan(String(p.props.storeyId), p.id); useUi.setState({ selection: p.id, selected: [p.id] }); }}>{p.name}</button>
            <button type="button" className="icon mini" title="Delete the view" onClick={() => remove(p.id)}><Trash2 size={11} /></button>
          </span>
        ))}
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Scissors size={13} />Sections<span className="count">{sections.length}</span>
          <button type="button" className="icon mini" title="Draw a section line in the plan" onClick={() => useUi.setState({ view: 'plan', tool: 'view.section' })}><Plus size={12} /></button>
        </div>
        {sections.map((v) => (
          <span key={v.id} className="tree-row">
            <button type="button" className={`tree-item${view === 'drawing' && ui.drawingViewId === v.id ? ' on' : ''}`} onClick={() => showDrawing(v.id)}>{v.name}</button>
            <button type="button" className="icon mini" title="Delete the view" onClick={() => remove(v.id)}><Trash2 size={11} /></button>
          </span>
        ))}
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><SquareStack size={13} />Elevations<span className="count">{elevations.length}</span>
          {elevations.length === 0 && <button type="button" className="icon mini" title="The four elevations" onClick={() => void addElevations()}><Plus size={12} /></button>}
        </div>
        {elevations.map((v) => (
          <span key={v.id} className="tree-row">
            <button type="button" className={`tree-item${view === 'drawing' && ui.drawingViewId === v.id ? ' on' : ''}`} onClick={() => showDrawing(v.id)}>{v.name}</button>
            <button type="button" className="icon mini" title="Delete the view" onClick={() => remove(v.id)}><Trash2 size={11} /></button>
          </span>
        ))}
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Box size={13} />3D views<span className="count">1</span></div>
        <button type="button" className={`tree-item${view === '3d' ? ' on' : ''}`} onClick={() => useUi.setState({ view: '3d' })}>{'{3D}'}</button>
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><FileText size={13} />Sheets<span className="count">{sheets.length}</span>
          <button type="button" className="icon mini" title="New sheet" onClick={() => void execute('sheet.create', { name: 'Sheet', paper: 'A3' }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) useUi.setState({ view: 'sheet', sheetId: id }); })}><Plus size={12} /></button>
        </div>
        {sheets.map((v) => (
          <span key={v.id} className="tree-row">
            <button type="button" className={`tree-item${view === 'sheet' && ui.sheetId === v.id ? ' on' : ''}`} onClick={() => useUi.setState({ view: 'sheet', sheetId: v.id, selection: v.id, selected: [v.id] })}>{String(v.props.number ?? '')} · {v.name}</button>
            <button type="button" className="icon mini" title="Delete the sheet" onClick={() => void execute('element.delete', { id: v.id }).then(() => { if (useUi.getState().sheetId === v.id) useUi.setState({ view: 'plan', sheetId: null }); })}><Trash2 size={11} /></button>
          </span>
        ))}
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Table2 size={13} />Schedules<span className="count">{schedules.length}</span>
          <select className="mini-select" value="" title="New schedule" onChange={(e) => { const kind = e.target.value; if (kind) void execute('architecture.createSchedule', { kind }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) useUi.setState({ view: 'schedule', scheduleId: id }); }); }}>
            <option value="">+</option><option value="all">Joinery</option><option value="door">Doors</option><option value="window">Windows</option><option value="lintel">Lintels</option>
          </select>
        </div>
        {schedules.map((v) => (
          <span key={v.id} className="tree-row">
            <button type="button" className={`tree-item${view === 'schedule' && ui.scheduleId === v.id ? ' on' : ''}`} onClick={() => useUi.setState({ view: 'schedule', scheduleId: v.id, selection: v.id, selected: [v.id] })}>{v.name}</button>
            <button type="button" className="icon mini" title="Delete the schedule" onClick={() => void execute('element.delete', { id: v.id }).then(() => { if (useUi.getState().scheduleId === v.id) useUi.setState({ view: 'plan', scheduleId: null }); })}><Trash2 size={11} /></button>
          </span>
        ))}
      </div>
    </nav>
  );
}
