/** An element's design checks in its properties (docs/plan-fem.md §12.7): each check with its clause, utilisation, combination and values, the notes, the reinforcement required; and the element's own design values (buckling, unrestrained length, service class, span system), each change one analysis.setDesign. Indicative. */

import { Fragment, useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query } from '../../engine/session';
import { selectionWith } from '../../shell/params';
import { useResults } from './useResults';
import { useAnalysis } from './hooks';
import { Field, UtilisationBar } from './ChecksSection';
import {
  barReinforcementText, formatArea, formatLength, lengthSource, LAYERS, LAYER_LABEL, ownDesign, parseDesignValue, SYSTEM_LABEL, valuesText,
  type BarReinforcementDto, type DesignOptionsDto, type ElementCheckDto, type ElementChecksDto, type LengthAxisDto, type LengthsDto, type ReinforcementLayersDto,
} from './checks';
import { BAR_CLASSES } from './results';

/** What analysis.check says of an element, asked again for every run (the checks are derived, never kept). */
function useElementChecks(id: string): { data: ElementChecksDto | null; asked: boolean } {
  const run = useResults((s) => s.run);
  const stale = useResults((s) => s.stale);
  const [state, set] = useState<{ data: ElementChecksDto | null; asked: boolean }>({ data: null, asked: false });
  useEffect(() => {
    if (!run?.ok || stale) { set({ data: null, asked: false }); return; }
    let live = true;
    void query<ElementChecksDto>('analysis.check', { id }).then((data) => { if (live) set({ data, asked: true }); });
    return () => { live = false; };
  }, [id, run, stale]);
  return state;
}

export function ChecksProps({ node }: { node: NodeDto }) {
  const isBar = (BAR_CLASSES as readonly string[]).includes(node.type);
  const run = useResults((s) => s.run);
  const stale = useResults((s) => s.stale);
  const { data, asked } = useElementChecks(node.id);
  const all: ElementCheckDto[] = data ? [...data.members, ...data.surfaces, ...data.walls] : [];
  return (
    <>
      <div data-testid="element-checks">
        {!run?.ok || stale ? (
          <p className="muted small">Run the analysis to check this element.</p>
        ) : !asked ? (
          <p className="muted small">Checking…</p>
        ) : all.length === 0 ? (
          <p className="muted small" data-testid="element-checks-none">This element is not checked: a bar of steel, concrete or timber, a concrete slab or wall, or a masonry wall, carried by the analysis.</p>
        ) : all.map((m) => <Member key={m.id} m={m} />)}
        <p className="muted small">Indicative: the Eurocodes&apos; recommended values and CR 6-2013; the checks are yours to verify.</p>
      </div>
      {isBar && <DesignFields node={node} />}
    </>
  );
}

