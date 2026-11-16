/**
 * Make a layered wall from solids (forum topic 101, architecture.wallFromSolids): the selected solids (Studio solids, or
 * references an IFC, SKP, OBJ, glTF or DAE import brought) that make one wall, one per layer, shown as the wall they
 * would make before anything is made: its layers in their order with their materials and thicknesses, the core, its
 * openings, and what differs from the solids by more than 1 mm or 0.5 %. The choices: a gap between layers an air
 * layer or its neighbour's, the stud layer a timber frame (with the wood module), each layer's material, the core, and
 * what becomes of the solids (kept hidden, deleted, kept). One undo step.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { GROUP, membersOf } from '../../platform/groups';
import { hasModule } from '../../platform/brand';

interface FoundLayer { material: string; named: boolean; thickness: number; kind: 'solid' | 'framing' | 'air'; core: boolean; members: string[]; inside: number; insideMaterial?: string | null; studWidth?: number | null; spacing?: number | null }
interface FoundOpening { along: number; sill: number; width: number; height: number; kind: 'opening' | 'door' | 'window'; fill?: string | null }
interface FoundWall {
  index: number; name: string; length: number; height: number; base: number; thickness: number; core: number; frame: boolean; frameSection?: string | null;
  layers: FoundLayer[]; openings: FoundOpening[]; differences: { id: string; name: string; what: string; mm: number }[]; notes: string[]; members: number;
}
interface Found { walls: FoundWall[]; left: { id: string; name: string; why: string }[]; made?: { id: string }[] }

type Choice = { core?: number; materials?: (string | null)[]; name?: string };

const mm = (m: number) => Math.round(m * 1000).toString();

/** The solids the selection holds (a group selected being its members). */
export function selectedSolids(): string[] {
  const nodes = useSession.getState().nodes;
  return [...new Set(useUi.getState().selected.flatMap((id) => (nodes.get(id)?.type === GROUP ? membersOf(id) : [id])))];
}

/** Whether the selection could be a wall made of solids: two or more elements, none of them a Lapin wall. */
export function looksLikeSolids(): boolean {
  const nodes = useSession.getState().nodes;
  const ids = selectedSolids();
  return ids.length >= 2 && ids.every((id) => { const n = nodes.get(id); return !!n?.storeyId && (n.type !== 'IfcWall' || !!n.props.reference); });
}

