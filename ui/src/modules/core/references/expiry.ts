/**
 * The reference documents' validity, as the interface reads it without asking the engine (docs/plan-cde-references.md §5): the
 * documents among the project's nodes, how many expire within the warning days or have expired, and the little store that tells
 * the dialog to open filtered. Small on purpose: the status bar imports it, the dialog itself is a lazy chunk.
 */

import { create } from 'zustand';
import { useUi } from '../../../app/store';
import type { NodeDto } from '../../../engine/protocol';

export const REFERENCE_CLASS = 'LapinReference';

/** Days before `validUntil` a document counts as expiring (the server's `references.warnDays`, 30 by default). */
export const WARN_DAYS = 30;

const pad = (n: number) => String(n).padStart(2, '0');

/** A date as ISO (2026-11-25), in the reader's own calendar day, not UTC's. */
export const isoDay = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

/** Today as the reader has it (the queries take it: the engine's clock may be elsewhere). */
export const today = () => isoDay(new Date());

const DAY = 86_400_000;
const parse = (iso: string) => { const [y, m, d] = iso.split('-').map(Number); return Date.UTC(y, (m || 1) - 1, d || 1); };

/** Whole days from `from` to `to` (both ISO days): negative when `to` is before `from`. */
export const daysBetween = (from: string, to: string) => Math.round((parse(to) - parse(from)) / DAY);

export type Expiry = { kind: 'expired' | 'expiring'; days: number } | null;

/** A received document past its `validUntil` is expired (days: how long ago); one within `warn` days of it is expiring (days left). */
export function expiryOf(status: string | undefined, validUntil: string | undefined | null, now: string, warn = WARN_DAYS): Expiry {
  if (!validUntil || (status && status !== 'received' && status !== 'expired')) return null;
  const left = daysBetween(now, validUntil);
  if (left < 0) return { kind: 'expired', days: -left };
  return left <= warn ? { kind: 'expiring', days: left } : null;
}

/** The project's reference documents among its nodes. */
export const referenceNodes = (nodes: Map<string, NodeDto>) => [...nodes.values()].filter((n) => n.type === REFERENCE_CLASS);

/** How many documents are expiring and how many have expired, read from the nodes. */
export function expiryCounts(nodes: Map<string, NodeDto>, now: string, warn = WARN_DAYS): { expiring: number; expired: number } {
  let expiring = 0, expired = 0;
  for (const n of referenceNodes(nodes)) {
    const e = expiryOf(n.props.status as string | undefined, n.props.validUntil as string | undefined, now, warn);
    if (e?.kind === 'expired') expired++; else if (e) expiring++;
  }
  return { expiring, expired };
}

/** What the dialog opens on: the status bar's counter asks for the expiring or the expired. */
export interface ReferencesOpen { status: '' | 'expired'; within: number | null; nonce: number }
export const useReferencesOpen = create<ReferencesOpen>(() => ({ status: '', within: null, nonce: 0 }));

/** Open Manage → Reference documents… (filtered: the expiring within the warning days, or the expired). */
export function openReferences(filter?: 'expiring' | 'expired'): void {
  useReferencesOpen.setState((s) => ({ status: filter === 'expired' ? 'expired' : '', within: filter === 'expiring' ? WARN_DAYS : null, nonce: s.nonce + 1 }));
  useUi.setState({ dialog: 'references' });
}
