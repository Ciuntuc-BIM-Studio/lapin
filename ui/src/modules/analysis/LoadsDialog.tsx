/** Loads (docs/plan-fem.md §8): the load cases, the imposed loads by category, what the model carries, the EN 1990 combinations. All values are indicative. */

import { useEffect, useState } from 'react';
import { FileUp, Play, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { t, useLang } from '../../i18n';
import { PendingDialog } from '../core/PendingDialog';
import { openImportTable } from '../core/tables/tables';
import { useAnalysis } from './hooks';
import { runAnalysis } from './useResults';
import {
  ACTION_SOURCES, ACTION_SOURCE_LABEL, ACTION_TYPES, ACTION_TYPE_LABEL, combinationName, EXPOSURES, formatFactor, formatKn, imposedArgs, psiFrom,
  snowArgs, TERRAINS, windArgs,
  type CategoryDto, type ClimateDto, type CombinationsDto, type LoadCaseDto, type LoadsDto,
} from './loads';

type Tab = 'cases' | 'imposed' | 'site' | 'summary' | 'combinations';
const TABS: [Tab, string][] = [['cases', 'Cases'], ['imposed', 'Imposed loads'], ['site', 'Snow and wind'], ['summary', 'Summary'], ['combinations', 'Combinations']];
const CATEGORY_EXTRA = ['snow', 'wind'];

export function LoadsDialog() {
  const [tab, setTab] = useState<Tab>('cases');
  const error = useSession((s) => s.error);
  const loads = useAnalysis<LoadsDto>('analysis.loads');
  const cats = useAnalysis<CategoryDto[]>('analysis.categories');
  const combos = useAnalysis<CombinationsDto>('analysis.combinations', tab === 'combinations');
  const climate = useAnalysis<ClimateDto>('analysis.climate', tab === 'site');
  if (!loads || !cats) return <PendingDialog title="Loads" />;
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Loads">
      <div className="dialog" style={{ minWidth: 760 }}>
        <header className="dialog-head">
          <span className="panel-title">Loads</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="row" role="tablist">
            {TABS.map(([id, label]) => <button key={id} type="button" role="tab" aria-selected={tab === id} className={`btn${tab === id ? ' primary' : ''}`} onClick={() => setTab(id)}>{label}</button>)}
          </div>
          {tab === 'cases' && <CasesTab loads={loads} cats={cats} />}
          {tab === 'imposed' && <ImposedTab cats={cats} />}
          {tab === 'site' && (climate ? <SiteTab climate={climate} /> : <p className="muted">Reading the site's snow and wind…</p>)}
          {tab === 'summary' && <SummaryTab loads={loads} />}
          {tab === 'combinations' && (combos ? <CombinationsTab data={combos} /> : <p className="muted">Reading the combinations…</p>)}
          <div className="row"><button type="button" className="btn primary" data-testid="loads-run" title="Analyse the frame under these loads and show the results" onClick={() => void runAnalysis()}><Play size={14} />Run analysis</button>
            <button type="button" className="btn" data-testid="loads-import" title="Point, line and area loads from a CSV or Excel file or pasted cells" onClick={() => openImportTable('analysis.loads', 'Import loads')}><FileUp size={14} />Import loads…</button></div>
          <p className="muted small">The values are indicative (EN 1990, EN 1991-1-1, EN 1991-1-3 and EN 1991-1-4 as recommended): set the national annex values and check them before using the results.</p>
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
      </div>
    </div>
  );
}

function CasesTab({ loads, cats }: { loads: LoadsDto; cats: CategoryDto[] }) {
  const [name, setName] = useState('');
  const [type, setType] = useState<string>('VARIABLE_Q');
  useLang((s) => s.lang);
  const update = (id: string, args: Record<string, unknown>) => void execute('analysis.updateCase', { id, ...args });
  const categoryKeys = ['', ...cats.map((c) => c.key), ...CATEGORY_EXTRA];
  return (
    <>
      <table className="standards-table">
        <thead><tr><th>Code</th><th>Name</th><th>Type</th><th>Source</th><th>Category</th><th title="Combination factors ψ0, ψ1, ψ2; empty: the table's">ψ0 ψ1 ψ2</th><th /></tr></thead>
        <tbody>
          {loads.cases.map((c) => (
            <tr key={c.id}>
              <td><input key={c.id + c.code} style={{ width: 64 }} defaultValue={c.code} aria-label="Code" onBlur={(e) => e.target.value.trim() && e.target.value !== c.code && update(c.id, { code: e.target.value.trim() })} /></td>
              <td><input key={c.id + c.name + t(c.name)} defaultValue={t(c.name)} aria-label="Name" onBlur={(e) => e.target.value.trim() && e.target.value !== t(c.name) && update(c.id, { name: e.target.value.trim() })} /></td>
              <td><select value={c.actionType} onChange={(e) => update(c.id, { actionType: e.target.value })}>{ACTION_TYPES.map((t) => <option key={t} value={t}>{ACTION_TYPE_LABEL[t]}</option>)}</select></td>
              <td><select value={c.actionSource} onChange={(e) => update(c.id, { actionSource: e.target.value })}>{ACTION_SOURCES.map((t) => <option key={t} value={t}>{ACTION_SOURCE_LABEL[t]}</option>)}</select></td>
              <td><select value={c.category ?? ''} onChange={(e) => update(c.id, { category: e.target.value || null })}>{categoryKeys.map((k) => <option key={k} value={k}>{k === 'snow' ? 'Snow' : k === 'wind' ? 'Wind' : k || '(none)'}</option>)}</select></td>
              <td><PsiFields c={c} onChange={(psi) => update(c.id, { psi })} /></td>
              <td className="row-tools"><button type="button" className="icon mini" title="Remove" onClick={() => void execute('analysis.removeCase', { id: c.id })}><Trash2 size={12} /></button></td>
            </tr>
          ))}
        </tbody>
      </table>
      {loads.cases.length === 0 && <p className="muted">No load cases yet: add the standard ones, or your own.</p>}
      <div className="row">
        <input value={name} placeholder="New load case" onChange={(e) => setName(e.target.value)} />
        <select value={type} onChange={(e) => setType(e.target.value)}>{ACTION_TYPES.map((t) => <option key={t} value={t}>{ACTION_TYPE_LABEL[t]}</option>)}</select>
        <button type="button" className="btn" disabled={!name.trim()} onClick={() => void execute('analysis.addCase', { name: name.trim(), actionType: type }).then(() => { if (!useSession.getState().error) setName(''); })}><Plus size={14} />Add</button>
      </div>
      <div className="row">
        <button type="button" className="btn" onClick={() => void execute('analysis.defaultCases', {})}>Add standard cases</button>
        <button type="button" className="btn" title="One imposed-load case for each category the rooms use that has none" onClick={() => void execute('analysis.addMissingCases', {})}>Add missing cases</button>
        <button type="button" className="btn" data-testid="cases-import" title="Load cases from a CSV or Excel file or pasted cells" onClick={() => openImportTable('analysis.cases', 'Import load cases')}><FileUp size={14} />Import load cases…</button>
      </div>
      <p className="muted small">The cases a load goes in. The derived loads (finishes, partitions, rooms, snow, wind) go in the cases that list them.</p>
    </>
  );
}

/** ψ0, ψ1 and ψ2 of a case: the ones it has (the table's by default), committed when a field is left. */
function PsiFields({ c, onChange }: { c: LoadCaseDto; onChange: (psi: Record<string, number> | null) => void }) {
  const [v, setV] = useState(['0', '1', '2'].map((k) => (c.psi ? String(c.psi[k as '0']) : '')));
  const invalid = psiFrom(v[0]!, v[1]!, v[2]!) === undefined;
  const commit = () => { const psi = psiFrom(v[0]!, v[1]!, v[2]!); if (psi !== undefined) onChange(psi); };
  return (
    <span className="row">
      {v.map((t, i) => <input key={i} style={{ width: 40 }} aria-label={`ψ${i}`} aria-invalid={invalid} className={invalid ? 'invalid' : undefined} title={invalid ? 'Not a valid value' : undefined} value={t} onChange={(e) => setV(v.map((x, j) => (j === i ? e.target.value : x)))} onBlur={commit} />)}
    </span>
  );
}

function ImposedTab({ cats }: { cats: CategoryDto[] }) {
  return (
    <table className="standards-table">
      <thead><tr><th>Category</th><th>Use</th><th>qk (kN/m²)</th><th>Default</th><th /></tr></thead>
      <tbody>
        {cats.map((c) => (
          <tr key={c.key + c.qk}>
            <td>{c.key}</td><td>{c.label}</td>
            <td><QkField c={c} /></td>
            <td>{formatKn(c.default)}</td>
            <td>{c.overridden && <button type="button" className="btn mini" title="Back to the default value" onClick={() => void execute('analysis.setImposedLoad', { category: c.key, qk: null })}>Overridden</button>}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

/** A category's qk: committed when the field is left; an entry that does not read is marked and not sent. */
function QkField({ c }: { c: CategoryDto }) {
  const [text, setText] = useState(formatKn(c.qk));
  const invalid = imposedArgs(c.key, text) === undefined;
  const commit = () => { const a = imposedArgs(c.key, text); if (a && (a.qk ?? c.default) !== c.qk) void execute('analysis.setImposedLoad', a); };
  return <input style={{ width: 64 }} aria-label={`qk ${c.key}`} value={text} aria-invalid={invalid} className={invalid ? 'invalid' : undefined} title={invalid ? 'Not a valid value' : undefined} onChange={(e) => setText(e.target.value)} onBlur={commit} />;
}

function SummaryTab({ loads }: { loads: LoadsDto }) {
  useLang((s) => s.lang);
  const caseOf = (id: string) => loads.cases.find((c) => c.id === id);
  return (
    <>
      <table className="standards-table">
        <thead><tr><th>Case</th><th>Loads you drew</th><th>Total (kN)</th>{loads.storeys.map((s) => <th key={s.id}>{s.name} (kN)</th>)}</tr></thead>
        <tbody>
          {loads.cases.map((c) => (
            <tr key={c.id} className={c.empty ? 'muted' : undefined}>
              <td>{c.code} · {t(c.name)}</td><td>{c.userLoads}</td><td>{c.selfWeight && c.total === 0 ? 'by the analysis (self weight)' : formatKn(c.total)}</td>
              {loads.storeys.map((s) => <td key={s.id}>{formatKn(s.byCase[c.id] ?? 0)}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
      {loads.derived.length > 0 && (
        <>
          <span className="eyebrow">Derived loads</span>
          <table className="standards-table">
            <thead><tr><th>Kind</th><th>From</th><th>Case</th><th>Value</th><th>Total (kN)</th></tr></thead>
            <tbody>
              {loads.derived.map((d, i) => (
                <tr key={i}><td>{d.kind}</td><td>{d.sourceName}</td><td>{caseOf(d.caseId)?.code ?? ''}</td><td>{formatKn(d.value)} {d.unit}</td><td>{formatKn(d.total)}</td></tr>
              ))}
            </tbody>
          </table>
        </>
      )}
      {loads.missing.length > 0 && (
        <p className="warn-text"><strong>Missing load cases (category: rooms)</strong> {loads.missing.map((m) => `${m.category}: ${m.rooms}`).join(' · ')}. <span>Use Add missing cases.</span></p>
      )}
      {loads.notes.map((n, i) => <p key={i} className="muted small">{n}</p>)}
    </>
  );
}

/**
 * The site's snow and wind (docs/plan-fem.md §9.9): the values the user reads for the site from CR 1-1-3 and CR 1-1-4 (Lapin
 * does not ship their maps), each committed with Apply; the cases their loads go in are made by the engine.
 */
function SiteTab({ climate }: { climate: ClimateDto }) {
  const s = climate.snow;
  const w = climate.wind;
  const [snow, setSnow] = useState({ s0k: s ? String(s.s0k) : '', ce: String(s?.ce ?? 1), ct: String(s?.ct ?? 1), importance: String(s?.importance ?? 1) });
  const [wind, setWind] = useState({ qb: w ? String(w.qb) : '', terrain: w?.terrain ?? 'II', importance: String(w?.importance ?? 1) });
  useEffect(() => { setSnow({ s0k: s ? String(s.s0k) : '', ce: String(s?.ce ?? 1), ct: String(s?.ct ?? 1), importance: String(s?.importance ?? 1) }); }, [s?.s0k, s?.ce, s?.ct, s?.importance]);
  useEffect(() => { setWind({ qb: w ? String(w.qb) : '', terrain: w?.terrain ?? 'II', importance: String(w?.importance ?? 1) }); }, [w?.qb, w?.terrain, w?.importance]);
  const sa = snowArgs(snow.s0k, snow.ce, snow.ct, snow.importance);
  const wa = windArgs(wind.qb, wind.terrain, wind.importance);
  const field = (label: string, title: string, value: string, set: (v: string) => void, invalid: boolean, testid: string) => (
    <label className="row" title={title}>
      <span style={{ minWidth: 220 }}>{label}</span>
      <input style={{ width: 72 }} aria-label={label} data-testid={testid} value={value} aria-invalid={invalid} className={invalid ? 'invalid' : undefined} onChange={(e) => set(e.target.value)} />
    </label>
  );
  return (
    <>
      <span className="eyebrow">Snow (CR 1-1-3/2012, EN 1991-1-3)</span>
      {field('Ground snow load s0,k (kN/m²)', 'The characteristic snow load on the ground at the site: CR 1-1-3 Figure 3.1 or its Annex A', snow.s0k, (v) => setSnow({ ...snow, s0k: v }), !!snow.s0k.trim() && !sa, 'climate-s0k')}
      <label className="row" title="CR 1-1-3 Table 4.1: how the site exposes the roof to the wind">
        <span style={{ minWidth: 220 }}>Exposure Ce</span>
        <select aria-label="Exposure Ce" value={snow.ce} onChange={(e) => setSnow({ ...snow, ce: e.target.value })}>
          {EXPOSURES.map(([v, label]) => <option key={v} value={String(v)}>{label}</option>)}
          {!EXPOSURES.some(([v]) => String(v) === snow.ce) && <option value={snow.ce}>{snow.ce}</option>}
        </select>
      </label>
      {field('Thermal coefficient Ct', '1.0, less only over a glazed roof that loses heat (CR 1-1-3 4.2)', snow.ct, (v) => setSnow({ ...snow, ct: v }), !sa && !!snow.s0k.trim(), 'climate-ct')}
      {field('Importance factor γIs', 'CR 1-1-3 Table 4.2: 1.0 for ordinary buildings (class III, IV), 1.10 class II, 1.15 class I', snow.importance, (v) => setSnow({ ...snow, importance: v }), !sa && !!snow.s0k.trim(), 'climate-gis')}
      <div className="row">
        <button type="button" className="btn primary" data-testid="climate-snow-apply" disabled={!sa} onClick={() => sa && void execute('analysis.setClimate', { snow: sa })}>Apply the snow</button>
        {s && <button type="button" className="btn" onClick={() => void execute('analysis.setClimate', { snow: null })}>No snow</button>}
      </div>
      <p className="muted small">On each roof plane s = γIs · μ1 · Ce · Ct · s0,k per m² of plan (μ1 0.8 to 30°, then less to 0 at 60°), 0.8 on a floor nothing covers. The case S holds the undrifted snow, S+X to S-Y a duo-pitch roof's drifted snow (half on the plane facing the wind).</p>

      <span className="eyebrow">Wind (CR 1-1-4/2012, EN 1991-1-4)</span>
      {field('Reference wind pressure qb (kN/m²)', 'The reference pressure at 10 m at the site: CR 1-1-4 Figure 2.1 or its Annex A', wind.qb, (v) => setWind({ ...wind, qb: v }), !!wind.qb.trim() && !wa, 'climate-qb')}
      <label className="row" title="EN 1991-1-4 Table 4.1: the roughness of the terrain round the site">
        <span style={{ minWidth: 220 }}>Terrain category</span>
        <select aria-label="Terrain category" data-testid="climate-terrain" value={wind.terrain} onChange={(e) => setWind({ ...wind, terrain: e.target.value })}>
          {TERRAINS.map((k) => { const x = climate.terrains.find((tt) => tt.key === k); return <option key={k} value={k}>{x ? `${k} · ${t(x.label)}` : k}</option>; })}
        </select>
      </label>
      {field('Importance factor γIw', 'CR 1-1-4 Table 3.3: 1.0 for ordinary buildings (class III, IV), 1.10 class II, 1.15 class I', wind.importance, (v) => setWind({ ...wind, importance: v }), !wa && !!wind.qb.trim(), 'climate-giw')}
      <div className="row">
        <button type="button" className="btn primary" data-testid="climate-wind-apply" disabled={!wa} onClick={() => wa && void execute('analysis.setClimate', { wind: wa })}>Apply the wind</button>
        {w && <button type="button" className="btn" onClick={() => void execute('analysis.setClimate', { wind: null })}>No wind</button>}
      </div>
      <p className="muted small">The wind blows towards +X, -X, +Y and -Y, each in two cases: the roofs' suction (/s) and their pressure (/p). Walls and columns make the building's outline; the façades go to the beams, rails and floor edges along them, the roofs to their rafters or purlins.</p>
      <p className="muted small">Lapin does not ship the national maps: read the site's values from CR 1-1-3 and CR 1-1-4 (or the national annex you work to). The rules and coefficients are the recommended ones, indicative.</p>
    </>
  );
}

const GROUPS: [string, string[]][] = [['Ultimate limit state', ['uls']], ['Serviceability limit state', ['sls-characteristic', 'sls-frequent', 'sls-quasi']]];
const KIND_LABEL: Record<string, string> = { 'sls-characteristic': 'Characteristic', 'sls-frequent': 'Frequent', 'sls-quasi': 'Quasi-permanent' };

function CombinationsTab({ data }: { data: CombinationsDto }) {
  return (
    <>
      {GROUPS.map(([label, kinds]) => {
        const list = data.combinations.filter((c) => kinds.includes(c.kind));
        return (
          <div key={label}>
            <span className="eyebrow">{label}</span>
            {list.length === 0 ? <p className="muted">None: nothing carries a load yet.</p> : (
              <table className="standards-table">
                <thead><tr><th>{kinds.length > 1 ? 'Kind' : 'Name'}</th>{kinds.length > 1 && <th>Name</th>}<th>Factors</th></tr></thead>
                <tbody>
                  {list.map((c) => (
                    <tr key={c.key}>
                      {kinds.length > 1 && <td>{KIND_LABEL[c.kind] ?? c.kind}</td>}
                      <td>{combinationName(c)}</td>
                      <td>{c.factors.map((f) => `${f.code} × ${formatFactor(f.factor)}`).join(', ')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        );
      })}
      {data.notes.map((n, i) => <p key={i} className="muted small">{n}</p>)}
    </>
  );
}
