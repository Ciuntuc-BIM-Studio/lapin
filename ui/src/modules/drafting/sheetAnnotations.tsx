/**
 * Annotating a sheet itself (decision 620): its paper is a view (`paper:<sheet>`, viewport/paperView.ts) where the Draft
 * and Annotate tools draw in paper millimetres. Here what is new for it, which works on any view too: a leader note (an
 * arrow to a text), a revision cloud (REVCLOUD: a rectangle or a polygon drawn as scallops), and the keys to a drawing,
 * a legend of what the sheet's views draw (the materials they cut, or their lines; a sheet's only) and a scale bar.
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import type { ToolDef, Vec2 } from '../../platform/module';
import { promptText } from '../../shell/prompts';
import { NumberField } from '../../shell/cells';
import { cadHost, cadLog, registerCommand, startCommand, useCad, type Step } from '../../viewport/cad/session';
import { toJson, type Shape } from '../../viewport/cad/shapes';
import { isPaperView, openPaper } from '../../viewport/paperView';
import { Cloud, PenLine as PenLineIcon, MessageSquareText, PencilRuler, TableProperties } from 'lucide-react';

/** A revision cloud's scallop on paper (mm), as CAD draws one by default. */
export const CLOUD_ARC = 8;

const rectOf = (a: Vec2, b: Vec2): Shape => ({ type: 'polyline', pts: [a, { x: b.x, y: a.y }, b, { x: a.x, y: b.y }], closed: true });

async function drawCloud(shape: Shape): Promise<void> {
  const h = cadHost();
  if (!h) return;
  const r = await execute('drafting.draw', { viewId: h.viewId, layer: useCad.getState().layer, shapes: [toJson(shape)], cloud: CLOUD_ARC }) as { ids?: string[] } | null;
  if (r?.ids?.length) cadLog('Revision cloud drawn.');
}

/** REVCLOUD: two corners of a rectangle, or P for a polygon of points closed with Enter. */
export function revisionCloud(): Step {
  const polygon = (pts: Vec2[]): Step => ({
    prompt: pts.length ? 'Next point (Enter closes the cloud)' : 'First point', want: 'point', base: pts[pts.length - 1] ?? null,
    preview: (c) => (pts.length ? [{ type: 'polyline', pts: [...pts, c], closed: pts.length >= 2 }] : []),
    point: (p) => polygon([...pts, p]),
    enter: async () => { if (pts.length >= 3) await drawCloud({ type: 'polyline', pts, closed: true }); return null; },
  });
  return {
    prompt: 'First corner [Polygon]', want: 'point',
    keywords: { P: () => polygon([]) },
    point: (a) => ({
      prompt: 'Other corner', want: 'point', base: a, preview: (c) => [rectOf(a, c)],
      point: async (b) => { if (Math.abs(b.x - a.x) > 1e-9 && Math.abs(b.y - a.y) > 1e-9) await drawCloud(rectOf(a, b)); return null; },
    }),
  };
}

registerCommand({ names: ['REVCLOUD'], label: 'Revision cloud', start: revisionCloud });

/** A leader note: the arrow's point, then where the text goes, then its words. */
export const leaderNote: ToolDef = {
  id: 'drafting.leaderNote', label: 'Leader note', icon: MessageSquareText, shortcut: 'LN',
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [tip, at] }, ctx) => {
    const body = promptText('Text');
    if (body?.trim()) await ctx.execute('drafting.addText', { viewId: ctx.viewId, at: [at.x, at.y], text: body.trim(), leader: [[tip.x, tip.y]] });
  },
};

export const revisionCloudTool: ToolDef = { id: 'cad.revcloud', label: 'Revision cloud', icon: Cloud, run: () => void startCommand('REVCLOUD') };

/** A legend of what a sheet's views draw, live: its top-left corner. */
export const legendTool: ToolDef = {
  id: 'drafting.legend', label: 'Legend', icon: TableProperties,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    if (!isPaperView(ctx.viewId)) { notify('A legend goes on a sheet: open the sheet and click Annotate.'); return; }
    await ctx.execute('sheet.addLegend', { viewId: ctx.viewId, at: [p.x, p.y], kind: 'materials' });
  },
};

