/**
 * Resources and their levelling (docs/plan-4d5d.md, decision 772; first written for Planning Pro, decision 308): what the
 * interface asks the engine (planning.resources, kept in step with the model), the notice a levelling gives, and the
 * histogram's pane under the model, as the Gantt's.
 */

import { useEffect, useState } from 'react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { openUnder } from './schedule';

export type ResourceKind = 'labour' | 'equipment' | 'formwork';

export interface ResourceDto {
  id: string; name: string; kind: ResourceKind; capacity: number; unit: string; rate: number; formworkType?: string | null;
  assign: { task: string; name: string; units: number; hold: number }[]; demand: number[]; peak: number; over: number; cost: number;
}

/** planning.resources: the days the schedule covers (from, to, the days off among them), the tasks levelled, each resource with its demand a day. */
export interface ResourcesDto { from: string; to: string; nonWorking: number[]; levelled: number; resources: ResourceDto[] }

export interface LevelResult { moved: number; finishBefore: string; finishAfter: string; delay: number; tooBig: string[]; tooBigNames?: string[] }

/** The resources, asked again on every change to the model. */
export function useResources(): ResourcesDto | null {
  const version = useSession((s) => s.version);
  const [data, setData] = useState<ResourcesDto | null>(null);
  useEffect(() => {
    let live = true;
    query<ResourcesDto>('planning.resources', {}).then((d) => { if (live) setData(d ?? null); }).catch(() => { /* the engine said why in the session's error */ });
    return () => { live = false; };
  }, [version]);
  return data;
}

/** What a levelling tells: how many tasks moved, how far the finish went, and the tasks it could not place (named). */
export function levelNotice(r: LevelResult): string {
  const names = r.tooBigNames?.length ? r.tooBigNames : r.tooBig;
  if (r.moved === 0 && names.length === 0) return 'No resource is asked for more than it has: nothing moved.';
  return `${r.moved} task(s) moved; the schedule finishes on ${r.finishAfter} (${r.delay} working days later).`
    + (names.length ? ` Asking for more than a resource has, not moved: ${names.join(', ')}.` : '');
}

/** Construction → Level resources: one undo step, reported. */
export async function level(): Promise<void> {
  const r = await execute('planning.level', {}) as LevelResult | null;
  if (r) notify(levelNotice(r));
}

export const clearLevelling = () => void execute('planning.clearLevelling', {});

export const openResources = () => useUi.setState({ dialog: 'planning.resources' });

/** The histogram in the pane under the view (as the Gantt), or brought in front. */
export const openHistogram = () => openUnder('resourceHistogram');

/** A number with two decimals and thin thousands (a resource's cost over the schedule). */
export function money(v: number | null | undefined): string {
  if (v == null || !Number.isFinite(v)) return '—';
  const [i, d] = Math.abs(v).toFixed(2).split('.');
  return `${v < 0 ? '−' : ''}${i.replace(/\B(?=(\d{3})+(?!\d))/g, ' ')}.${d}`;
}

export const showDay = (d: string | null | undefined) => (d ? `${d.slice(8, 10)}.${d.slice(5, 7)}.${d.slice(0, 4)}` : '');
