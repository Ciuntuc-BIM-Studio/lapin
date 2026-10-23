/** The project browser: levels, views, sheets and schedules. No IFC tree: the model is browsed by what you draw on. */

import { Box, ChevronDown, FileText, Layers, Scissors, Square, Table2 } from 'lucide-react';
import { useUi } from '../app/store';

const GROUPS = [
  { label: 'Levels', icon: Layers, items: [['level1', 'Level 1 · +3.00'], ['ground', 'Ground floor · ±0.00']] },
  { label: 'Floor plans', icon: Square, items: [['plan', 'Ground floor'], ['plan1', 'Level 1'], ['found', 'Foundation plan']] },
  { label: 'Sections', icon: Scissors, items: [['secA', 'Section A–A']] },
  { label: '3D views', icon: Box, items: [['3d', '{3D}']] },
  { label: 'Sheets', icon: FileText, items: [['A101', 'A101 · Ground floor plan']] },
  { label: 'Schedules', icon: Table2, items: [['walls', 'Wall schedule']] },
] as const;

export function ProjectBrowser() {
  const view = useUi((s) => s.view);
  return (
    <nav className="panel browser" aria-label="Project browser">
      <div className="panel-head"><span className="eyebrow">Project</span><span className="panel-title">Casa P+1 Huși</span></div>
      <input className="filter" placeholder="Filter views and sheets" />
      {GROUPS.map((g) => (
        <div key={g.label} className="tree-group">
          <div className="tree-label"><ChevronDown size={12} /><g.icon size={13} />{g.label}<span className="count">{g.items.length}</span></div>
          {g.items.map(([id, name]) => (
            <button type="button" key={id} className={`tree-item${view === id ? ' on' : ''}`} onClick={() => useUi.setState({ view: id === 'plan' || id === '3d' ? id : view })}>{name}</button>
          ))}
        </div>
      ))}
    </nav>
  );
}
