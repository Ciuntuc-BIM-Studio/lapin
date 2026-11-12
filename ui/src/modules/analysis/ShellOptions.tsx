/** The analysis' options for slabs and walls (docs/plan-fem.md §10.10, §10.12): which are shells and the mesh's size; and the largest values on them. */

import { useEffect, useState } from 'react';
import { execute } from '../../engine/session';
import { useAnalysis } from './hooks';
import { formatValue, type ResultsDto } from './results';
import {
  shellExtremes, shownScale, shownUnit, SHELL_CHOICES, SHELL_CHOICE_LABEL, SHELL_QUANTITIES, SHELL_TITLE,
  type AnalysisOptionsDto,
} from './shells';

/** Which slabs and walls are shells, and the mesh's target size: the project's, one undo step each; the next run takes them. */
export function ShellOptions() {
  const o = useAnalysis<AnalysisOptionsDto>('analysis.options');
  const [size, setSize] = useState('');
  useEffect(() => { if (o) setSize(String(o.meshSize)); }, [o]);
  if (!o) return null;
  const apply = () => {
    const v = Number(size.trim().replace(',', '.'));
    if (Number.isFinite(v) && v > 0 && v !== o.meshSize) void execute('analysis.setOptions', { meshSize: v });
    else setSize(String(o.meshSize));
  };
  return (
    <div className="row wrap" data-testid="analysis-options">
      <span className="eyebrow">Shells</span>
      <select aria-label="Slabs and walls as shells" data-testid="analysis-shells" value={o.shells} title="Concrete slabs and walls analysed as shells; the others as before (a slab's loads by the 45° rule, a wall a line support)"
        onChange={(e) => void execute('analysis.setOptions', { shells: e.target.value })}>
        {SHELL_CHOICES.map((c) => <option key={c} value={c}>{SHELL_CHOICE_LABEL[c]}</option>)}
      </select>
      <label className="row small" title="The mesh's size in metres; a narrow piece gets at least four elements across it">
        <span>Mesh</span>
        <input aria-label="Mesh size" data-testid="analysis-mesh" style={{ width: 52 }} value={size} onChange={(e) => setSize(e.target.value)} onBlur={apply} onKeyDown={(e) => { if (e.key === 'Enter') apply(); }} />
        <span className="muted" data-no-i18n>m</span>
      </label>
    </div>
  );
}

/** The largest and least of each quantity over the slabs and walls, where (the engineer's sign: a slab's moments with the bottom in tension positive). */
export function ShellMaxima({ results }: { results: ResultsDto }) {
  const surfaces = results.shells ?? [];
  if (surfaces.length === 0) return null;
  const where = (e: { name: string; at: number[] }) => `${e.name} · (${e.at.map((x) => formatValue(x)).join(', ')})`;
  return (
    <table className="standards-table" data-testid="analysis-shell-maxima">
      <thead><tr><th>Slabs and walls</th><th>Largest</th><th>Where</th><th>Smallest</th><th>Where</th></tr></thead>
      <tbody>
        {SHELL_QUANTITIES.map((q) => {
          const m = shellExtremes(surfaces, q);
          if (!m) return null;
          const unit = q === 'w' ? 'm' : q[0] === 'm' ? 'kN·m/m' : 'kN/m';
          const k = shownScale(unit);
          const [hi, lo] = [m.max, m.min];
          return (
            <tr key={q}>
              <td title={SHELL_TITLE[q]}><span data-no-i18n>{q}</span> <span className="muted small" data-no-i18n>{shownUnit(unit)}</span></td>
              <td data-no-i18n>{formatValue(hi.value * k)}</td><td data-no-i18n>{where(hi)}</td>
              <td data-no-i18n>{formatValue(lo.value * k)}</td><td data-no-i18n>{where(lo)}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