function Member({ m }: { m: ElementCheckDto }) {
  const lines = m.checks ?? [];
  return (
    <div className="check-member" data-testid="element-check-member">
      <p className="row between small">
        <b data-no-i18n>{m.name}{m.grade ? ` · ${m.grade}` : ''}</b>
        <UtilisationBar u={m.utilisation} />
      </p>
      {lines.length > 0 && (
        <table className="standards-table" data-testid="element-check-lines">
          <thead><tr><th>Check</th><th>Clause</th><th>Utilisation</th><th title="The combination that governs this check">Combination</th></tr></thead>
          <tbody>
            {lines.map((c) => (
              <tr key={c.key} data-ok={String(c.ok)} className={c.ok ? undefined : 'warn-text'}>
                <td title={c.note || undefined}>
                  <details className="check-line">
                    <summary data-no-i18n>{c.name}</summary>
                    <p className="muted small values" data-no-i18n>{valuesText(c.values)}{c.x != null && c.x > 0 ? ` · x = ${+c.x.toFixed(2)} m` : ''}{c.note ? ` · ${c.note}` : ''}</p>
                  </details>
                </td>
                <td data-no-i18n>{c.clause}</td>
                <td><UtilisationBar u={c.done === false ? null : c.utilisation} /></td>
                <td data-no-i18n>{c.combinationName || c.combination || ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {(m.notes ?? []).map((n, i) => <p key={i} className="muted small">{n}</p>)}
      {m.reinforcement && <Reinforcement r={m.reinforcement} />}
    </div>
  );
}

function Reinforcement({ r }: { r: BarReinforcementDto | ReinforcementLayersDto }) {
  const layers = 'asxBottom' in r;
  const rows = layers ? LAYERS.map((l) => ({ label: LAYER_LABEL[l], value: `${formatArea((r as ReinforcementLayersDto)[l])} cm²/m` })) : barReinforcementText(r as BarReinforcementDto);
  if (rows.length === 0) return null;
  return (
    <dl className="fields" data-testid="element-reinforcement">
      <dt className="eyebrow">Reinforcement required</dt><dd />
      {rows.map((x) => <Fragment key={x.label}><dt>{x.label}</dt><dd data-no-i18n>{x.value}</dd></Fragment>)}
    </dl>
  );
}

/** The bar's own design values: empty is the default again. */
function DesignFields({ node }: { node: NodeDto }) {
  const own = ownDesign(node.props.analysisDesign);
  const ids = selectionWith(node, (n) => (BAR_CLASSES as readonly string[]).includes(n.type));
  const o = useAnalysis<DesignOptionsDto>('analysis.designOptions');
  const set = (key: string, v: number | string | null) => void execute('analysis.setDesign', { ids, [key]: v });
  const num = (key: 'ky' | 'kz' | 'lcrY' | 'lcrZ' | 'llt', label: string, title: string) => (
    <Field key={key} label={label} title={title} test={`design-own-${key}`} value={own[key] === undefined ? '' : String(own[key])}
      onApply={(text) => { const v = parseDesignValue(text); if (v === undefined) return false; set(key, v); return true; }} />
  );
  return (
    <>
      <dl className="fields" data-testid="design-values">
        <dt className="eyebrow">Design values</dt><dd />
      </dl>
      <div className="row wrap">
        {num('ky', 'ky', 'The buckling length factor about the strong axis (0.3 to 10): the buckling length is this times the bar\'s length; empty: 1')}
        {num('kz', 'kz', 'The buckling length factor about the weak axis (0.3 to 10); empty: 1')}
        {num('lcrY', 'Lcr,y (m)', 'The buckling length about the strong axis, in metres: instead of the factor')}
        {num('lcrZ', 'Lcr,z (m)', 'The buckling length about the weak axis, in metres: instead of the factor')}
        {num('llt', 'LLT (m)', 'The laterally unrestrained length of a beam, in metres (0: restrained); empty: what the slab it carries gives')}
        <label className="row small" title="Where the buckling lengths come from: the k·L above, or the lowest buckling mode of the model (αcr), never shorter than k·L; empty: the project's">
          <span>Buckling lengths</span>
          <select aria-label="Buckling lengths" data-testid="design-own-bucklingLengths" value={own.bucklingLengths ?? ''} onChange={(e) => set('bucklingLengths', e.target.value === '' ? null : e.target.value)}>
            <option value="">The project&apos;s</option>
            <option value="k">k from the user</option>
            <option value="alpha">From αcr</option>
          </select>
        </label>
        <label className="row small" title="The timber's service class (EN 1995-1-1 2.3.1.3): 1 dry, 2 humid, 3 outdoors; empty: the project's">
          <span>Service class</span>
          <select aria-label="Service class" data-testid="design-own-serviceClass" value={own.serviceClass ?? ''} onChange={(e) => set('serviceClass', e.target.value === '' ? null : Number(e.target.value))}>
            <option value="">Default</option>
            {[1, 2, 3].map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
          </select>
        </label>
        <label className="row small" title="The concrete beam's span/depth system (EN 1992-1-1 Table 7.4N), for the deflection check">
          <span>Span system</span>
          <select aria-label="Span system" data-testid="design-own-system" value={own.system ?? ''} onChange={(e) => set('system', e.target.value === '' ? null : e.target.value)}>
            <option value="">Default</option>
            {(o?.systems ?? Object.keys(SYSTEM_LABEL)).map((s) => <option key={s} value={s}>{SYSTEM_LABEL[s] ?? s}</option>)}
          </select>
        </label>
      </div>
      <p className="muted small">An empty field takes the default. Each change is one undo step; run the analysis again to check with it.</p>
      <BucklingLengths node={node} />
    </>
  );
}

/** What analysis.check says of the bar's buckling lengths: k·L, the one from αcr and which is used. */
function BucklingLengths({ node }: { node: NodeDto }) {
  const run = useResults((s) => s.run);
  const stale = useResults((s) => s.stale);
  const { data } = useElementChecks(node.id);
  const l = data?.members.find((m) => m.lengths)?.lengths;
  if (!run?.ok || stale || !l) return null;
  return (
    <div className="small" data-testid="design-lengths" data-source={`${l.y.source}/${l.z.source}`}>
      <span className="eyebrow">Buckling lengths</span>
      <AxisLengths name="Strong axis (y-y)" a={l.y} l={l} />
      <AxisLengths name="Weak axis (z-z)" a={l.z} l={l} />
      {l.informative && <p className="muted">Shown only (concrete: EN 1992-1-1 5.8.3.2)</p>}
    </div>
  );
}

function AxisLengths({ name, a, l }: { name: string; a: LengthAxisDto; l: LengthsDto }) {
  const hasAlpha = a.alpha != null && Number.isFinite(a.alpha);
  return (
    <div data-testid="design-lengths-axis">
      <b>{name}</b>
      <dl className="fields">
        <dt>k·L</dt><dd data-no-i18n>{formatLength(a.kL)} m</dd>
        <dt>From αcr</dt><dd data-no-i18n>{hasAlpha ? `${formatLength(a.alpha)} m (αcr = ${a.alphaCr != null ? +a.alphaCr.toFixed(2) : '–'}${a.mode != null ? `, mode ${a.mode}` : ''})` : '–'}</dd>
        <dt>Used</dt><dd data-no-i18n>{l.informative ? '–' : `${formatLength(a.used)} m (${lengthSource(a)})`}</dd>
      </dl>
      {a.how && <p className="muted" data-no-i18n>{a.how}</p>}
    </div>
  );
}