export function WallFromSolidsDialog() {
  const close = () => useUi.setState({ dialog: null });
  const ids = useMemo(selectedSolids, []);
  const [gaps, setGaps] = useState<'air' | 'absorb'>('air');
  const [frame, setFrame] = useState(false);
  const [originals, setOriginals] = useState<'hide' | 'delete' | 'keep'>('hide');
  const [choices, setChoices] = useState<Choice[]>([]);
  const [found, setFound] = useState<Found | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const wood = hasModule('wood');
  const args = { ids, gaps, frame, walls: choices.map((c) => ({ ...c })) };
  const key = JSON.stringify(args);

  useEffect(() => {
    let live = true;
    if (!ids.length) { setProblem('Select the solids that make the wall.'); return; }
    engine.call<Found>('doc.query', { name: 'architecture.wallFromSolids', args })
      .then((r) => { if (live) { setFound(r); setProblem(null); } })
      .catch((e: unknown) => { if (live) { setFound(null); setProblem(e instanceof Error ? e.message : String(e)); } });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  const choose = (i: number, c: Choice) => setChoices((all) => { const next = [...all]; while (next.length <= i) next.push({}); next[i] = { ...next[i], ...c }; return next; });
  const make = async () => {
    const r = await execute('architecture.wallFromSolids', { ...args, originals }) as Found | null;
    if (r?.made) { notify(r.made.length === 1 ? 'The layered wall is made.' : 'The layered walls are made.'); close(); }
  };
  const walls = found?.walls ?? [];
  const studs = walls.some((w) => w.frameSection);

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Make a layered wall from solids" data-testid="wall-from-solids" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Make a layered wall from solids</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">The selected solids, side by side across a wall, one per layer, become one wall of a type with those layers, with the openings that go through all of them.</p>
          <div className="field-row">
            <label className="field-line"><span className="eyebrow">Gaps between layers</span>
              <select value={gaps} onChange={(e) => setGaps(e.target.value as 'air' | 'absorb')} data-testid="wfs-gaps">
                <option value="air">An air layer</option>
                <option value="absorb">Into the thicker neighbour</option>
              </select>
            </label>
            <label className="field-line"><span className="eyebrow">The solids</span>
              <select value={originals} onChange={(e) => setOriginals(e.target.value as 'hide' | 'delete' | 'keep')} data-testid="wfs-originals">
                <option value="hide">Keep them hidden</option>
                <option value="delete">Delete them</option>
                <option value="keep">Keep them</option>
              </select>
            </label>
            {wood && studs && (
              <label className="field-line" title="The layer with studs becomes the core, a timber frame of the wood module with their section and spacing">
                <input type="checkbox" checked={frame} onChange={(e) => setFrame(e.target.checked)} data-testid="wfs-frame" /> Timber frame layer
              </label>
            )}
          </div>
          {problem && <p className="error-text" role="alert">{problem}</p>}
          {found && walls.length === 0 && <p className="muted">No wall found in the selection: select the solids of its layers.</p>}
          {walls.map((w) => (
            <section key={w.index} className="wfs-wall" data-testid="wfs-wall" style={{ marginTop: 14 }}>
              <div className="eyebrow">Wall <span>{w.index + 1}</span>: <span>{w.name}</span></div>
              <p className="muted small">
                <span>Length</span> <span>{mm(w.length)} mm</span> · <span>Height</span> <span>{mm(w.height)} mm</span> · <span>Thickness</span> <span>{mm(w.thickness)} mm</span>
              </p>
              <table className="table small" style={{ width: '100%' }}>
                <thead><tr><th>Core</th><th>Material</th><th>Thickness</th><th>Made of</th></tr></thead>
                <tbody>
                  {w.layers.map((l, i) => (
                    <tr key={i} data-testid="wfs-layer">
                      <td><input type="radio" name={`core-${w.index}`} checked={l.core} disabled={l.kind === 'air'} onChange={() => choose(w.index, { core: i })} title="The core: the wall's axis on it, the layer that joins" /></td>
                      <td>
                        <input type="text" defaultValue={l.material} className={l.named ? '' : 'warn'} title={l.named ? 'From the solid' : 'The solid names no material: rename it'}
                          onBlur={(e) => { if (e.target.value.trim() && e.target.value.trim() !== l.material) { const ms = [...(choices[w.index]?.materials ?? w.layers.map(() => null))]; ms[i] = e.target.value.trim(); choose(w.index, { materials: ms }); } }} />
                      </td>
                      <td className="num" style={{ paddingRight: 16 }}>{mm(l.thickness)} mm</td>
                      <td className="muted">
                        {l.kind === 'air' ? <span>Gap between solids</span>
                          : l.kind === 'framing' ? <span>Pieces with air between</span>
                          : l.inside > 0 ? <><span>{l.members.length - l.inside}</span> <span>solid(s), with</span> <span>{l.inside} × {l.insideMaterial}</span> <span>inside</span></>
                          : <><span>{l.members.length}</span> <span>solid(s)</span></>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {w.openings.length > 0 && (
                <p className="small"><span className="eyebrow">Openings</span> {w.openings.map((o, i) => (
                  <span key={i}>{i ? ', ' : ''}<span>{o.kind === 'door' ? 'Door' : o.kind === 'window' ? 'Window' : 'Opening'}</span> {mm(o.width)} × {mm(o.height)}</span>
                ))}</p>
              )}
              {w.differences.length > 0 && (
                <div className="small" data-testid="wfs-differences">
                  <div className="eyebrow">Differences over 1 mm or 0.5 %</div>
                  <ul>{w.differences.map((d, i) => <li key={i}><button type="button" className="link" onClick={() => useUi.setState({ selected: [d.id], selection: d.id })}>{d.name}</button>: {d.what}</li>)}</ul>
                </div>
              )}
              {w.notes.length > 0 && <ul className="muted small">{w.notes.map((n, i) => <li key={i}>{n}</li>)}</ul>}
            </section>
          ))}
          {found && found.left.length > 0 && (
            <div className="small">
              <div className="eyebrow">Left as they are</div>
              <ul className="muted">{found.left.map((x) => <li key={x.id}>{x.name}: {x.why}</li>)}</ul>
            </div>
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!walls.length} onClick={() => void make()} data-testid="wfs-make">Make</button>
        </footer>
      </div>
    </div>
  );
}
