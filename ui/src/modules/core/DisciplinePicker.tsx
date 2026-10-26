/**
 * A view's or sheet's discipline: the browser files it under the set of that
 * discipline (Architecture, Structure, MEP, or the project's own), and a view
 * takes its discipline's style. One of the known ones, or another typed in.
 */

import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { knownDisciplines } from './disciplines';
import { useViewStyles } from './viewStyles';

const OTHER = '\u0000other';

export function DisciplinePicker({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const styles = useViewStyles();
  const known = knownDisciplines(nodes, styles);
  const current = typeof node.props.discipline === 'string' && node.props.discipline.trim() ? node.props.discipline.trim() : 'Architecture';
  const set = (discipline: string) => void execute('element.update', { id: node.id, attrs: { discipline } });
  const pick = (v: string) => {
    if (v !== OTHER) { set(v); return; }
    const typed = window.prompt('The discipline (the browser groups views and sheets by it)', current)?.trim();
    if (typed) set(typed);
  };
  return (
    <select value={known.includes(current) ? current : OTHER + current} onChange={(e) => pick(e.target.value)} disabled={!node.editable} title="The browser files the view under this discipline's set; a view takes its discipline's style">
      {known.map((d) => <option key={d} value={d}>{d}</option>)}
      {!known.includes(current) && <option value={OTHER + current}>{current}</option>}
      <option value={OTHER}>Other…</option>
    </select>
  );
}
