/** "Bars along edges": a bar (or a stirrup, all round) laid along the sides of the cut an element has in a section (rebar.alongOutline). */

import { create } from 'zustand';
import { notify } from '../../app/store';
import { query, useSession } from '../../engine/session';
import type { DrawingDto } from '../../viewport/drawing';
import { markOwner } from '../../viewport/planPick';
import type { ToolContext, ToolInput } from '../../platform/module';
import { PartitionOptions, partitionArg } from './partitions';
import { diameterChoices, steelChoices, useRebarRules } from './rebarRules';
import { outlineAt, toggleSides } from './alongEdges';

export const useAlongEdges = create<{ mode: 'all' | 'pick'; cover: number; diameter: number; steel: string }>(() => ({ mode: 'all', cover: 0.03, diameter: 0.012, steel: '' }));
const set = (patch: Partial<ReturnType<typeof useAlongEdges.getState>>) => useAlongEdges.setState(patch);
/** The catalog's lists, until the rules answer. */
const CATALOG_DIAMETERS = [0.006, 0.008, 0.01, 0.012, 0.014, 0.016, 0.02, 0.025, 0.032];

export function AlongEdgesOptions() {
  const s = useAlongEdges();
  const rules = useRebarRules();
  const diameters = diameterChoices(rules, CATALOG_DIAMETERS, s.diameter);
  const steels = steelChoices(rules, [], s.steel);
  return (
    <>
      <span className="cad-layer on">
        <span className="small muted">Cover</span>
        <input type="number" className="cell" min={0} max={500} step={5} value={Math.round(s.cover * 1000)} data-field="rebar-along-cover" style={{ width: 64 }}
          onChange={(e) => { const v = Number(e.target.value); if (Number.isFinite(v) && v >= 0) set({ cover: v / 1000 }); }}
          onKeyDown={(e) => e.stopPropagation()} />
        <span className="small muted" style={{ whiteSpace: 'nowrap' }}>mm</span>
      </span>
      <span className="cad-layer on">
        <span className="small muted">Diameter</span>
        <select value={s.diameter} data-field="rebar-along-diameter" onChange={(e) => set({ diameter: Number(e.target.value) })}>
          {diameters.map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}
        </select>
      </span>
      {steels.length > 0 && (
        <span className="cad-layer on">
          <span className="small muted">Steel</span>
          <select value={s.steel || rules?.defaultSteel || steels[0]} data-field="rebar-along-steel" onChange={(e) => set({ steel: e.target.value })}>
            {steels.map((x) => <option key={x} value={x}>{x}</option>)}
          </select>
        </span>
      )}
      <span className="cad-layer on">
        <select value={s.mode} data-field="rebar-along-mode" onChange={(e) => set({ mode: e.target.value as 'all' | 'pick' })}>
          <option value="all">All round</option>
          <option value="pick">Pick edges</option>
        </select>
      </span>
      <PartitionOptions />
    </>
  );
}

/** The cut pieces of the elements a section cuts, as the view's drawing marks them: their loops (x y x y …). */
async function cutMarks(viewId: string): Promise<{ loops: number[][] }[]> {
  const drawing = await query<DrawingDto>('view.drawing', { id: viewId });
  const nodes = useSession.getState().nodes;
  const out: { loops: number[][] }[] = [];
  for (const m of drawing?.marks ?? []) {
    const loops = (m.data as { loops?: number[][] } | undefined)?.loops;
    const owner = loops && markOwner(m.id, nodes)?.owner;
    // Bars and annotations draw loops too: only an element a section cuts across.
    if (loops && owner && owner.id === m.id && !owner.type.startsWith('LapinRebar')) out.push({ loops });
  }
  return out;
}

/**
 * The first click is inside the element's cut; with "Pick edges" each further click toggles the side nearest to it, Enter places.
 * All round: the cut's outline a stirrup; some sides: an open bar along them (they must follow one another).
 */
export async function placeAlongEdges({ points }: ToolInput, ctx: ToolContext) {
  const [at, ...clicks] = points;
  if (!at) return;
  const s = useAlongEdges.getState();
  const args: Record<string, unknown> = { viewId: ctx.viewId, at: [at.x, at.y], cover: s.cover, diameter: s.diameter, ...partitionArg() };
  if (s.steel) args.steel = s.steel;
  if (s.mode === 'pick') {
    const loop = outlineAt(await cutMarks(ctx.viewId), at);
    if (!loop) { notify('Click inside the cut of an element in a section first.'); return; }
    const edges = toggleSides(loop, clicks);
    if (edges.length === 0) { notify('Click near the sides to lay bars along, after the first click inside the cut.'); return; }
    args.edges = edges;
  }
  await ctx.execute('rebar.alongOutline', args);
}