/** A scale bar: its left end; on a sheet at its first view's scale (Properties: another view's, or its own). */
export const scaleBarTool: ToolDef = {
  id: 'drafting.scaleBar', label: 'Scale bar', icon: PencilRuler,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('sheet.addScaleBar', { viewId: ctx.viewId, at: [p.x, p.y] }); },
};

/** Drawing on the sheet open now: its paper in the drawing pane. */
export const annotateSheetTool: ToolDef = {
  id: 'drafting.annotateSheet', label: 'Annotate sheet', icon: PenLineIcon,
  run: () => {
    const ui = useUi.getState();
    if (ui.view === 'sheet' && ui.sheetId) { useUi.setState(openPaper(ui.sheetId)); return; }
    if (ui.view === 'drawing' && isPaperView(ui.drawingViewId)) return;
    notify('Open a sheet first: then draw on it here.');
  },
};

// ------------------------------------------------------------ Properties

const num = (v: unknown): number | null => (typeof v === 'number' && Number.isFinite(v) ? v : null);

/** A legend's or a scale bar's settings. */
export function KeyFields({ node }: { node: NodeDto }) {
  const set = (args: Record<string, unknown>) => void execute('sheet.updateKey', { id: node.id, ...args });
  const legend = node.props.predefinedType === 'SHEETLEGEND';
  const [title, setTitle] = useState(typeof node.props.title === 'string' ? node.props.title : '');
  useEffect(() => setTitle(typeof node.props.title === 'string' ? node.props.title : ''), [node.props.title]);
  if (legend) {
    return (
      <dl className="fields">
        <dt>Lists</dt>
        <dd><select data-testid="legend-kind" value={node.props.kind === 'lines' ? 'lines' : 'materials'} onChange={(e) => set({ kind: e.target.value })}>
          <option value="materials">materials (hatches and fills)</option>
          <option value="lines">line styles</option>
        </select></dd>
        <dt title="Its heading; {…} tokens as in a title block. Empty: LEGEND">Title</dt>
        <dd><input value={title} placeholder="LEGEND" onChange={(e) => setTitle(e.target.value)} onBlur={() => set({ title: title.trim() || null })} /></dd>
      </dl>
    );
  }
  return (
    <dl className="fields">
      <dt title="Its length in metres of the model; empty: a round length about 80 mm long on paper">Length</dt>
      <dd><NumberField value={num(node.props.length)} placeholder="auto" suffix="m" min={0.01} onCommit={(v) => set({ length: v && v > 0 ? v : null })} /></dd>
      <dt>Divisions</dt>
      <dd><NumberField value={num(node.props.divisions)} placeholder="auto" min={1} max={20} onCommit={(v) => set({ divisions: v ?? null })} /></dd>
      <dt title="On a sheet: its own scale when it follows no view; empty: the sheet's first view's">Scale 1:</dt>
      <dd><NumberField value={num(node.props.scale)} placeholder="by view" min={1} onCommit={(v) => set({ scale: v && v > 0 ? v : null })} /></dd>
    </dl>
  );
}

/** A closed shape's revision cloud: on or off, and its scallop on paper. */
export function CloudFields({ node, ids }: { node: NodeDto; ids: string[] }) {
  const arc = num(node.props.cloud);
  return (
    <>
      <dt title="Drawn as a revision cloud: scallops about this long on paper">Revision cloud</dt>
      <dd className="pair">
        <input type="checkbox" data-testid="draft-cloud" checked={arc != null} onChange={(e) => void execute('drafting.updateDraft', { ids, cloud: e.target.checked ? CLOUD_ARC : null })} />
        {arc != null && <NumberField value={arc} suffix="mm" min={1} max={100} onCommit={(v) => { if (v) void execute('drafting.updateDraft', { ids, cloud: v }); }} />}
      </dd>
    </>
  );
}
