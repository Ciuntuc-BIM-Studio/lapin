/**
 * Annotating a sheet itself (decision 620): its paper is a view (`paper:<sheet>`, viewport/paperView.ts) where the Draft
 * and Annotate tools draw in paper millimetres. Here what is new for it, which works on any view too: a leader note (an
 * arrow to a text), a revision cloud (REVCLOUD: a rectangle or a polygon drawn as scallops), and the keys to a drawing,
 * a legend of what the sheet's views draw (the materials they cut, or their lines; a sheet's only), a scale bar and a north
 * arrow (issue #111: on a plan or a sheet; a site plan's own two are these, standing by themselves until moved by their grip).
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import { promptText } from '../../shell/prompts';
import { NumberField } from '../../shell/cells';
import { cadHost, cadLog, registerCommand, startCommand, useCad, type Step } from '../../viewport/cad/session';
import { toJson, type Shape } from '../../viewport/cad/shapes';
import { isPaperView, openPaper } from '../../viewport/paperView';
import { LANGUAGES, useLang } from '../../i18n';
import { Cloud, Compass, PenLine as PenLineIcon, MessageSquareText, PencilRuler, TableProperties } from 'lucide-react';
import type { Grip, GripView, ToolDef, Vec2 } from '../../platform/module';
import { pt } from '../../viewport/gripKit';

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
    // Its own words (heading, line roles and types) in the interface's language, kept with it.
    await ctx.execute('sheet.addLegend', { viewId: ctx.viewId, at: [p.x, p.y], kind: 'materials', lang: useLang.getState().lang });
  },
};

/** A scale bar: its left end; on a sheet at its first view's scale (Properties: another view's, or its own). */
export const scaleBarTool: ToolDef = {
  id: 'drafting.scaleBar', label: 'Scale bar', icon: PencilRuler,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('sheet.addScaleBar', { viewId: ctx.viewId, at: [p.x, p.y] }); },
};

/** A north arrow (issue #111): its middle, on a plan or a sheet's paper; pointing to true north (Manage → Location). */
export const northArrowTool: ToolDef = {
  id: 'drafting.northArrow', label: 'North arrow', icon: Compass,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('sheet.addNorthArrow', { viewId: ctx.viewId, at: [p.x, p.y] }); },
};

/**
 * A legend's, a scale bar's or a north arrow's grip (issue #111): its point where the view drew it (one standing by itself
 * included), dragged to move it there.
 */
