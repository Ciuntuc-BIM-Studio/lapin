/** The results dialog's seismic section (docs/plan-fem.md §11.5): the site's parameters, the modes, the response spectrum, the storeys' shears and drifts. Indicative: the site's ag and TC are the engineer's, from the code's map. */

import { Play } from 'lucide-react';
import { useEffect, useState } from 'react';
import { execute } from '../../engine/session';
import { useUi } from '../../app/store';
import { useAnalysis } from './hooks';
import { animateMode, stopMode, useMode } from './useMode';
import { formatValue } from './results';
import { NotesList } from './NotesList';
import {
  CODE_LABEL, DIAPHRAGM_LABEL, DIRECTIONS_LABEL, MASONRY_KIND_TITLE, MODAL_LABEL, modeRows, readNumber, reachedMass, spectrumChart, storeyRows, formatPercent, formatPeriod,
  type ModalDto, type SeismicDto, type SeismicOptionsDto, type SpectrumDto,
} from './seismic';

const set = (args: Record<string, unknown>) => void execute('analysis.setSeismic', args);

/** A number typed in a field: written to the project when it reads and differs, else put back. */
function NumberField({ label, title, value, min, max, test, width = 60, onApply }: {
  label: string; title: string; value: number; min: number; max: number; test: string; width?: number; onApply: (v: number) => void;
}) {
  const [text, setText] = useState(String(value));
  useEffect(() => setText(String(value)), [value]);
  const apply = () => {
    const v = readNumber(text, min, max);
    if (v !== null && v !== value) onApply(v); else setText(String(value));
  };
  return (
    <label className="row small" title={title}>
      <span>{label}</span>
      <input aria-label={label} data-testid={test} style={{ width }} value={text} onChange={(e) => setText(e.target.value)} onBlur={apply} onKeyDown={(e) => { if (e.key === 'Enter') apply(); }} />
    </label>
  );
}

