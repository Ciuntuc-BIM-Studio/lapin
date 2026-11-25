/** The Combinations tab of the Loads dialog (docs/plan-fem.md §13): the project's factors, the combinations (switched on or off), the user's own. */

import { useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import { t, useLang } from '../../i18n';
import { Field } from './ChecksSection';
import {
  addCombinationArgs, boundedOrNull, COMBINATION_KINDS, COMBINATION_KIND_LABEL, combinationName, factorSetOptions, factorsPayload, factorTexts, formatFactor,
  GAMMA_RANGES, groupCombinations, PSI_CATEGORIES, psiFrom,
  type CombinationDto, type CombinationKind, type CombinationOptionsDto, type CombinationsDto, type FactorSet, type LoadCaseDto,
} from './loads';

const setOptions = (args: Record<string, unknown>) => void execute('analysis.setCombinationOptions', args);

export function CombinationsTab({ data, cases }: { data: CombinationsDto; cases: LoadCaseDto[] }) {
  useLang((s) => s.lang);
  const { ultimate, serviceability } = groupCombinations(data.combinations);
  const groups: [string, CombinationDto[]][] = [['Ultimate limit state', ultimate], ['Serviceability limit state', serviceability]];
  return (
    <>
      {data.options && <FactorsSection o={data.options} />}
      {groups.map(([label, list]) => (
        <div key={label}>
          <span className="eyebrow">{label}</span>
          {list.length === 0 ? <p className="muted">None: nothing carries a load yet.</p> : (
            <table className="standards-table">
              <thead><tr><th>On</th><th>Kind</th><th>Name</th><th>Factors</th><th /></tr></thead>
              <tbody>{list.map((c) => <CombinationRow key={c.key + (c.id ?? '')} c={c} cases={cases} />)}</tbody>
            </table>
          )}
        </div>
      ))}
      <AddCombination cases={cases} />
      {data.notes.map((n, i) => <p key={i} className="muted small">{n}</p>)}
    </>
  );
}

/** The factor set, γG,sup, γG,inf, γQ, the expression, ξ and the ψ by category; what the project overrides is marked and can be reset. */
function FactorsSection({ o }: { o: CombinationOptionsDto }) {
  const gammas: [keyof typeof GAMMA_RANGES, string, string][] = [
    ['gammaGSup', 'γG,sup', 'Partial factor of an unfavourable permanent action'],
    ['gammaGInf', 'γG,inf', 'Partial factor of a favourable permanent action'],
    ['gammaQ', 'γQ', 'Partial factor of a variable action'],
  ];
  return (
    <>
      <span className="eyebrow">Factors</span>
      <div className="row wrap" data-testid="combo-factors">
        <label className="row small" title="The partial factors γ and the combination factors ψ the combinations use: the project's choice, indicative values">
          <span>Set of factors</span>
          <select aria-label="Set of factors" data-testid="combo-set" value={o.set} onChange={(e) => setOptions({ set: e.target.value as FactorSet })}>
            {factorSetOptions(o.sets).map((x) => <option key={x.key} value={x.key} data-no-i18n={x.ours ? undefined : true}>{x.label}</option>)}
          </select>
        </label>
        {Object.keys(o.sets[o.set]?.safetyClasses ?? {}).length > 0 && (
          <label className="row small" title="γd of the class multiplies the unfavourable actions of the ultimate combinations (the rules' Loads give it)">
            <span>Safety class</span>
            <select aria-label="Safety class" data-testid="combo-safety-class" value={o.safetyClass ?? ''} onChange={(e) => setOptions({ safetyClass: e.target.value || null })}>
              <option value="">None (γd 1)</option>
              {Object.entries(o.sets[o.set]!.safetyClasses!).map(([k, g]) => <option key={k} value={k} data-no-i18n>{`${k} (γd ${formatFactor(g)})`}</option>)}
            </select>
          </label>
        )}
        {gammas.map(([key, label, title]) => {
          const own = o.own[key];
          return (
            <span key={key} className="row">
              <Field label={label} title={title} test={`combo-${key}`} width={52} value={formatFactor(o[key])}
                onApply={(text) => { const v = boundedOrNull(text, GAMMA_RANGES[key][0], GAMMA_RANGES[key][1]); if (v === undefined) return false; setOptions({ [key]: v }); return true; }} />
              {own != null && <button type="button" className="btn mini" data-testid={`combo-reset-${key}`} title="Back to the set's value" onClick={() => setOptions({ [key]: null })}>Reset</button>}
            </span>
          );
        })}
        <label className="row small" title="6.10 is one expression; 6.10a and 6.10b are two, the second with ξ on the permanent actions (EN 1990 A1.2.2)">
          <span>Expression</span>
          <select aria-label="Expression" data-testid="combo-expression" value={o.expression} onChange={(e) => setOptions({ expression: e.target.value })}>
            <option value="6.10" data-no-i18n>6.10</option>
            <option value="6.10ab">6.10a and 6.10b</option>
          </select>
        </label>
        {o.expression === '6.10ab' && (
          <span className="row">
            <Field label="ξ" title="The reduction of the permanent actions in 6.10b (0.5 to 1; 0.85 recommended)" test="combo-xi" width={52} value={formatFactor(o.xi)}
              onApply={(text) => { const v = boundedOrNull(text, GAMMA_RANGES.xi[0], GAMMA_RANGES.xi[1]); if (v === undefined) return false; setOptions({ xi: v }); return true; }} />
            {o.own.xi != null && <button type="button" className="btn mini" data-testid="combo-reset-xi" title="Back to the set's value" onClick={() => setOptions({ xi: null })}>Reset</button>}
          </span>
        )}
      </div>
      <details className="small" data-testid="combo-psi">
        <summary>ψ by category</summary>
        <table className="standards-table">
          <thead><tr><th>Category</th><th>ψ0 ψ1 ψ2</th><th /></tr></thead>
          <tbody>{PSI_CATEGORIES.map((cat) => <PsiRow key={cat + JSON.stringify(o.psi[cat])} cat={cat} o={o} />)}</tbody>
        </table>
        <p className="muted">An empty row takes the set's values.</p>
      </details>
    </>
  );
}

/** One category's ψ: the project's own values in the fields, the effective ones when it has none; committed when a field is left. */
function PsiRow({ cat, o }: { cat: string; o: CombinationOptionsDto }) {
  const own = o.own.psi[cat];
  const eff = o.psi[cat];
  const [v, setV] = useState(['0', '1', '2'].map((k) => (own ? String(own[k as '0']) : '')));
  const invalid = psiFrom(v[0]!, v[1]!, v[2]!) === undefined;
  const commit = () => { const psi = psiFrom(v[0]!, v[1]!, v[2]!); if (psi !== undefined && JSON.stringify(psi) !== JSON.stringify(own ?? null)) setOptions({ psi: { [cat]: psi } }); };
  return (
    <tr data-key={cat}>
      <td data-no-i18n>{cat}</td>
      <td>
        <span className="row">
          {v.map((text, i) => (
            <input key={i} style={{ width: 44 }} aria-label={`${cat} ψ${i}`} data-testid={`combo-psi-${cat}-${i}`} aria-invalid={invalid} className={invalid ? 'invalid' : undefined}
              placeholder={eff ? String(eff[String(i) as '0']) : ''} value={text} onChange={(e) => setV(v.map((x, j) => (j === i ? e.target.value : x)))} onBlur={commit} />
          ))}
        </span>
      </td>
      <td>{own && <button type="button" className="btn mini" title="Back to the set's value" onClick={() => setOptions({ psi: { [cat]: null } })}>Reset</button>}</td>
    </tr>
  );
}

function CombinationRow({ c, cases }: { c: CombinationDto; cases: LoadCaseDto[] }) {
  const enabled = c.enabled !== false;
  const user = c.source === 'user' && !!c.id;
  const [editing, setEditing] = useState(false);
  const label = t(COMBINATION_KIND_LABEL[c.kind] ?? c.kind);
  return (
    <>
      <tr data-key={c.key} data-testid={`combo-row-${c.key}`} style={enabled ? undefined : { opacity: 0.5 }}>
        <td><input type="checkbox" aria-label="On" data-testid={`combo-enabled-${c.key}`} checked={enabled} title="Off: left out of the results, the envelopes, the checks and the export"
          onChange={(e) => void execute('analysis.setCombinationEnabled', { key: c.key, enabled: e.target.checked })} /></td>
        <td>{user ? (
          <select aria-label="Kind" value={c.kind} onChange={(e) => void execute('analysis.updateCombination', { id: c.id, kind: e.target.value })}>
            {COMBINATION_KINDS.map((k) => <option key={k} value={k}>{COMBINATION_KIND_LABEL[k]}</option>)}
          </select>
        ) : label}</td>
        <td>{user ? (
          <input key={c.id + c.name} defaultValue={c.name} aria-label="Name" onBlur={(e) => { const n = e.target.value.trim(); if (n && n !== c.name) void execute('analysis.updateCombination', { id: c.id, name: n }); }} />
        ) : combinationName(c)}
          {c.missing && c.missing.length > 0 && <div className="warn-text small">Names a load case that is gone</div>}
        </td>
        <td>{c.factors.map((f) => `${f.code} × ${formatFactor(f.factor)}`).join(', ')}</td>
        <td className="row-tools">{user && (
          <>
            <button type="button" className="icon mini" title="Edit the factors" data-testid={`combo-edit-${c.key}`} onClick={() => setEditing(!editing)}><Pencil size={12} /></button>
            <button type="button" className="icon mini" title="Remove" data-testid={`combo-remove-${c.key}`} onClick={() => void execute('analysis.removeCombination', { id: c.id })}><Trash2 size={12} /></button>
          </>
        )}</td>
      </tr>
      {user && editing && (
        <tr><td /><td colSpan={4}>
          <FactorForm cases={cases} initial={factorTexts(c.factors)} actionLabel="Save" testPrefix={`combo-${c.key}`}
            onSubmit={(texts) => { const f = factorsPayload(texts); if (!f) return; void execute('analysis.updateCombination', { id: c.id, factors: f }).then(() => { if (!useSession.getState().error) setEditing(false); }); }} />
        </td></tr>
      )}
    </>
  );
}

/** One field for each load case; an empty field leaves the case out. */
function FactorForm({ cases, initial, actionLabel, testPrefix, onSubmit, extra, disabled }: {
  cases: LoadCaseDto[]; initial: Record<string, string>; actionLabel: string; testPrefix: string; onSubmit: (texts: Record<string, string>) => void; extra?: boolean; disabled?: boolean;
}) {
  const [texts, setTexts] = useState(initial);
  const invalid = factorsPayload(texts) === undefined;
  return (
    <div className="row wrap">
      {cases.map((c) => (
        <label key={c.id} className="row small" title={t(c.name)}>
          <span>{c.code}</span>
          <input style={{ width: 52 }} aria-label={c.code} data-testid={`${testPrefix}-factor-${c.code}`} aria-invalid={invalid} value={texts[c.id] ?? ''} placeholder="–"
            onChange={(e) => setTexts({ ...texts, [c.id]: e.target.value })} />
        </label>
      ))}
      <button type="button" className="btn" data-testid={extra ? 'combo-add' : `${testPrefix}-save`} disabled={invalid || disabled} onClick={() => onSubmit(texts)}>{extra ? <Plus size={14} /> : null}{actionLabel}</button>
    </div>
  );
}

function AddCombination({ cases }: { cases: LoadCaseDto[] }) {
  const [name, setName] = useState('');
  const [kind, setKind] = useState<CombinationKind>('uls');
  const [version, setVersion] = useState(0);
  return (
    <>
      <span className="eyebrow">Add a combination</span>
      <div className="row wrap">
        <input value={name} placeholder="New combination" aria-label="Name" data-testid="combo-add-name" onChange={(e) => setName(e.target.value)} />
        <select aria-label="Kind" data-testid="combo-add-kind" value={kind} onChange={(e) => setKind(e.target.value as CombinationKind)}>
          {COMBINATION_KINDS.map((k) => <option key={k} value={k}>{COMBINATION_KIND_LABEL[k]}</option>)}
        </select>
      </div>
      <FactorFormAdd key={version} cases={cases} name={name} kind={kind} onDone={() => { setName(''); setVersion(version + 1); }} />
      <p className="muted small">A factor on each case it takes; an empty field leaves the case out. Ultimate and accidental combinations go to the ultimate envelope and the checks, serviceability ones to their envelopes (the characteristic one also to the deflection checks), a seismic one is its factors ± AEd once the seismic parameters are given.</p>
    </>
  );
}

function FactorFormAdd({ cases, name, kind, onDone }: { cases: LoadCaseDto[]; name: string; kind: CombinationKind; onDone: () => void }) {
  return (
    <FactorForm cases={cases} initial={{}} actionLabel="Add combination" testPrefix="combo-add" extra disabled={!name.trim()}
      onSubmit={(texts) => {
        const args = addCombinationArgs(name, kind, texts);
        if (!args) return;
        void execute('analysis.addCombination', args).then(() => { if (!useSession.getState().error) onDone(); });
      }} />
  );
}
