/**
 * The shells' contours beside the 3D ones (docs/plan-fem.md §10.10 step 6): their legend (the bands with their values and
 * unit, the sign shown, the extremes), the quantity picker, and the contours on a plan of the slab's storey (screen only:
 * they are the analysis', derived, and a sheet does not print them yet).
 */

import { useSession } from '../../engine/session';
import type { PlanOverlayProps } from '../../platform/module';
import { formatValue } from './results';
import { setShellQuantity, useResults } from './useResults';
import { BANDS } from './ShellContours3D';
import {
  bandColours, bandEdges, bandPieces, shownRange, shownScale, shownUnit, shownValues, signNote, triangles,
  SHELL_QUANTITIES, SHELL_TITLE, type ContoursDto,
} from './shells';

/** The quantity buttons for the slabs and walls, and the legend of what is shown. */
export function ShellPicker() {
  const run = useResults((s) => s.run);
  const q = useResults((s) => s.shellQuantity);
  if (!run?.shells) return null;
  return (
    <>
      <div className="row quantities" role="group" aria-label="Slabs and walls">
        <span className="small muted">Slabs and walls</span>
        {SHELL_QUANTITIES.map((x) => (
          <button key={x} type="button" className={`btn mini${q === x ? ' primary' : ''}`} aria-pressed={q === x} title={SHELL_TITLE[x]} data-testid={`analysis-shell-${x}`} onClick={() => setShellQuantity(q === x ? null : x)} data-no-i18n>{x}</button>
        ))}
      </div>
      <ContourLegend />
    </>
  );
}

export function ContourLegend() {
  const contours = useResults((s) => s.contours);
  if (!contours || contours.surfaces.length === 0) return null;
  return <Legend c={contours} />;
}

function Legend({ c }: { c: ContoursDto }) {
  const { min, max } = shownRange(c);
  const edges = bandEdges(min, max, BANDS);
  const colours = bandColours(edges.length - 1);
  const k = shownScale(c.unit), unit = shownUnit(c.unit);
  const note = signNote(c.quantity, [...new Set(c.surfaces.map((s) => s.kind))]);
  return (
    <div className="contour-legend" data-testid="analysis-legend" data-quantity={c.quantity}>
      <div className="small"><b data-no-i18n>{c.quantity}</b> <span className="muted" data-no-i18n>{unit}</span></div>
      <div className="contour-bands">
        {colours.map((col, i) => (
          <div key={i} className="row small contour-band">
            <span className="contour-swatch" style={{ background: col }} />
            <span data-no-i18n>{formatValue(edges[i]! * k)} … {formatValue(edges[i + 1]! * k)}</span>
          </div>
        )).reverse()}
      </div>
      {note && <p className="muted small">{note}</p>}
    </div>
  );
}

/** The slabs' contours on a plan of their storey, in bands (each face's triangles cut where the value crosses a band's edge). */
export function ShellContoursPlan({ toScreen, view }: PlanOverlayProps) {
  const show = useResults((s) => s.show);
  const stale = useResults((s) => s.stale);
  const contours = useResults((s) => s.contours);
  const nodes = useSession((s) => s.nodes);
  if (!show || stale || !contours || !view.storeyId) return null;
  const mine = contours.surfaces.filter((s) => s.kind === 'slab' && nodes.get(s.sourceId)?.storeyId === view.storeyId);
  if (mine.length === 0) return null;
  const { min, max } = shownRange(contours);
  const edges = bandEdges(min, max, BANDS);
  const colours = bandColours(edges.length - 1);
  const paths: string[] = colours.map(() => '');
  for (const s of mine) {
    const values = shownValues(s, contours.quantity);
    const pts = values.map((_, i) => { const p = toScreen({ x: s.positions[i * 3]!, y: s.positions[i * 3 + 1]! }); return [p.x, p.y] as [number, number]; });
    for (const [a, b, d] of triangles(s.faces))
      for (const piece of bandPieces([pts[a]!, pts[b]!, pts[d]!], [values[a]!, values[b]!, values[d]!], edges))
        paths[piece.band] += `M${piece.poly.map((p) => `${p[0].toFixed(1)} ${p[1].toFixed(1)}`).join('L')}Z`;
  }
  return (
    <g className="analysis-contours-plan" data-testid="analysis-contours-plan" opacity={0.8}>
      {paths.map((d, i) => (d ? <path key={i} d={d} fill={colours[i]} stroke={colours[i]} strokeWidth={0.5} /> : null))}
    </g>
  );
}
