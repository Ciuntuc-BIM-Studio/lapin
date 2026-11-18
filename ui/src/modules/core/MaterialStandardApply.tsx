/**
 * Apply a material standard to the project, with the preview first (docs/plan-material-graphics.md, stage 3): per material, its
 * graphics now and after (swatches for the cut in a section, in a plan and the elevation), only the ones that change when asked,
 * and "Overwrite graphics set by hand": without it a material with graphics of its own is left as it is. One undo step.
 */

import { useEffect, useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { notify } from '../../app/store';
import { HatchSwatch } from './HatchSwatch';
import { ask, run } from './LibraryChoices';
import { swatchFill, swatchInk, type ResolvedGraphics } from './materialGraphicsModel';
import { appliedSentence, applyArgs, shownRows, type ApplyPreview, type ApplyRow } from './materialStandardsModel';

const KIND_TITLES = { section: 'Cut (section)', plan: 'Cut (plan)', elevation: 'Elevation' } as const;

function Look({ look, kind }: { look: unknown; kind: keyof typeof KIND_TITLES }) {
  const r = (look ?? undefined) as ResolvedGraphics | undefined;
  if (!r) return <span className="standard-look-none muted small">—</span>;
  const ink = swatchInk(r, '#000000');
  return <span title={KIND_TITLES[kind]}><HatchSwatch hatch={r.hatch} scale={r.hatchScale} fill={swatchFill(r, '#cccccc')} ink={ink.color} inkOpacity={ink.opacity} size={22} membrane={kind !== 'elevation' && r.membrane} /></span>;
}

const Row = ({ m }: { m: ApplyRow }) => (
  <tr data-testid="apply-row" data-changed={m.changed || undefined}>
    <td data-no-i18n>{m.name}</td>
    {(['section', 'plan', 'elevation'] as const).map((k) => (
      <td key={k} className="standard-look">
        <span className="standard-pair"><Look look={m.before[k]} kind={k} />{m.changed ? <><ArrowRight size={11} aria-hidden /><Look look={m.after[k]} kind={k} /></> : null}</span>
      </td>
    ))}
    <td className="muted small">{m.changed ? (m.cleared ? 'its own graphics are replaced' : 'changes') : 'unchanged'}</td>
  </tr>
);

export function MaterialStandardApply({ id, name, onDone }: { id: string; name: string; onDone: (applied: boolean) => void }) {
  const [overwrite, setOverwrite] = useState(false);
  const [changedOnly, setChangedOnly] = useState(true);
  const [preview, setPreview] = useState<ApplyPreview | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    let live = true;
    setPreview(null);
    void ask<ApplyPreview>('standards.applyPreview', applyArgs(id, overwrite)).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [id, overwrite]);
  const apply = async () => {
    setBusy(true);
    try {
      const r = await run<{ standard: string; materials: number; patternsAdded: number; patternsUpdated: number }>('standards.applyMaterials', applyArgs(id, overwrite));
      if (r) { notify(appliedSentence(r)); onDone(true); }
    } finally { setBusy(false); }
  };
  const rows = preview ? shownRows(preview, changedOnly) : [];
  return (
    <section className="standard-apply" data-testid="standard-apply">
      <div className="row"><span className="panel-title">Apply to the project: <span data-no-i18n>{name}</span></span><span className="grow" /></div>
      <label className="field-line check"><input type="checkbox" data-field="apply-overwrite" checked={overwrite} onChange={(e) => setOverwrite(e.target.checked)} />
        <span>Overwrite graphics set by hand</span></label>
      <p className="muted small">Without it, a material with graphics of its own keeps them: the standard gives only the materials that have none.</p>
      <label className="field-line check"><input type="checkbox" data-field="apply-changed-only" checked={changedOnly} onChange={(e) => setChangedOnly(e.target.checked)} /><span>Show only what changes</span></label>
      {!preview && <p className="muted small">Reading…</p>}
      {preview && (
        <>
          <p className="small" data-testid="apply-count">{`${preview.changed} of ${preview.materials.length} materials change.`}</p>
          <div className="standard-apply-table">
            <table>
              <thead><tr><th>Material</th>{(['section', 'plan', 'elevation'] as const).map((k) => <th key={k}>{KIND_TITLES[k]}</th>)}<th /></tr></thead>
              <tbody>{rows.map((m) => <Row key={m.id} m={m} />)}</tbody>
            </table>
          </div>
        </>
      )}
      <div className="row">
        <button type="button" className="btn primary" data-action="standard-apply" disabled={busy || !preview} onClick={() => void apply()}>Apply</button>
        <button type="button" className="btn" onClick={() => onDone(false)}>Cancel</button>
      </div>
    </section>
  );
}
