/**
 * Room tag…: what every room's stamp in plan shows, one line under another,
 * each a field of the room (its name, number, function, area, perimeter…) or
 * a text of one's own. It writes the room tag type of the project's drafting
 * standards (its template, "{name}|{number}|{area}" by default), which
 * Drafting standards → Tags shows as text.
 */

import { useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { PendingDialog } from '../core/PendingDialog';

type TagType = { id: string; name?: string; shape?: string; template?: string; classes?: string | string[] };

/** The room's fields a stamp line can show (the engine's Rooms.Values and the room's attributes). */
const ROOM_FIELDS: { key: string; label: string }[] = [
  { key: 'name', label: 'Name' }, { key: 'number', label: 'Number' }, { key: 'function', label: 'Function' },
  { key: 'department', label: 'Department' }, { key: 'area', label: 'Area' }, { key: 'grossArea', label: 'Gross area' },
  { key: 'perimeter', label: 'Perimeter' }, { key: 'height', label: 'Height' }, { key: 'volume', label: 'Volume' },
  { key: 'finishFloor', label: 'Floor finish' },
];

const TEXT = '';
/** A line's field when it is exactly one, else '' (a text of one's own). */
const fieldOf = (line: string) => /^\{([^{}=|]+)\}$/.exec(line.trim())?.[1] ?? TEXT;

/** A field the engine offers for a room (params.fields): its parameters and its scopes' (decision 550: its storey's, the project's). */
interface FieldDto { field: string; set: string | null; name: string; group: string; value: string }
const SCOPE_GROUPS: [string, string][] = [['element', 'Parameters'], ['storey', 'Its storey'], ['building', 'Its building'], ['site', 'Its site'], ['project', 'The project']];

/** What a room can show beyond its own fields, read on a room of the model (none yet: nothing more). */
function useRoomFields(): FieldDto[] {
  const nodes = useSession((s) => s.nodes);
  const sample = [...nodes.values()].find((n) => n.type === 'IfcSpace')?.id;
  const [fields, setFields] = useState<FieldDto[]>([]);
  useEffect(() => {
    let live = true;
    if (sample) void query<FieldDto[]>('params.fields', { id: sample }).then((r) => { if (live) setFields(Array.isArray(r) ? r : []); });
    else setFields([]);
    return () => { live = false; };
  }, [sample, nodes]);
  return fields.filter((f) => !ROOM_FIELDS.some((r) => r.key === f.field) && !['mark', 'class', 'type'].includes(f.field));
}

const splitTemplate = (t: string) => t.split('|').map((l) => l.trim()).filter((l) => l.length > 0);
const joinTemplate = (lines: string[]) => lines.map((l) => l.trim()).filter((l) => l.length > 0).join('|');

/** The room tag type: the first that tags IfcSpace with a room's shape, as the engine picks it. */
const roomTag = (types: TagType[]) => types.find((t) => t.shape === 'room' && (Array.isArray(t.classes) ? t.classes : String(t.classes ?? '').split(/[,\s]+/)).includes('IfcSpace'));

export function RoomTagDialog() {
  const nodes = useSession((s) => s.nodes);
  const [tag, setTag] = useState<TagType | null>(null);
  const [lines, setLines] = useState<string[]>([]);
  const [missing, setMissing] = useState(false);
  const more = useRoomFields();
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    void query<{ tagTypes: TagType[] }>('standards.get').then((b) => {
      if (!live || !b) return;
      const t = roomTag(b.tagTypes ?? []);
      setMissing(!t);
      setTag(t ?? { id: 'room', name: 'Room stamp', shape: 'room', template: '{name}|{number}|{area}', classes: ['IfcSpace'] });
    });
    return () => { live = false; };
  }, [nodes]);
  useEffect(() => { if (tag) setLines(splitTemplate(tag.template ?? '')); }, [tag]);
  if (!tag) return <PendingDialog title="Room tag" />;
  const save = (next: string[]) => {
    setLines(next);
    const template = joinTemplate(next);
    if (template) void execute('standards.set', { kind: 'tag', entry: missing ? { ...tag, template } : { id: tag.id, template } });
  };
  const move = (i: number, d: number) => { const n = [...lines]; [n[i], n[i + d]] = [n[i + d]!, n[i]!]; save(n); };
  const unused = ROOM_FIELDS.find((f) => !lines.some((l) => fieldOf(l) === f.key));
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Room tag" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Room tag</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">What each room's stamp shows in plan, line by line. The first line is written in bold.</p>
          <table className="standards-table">
            <tbody>
              {lines.map((line, i) => {
                const field = fieldOf(line);
                return (
                  <tr key={i}>
                    <td>
                      <select value={field} aria-label="Line" onChange={(e) => { const n = [...lines]; n[i] = e.target.value ? `{${e.target.value}}` : line.replace(/[{}]/g, ''); save(n); }}>
                        {ROOM_FIELDS.map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
                        {SCOPE_GROUPS.map(([g, label]) => {
                          const list = more.filter((f) => f.group === g);
                          return list.length ? <optgroup key={g} label={label}>{list.map((f) => <option key={f.field} value={f.field} data-no-i18n>{f.field + (f.value ? ` = ${f.value}` : '')}</option>)}</optgroup> : null;
                        })}
                        {field && !ROOM_FIELDS.some((f) => f.key === field) && !more.some((f) => f.field === field) && <option value={field} data-no-i18n>{field}</option>}
                        <option value={TEXT}>Text…</option>
                      </select>
                    </td>
                    <td>{field === TEXT && <input className="cell" defaultValue={line} title="Text; {field} reads a field: {number}, {area}…" onBlur={(e) => { if (e.target.value !== line) { const n = [...lines]; n[i] = e.target.value; save(n); } }} />}</td>
                    <td className="row-tools"><div className="row">
                      <button type="button" className="icon mini" title="Up" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp size={12} /></button>
                      <button type="button" className="icon mini" title="Down" disabled={i === lines.length - 1} onClick={() => move(i, 1)}><ArrowDown size={12} /></button>
                      <button type="button" className="icon mini" title="Remove" disabled={lines.length === 1} onClick={() => save(lines.filter((_, j) => j !== i))}><Trash2 size={12} /></button>
                    </div></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <button type="button" className="btn small" onClick={() => save([...lines, `{${(unused ?? ROOM_FIELDS[0]!).key}}`])}><Plus size={12} />Add a line</button>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={() => save(['{name}', '{number}', '{area}'])}>As at first</button>
          <button type="button" className="btn primary" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
