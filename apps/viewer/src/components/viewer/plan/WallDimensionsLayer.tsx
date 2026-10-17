/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

/**
 * The selected wall's temporary dimensions (`wall-dimensions.ts`), drawn over
 * a plan in screen pixels: a line from its face to each neighbour's facing
 * face, ticked at both ends, its clear distance in millimetres on it. A click
 * on the value edits it: Enter moves the wall to the typed distance (one undo
 * step), Escape or leaving the field keeps it where it is. Shared by the
 * Model workspace plan and floor-plan drawing tabs.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useViewerStore } from '@/store';
import type { Workplane } from '@/lib/commands/modeling/types';
import { useTranslation } from '@/i18n';
import type { Vec2 } from '@/lib/snap/types';
import { selectedWallDimensions, setWallDimension, type WallDimension } from './wall-dimensions';

/** Millimetres as the plan shows them: whole, or to a tenth below a metre's fraction. */
const mm = (metres: number) => String(Math.round(metres * 10000) / 10);

/** A typed length in millimetres (comma or point), metres; null when it does not read. */
function readMm(text: string): number | null {
  const v = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(v) && v >= 0 ? v / 1000 : null;
}

function DimensionValue({ dim, x, y }: { dim: WallDimension; x: number; y: number }) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => { if (editing) input.current?.select(); }, [editing]);
  const stop = (e: { stopPropagation(): void }) => e.stopPropagation();
  const style = { left: x, top: y, transform: 'translate(-50%, -50%)' } as const;
  if (!editing) {
    return (
      <button type="button" data-wall-dimension={dim.side} title={t('planHandles.dimension.edit')} style={style}
        className="pointer-events-auto absolute rounded-sm bg-background/90 px-1 text-[11px] leading-4 tabular-nums text-overlay-accent shadow-sm ring-1 ring-overlay-accent/60 hover:ring-2"
        onPointerDown={stop} onPointerUp={stop} onDoubleClick={stop} onClick={(e) => { e.stopPropagation(); setEditing(true); }}>
        {mm(dim.value)}
      </button>
    );
  }
  return (
    <input ref={input} data-wall-dimension-input={dim.side} aria-label={t('planHandles.dimension.edit')} defaultValue={mm(dim.value)} inputMode="decimal" style={style}
      className="pointer-events-auto absolute w-16 rounded-sm border border-overlay-accent bg-background px-1 text-[11px] leading-4 tabular-nums outline-none"
      onPointerDown={stop} onPointerUp={stop}
      onKeyDown={(e) => {
        e.stopPropagation();
        if (e.key === 'Escape') setEditing(false);
        if (e.key !== 'Enter') return;
        const value = readMm(e.currentTarget.value);
        setEditing(false);
        if (value !== null) setWallDimension(dim, value);
      }}
      onBlur={() => setEditing(false)} />
  );
}

/** Tick marks across a dimension line's end, screen px. */
function tick(p: Vec2, ux: number, uy: number): string {
  const r = 4;
  // A short slash at 45° to the line, as architectural dimensions end.
  const dx = (ux - uy) * r * Math.SQRT1_2, dy = (uy + ux) * r * Math.SQRT1_2;
  return `M${(p[0] - dx).toFixed(1)} ${(p[1] - dy).toFixed(1)}L${(p[0] + dx).toFixed(1)} ${(p[1] + dy).toFixed(1)}`;
}

export function WallDimensionsLayer({ dims, toScreen }: { dims: readonly WallDimension[]; toScreen: (p: Vec2) => readonly [number, number] }) {
  if (dims.length === 0) return null;
  const shown = dims.map((dim) => {
    const a = toScreen(dim.from), b = toScreen(dim.to);
    const len = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1;
    return { dim, a: [a[0], a[1]] as Vec2, b: [b[0], b[1]] as Vec2, ux: (b[0] - a[0]) / len, uy: (b[1] - a[1]) / len };
  });
  return (
    <div data-wall-dimensions className="pointer-events-none absolute inset-0">
      <svg className="absolute inset-0 h-full w-full overflow-visible">
        {shown.map(({ dim, a, b, ux, uy }) => (
          <path key={`${dim.wall}-${dim.side}`} d={`M${a[0].toFixed(1)} ${a[1].toFixed(1)}L${b[0].toFixed(1)} ${b[1].toFixed(1)}${tick(a, ux, uy)}${tick(b, ux, uy)}`}
            className="stroke-overlay-accent" strokeWidth={1} fill="none" />
        ))}
      </svg>
      {shown.map(({ dim, a, b }) => <DimensionValue key={`${dim.wall}-${dim.side}`} dim={dim} x={(a[0] + b[0]) / 2} y={(a[1] + b[1]) / 2} />)}
    </div>
  );
}

/** The selected wall's dimensions in the plan of `plane`, kept current through edits, selection and tool changes. */
export function useWallDimensions(plane: Workplane | null): WallDimension[] {
  const selectedId = useViewerStore((s) => s.selectedEntityId);
  const selectedIds = useViewerStore((s) => s.selectedEntityIds);
  const activeTool = useViewerStore((s) => s.activeTool);
  const mutationVersion = useViewerStore((s) => s.mutationVersion);
  const editEnabled = useViewerStore((s) => s.editEnabled);
  const session = useViewerStore((s) => s.session);
  return useMemo(() => {
    void selectedId; void selectedIds; void activeTool; void mutationVersion; void editEnabled; void session;
    return selectedWallDimensions(useViewerStore.getState(), plane);
  }, [plane, selectedId, selectedIds, activeTool, mutationVersion, editEnabled, session]);
}
