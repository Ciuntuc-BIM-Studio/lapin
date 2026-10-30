/** A sheet in the inspector: its discipline (the browser's set it sits in) and its title block fields (empty: the project's name, today's date). */

import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { DisciplinePicker } from './DisciplinePicker';

// The revision and the status are set by an issue (Manage → Issue…) and may be typed; the phase is the design stage the sheet belongs to.
const FIELDS: [string, string][] = [['project', 'Project'], ['client', 'Client'], ['drawnBy', 'Drawn by'], ['checkedBy', 'Checked by'], ['date', 'Date'], ['phase', 'Phase'], ['revision', 'Revision'], ['status', 'Status']];

export function SheetSection({ node }: { node: NodeDto }) {
  const tb = (node.props.titleBlock as Record<string, string> | undefined) ?? {};
  return (
    <dl className="fields">
      <dt title="The browser files the sheet under this discipline's set">Discipline</dt>
      <dd><DisciplinePicker node={node} /></dd>
      {FIELDS.map(([key, label]) => (
        <span key={key} style={{ display: 'contents' }}>
          <dt>{label}</dt>
          <dd><input key={`${node.id}${key}${tb[key] ?? ''}`} defaultValue={tb[key] ?? ''} disabled={!node.editable}
            onBlur={(e) => e.target.value !== (tb[key] ?? '') && void execute('sheet.titleBlock', { id: node.id, fields: { [key]: e.target.value } })} /></dd>
        </span>
      ))}
    </dl>
  );
}
