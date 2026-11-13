/**
 * The part tree's controls for the part's own sketches and the bodies drawn from them (docs/plan-studio-parametric.md §3.2, §9
 * point 2): Constrain part, a sketch's name and removal, + Sketch on a plane, + Body from sketch with its depth (a number or a
 * formula of the parameters), a body's depth and condition, its removal. Each is one engine command, so one undo step.
 */

import { useEffect, useState } from 'react';
import { Lock, Pencil, Plus, Trash2, X } from 'lucide-react';
import { notify } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { t as tr } from '../../i18n';
import { Cell } from '../../shell/cells';
import { lengthText, parseLength } from '../../engine/unitsStore';
import { depthInput, localSketches, PLANE_LABELS, SKETCH_PLANES, canConstrain, type PartPlane, type PartTree, type StepRow, type TreeSketch } from './partTreeModel';

/** An engine command; its refusal (or an unknown command) is the engine's own sentence, shown. */
async function run(command: string, args: Record<string, unknown>): Promise<boolean> {
  const r = await execute(command, args);
  if (r == null) { const e = useSession.getState().error; if (e) notify(e); return false; }
  return true;
}

/** Constrain part: a formula part's corners become a sketch of its own with dimensions (`studio.constrainPart`). Nothing when there is nothing to do. */
export function ConstrainPartButton({ tree, className = 'btn small' }: { tree: PartTree | null | undefined; className?: string }) {
  if (!tree || !canConstrain(tree)) return null;
  return (
    <button type="button" className={className} data-testid="constrain-part" data-field="constrain-part" title="The corners of its bodies become a sketch of the part's own, each dimension a formula of the parameters"
      onClick={() => void run('studio.constrainPart', { id: tree.id })}><Lock size={12} />Constrain part</button>
  );
}

/** A sketch's name and removal, beside it in the tree. */
export function LocalSketchActions({ partId, sketch }: { partId: string; sketch: TreeSketch }) {
  const [renaming, setRenaming] = useState(false);
  if (renaming) {
    return (
      <Cell value={sketch.name ?? sketch.id} testId="part-sketch-rename" style={{ width: 110 }}
        onCommit={(name) => { setRenaming(false); if (name.trim() && name.trim() !== sketch.name) void run('studio.renamePartSketch', { partId, sketchId: sketch.id, name: name.trim() }); }} />
    );
  }
  return (
    <>
      <button type="button" className="icon mini" data-part-sketch-rename={sketch.id} title="Rename the sketch" onClick={() => setRenaming(true)}><Pencil size={11} /></button>
      <button type="button" className="icon mini" data-part-sketch-remove={sketch.id} title="Remove the sketch (and the bodies drawn from it)"
        onClick={() => void run('studio.removePartSketch', { partId, sketchId: sketch.id })}><Trash2 size={11} /></button>
    </>
  );
}

/** + Sketch: a plane picker and the button. */
export function AddSketch({ partId }: { partId: string }) {
  const [plane, setPlane] = useState<PartPlane>('xy');
  return (
    <div className="row part-add" data-testid="part-add-sketch">
      <select value={plane} aria-label={tr('Plane')} onChange={(e) => setPlane(e.target.value as PartPlane)}>
        {SKETCH_PLANES.map((p) => <option key={p} value={p}>{tr(PLANE_LABELS[p])}</option>)}
      </select>
      <button type="button" className="btn small" data-testid="part-add-sketch-button" onClick={() => void run('studio.addPartSketch', { partId, plane })}><Plus size={12} />Sketch</button>
    </div>
  );
}

/** The bodies of a part's definition by name: what the tree does not say (depth, condition). */
function useBodies(typeId: string | undefined): Record<string, { depth: unknown; when?: string }> {
  const version = useSession((s) => s.version);
  const [bodies, setBodies] = useState<Record<string, { depth: unknown; when?: string }>>({});
  useEffect(() => {
    if (!typeId) return;
    let live = true;
    void query<{ parts: { id: string; def?: { bodies?: { name?: string; depth?: unknown; when?: unknown }[] } }[] }>('studio.parts', {}).then((r) => {
      const def = r?.parts.find((p) => p.id === typeId)?.def;
      if (!live || !def) return;
      const out: Record<string, { depth: unknown; when?: string }> = {};
      (def.bodies ?? []).forEach((b, i) => { out[b.name || `Body ${i + 1}`] = { depth: b.depth, ...(b.when != null && b.when !== '' ? { when: String(b.when) } : {}) }; });
      setBodies(out);
    });
    return () => { live = false; };
  }, [typeId, version]);
  return bodies;
}

const shownDepth = (d: unknown): string => (typeof d === 'number' ? lengthText(d) : typeof d === 'string' ? `=${d}` : '');

/** A body drawn from a sketch: its depth and condition, edited, and its removal; under its step in the tree. */
export function BodyControls({ tree, step }: { tree: PartTree; step: StepRow }) {
  const bodies = useBodies(tree.partTypeId);
  const own = bodies[step.label];
  return (
    <div className="part-body-edit" data-part-body={step.label}>
      <span className="muted small">Depth</span>
      <Cell value={shownDepth(own?.depth)} testId="part-body-depth" style={{ width: '100%' }} title="A length, or =formula of the parameters"
        onCommit={(text) => {
          const d = depthInput(text, parseLength);
          if (d == null) return false;
          void run('studio.updatePartBody', { partId: tree.id, name: step.label, depth: d });
        }} />
      <button type="button" className="icon mini" data-part-body-remove={step.label} title="Remove the body" onClick={() => void run('studio.removePartBody', { partId: tree.id, name: step.label })}><X size={11} /></button>
      <span className="muted small">Made when</span>
      <Cell value={own?.when ?? ''} testId="part-body-when" style={{ width: '100%' }} placeholder="HasShelf"
        onCommit={(text) => { void run('studio.updatePartBody', { partId: tree.id, name: step.label, when: text.trim() || null }); }} />
    </div>
  );
}

/** + Body from sketch: the sketch, its depth, the button. */
export function AddBody({ tree }: { tree: PartTree }) {
  const own = localSketches(tree);
  const [sketchId, setSketchId] = useState('');
  const [depth, setDepth] = useState('');
  useEffect(() => { if (!own.some((s) => s.id === sketchId)) setSketchId(own[0]?.id ?? ''); }, [own.map((s) => s.id).join(','), sketchId]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!own.length) return null;
  const add = () => {
    const d = depthInput(depth, parseLength);
    if (d == null) { notify('Type the body\'s depth: a length, or =formula.'); return; }
    void run('studio.addPartBody', { partId: tree.id, sketchId, depth: d }).then((ok) => { if (ok) setDepth(''); });
  };
  return (
    <div className="row part-add" data-testid="part-add-body">
      <select value={sketchId} aria-label={tr('Sketch')} onChange={(e) => setSketchId(e.target.value)}>
        {own.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name ?? s.id}</option>)}
      </select>
      <input value={depth} placeholder={tr('Depth')} style={{ width: 70 }} aria-label={tr('Depth')} data-testid="part-add-body-depth" data-no-i18n
        onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') add(); }} onChange={(e) => setDepth(e.target.value)} />
      <button type="button" className="btn small" data-testid="part-add-body-button" onClick={add}><Plus size={12} />Body from sketch</button>
    </div>
  );
}
