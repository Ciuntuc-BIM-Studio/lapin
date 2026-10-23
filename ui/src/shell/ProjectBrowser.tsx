/** The project browser: the levels, a floor plan per level, the 3D view; sheets and schedules come with M6. No IFC tree: the model is browsed by what you draw on. */

import { Box, ChevronDown, FileText, Layers, Plus, Square, Table2 } from 'lucide-react';
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
  const showPlan = (id: string) => useUi.setState({ view: 'plan', storeyId: id });
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
        <div className="tree-label"><ChevronDown size={12} /><Square size={13} />Floor plans<span className="count">{levels.length}</span></div>
        {levels.map((s) => (
          <button type="button" key={s.id} className={`tree-item${view === 'plan' && active?.id === s.id ? ' on' : ''}`} onClick={() => showPlan(s.id)}>{s.name}</button>
        ))}
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Box size={13} />3D views<span className="count">1</span></div>
        <button type="button" className={`tree-item${view === '3d' ? ' on' : ''}`} onClick={() => useUi.setState({ view: '3d' })}>{'{3D}'}</button>
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><FileText size={13} />Sheets<span className="count">0</span></div>
      </div>
      <div className="tree-group">
        <div className="tree-label"><ChevronDown size={12} /><Table2 size={13} />Schedules<span className="count">0</span></div>
      </div>
    </nav>
  );
}
