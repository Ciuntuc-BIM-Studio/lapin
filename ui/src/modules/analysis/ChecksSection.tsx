/** The results dialog's design checks (docs/plan-fem.md §12.7): the members' utilisation, the slabs' and walls' required reinforcement, the masonry walls per storey, the project's design values. Indicative: the Eurocodes' recommended values and CR 6-2013, the signed design is the engineer's. */

import { useEffect, useMemo, useState } from 'react';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { useResults } from './useResults';
import { ensureChecks, setColour, useChecks } from './useChecks';
import {
  filterRows, formatArea, formatUtilisation, groupBySource, LAYERS, LAYER_LABEL, MATERIAL_LABEL, MATERIALS, PROJECT_FIELDS, projectText, projectValue,
  storeysOf, summary, surfaceRows, utilisationColour, wallRows, wallSums, type DesignOptionsDto, type ElementRow, type SurfaceRow,
} from './checks';
import { useAnalysis } from './hooks';

/** Selects an element in the model and closes the dialog so it can be seen. */
function select(id: string): void { useUi.setState({ selected: [id], selection: id, dialog: null }); }

export function UtilisationBar({ u }: { u: number | null }) {
  const w = u === null ? 0 : Math.min(1, u) * 100;
  return (
    <span className="util" data-testid="utilisation" data-u={u === null ? '' : u.toFixed(3)}>
      <span className="util-bar" aria-hidden="true"><span style={{ width: `${w}%`, background: utilisationColour(u) }} /></span>
      <b className={u !== null && u > 1 ? 'warn-text' : undefined} data-no-i18n>{formatUtilisation(u)}</b>
    </span>
  );
}

const governing = (r: ElementRow) => (r.governing ? r.governing.name : '–');

