/**
 * Rebar partitions (decision 483, as Revit's): a text on each bar that sorts it into a set of its own — beam stirrups, wall
 * bars, a storey — numbered apart, scheduled apart and filtered in views (a view filter's parameter rule on Partition). The
 * partition new bars go in, chosen in the drawing tools' bar; the schedule's choice of partitions; the list the model has.
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { execute, query, useSession } from '../../engine/session';

const KEY = 'lapin.rebar.partition';
const read = (): string => { try { return localStorage.getItem(KEY) ?? ''; } catch { return ''; } };

/** The partition the drawing tools put new bars in ("" none). */
export const useRebarPartition = create<{ current: string }>(() => ({ current: read() }));
export function setRebarPartition(current: string) {
  const v = current.trim();
  useRebarPartition.setState({ current: v });
  try { if (v) localStorage.setItem(KEY, v); else localStorage.removeItem(KEY); } catch { /* storage refused: kept for the session */ }
}
/** What a placing command adds for it. */
export const partitionArg = (): { partition?: string } => { const p = useRebarPartition.getState().current; return p ? { partition: p } : {}; };

/** The partitions the model's bars are in ("" for those with none), asked again as the model changes. */
export function useRebarPartitions(): string[] {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<string[]>([]);
  useEffect(() => { void query<string[]>('rebar.partitions').then((r) => setList(r ?? [])); }, [version]);
  return list;
}

/** A text typed or picked from the model's partitions. */
export function PartitionInput({ value, onCommit, testId, title }: { value: string; onCommit: (v: string) => void; testId?: string; title?: string }) {
  const known = useRebarPartitions().filter(Boolean);
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const listId = `rebar-partitions-${testId ?? 'any'}`;
  return <>
    <input className="cell" list={listId} value={text} placeholder="none" data-field={testId} title={title ?? 'Partition: bars in one are numbered, scheduled and filtered apart'} style={{ width: 120 }}
      onChange={(e) => setText(e.target.value)}
      onBlur={() => { if (text.trim() !== value) onCommit(text.trim()); }}
      onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape') setText(value); e.stopPropagation(); }} />
    <datalist id={listId}>{known.map((p) => <option key={p} value={p} />)}</datalist>
  </>;
}

/** The drawing tools' bar: the partition new bars go in. */
export function PartitionOptions() {
  const current = useRebarPartition((s) => s.current);
  return (
    <span className="cad-layer on">
      <span className="small muted">Partition</span>
      <PartitionInput value={current} onCommit={setRebarPartition} testId="rebar-tool-partition" title="The partition new bars go in: numbered from 1 in it, scheduled and filtered apart (empty: none)" />
    </span>
  );
}

/** A bar bending schedule's partitions: all, or one. */
export function RebarScheduleControls({ id, partitions }: { id: string; partitions: unknown }) {
  const all = useRebarPartitions();
  if (all.length < 2 && !(all.length === 1 && all[0])) return null;
  const chosen = Array.isArray(partitions) && partitions.length === 1 ? String(partitions[0]) : null;
  return (
    <select value={chosen ?? '*'} title="The partitions the schedule lists" data-testid="rebar-schedule-partition"
      onChange={(e) => void execute('architecture.updateSchedule', { id, partitions: e.target.value === '*' ? null : [e.target.value] })}>
      <option value="*">All partitions</option>
      {all.map((p) => <option key={p || '(none)'} value={p}>{p || 'No partition'}</option>)}
    </select>
  );
}