export function SeismicSection() {
  const o = useAnalysis<SeismicOptionsDto>('analysis.seismicOptions');
  const given = !!o?.given;
  const modal = useAnalysis<ModalDto>('analysis.modal', given);
  const spectrum = useAnalysis<SpectrumDto>('analysis.spectrum', given);
  const seismic = useAnalysis<SeismicDto>('analysis.seismic', given);
  const [ag, setAg] = useState('');
  useEffect(() => { if (o) setAg(o.ag == null ? '' : String(o.ag)); }, [o]);
  if (!o) return null;
  const p100 = o.code === 'P100-1/2013';
  const applyAg = () => {
    const v = readNumber(ag, 0.01, 1.5);
    if (v !== null && v !== o.ag) set({ ag: v }); else setAg(o.ag == null ? '' : String(o.ag));
  };
  return (
    <section className="seismic" data-testid="analysis-seismic">
      <div className="row between">
        <span className="eyebrow">Seismic</span>
        {given && <button type="button" className="btn mini" data-testid="seismic-off" title="Forget the seismic parameters: no modal or seismic analysis" onClick={() => { stopMode(); set({ ag: null }); }}>Turn off</button>}
      </div>
      <div className="row wrap">
        <label className="row small" title="The code the response spectrum and the checks follow; its values are the recommended ones, indicative">
          <span>Code</span>
          <select aria-label="Seismic code" data-testid="seismic-code" value={o.code} onChange={(e) => set({ code: e.target.value })}>
            {o.choices.code.map((c) => <option key={c} value={c}>{CODE_LABEL[c] ?? c}</option>)}
          </select>
        </label>
        <label className="row small" title="The design ground acceleration of the site as a fraction of g (0.01 to 1.5): take it from the code's seismic zoning map; Lapin ships no map">
          <span>ag (g)</span>
          <input aria-label="Design ground acceleration ag (g)" data-testid="seismic-ag" style={{ width: 60 }} value={ag} placeholder="0.20" onChange={(e) => setAg(e.target.value)} onBlur={applyAg} onKeyDown={(e) => { if (e.key === 'Enter') applyAg(); }} />
        </label>
        {p100 ? (
          <label className="row small" title="The corner period TC of the site's spectrum, from the code's map (P100-1/2013 Table 3.1)">
            <span>TC (s)</span>
            <select aria-label="Corner period TC" data-testid="seismic-tc" value={o.tc} onChange={(e) => set({ tc: Number(e.target.value) })}>
              {o.choices.tc.map((c) => <option key={c} value={c}>{c.toFixed(1)}</option>)}
            </select>
          </label>
        ) : (
          <>
            <label className="row small" title="The ground type, A to E (EN 1998-1 Table 3.1)">
              <span>Ground</span>
              <select aria-label="Ground type" data-testid="seismic-ground" value={o.ground} onChange={(e) => set({ ground: e.target.value })}>
                {o.choices.ground.map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
              </select>
            </label>
            <label className="row small" title="The spectrum's type, 1 or 2 (EN 1998-1 3.2.2.2)">
              <span>Spectrum type</span>
              <select aria-label="Spectrum type" data-testid="seismic-spectrum" value={o.spectrum} onChange={(e) => set({ spectrum: Number(e.target.value) })}>
                {[1, 2].map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
              </select>
            </label>
          </>
        )}
        <label className="row small" title="The building's importance class: its importance factor multiplies the seismic action">
          <span>Importance class</span>
          <select aria-label="Importance class" data-testid="seismic-importance" value={o.importance} onChange={(e) => set({ importance: e.target.value })}>
            {o.choices.importance.map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
          </select>
          <span className="muted" data-no-i18n>γ = {o.gamma}</span>
        </label>
        <NumberField label="q" title="The behaviour factor q, 1 to 8: the design spectrum is the elastic one divided by it (1.5 by default, the least for unreinforced masonry)" value={o.q} min={1} max={8} test="seismic-q" onApply={(v) => set({ q: v })} />
      </div>
      <div className="row wrap">
        <label className="row small" title="How the modes' responses are added: CQC takes their correlation into account (the default)">
          <span>Modal combination</span>
          <select aria-label="Modal combination" data-testid="seismic-modal" value={o.modal} onChange={(e) => set({ modal: e.target.value })}>
            {o.choices.modal.map((c) => <option key={c} value={c}>{MODAL_LABEL[c] ?? c}</option>)}
          </select>
        </label>
        <label className="row small" title="How the two horizontal directions are combined">
          <span>Directions</span>
          <select aria-label="Directions" data-testid="seismic-directions" value={o.directions} onChange={(e) => set({ directions: e.target.value })}>
            {o.choices.directions.map((c) => <option key={c} value={c}>{DIRECTIONS_LABEL[c] ?? c}</option>)}
          </select>
        </label>
        <label className="row small" title="A rigid diaphragm on each level without a shell floor; a level with a shell floor never gets one, the slab gives it">
          <span>Diaphragm</span>
          <select aria-label="Diaphragm" data-testid="seismic-diaphragm" value={o.diaphragm} onChange={(e) => set({ diaphragm: e.target.value })}>
            {o.choices.diaphragm.map((c) => <option key={c} value={c}>{DIAPHRAGM_LABEL[c] ?? c}</option>)}
          </select>
        </label>
        <NumberField label="Modes' limit" title="The most modes worked out (1 to 300); more are asked for until 90 % of the mass is reached in both directions" value={o.modes} min={1} max={300} test="seismic-modes" width={48} onApply={(v) => set({ modes: Math.round(v) })} />
      </div>
      {p100 && o.masonryQ.length > 0 && (
        <details className="small" data-testid="seismic-masonry-q">
          <summary>Behaviour factors of masonry (P100-1/2013 Table 8.10)</summary>
          <table className="standards-table">
            <thead><tr><th>Masonry</th><th title="Regular in plan and elevation">Regular</th><th title="Irregular">Irregular</th></tr></thead>
            <tbody>
              {o.masonryQ.map((m) => (
                <tr key={m.kind}><td title={MASONRY_KIND_TITLE[m.kind]} data-no-i18n>{m.kind}</td><td data-no-i18n>{m.regular}</td><td data-no-i18n>{m.irregular}</td></tr>
              ))}
            </tbody>
          </table>
          <p className="muted">Each is to be multiplied by αu/α1 of the building; indicative: take q from the code.</p>
        </details>
      )}
      {!given && <p className="muted small" data-testid="seismic-hint">Give the site&apos;s ag (and its TC, or its ground type) to turn the seismic analysis on: the modes, the response spectrum, the storeys&apos; shears and drifts, and the seismic combination.</p>}
      {given && <Results modal={modal} spectrum={spectrum} seismic={seismic} />}
      <p className="muted small">The seismic values are indicative: the site&apos;s ag and TC, the behaviour factor and the code&apos;s values are yours to check.</p>
    </section>
  );
}

function Results({ modal, spectrum, seismic }: { modal: ModalDto | null; spectrum: SpectrumDto | null; seismic: SeismicDto | null }) {
  return (
    <>
      {modal && modal.ok && modal.modes.length > 0 && <Modes modal={modal} />}
      {spectrum && spectrum.points.length > 0 && <SpectrumChart spectrum={spectrum} />}
      {seismic && seismic.ok && <Storeys seismic={seismic} />}
      <NotesList notes={[...(modal?.notes ?? []), ...(seismic?.notes ?? [])]} test="seismic-notes" />
    </>
  );
}

/** The dialog's footer's way to a mode's shape (§11.10): the mode, and Animate, in reach without scrolling to the modes' table. */
export function ModeAction() {
  const o = useAnalysis<SeismicOptionsDto>('analysis.seismicOptions');
  const modal = useAnalysis<ModalDto>('analysis.modal', !!o?.given);
  const shown = useMode((s) => s.mode);
  const [n, setN] = useState(1);
  if (!o?.given || !modal?.ok || modal.modes.length === 0) return null;
  const mode = Math.min(n, modal.modes.length);
  return (
    <span className="row" style={{ marginLeft: 'auto' }}>
      <select aria-label="Mode" data-testid="results-mode" value={mode} onChange={(e) => setN(Number(e.target.value))}>
        {modal.modes.map((m) => <option key={m.n} value={m.n} data-no-i18n>{m.n} · T = {formatPeriod(m.T)} s</option>)}
      </select>
      <button type="button" className={`btn${shown === mode ? ' primary' : ''}`} data-testid="results-animate" title="Show this mode's shape in the 3D view, swinging" onClick={() => { void animateMode(mode); useUi.setState({ dialog: null }); }}><Play size={12} />Animate</button>
    </span>
  );
}

function Modes({ modal }: { modal: ModalDto }) {
  const shown = useMode((s) => s.mode);
  const rows = modeRows(modal);
  const [x, y] = reachedMass(modal);
  const animate = (n: number) => { void animateMode(n); useUi.setState({ dialog: null }); };
  return (
    <>
      <span className="eyebrow">Modes</span>
      <div className="seismic-scroll">
        <table className="standards-table" data-testid="seismic-modes-table">
          <thead>
            <tr>
              <th>Mode</th><th>T (s)</th><th>f (Hz)</th>
              <th title="Effective modal mass along X, as a share of the mass that can move">Mass X</th><th title="Effective modal mass along Y">Mass Y</th>
              <th title="Sum of the effective masses up to this mode, along X">Σ X</th><th title="Sum of the effective masses up to this mode, along Y">Σ Y</th><th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.n} className={shown === r.n ? 'seismic-shown' : undefined}>
                <td data-no-i18n>{r.n}</td><td data-no-i18n>{r.T}</td><td data-no-i18n>{r.f}</td><td data-no-i18n>{r.x}</td><td data-no-i18n>{r.y}</td><td data-no-i18n>{r.cumX}</td><td data-no-i18n>{r.cumY}</td>
                <td><button type="button" className="btn mini" data-testid={`mode-animate-${r.n}`} title="Show this mode's shape in the 3D view, swinging" onClick={() => animate(r.n)}><Play size={11} />Animate</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!modal.reached && (
        <p className="warn-text small" role="status" data-testid="seismic-not-reached">
          <span>The modes reach only</span> <b data-no-i18n>{formatPercent(x)} · {formatPercent(y)}</b> <span>of the mass along X and Y: less than 90 %. Raise the modes&apos; limit.</span>
        </p>
      )}
      <p className="muted small"><span>Mass that can move</span> <span data-no-i18n>X {formatValue(modal.totalMass[0]!)} t · Y {formatValue(modal.totalMass[1]!)} t</span>{modal.supportMass > 0 && <> <span>; on the supports</span> <span data-no-i18n>{formatValue(modal.supportMass)} t</span></>}</p>
    </>
  );
}

function SpectrumChart({ spectrum }: { spectrum: SpectrumDto }) {
  const c = spectrumChart(spectrum);
  return (
    <>
      <span className="eyebrow">Response spectrum</span>
      <svg className="seismic-chart" viewBox={`0 0 ${c.width} ${c.height}`} role="img" aria-label="Response spectrum" data-testid="seismic-spectrum-chart" data-modes={c.modes.length}>
        {c.yTicks.map((t) => (
          <g key={`y${t.v}`}><line x1={c.left} x2={c.width - c.right} y1={t.at} y2={t.at} className="seismic-grid" /><text x={c.left - 5} y={t.at + 3} textAnchor="end" className="seismic-axis">{formatValue(t.v)}</text></g>
        ))}
        {c.xTicks.map((t) => (
          <g key={`x${t.v}`}><line x1={t.at} x2={t.at} y1={c.top} y2={c.height - c.bottom} className="seismic-grid" /><text x={t.at} y={c.height - c.bottom + 14} textAnchor="middle" className="seismic-axis">{formatValue(t.v)}</text></g>
        ))}
        <line x1={c.left} x2={c.width - c.right} y1={c.height - c.bottom} y2={c.height - c.bottom} className="seismic-frame" />
        <line x1={c.left} x2={c.left} y1={c.top} y2={c.height - c.bottom} className="seismic-frame" />
        {c.corners.map((k) => <text key={k.name} x={k.at} y={c.top + 8} textAnchor="middle" className="seismic-axis">{k.name}</text>)}
        <path d={c.se} className="seismic-se" />
        <path d={c.sd} className="seismic-sd" />
        {c.modes.map((m) => (
          <g key={m.n} className="seismic-mode"><circle cx={m.cx} cy={m.cy} r={3.2} /><text x={m.cx + 5} y={m.cy - 5}>{m.n}</text></g>
        ))}
        <text x={c.width - c.right} y={c.height - 4} textAnchor="end" className="seismic-axis">T (s)</text>
        <text x={4} y={c.top + 8} className="seismic-axis">m/s²</text>
      </svg>
      <p className="row small muted seismic-legend">
        <span className="seismic-key sd" aria-hidden="true" /><span>Design spectrum Sd</span>
        <span className="seismic-key se" aria-hidden="true" /><span>Elastic spectrum Se</span>
        <span className="seismic-key mode" aria-hidden="true" /><span>The modes' periods</span>
        <span data-no-i18n>· ag = {formatValue(spectrum.ag)} m/s² · q = {spectrum.q} · γ = {spectrum.gamma}</span>
      </p>
    </>
  );
}

function Storeys({ seismic }: { seismic: SeismicDto }) {
  const rows = storeyRows(seismic.storeys ?? []);
  const base = seismic.base, eq = seismic.equivalent;
  const mark = (ok: boolean | null) => (ok === null ? '' : ok ? ' ✓' : ' ✗');
  const cls = (ok: boolean | null) => (ok === null ? undefined : ok ? 'seismic-ok' : 'warn-text');
  return (
    <>
      <span className="eyebrow">Storeys</span>
      {seismic.combination && <p className="small" data-testid="seismic-combination"><span>Seismic combination</span> <b data-no-i18n>{seismic.combination}</b></p>}
      {base && (
        <p className="small" data-testid="seismic-base">
          <span>Base shear</span> <b data-no-i18n>X {formatValue(base[0]!)} · Y {formatValue(base[1]!)} kN</b>
          {eq && <> <span className="muted">· <span>lateral force method, as a check</span> <span data-no-i18n>X {formatValue(eq[0]!)} · Y {formatValue(eq[1]!)} kN</span></span></>}
        </p>
      )}
      <div className="seismic-scroll">
        <table className="standards-table" data-testid="seismic-storeys">
          <thead>
            <tr>
              <th>Storey</th><th title="Storey height">h (m)</th><th>Mass (t)</th>
              <th title="Storey shear force along X">Shear X (kN)</th><th title="Storey shear force along Y">Shear Y (kN)</th>
              <th title="Elastic interstorey drift along X">Drift X (mm)</th><th title="Elastic interstorey drift along Y">Drift Y (mm)</th>
              <th title="Serviceability check: ν · q · drift, along X · along Y, against its limit">SLS (mm)</th><th title="Ultimate check: c · q · drift, along X · along Y, against its limit (P100-1/2013 only)">ULS (mm)</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id}>
                <td data-no-i18n>{r.name}</td><td data-no-i18n>{r.h}</td><td data-no-i18n>{r.mass}</td><td data-no-i18n>{r.shearX}</td><td data-no-i18n>{r.shearY}</td><td data-no-i18n>{r.driftX}</td><td data-no-i18n>{r.driftY}</td>
                <td className={cls(r.slsOk)} title={`≤ ${r.slsTitle}`} data-no-i18n data-ok={String(r.slsOk)}>{r.sls}{mark(r.slsOk)}</td>
                <td className={cls(r.ulsOk)} title={r.ulsTitle ? `≤ ${r.ulsTitle}` : undefined} data-no-i18n data-ok={String(r.ulsOk)}>{r.uls}{mark(r.ulsOk)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {(seismic.closeModes ?? 0) > 0 && <p className="muted small"><span>Pairs of neighbouring modes</span> <span data-no-i18n>{seismic.closeModes}</span></p>}
    </>
  );
}
