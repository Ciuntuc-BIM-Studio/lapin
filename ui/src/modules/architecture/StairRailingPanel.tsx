/**
 * One railing of a stair, selected on its own (forum topic 102: "sometimes we need both sides of the railings, and sometimes
 * not; would be good to select them one by one"). A click on a stair's railing in a plan or in 3D selects it as
 * `stairId#railing:left` (or `…:right`), as a grid's axis is selected; this panel shows its side, its kind (a balustrade or a
 * handrail on the wall), its height and design, each blank as the stair's type; Delete takes that side's railing off the stair.
 * The railing stays a part of the stair (exported as an IfcRailing in the IfcStair): decision in docs/decisions-pending.md.
 */

import { Trash2, Undo2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { relatingOf } from '../../engine/model';
import { select, useUi } from '../../app/store';
import { formatLength } from '../../engine/unitsStore';
import { LengthField } from '../../shell/cells';

const STYLES: Record<string, string> = { bar: 'Posts and a handrail', balusters: 'Balusters', glass: 'Glass panels', cable: 'Cables', panel: 'Solid balustrade' };

/** A railing's selection id read back: its side, or null when the tag is not a stair railing's. */
export const railingSide = (tag: string): 'left' | 'right' | null => (tag === 'railing:left' ? 'left' : tag === 'railing:right' ? 'right' : null);

/** A 3D click on a stair's railing (its bars, its glass) selects that railing on its own. */
export function railingPick(_node: NodeDto, partKey: string): string | null {
  const m = /^railing(?:-glass)?:(left|right)$/.exec(partKey);
  return m ? `railing:${m[1]}` : null;
}

interface TypeRailing { sides?: string; height?: number; style?: string; profile?: string }
interface OwnRailing { kind?: string; height?: number; style?: string; profile?: string }

export function StairRailingPanel({ owner, tag, id }: { owner: NodeDto; tag: string; id: string }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const side = railingSide(tag);
  if (!side) return null;
  const type = relatingOf({ nodes, relations }, owner.id, 'IfcRelDefinesByType');
  const typeRail = ((type?.props.spec as { railing?: TypeRailing } | undefined)?.railing) ?? {};
  const sides = (owner.props.railingSides as string | undefined) ?? typeRail.sides ?? 'none';
  const on = sides === side || sides === 'both';
  const own = ((owner.props.railings as Record<string, OwnRailing> | undefined) ?? {})[side] ?? {};
  const set = (args: Record<string, unknown>) => void execute('architecture.updateStairRailing', { id, ...args });
  return (
    <aside className="panel props" aria-label="Properties" data-testid="stair-railing-panel">
      <div className="panel-head"><span className="eyebrow">Stair railing</span><span className="panel-title">{side === 'left' ? 'Left railing' : 'Right railing'}</span></div>
      <p className="muted small pad">What is set here is this side's alone; blank, as the stair's type.</p>
      {!on ? (
        <div className="pad">
          <p className="muted small">The stair has no railing on this side.</p>
          <button type="button" className="btn" onClick={() => set({ on: true })}><Undo2 size={14} />Put it back</button>
        </div>
      ) : <>
        <dl className="fields pad">
          <dt>Side</dt><dd><span>{side === 'left' ? 'Left' : 'Right'}</span> <span className="muted small">as one climbs</span></dd>
          <dt>Kind</dt>
          <dd><select data-field="stair-railing-kind" value={own.kind ?? 'balustrade'} onChange={(e) => set({ kind: e.target.value === 'balustrade' ? null : e.target.value })}>
            <option value="balustrade">Balustrade</option><option value="handrail">Handrail on the wall</option>
          </select></dd>
          <dt>Height</dt>
          <dd><LengthField testId="stair-railing-height" metres={own.height} blank placeholder={typeRail.height != null ? formatLength(typeRail.height) : ''} title="Over the nosings, to the handrail's axis; blank: the type's" onCommit={(v) => set({ height: v })} /></dd>
          {own.kind !== 'handrail' && <>
            <dt>Design</dt>
            <dd><select data-field="stair-railing-style" value={own.style ?? ''} onChange={(e) => set({ style: e.target.value || null })}>
              <option value="">As the type</option>
              {Object.entries(STYLES).map(([k, label]) => <option key={k} value={k}>{label}</option>)}
            </select></dd>
          </>}
          <dt>Handrail</dt>
          <dd><select value={own.profile ?? ''} onChange={(e) => set({ profile: e.target.value || null })}>
            <option value="">As the type</option><option value="square">Square</option><option value="round">Round</option>
          </select></dd>
        </dl>
        <div className="pad"><button type="button" className="btn" data-testid="stair-railing-delete" onClick={() => void execute('element.delete', { ids: [id] }).then(() => select(owner.id))}><Trash2 size={14} />Delete railing</button></div>
      </>}
      <div className="pad"><button type="button" className="btn" onClick={() => useUi.setState({ selection: owner.id, selected: [owner.id] })}>Select the stair</button></div>
    </aside>
  );
}
