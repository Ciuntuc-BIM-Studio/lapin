/** A structure type in a few words: its section, or its footing's shape. */

import { mm } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';

export function describeType(t: NodeDto | null): string {
  if (!t) return '—';
  const s = t.props.section as { kind?: string; width?: number; depth?: number } | undefined;
  if (s) return s.kind === 'circle' ? `Ø ${mm(s.width ?? 0)}` : `${mm(s.width ?? 0)} × ${mm(s.depth ?? 0)}`;
  if (t.props.kind === 'strip') return `stem ${mm(Number(t.props.stemHeight ?? 0))} on ${mm(Number(t.props.footingWidth ?? 0))} × ${mm(Number(t.props.footingHeight ?? 0))}, bottom ${mm(Number(t.props.bottom ?? 0))}`;
  if (t.props.kind === 'pad') return `${(t.props.steps as unknown[] | undefined)?.length ?? 0} steps, bottom ${mm(Number(t.props.bottom ?? 0))}`;
  return '';
}