export function keyGrips(node: NodeDto, view?: GripView): Grip[] {
  const kind = node.props.predefinedType;
  if (kind !== 'SCALEBAR' && kind !== 'NORTHARROW' && kind !== 'SHEETLEGEND') return [];
  const at = pt((view?.mark?.data as { at?: unknown } | undefined)?.at) ?? pt(node.props.at);
  if (!at) return [];
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return [{
    key: 'at', kind: 'move', at, view: viewId, title: 'Drag: move it here',
    step: (to: Vec2) => ({ command: 'sheet.updateKey', args: { id: node.id, at: [to.x, to.y] } }),
  }];
}

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
    // A module's kind (the MEP legend: the symbols and line types a view draws) when the product has the module.
    const mep = node.props.kind === 'mep';
    const hasMep = mep || !!useSession.getState().info?.catalogs['mep.roles'];
    const onSheet = isPaperView(typeof node.props.viewId === 'string' ? node.props.viewId : null);
    const views = mep ? [...useSession.getState().nodes.values()].filter((n) => n.type === 'LapinView' && ['plan', 'section', 'elevation'].includes(String(n.props.kind ?? 'plan'))) : [];
    return (
      <dl className="fields">
        <dt>Lists</dt>
        <dd><select data-testid="legend-kind" value={mep ? 'mep' : node.props.kind === 'lines' ? 'lines' : 'materials'} onChange={(e) => set({ kind: e.target.value })}>
          {(onSheet || !mep) && <option value="materials">materials (hatches and fills)</option>}
          {(onSheet || !mep) && <option value="lines">line styles</option>}
          {hasMep && <option value="mep">MEP symbols and lines</option>}
        </select></dd>
        {!mep && node.props.kind !== 'lines' && <LegendMaterialRows node={node} set={set} />}
        {mep && (
          <>
            <dt title="The view whose symbols and lines it lists; by itself: the sheet's views, or the view it is on">Of the view</dt>
            <dd><select data-testid="legend-source" value={typeof node.props.sourceViewId === 'string' ? node.props.sourceViewId : ''} onChange={(e) => set({ sourceViewId: e.target.value || null })}>
              <option value="">{onSheet ? "the sheet's views" : 'this view'}</option>
              {views.map((v) => <option key={v.id} value={v.id} data-no-i18n>{v.name}</option>)}
            </select></dd>
          </>
        )}
        <dt title="Its heading; {…} tokens as in a title block. Empty: LEGEND">Title</dt>
        <dd><input value={title} placeholder={mep ? 'MEP LEGEND' : 'LEGEND'} onChange={(e) => setTitle(e.target.value)} onBlur={() => set({ title: title.trim() || null })} /></dd>
        <dt title="The language of its own words: its heading, the lines' roles and types">Language</dt>
        <dd><select data-testid="legend-lang" value={typeof node.props.lang === 'string' ? node.props.lang : 'en'} onChange={(e) => set({ lang: e.target.value === 'en' ? null : e.target.value })}>
          {LANGUAGES.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
        </select></dd>
      </dl>
    );
  }
  // On a view, a scale bar or a north arrow may stand by itself (where a site plan draws its own) until it is moved.
  const onView = typeof node.props.viewId === 'string' && !isPaperView(node.props.viewId);
  const placed = Array.isArray(node.props.at);
  const position = onView && (
    <>
      <dt title="By itself: where a site plan draws it, round the drawing, as the drawing grows; moved by its grip, it stays where it is put">Position</dt>
      <dd className="pair">
        <span className="muted">{placed ? 'where it was put' : 'by itself'}</span>
        {placed && <button type="button" className="btn mini" data-key-auto onClick={() => set({ at: null })}>Put it back by itself</button>}
      </dd>
    </>
  );
  if (node.props.predefinedType === 'NORTHARROW') {
    return (
      <dl className="fields">
        <dt title="Its circle across on paper; empty: 14 mm. Its symbol: Marker symbol, below">Size</dt>
        <dd><NumberField value={num(node.props.size)} placeholder="14" suffix="mm" min={3} max={200} onCommit={(v) => set({ size: v && v > 0 ? v : null })} /></dd>
        {position}
      </dl>
    );
  }
  return (
    <dl className="fields">
      <dt title="Lapin's (round divisions, each numbered), or a site plan's (about 60 mm in five parts, its half and its length written)">Look</dt>
      <dd><select data-testid="scalebar-look" value={node.props.look === 'site' ? 'site' : ''} onChange={(e) => set({ look: e.target.value || null })}>
        <option value="">Lapin's</option>
        <option value="site">site plan's</option>
      </select></dd>
      {position}
      <dt title="Its length in metres of the model; empty: a round length about 80 mm long on paper">Length</dt>
      <dd><NumberField value={num(node.props.length)} placeholder="auto" suffix="m" min={0.01} onCommit={(v) => set({ length: v && v > 0 ? v : null })} /></dd>
      <dt>Divisions</dt>
      <dd><NumberField value={num(node.props.divisions)} placeholder="auto" min={1} max={20} onCommit={(v) => set({ divisions: v ?? null })} /></dd>
      <dt title="On a sheet: its own scale when it follows no view; empty: the sheet's first view's">Scale 1:</dt>
      <dd><NumberField value={num(node.props.scale)} placeholder="by view" min={1} onCommit={(v) => set({ scale: v && v > 0 ? v : null })} /></dd>
    </dl>
  );
}

/**
 * A material legend's rows (forum topic 255): what it lists (its views' cut areas, what they show with the faces they see, or what
 * their material key tags read), keyed or not (each row its material's key in a circle, in the keys' order), and its rows' text.
 */
function LegendMaterialRows({ node, set }: { node: NodeDto; set: (args: Record<string, unknown>) => void }) {
  const of = typeof node.props.of === 'string' ? node.props.of : 'cut';
  const keys = typeof node.props.keys === 'boolean' ? node.props.keys : legendKeyedByDefault(of);
  const own = typeof node.props.row === 'string' ? node.props.row : '';
  const [row, setRow] = useState(own);
  useEffect(() => setRow(own), [own]);
  return (
    <>
      <dt title="Cut areas: the materials the views' cuts are hatched with. What the views show: their seen faces too (an elevation's render, brick, cladding). Tagged: only the materials their material key tags read">Materials</dt>
      <dd><select data-testid="legend-of" value={of} onChange={(e) => set({ of: e.target.value === 'cut' ? null : e.target.value })}>
        <option value="cut">of the cut areas</option>
        <option value="seen">what the views show</option>
        <option value="tagged">those tagged with a key</option>
      </select></dd>
      <dt title="Each row its material's key in a circle (as a material key tag writes it), the rows in the keys' order; the materials listed are numbered for good">Keys</dt>
      <dd><input type="checkbox" data-testid="legend-keys" checked={keys} onChange={(e) => set({ keys: e.target.checked === legendKeyedByDefault(of) ? null : e.target.checked })} /></dd>
      {keys && <>
        <dt title="A row's text: {key}, {name}, {description} (the first line of the material's), {category}, or any parameter of the material ({Finish}); a part between — that comes out empty is left out">Row</dt>
        <dd><input data-testid="legend-row" value={row} placeholder={LEGEND_KEYED_ROW} onChange={(e) => setRow(e.target.value)} onBlur={() => { if (row.trim() !== own) set({ row: row.trim() || null }); }} /></dd>
      </>}
    </>
  );
}

/** A keyed legend's row when it says none: the material's name, then its description's first line. */
export const LEGEND_KEYED_ROW = '{name} — {description}';

/** Whether a material legend keys its rows when it says nothing: when it lists what its views show or what their tags read. */
export const legendKeyedByDefault = (of: string): boolean => of !== 'cut';

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
