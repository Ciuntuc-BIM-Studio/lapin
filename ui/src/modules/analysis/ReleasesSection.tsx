/** A bar's analysis in its properties (docs/plan-fem.md §9.1, §9.6): the releases at its two ends, and its extremes in the results shown. */

import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { selectionWith } from '../../shell/params';
import { useResults } from './useResults';
import { ACTS_IN, actsIn, onlyArg, type ActsIn } from './stability';
import {
  BAR_CLASSES, DOFS, DOF_TITLE, formatMm, formatValue, FORCE_QUANTITIES, ownReleases, releasePreset, releaseValue, spanRatio, toggleRelease,
  type Release, type ReleasePreset,
} from './results';

const PRESETS: [ReleasePreset, string][] = [['default', 'Default'], ['fixed', 'Fixed'], ['pinned', 'Pinned'], ['custom', 'Custom']];
const LABEL: Record<string, string> = { ux: 'Ux', uy: 'Uy', uz: 'Uz', rx: 'Rx', ry: 'Ry', rz: 'Rz' };

function End({ end, own, ids }: { end: 'i' | 'j'; own: Release | null; ids: string[] }) {
  const preset = releasePreset(own);
  const set = (value: unknown) => void execute('analysis.setReleases', { ids, [end]: value });
  return (
    <>
      <dt>{end === 'i' ? 'Start (i)' : 'End (j)'}</dt>
      <dd>
        <select data-field={`release-${end}`} aria-label={end === 'i' ? 'Release at the start' : 'Release at the end'} value={preset}
          onChange={(e) => set(releaseValue(e.target.value as ReleasePreset, own))}>
          {PRESETS.map(([p, label]) => <option key={p} value={p}>{label}</option>)}
        </select>
        {preset === 'custom' && own && (
          <span className="row releases" data-testid={`releases-${end}`}>
            {DOFS.map((d, k) => (
              <label key={d} className="small" title={DOF_TITLE[d]}>
                <input type="checkbox" checked={own[k]} onChange={() => set(toggleRelease(own, k))} />{LABEL[d]}
              </label>
            ))}
          </span>
        )}
      </dd>
    </>
  );
}

export function ReleasesSection({ node }: { node: NodeDto }) {
  const ids = selectionWith(node, (n) => (BAR_CLASSES as readonly string[]).includes(n.type));
  const own = ownReleases(node.props.analysisReleases);
  const results = useResults((s) => (s.stale ? null : s.results));
  const mine = ids.length === 1 && results ? results.frames.filter((f) => f.sourceId === node.id) : [];
  return (
    <>
      <dl className="fields" data-testid="releases">
        <End end="i" own={own.i} ids={ids} />
        <End end="j" own={own.j} ids={ids} />
        <dt>Axial member</dt>
        <dd>
          <select data-field="axial" aria-label="Axial member" title="A rod or tie works in tension and compression only: pinned at its ends, its weight carried to them"
            value={typeof node.props.analysisAxial === 'boolean' ? String(node.props.analysisAxial) : ''}
            onChange={(e) => void execute('analysis.setAxial', { ids, axial: e.target.value === '' ? null : e.target.value === 'true' })}>
            <option value="">By its section and use</option>
            <option value="true">Yes (a rod or tie)</option>
            <option value="false">No (a beam)</option>
          </select>
        </dd>
        <dt>Acts in</dt>
        <dd>
          <select data-field="acts-in" aria-label="Acts in" title="A one-way member (a brace, a tie, a strut) is taken out of the combinations where it would work the other way: each combination is then solved on its own"
            value={actsIn(node.props.analysisOnly)} onChange={(e) => void execute('analysis.setOnly', { ids, only: onlyArg(e.target.value as ActsIn) })}>
            {ACTS_IN.map(([v, label]) => <option key={v} value={v}>{label}</option>)}
          </select>
        </dd>
        <dt>Braced by sheeting</dt>
        <dd>
          <input type="checkbox" data-field="sheeting" aria-label="Braced by sheeting" data-testid="analysis-sheeting" title="Held straight across in its sheeting's plane between the members it rests on (a purlin, a wall rail)"
            checked={node.props.analysisSheeting === true} onChange={(e) => void execute('analysis.setSheeting', { ids, sheeting: e.target.checked ? true : null })} />
        </dd>
      </dl>
      <p className="muted small">A ticked freedom is released at that end. Default: what the connection says (a cleat or gusset pins), a timber roof member pinned about its strong axis, any other bar fixed.</p>
      {mine.length > 0 && results && <Extremes frames={mine} name={results.name} />}
    </>
  );
}

function Extremes({ frames, name }: { frames: { id: string; length: number; extremes: Record<string, { max: number; min: number }>; deflection?: { max: number } }[]; name: string }) {
  const hi = (q: string) => Math.max(...frames.map((f) => f.extremes[q]?.max ?? -Infinity));
  const lo = (q: string) => Math.min(...frames.map((f) => f.extremes[q]?.min ?? Infinity));
  const length = frames.reduce((a, f) => a + f.length, 0);
  const defl = Math.max(...frames.map((f) => Math.abs(f.deflection?.max ?? 0)));
  return (
    <table className="standards-table" data-testid="bar-extremes">
      <caption className="muted small" data-no-i18n>{name}</caption>
      <thead><tr><th /><th>Largest</th><th>Smallest</th></tr></thead>
      <tbody>
        {FORCE_QUANTITIES.filter((q) => Number.isFinite(hi(q))).map((q) => <tr key={q}><td>{q}</td><td data-no-i18n>{formatValue(hi(q))}</td><td data-no-i18n>{formatValue(lo(q))}</td></tr>)}
        {defl > 0 && <tr><td>Deflection</td><td colSpan={2} data-no-i18n>{formatMm(defl)}{spanRatio(length, defl) ? ` · ${spanRatio(length, defl)}` : ''}</td></tr>}
      </tbody>
    </table>
  );
}