export function ChecksSection() {
  const run = useResults((s) => s.run);
  const stale = useResults((s) => s.stale);
  const { checks, loading, error, colour } = useChecks();
  const nodes = useSession((s) => s.nodes);
  const [material, setMaterial] = useState('');
  const [storey, setStorey] = useState('');
  useEffect(() => { void ensureChecks(); }, [run, stale]);

  const members = useMemo(() => groupBySource(checks?.members ?? []), [checks]);
  const slabs = useMemo(() => surfaceRows(checks?.surfaces ?? []), [checks]);
  const names = useMemo(() => new Map([...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey').map((n) => [n.id, n.name ?? n.id] as const)), [nodes]);
  const storeys = storeysOf([...members, ...slabs], names);
  const shownMembers = filterRows(members, material, storey);
  const shownSlabs = filterRows(slabs, material, storey);
  const sum = summary(shownMembers);

  if (!run?.ok) return null;
  return (
    <section className="checks" data-testid="analysis-checks">
      <div className="row between">
        <span className="eyebrow">Checks</span>
        <label className="row small" title="Colour the bars, slabs and walls in the view by their utilisation, from green to red">
          <input type="checkbox" data-testid="checks-colour" checked={colour} onChange={(e) => { setColour(e.target.checked); if (e.target.checked) useResults.setState({ show: true }); }} />Colour the model by utilisation
        </label>
      </div>
      <p className="warn-text small" role="note" data-testid="checks-indicative">Indicative results: the Eurocodes&apos; recommended values and CR 6-2013, not the national annexes&apos;. The checks, the loads and the design values are yours to verify before you rely on them.</p>
      {stale && <p className="warn-text small" role="status">The model has changed since this run: the checks are out of date.</p>}
      {loading && !checks && <p className="muted small">Checking…</p>}
      {error && !checks && <p className="muted small">{error}</p>}
      {checks && (
        <>
          <div className="row wrap">
            <label className="row small"><span>Material</span>
              <select aria-label="Material" data-testid="checks-material" value={material} onChange={(e) => setMaterial(e.target.value)}>
                <option value="">All</option>
                {MATERIALS.map((m) => <option key={m} value={m}>{MATERIAL_LABEL[m]}</option>)}
              </select>
            </label>
            <label className="row small"><span>Storey</span>
              <select aria-label="Storey" data-testid="checks-storey" value={storey} onChange={(e) => setStorey(e.target.value)}>
                <option value="">All</option>
                {storeys.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
              </select>
            </label>
            <span className="muted small" data-testid="checks-summary"><span>Checked</span> <b data-no-i18n>{sum.checked}</b>{sum.over > 0 && <> · <span className="warn-text"><span>over 1</span> <b data-no-i18n>{sum.over}</b></span></>}{sum.worst !== null && <> · <span>worst</span> <b data-no-i18n>{formatUtilisation(sum.worst)}</b></>}</span>
          </div>
          {shownMembers.length > 0 ? (
            <div className="seismic-scroll">
              <table className="standards-table" data-testid="checks-members">
                <thead><tr><th>Element</th><th>Material</th><th title="The most loaded check divided by its resistance: more than 1 fails">Utilisation</th><th>Governing check</th><th>Clause</th><th title="The combination that gives the governing check">Governing combination</th></tr></thead>
                <tbody>
                  {shownMembers.map((r) => (
                    <tr key={r.sourceId} className="clickable" data-testid="checks-member-row" data-id={r.sourceId} onClick={() => select(r.sourceId)}>
                      <td data-no-i18n>{r.name}{r.pieces > 1 ? ` ×${r.pieces}` : ''}</td>
                      <td title={MATERIAL_LABEL[r.material] ?? r.material}><span data-no-i18n>{r.grade || r.material}</span></td>
                      <td><UtilisationBar u={r.utilisation} /></td>
                      <td>{governing(r) === '–' ? '–' : <span data-no-i18n>{governing(r)}</span>}</td>
                      <td data-no-i18n>{r.governing?.clause ?? ''}</td>
                      <td data-no-i18n>{r.governing?.combinationName ?? ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : <p className="muted small">No bar is checked here: steel, concrete and timber bars with a section and a material are.</p>}
          {shownSlabs.length > 0 && <Surfaces rows={shownSlabs} />}
          <MasonryWalls storey={storey} />
          {checks.notes.map((n, i) => <p key={i} className="muted small">{n}</p>)}
        </>
      )}
      <DesignValues />
    </section>
  );
}

function Surfaces({ rows }: { rows: SurfaceRow[] }) {
  return (
    <>
      <span className="eyebrow">Slabs and walls</span>
      <div className="seismic-scroll">
        <table className="standards-table" data-testid="checks-surfaces">
          <thead>
            <tr>
              <th>Element</th><th>Utilisation</th><th>Governing check</th>
              {LAYERS.map((l) => <th key={l} title="The reinforcement required in this layer, the largest over the element (cm²/m)">{LAYER_LABEL[l]} (cm²/m)</th>)}
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.sourceId} className="clickable" data-testid="checks-surface-row" onClick={() => select(r.sourceId)}>
                <td data-no-i18n>{r.name}</td><td><UtilisationBar u={r.utilisation} /></td><td data-no-i18n>{governing(r)}</td>
                {LAYERS.map((l) => <td key={l} data-no-i18n>{formatArea(r.layers?.[l])}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/** The masonry designer's main table: each wall of a storey with its forces and resistances, and the sums per direction. */
function MasonryWalls({ storey }: { storey: string }) {
  const walls = useChecks((s) => s.walls);
  const list = (walls?.storeys ?? []).filter((s) => s.walls.length > 0 && (!storey || s.id === storey));
  if (list.length === 0) return null;
  return (
    <>
      <span className="eyebrow">Masonry walls</span>
      {list.map((st) => {
        const rows = wallRows(st);
        const sums = wallSums(st);
        return (
          <div key={st.id} data-testid="checks-walls-storey">
            <p className="small"><b data-no-i18n>{st.name}</b></p>
            <div className="seismic-scroll">
              <table className="standards-table" data-testid="checks-walls">
                <thead>
                  <tr>
                    <th>Wall</th><th title="The wall's direction in plan, X or Y (±30°)">Dir.</th><th>L (m)</th><th>t (m)</th>
                    <th title="The axial force at the foot, kN (compression negative); hover a value for its combination">N (kN)</th><th title="The shear force, kN">V (kN)</th><th title="The bending moment in the plane, kN·m">M (kN·m)</th>
                    <th title="The resistance to the axial force, kN">NRd (kN)</th><th title="The resistance to shear, kN">VRd (kN)</th><th title="The resistance to bending in the plane, kN·m">MRd (kN·m)</th>
                    <th>Utilisation</th><th>Governing check</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id} className="clickable" data-testid="checks-wall-row" onClick={() => select(r.sourceId)}>
                      <td data-no-i18n>{r.name}</td><td data-no-i18n>{r.direction}</td><td data-no-i18n>{r.length}</td><td data-no-i18n>{r.thickness}</td>
                      <td title={r.nTitle} data-no-i18n>{r.N}</td><td title={r.vTitle} data-no-i18n>{r.V}</td><td title={r.mTitle} data-no-i18n>{r.M}</td>
                      <td data-no-i18n>{r.NRd}</td><td data-no-i18n>{r.VRd}</td><td data-no-i18n>{r.MRd}</td>
                      <td><UtilisationBar u={r.utilisation} /></td><td data-no-i18n>{r.governing}</td>
                    </tr>
                  ))}
                </tbody>
                {sums.length > 0 && (
                  <tfoot>
                    {sums.map((s) => (
                      <tr key={s.direction} data-testid="checks-wall-sum">
                        <td colSpan={2}><span>Sum, direction</span> <b data-no-i18n>{s.direction}</b></td>
                        <td colSpan={2} className="muted" data-no-i18n>{s.walls} · {s.area} m²</td>
                        <td /><td data-no-i18n>ΣV = {s.V}</td><td />
                        <td /><td data-no-i18n>ΣVRd = {s.VRd}</td><td /><td colSpan={2} />
                      </tr>
                    ))}
                  </tfoot>
                )}
              </table>
            </div>
          </div>
        );
      })}
    </>
  );
}

/** The project's design values: the concrete cover and bars, fyk, the masonry's fvk0 and γM, the timber's service class. Empty is the default again. */
function DesignValues() {
  const o = useAnalysis<DesignOptionsDto>('analysis.designOptions');
  if (!o) return null;
  const own = o.project ?? {};
  const set = (key: string, v: number | string | null) => void execute('analysis.setDesign', { [key]: v });
  return (
    <details className="small" data-testid="checks-design-values">
      <summary>Design values of the project</summary>
      <div className="row wrap">
        {PROJECT_FIELDS.map((f) => (
          <Field key={f.key} label={f.label} title={f.title} test={`design-${f.key}`} value={projectText(own[f.key] as number | undefined, f.scale)}
            placeholder={projectText(o.defaults[f.key], f.scale)} onApply={(text) => { const v = projectValue(text, f.scale); if (v !== undefined) set(f.key, v); return v !== undefined; }} />
        ))}
        <label className="row small" title="The timber's service class (EN 1995-1-1 2.3.1.3): 1 dry, 2 humid, 3 outdoors; an element can have its own">
          <span>Timber service class</span>
          <select aria-label="Timber service class" data-testid="design-serviceClass" value={(own.serviceClass as number | undefined) ?? ''} onChange={(e) => set('serviceClass', e.target.value === '' ? null : Number(e.target.value))}>
            <option value="">Default</option>
            {[1, 2, 3].map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
          </select>
        </label>
        <label className="row small" title="The interaction factors of members in bending and compression (EN 1993-1-1 6.3.3): Annex A (method 1, the Romanian national annex's) or Annex B (method 2)">
          <span>Steel interaction</span>
          <select aria-label="Steel interaction" data-testid="design-steelInteraction" value={(own.steelInteraction as string | undefined) ?? ''} onChange={(e) => set('steelInteraction', e.target.value === '' ? null : e.target.value)}>
            <option value="">Default (Annex A)</option>
            <option value="A">Annex A (method 1)</option>
            <option value="B">Annex B (method 2)</option>
          </select>
        </label>
        <label className="row small" title="The snow's load duration for timber's kmod (EN 1995-1-1 Table 2.2): short-term, or medium-term for a mountain site">
          <span>Snow on timber</span>
          <select aria-label="Snow on timber" data-testid="design-snowDuration" value={(own.snowDuration as string | undefined) ?? ''} onChange={(e) => set('snowDuration', e.target.value === '' ? null : e.target.value)}>
            <option value="">Default (short-term)</option>
            <option value="short">Short-term</option>
            <option value="medium">Medium-term</option>
          </select>
        </label>
      </div>
      <p className="muted">An empty field takes the default. Each change is one undo step; run the analysis again to check with it.</p>
    </details>
  );
}

/** A number typed in a field: applied on Enter or leaving it, put back when it does not read. */
export function Field({ label, title, test, value, placeholder, onApply, width = 64 }: {
  label: string; title: string; test: string; value: string; placeholder?: string; width?: number; onApply: (text: string) => boolean;
}) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const apply = () => { if (text.trim() === value.trim()) return; if (!onApply(text)) setText(value); };
  return (
    <label className="row small" title={title}>
      <span>{label}</span>
      <input aria-label={label} data-testid={test} style={{ width }} value={text} placeholder={placeholder} onChange={(e) => setText(e.target.value)} onBlur={apply} onKeyDown={(e) => { if (e.key === 'Enter') apply(); }} />
    </label>
  );
}
