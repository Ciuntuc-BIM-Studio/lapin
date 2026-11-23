/**
 * Grading (site grading T5, docs/plan-site-grading.md; forum 230 §4): Grade between edges (pick two hardscapes, feature lines,
 * platforms or paths: a green patch between them, its edges at their heights, live as they change; with More edges, pick as many
 * and press Grade), Slope to ground (pick an edge: a slope at 1:N in cut and 1:M in fill out to the ground, on the side clicked
 * of an open line, outwards from an outline), and the grading's section in Properties.
 */

import { create } from 'zustand';
import { Mountain, Waves } from 'lucide-react';
import type { ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { formatLength, levelUnit, parseLevel } from '../../engine/unitsStore';
import { MaterialPicker } from '../core/MaterialPicker';
import { CommitInput } from './FeatureLineSection';
import { Mm } from './SiteSections';
import { EDGE_TYPES, gradingEdges, ownPoints, parseSlope, readyToGrade, slopeSide, slopeText, withPick } from './grading';

type Surface = 'auto' | 'loft' | 'tin';

/** What the grading tools lay: the edges picked so far, whether more than two are to come, the patch's surface, the slopes. */
export const useGradingTool = create<{ picked: string[]; more: boolean; surface: Surface; cut: number; fill: number; maxWidth: number }>(() => ({
  picked: [], more: false, surface: 'auto', cut: 2, fill: 3, maxWidth: 20,
}));

const nameOf = (id: string) => useSession.getState().nodes.get(id)?.name ?? id;

async function gradeBetween(ids: string[]): Promise<void> {
  const s = useGradingTool.getState();
  useGradingTool.setState({ picked: [] });
  const made = await execute('civil.addGrading', { kind: 'patch', edges: ids.map((id) => ({ id })), ...(s.surface !== 'auto' ? { surface: s.surface } : {}) }) as { id?: string; notes?: string[] } | null;
  if (made?.id) { useUi.setState({ selected: [made.id] }); if (made.notes?.length) notify(made.notes[0]); }
}

function GradeBetweenBar() {
  const s = useGradingTool();
  return (
    <>
      {s.picked.length === 0 ? <span className="cad-layer muted" data-testid="grading-picked">Pick the first edge</span>
        : <span className="cad-layer muted" data-testid="grading-picked"><span>Picked:</span> {s.picked.map(nameOf).join(', ')}</span>}
      <label className="cad-layer" title="Pick more than two edges, then press Make the grading">
        <input type="checkbox" checked={s.more} data-testid="grading-more" onChange={(e) => useGradingTool.setState({ more: e.target.checked })} /> More edges
      </label>
      {s.more && <button type="button" className="cad-layer" disabled={s.picked.length < 2} data-testid="grading-make" onClick={() => void gradeBetween(s.picked)}>Make the grading</button>}
      {s.picked.length > 0 && <button type="button" className="cad-layer" onClick={() => useGradingTool.setState({ picked: [] })}>Clear</button>}
      <select className="cad-layer" value={s.surface} data-testid="grading-surface" title="Between two edges a ruled surface (loft); from more, triangulated from the edges (TIN)"
        onChange={(e) => useGradingTool.setState({ surface: e.target.value as Surface })}>
        <option value="auto">Surface: auto</option>
        <option value="loft">Ruled (loft)</option>
        <option value="tin">Triangulated (TIN)</option>
      </select>
    </>
  );
}

/** A green patch between edges picked one after another: two make it; with More edges, as many as wanted, then Grade. */
export const gradeBetweenEdges: ToolDef = {
  id: 'civil.gradeBetween', label: 'Grade between edges', icon: Waves, shortcut: 'GB',
  tooltip: 'Pick two hardscapes, feature lines, platforms or paths: a sloped green patch is graded between their edges, at their heights, and follows them when they change',
  gesture: { kind: 'pick', types: EDGE_TYPES, reach: 12 },
  options: GradeBetweenBar,
  complete: async ({ entity }) => {
    if (!entity) return;
    const s = useGradingTool.getState();
    const picked = withPick(s.picked, entity.id);
    useGradingTool.setState({ picked });
    if (readyToGrade(picked, s.more)) await gradeBetween(picked);
    else if (picked.length === 1) notify('Now pick the edge to grade to.');
  },
};

function SlopeBar() {
  const s = useGradingTool();
  const field = (label: string, value: number, key: 'cut' | 'fill', title: string) => (
    <label className="cad-layer" title={title}>{label}{' '}
      <CommitInput value={slopeText(value)} width="4.5em" label={label} testId={`grading-${key}`} onCommit={(t) => { const n = parseSlope(t); if (n == null) { notify('That slope does not read: type 1:2, 2 or 50 %.'); return; } useGradingTool.setState({ [key]: n }); }} />
    </label>
  );
  return (
    <>
      {field('Cut', s.cut, 'cut', 'The slope where the ground is above the edge (1:N, N metres across for one up)')}
      {field('Fill', s.fill, 'fill', 'The slope where the ground is below the edge (1:N)')}
      <label className="cad-layer" title="Where the slope does not meet the ground within this width, it stops and says so">At most{' '}
        <CommitInput value={String(s.maxWidth)} width="3.5em" label="At most" testId="grading-max" onCommit={(t) => { const v = Number(t.replace(',', '.')); if (Number.isFinite(v) && v > 0) useGradingTool.setState({ maxWidth: v }); }} /> m
      </label>
    </>
  );
}

/** A slope from an edge picked to the ground: outwards from an outline, on the side clicked of an open line. */
export const slopeToGround: ToolDef = {
  id: 'civil.slopeToGround', label: 'Slope to ground', icon: Mountain, shortcut: 'SG',
  tooltip: 'Pick an edge (a hardscape, a platform, a feature line, a path): a slope at 1:N in cut and in fill runs from it until it meets the ground; click on the side it should go',
  gesture: { kind: 'pick', types: EDGE_TYPES, reach: 12 },
  options: SlopeBar,
  complete: async ({ entity, points }) => {
    if (!entity) return;
    const s = useGradingTool.getState();
    const at = points[points.length - 1] ?? { x: 0, y: 0 };
    const made = await execute('civil.addGrading', {
      kind: 'slope', edges: [{ id: entity.id }], side: slopeSide(entity, at), cutSlope: s.cut, fillSlope: s.fill, maxWidth: s.maxWidth,
    }) as { id?: string; notes?: string[] } | null;
    if (made?.id) { useUi.setState({ selected: [made.id] }); if (made.notes?.length) notify(made.notes[0]); }
  },
};

/** A grading in Properties: what it is, its edges (what each follows, detach, its own points' heights), its surface or its slope, its material. */
export function GradingSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const set = (args: Record<string, unknown>) => void execute('civil.updateGrading', { id: node.id, ...args });
  const slope = node.props.kind === 'slope';
  const edges = gradingEdges(node);
  const terrain = typeof node.props.terrainId === 'string' ? nodes.get(node.props.terrainId) : undefined;
  const material = typeof node.props.material === 'string' ? nodes.get(node.props.material)?.name ?? '' : '';
  const target = typeof node.props.target === 'string' ? node.props.target : 'ground';
  const num = (k: string, fallback: number) => (typeof node.props[k] === 'number' ? node.props[k] as number : fallback);
  const slopeField = (k: 'cutSlope' | 'fillSlope', fallback: number) => (
    <CommitInput value={slopeText(num(k, fallback))} label={k === 'cutSlope' ? 'Cut slope' : 'Fill slope'} testId={`grading-${k}`} width="5em" disabled={!node.editable}
      onCommit={(t) => { const n = parseSlope(t); if (n == null) { notify('That slope does not read: type 1:2, 2 or 50 %.'); return; } set({ [k]: n }); }} />
  );
  return (
    <div className="layers" data-testid="grading-section">
      <dl className="fields">
        <dt>Grading</dt><dd>{slope ? 'Slope to the ground' : 'Between edges'}</dd>
        <dt>Terrain</dt><dd>{terrain ? terrain.name : <span className="muted">standalone</span>}</dd>
        {!slope && <>
          <dt>Surface</dt>
          <dd><select value={typeof node.props.surface === 'string' ? node.props.surface : 'auto'} disabled={!node.editable} data-testid="grading-section-surface" onChange={(e) => set({ surface: e.target.value })}>
            <option value="auto">Auto</option><option value="loft">Ruled (loft)</option><option value="tin">Triangulated (TIN)</option>
          </select> {typeof node.props.surfaceUsed === 'string' && <span className="muted small">{node.props.surfaceUsed === 'loft' ? 'ruled' : 'triangulated'}</span>}</dd>
        </>}
        {slope && <>
          <dt>Side</dt>
          <dd><select value={typeof node.props.side === 'string' ? node.props.side : 'out'} disabled={!node.editable} data-testid="grading-side" onChange={(e) => set({ side: e.target.value })}>
            <option value="out">Outwards</option><option value="in">Inwards</option><option value="left">Left</option><option value="right">Right</option>
          </select></dd>
          <dt>Cut slope</dt><dd>{slopeField('cutSlope', 2)}</dd>
          <dt>Fill slope</dt><dd>{slopeField('fillSlope', 3)}</dd>
          <dt>Down to</dt>
          <dd><select value={target} disabled={!node.editable} data-testid="grading-target" onChange={(e) => set({ target: e.target.value })}>
            <option value="ground">The ground</option><option value="width">A width</option><option value="level">A level</option>
          </select></dd>
          {target === 'width' && <><dt>Width</dt><dd><Mm value={node.props.width} fallback={3} min={10} onCommit={(v) => set({ width: v })} /></dd></>}
          {target === 'level' && <><dt>Level</dt><dd>
            <CommitInput value={formatLength(num('level', 0), levelUnit())} label="Level" testId="grading-level" disabled={!node.editable}
              onCommit={(t) => { const z = parseLevel(t); if (z == null || !Number.isFinite(z)) { notify('That elevation does not read: type a number in the project\'s unit.'); return; } set({ level: z }); }} />
          </dd></>}
          <dt title="Where the slope does not reach its target within this width, it stops and says so">At most</dt><dd><Mm value={node.props.maxWidth} fallback={20} min={100} onCommit={(v) => set({ maxWidth: v })} /></dd>
        </>}
        <dt>Material</dt><dd><MaterialPicker value={material} onChange={(m) => { if (m && m !== material) set({ material: m }); }} /></dd>
        <dt>Plan area</dt><dd><span className="num">{typeof node.props.planArea === 'number' ? `${node.props.planArea.toFixed(2)} m²` : ''}</span></dd>
      </dl>
      <p className="eyebrow" style={{ margin: '8px 0 2px' }}>Edges</p>
      <table className="layer-edit small" style={{ width: '100%' }} data-testid="grading-edges"><tbody>
        {edges.map((e, k) => (
          <tr key={k}>
            <td className="muted">{k + 1}</td>
            <td>{e.own
              ? <OwnEdge node={node} edge={k} points={ownPoints(e)} />
              : <>follows {e.name} {node.editable && <button type="button" className="link small" title="Make its points the grading's own, as they are now, to type their heights" onClick={() => void execute('civil.detachGradingEdge', { id: node.id, edge: k })}>detach</button>}</>}</td>
          </tr>
        ))}
      </tbody></table>
    </div>
  );
}

/** An edge of the grading's own points: each one's height, typed (empty: the ground's). */
function OwnEdge({ node, edge, points }: { node: NodeDto; edge: number; points: { x: number; y: number; z: number | null }[] }) {
  return (
    <div>
      {points.map((p, i) => (
        <div key={i} style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
          <span className="muted">{i + 1}</span>
          <CommitInput value={p.z != null ? formatLength(p.z, levelUnit()) : ''} placeholder="ground" label="Point elevation" testId={`grading-z${edge}-${i}`} disabled={!node.editable}
            onCommit={(t) => {
              if (!t.trim()) { void execute('civil.setGradingPoints', { id: node.id, edge, indices: [i], ground: true }); return; }
              const z = parseLevel(t);
              if (z == null || !Number.isFinite(z)) { notify('That elevation does not read: type a number in the project\'s unit.'); return; }
              void execute('civil.setGradingPoints', { id: node.id, edge, indices: [i], z });
            }} />
        </div>
      ))}
    </div>
  );
}
