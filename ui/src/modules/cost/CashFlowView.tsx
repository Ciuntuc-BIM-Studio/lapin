/**
 * The cash flow (docs/plan-4d5d.md, cash flow) as a view beside the S-curve: a bar a period for the net cash flow, the
 * cumulative as a line, a marker at the peak funding need; the contract's terms as a form (advance, retention and its
 * releases, certificates and payment terms, VAT, the value certified, the suppliers' terms); the period table and the
 * certificates under it. All on the planned schedule; the terms are the user's, Lapin's defaults marked indicative.
 */

import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { CalendarRange, FileSpreadsheet, FileText, RotateCcw, Table2, Wallet } from 'lucide-react';
import { query, useSession } from '../../engine/session';
import { formOnSheet, money, useEstimateData } from './estimate';
import { shortMoney } from './costTime';
import {
  cashChartOf, exportCashCsv, exportCashXlsx, isCash, periodLabel, setCashTerms, showCurve, useCash, useCashFlow,
  type CashFlowDto, type CashSettingsDto,
} from './cashFlow';

const W = 900, H = 300;
const BOX = { x0: 64, x1: W - 20, y0: 18, y1: H - 28 };

function Chart({ d }: { d: CashFlowDto }) {
  const c = useMemo(() => cashChartOf(d, BOX), [d]);
  const every = Math.max(1, Math.ceil(c.bars.length * 44 / (BOX.x1 - BOX.x0)));
  return (
    <svg className="cost-chart cash-chart" viewBox={`0 0 ${W} ${H}`} role="img" aria-label="Cash flow" data-testid="cash-chart">
      {c.axis.values.map((v) => (
        <g key={`a${v}`}>
          <line className="grid" x1={BOX.x0} x2={BOX.x1} y1={c.y(v)} y2={c.y(v)} />
          <text className="tick" x={BOX.x0 - 6} y={c.y(v) + 4} textAnchor="end" data-no-i18n>{shortMoney(v)}</text>
        </g>
      ))}
      {c.bars.map((b, i) => (
        <g key={b.start}>
          {b.h > 0 && <rect className={b.out ? 'bar-out' : 'bar-in'} x={b.x} y={b.y} width={b.w} height={b.h}><title>{`${periodLabel(b.start, d.period)}: ${money(d.periods[i].net)}`}</title></rect>}
          {i % every === 0 && <text className="tick" x={b.mid} y={BOX.y1 + 16} textAnchor="middle" data-no-i18n>{periodLabel(b.start, d.period)}</text>}
        </g>
      ))}
      <line className="axis" x1={BOX.x0} x2={BOX.x0} y1={BOX.y0} y2={BOX.y1} />
      <line className="axis zero" x1={BOX.x0} x2={BOX.x1} y1={c.zero} y2={c.zero} />
      <path className="curve-cash" d={c.line} data-testid="cash-curve" />
      {c.peak && (
        <g data-testid="cash-peak">
          <circle className="peak" cx={c.peak.x} cy={c.peak.y} r={5} />
          <text className="tick peak-label" x={c.peak.x + 8} y={c.peak.y + 14} data-no-i18n>{shortMoney(d.peak)}</text>
        </g>
      )}
    </svg>
  );
}

/** A number typed, sent when left or on Enter; empty puts the default back. */
function Num({ label, value, onSet, unit, title, testId, min = 0, max }: { label: string; value: number | null; onSet: (v: number | null) => void; unit?: string; title?: string; testId?: string; min?: number; max?: number }) {
  const [text, setText] = useState(value == null ? '' : String(value));
  useEffect(() => setText(value == null ? '' : String(value)), [value]);
  const commit = () => {
    const t = text.trim().replace(',', '.');
    const v = t === '' ? null : Number(t);
    if (v === value || (v != null && !Number.isFinite(v))) { setText(value == null ? '' : String(value)); return; }
    onSet(v);
  };
  return (
    <label className="cash-field" title={title}>
      <span>{label}</span>
      <input type="number" min={min} max={max} value={text} onChange={(e) => setText(e.target.value)} onBlur={commit}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} data-testid={testId} />
      {unit && <span className="muted">{unit}</span>}
    </label>
  );
}

function Row({ label, children, title }: { label: string; children: ReactNode; title?: string }) {
  return <label className="cash-field" title={title}><span>{label}</span>{children}</label>;
}

/** The tasks of the schedule, for the advance's and the taking-over's days (milestones first). */
function useTasks(): { id: string; name: string; milestone: boolean }[] {
  const version = useSession((s) => s.version);
  const [tasks, setTasks] = useState<{ id: string; name: string; milestone: boolean }[]>([]);
  useEffect(() => {
    let live = true;
    void query<{ tasks?: { id: string; name: string; milestone: boolean; summary: boolean }[] }>('planning.schedule').then((s) => {
      if (live) setTasks((s?.tasks ?? []).filter((t) => !t.summary).sort((a, b) => Number(b.milestone) - Number(a.milestone)));
    });
    return () => { live = false; };
  }, [version]);
  return tasks;
}

function Terms({ s, currency }: { s: CashSettingsDto; currency: string }) {
  const tasks = useTasks();
  const set = (k: keyof CashSettingsDto) => (v: unknown) => void setCashTerms({ [k]: v });
  const when = (dateKey: 'advanceDate' | 'takeoverDate', taskKey: 'advanceTask' | 'takeoverTask', none: string) => (
    <>
      <select value={s[taskKey] ?? ''} onChange={(e) => void setCashTerms({ [taskKey]: e.target.value || null })} data-testid={`cash-${taskKey}`}>
        <option value="">{none}</option>
        {tasks.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.milestone ? `◆ ${t.name}` : t.name}</option>)}
      </select>
      {!s[taskKey] && <input type="date" value={s[dateKey] ?? ''} onChange={(e) => void setCashTerms({ [dateKey]: e.target.value || null })} data-testid={`cash-${dateKey}`} />}
    </>
  );
  return (
    <div className="cash-terms" data-testid="cash-terms">
      {s.indicative && <p className="muted cash-indicative" data-testid="cash-indicative">Indicative terms (Romanian practice: monthly statements of works, 5 % retention released in two parts). Your contract decides: change any of them.</p>}
      <fieldset>
        <legend>Contract</legend>
        <Row label="Value certified" title="What the certificates are worth: the estimate without VAT (with overheads and profit), its direct costs, or the price agreed with the client">
          <select value={s.basis} onChange={(e) => set('basis')(e.target.value)} data-testid="cash-basis">
            <option value="net">Estimate without VAT</option>
            <option value="direct">Direct costs</option>
            <option value="contract">Contract price</option>
          </select>
        </Row>
        {s.basis === 'contract' && <Num label="Contract price" value={s.contractValue} onSet={set('contractValue')} unit={currency} testId="cash-contract" />}
        <Row label="VAT" title="VAT at the estimate's rate, paid with each payment (in and out)">
          <input type="checkbox" checked={s.vat} onChange={(e) => set('vat')(e.target.checked)} data-testid="cash-vat" />
        </Row>
      </fieldset>
      <fieldset>
        <legend>Advance</legend>
        <Num label="Advance" value={s.advance} onSet={set('advance')} unit="%" max={100} title="A percentage of the contract value, recovered pro rata from each certificate" testId="cash-advance" />
        <Row label="Paid on">{when('advanceDate', 'advanceTask', "The schedule's start")}</Row>
      </fieldset>
      <fieldset>
        <legend>Retention</legend>
        <Num label="Retention" value={s.retention} onSet={set('retention')} unit="%" max={100} title="The part of each certificate held back" testId="cash-retention" />
        <Num label="Released at taking-over" value={s.retentionAtTakeover} onSet={set('retentionAtTakeover')} unit="%" max={100} title="The rest is released after the defects period" testId="cash-release" />
        <Num label="Defects period" value={s.defectsMonths} onSet={set('defectsMonths')} unit="months" testId="cash-defects" />
        <Row label="Taking-over">{when('takeoverDate', 'takeoverTask', "The schedule's finish")}</Row>
      </fieldset>
      <fieldset>
        <legend>Certificates and payment</legend>
        <Row label="Certificates">
          <select value={s.cycle} onChange={(e) => set('cycle')(e.target.value)} data-testid="cash-cycle">
            <option value="monthly">Monthly</option>
            <option value="weekly">Weekly</option>
          </select>
        </Row>
        {s.cycle === 'monthly' && <Num label="On day" value={s.certificateDay} onSet={set('certificateDay')} max={28} title="The day of the month a certificate is made; 0 for the month's last day" testId="cash-day" />}
        <Num label="Paid after" value={s.paymentDays} onSet={set('paymentDays')} unit="days" title="Days from a certificate to its payment" testId="cash-terms-days" />
      </fieldset>
      <fieldset>
        <legend>Payments out</legend>
        <Row label="Show payments out">
          <input type="checkbox" checked={s.costSide} onChange={(e) => set('costSide')(e.target.checked)} data-testid="cash-cost-side" />
        </Row>
        {s.costSide && <>
          <Row label="Cost paid out" title="What the contractor pays: the direct costs, or everything but the profit">
            <select value={s.costBasis} onChange={(e) => set('costBasis')(e.target.value)} data-testid="cash-cost-basis">
              <option value="withOverheads">Costs with overheads</option>
              <option value="direct">Direct costs</option>
            </select>
          </Row>
          <Num label="Suppliers paid after" value={s.supplierDays} onSet={set('supplierDays')} unit="days" testId="cash-supplier-days" />
        </>}
      </fieldset>
      {!s.indicative && <button type="button" className="link" onClick={() => void setCashTerms({ reset: true })} data-testid="cash-reset"><RotateCcw size={12} /> Lapin's defaults</button>}
    </div>
  );
}

export function CashFlowView() {
  const d = useCash();
  const estimate = useEstimateData();
  const period = useCashFlow((s) => s.period);
  if (!d) return <div className="estimate-pane" data-testid="cash-flow"><p className="muted estimate-empty">Reading the estimate…</p></div>;
  if (!isCash(d)) {
    return <div className="estimate-pane" data-testid="cash-flow"><div className="estimate-empty"><p data-testid="cash-why">{d.why}</p></div></div>;
  }
  const name = estimate?.name ?? 'Estimate';
  const p = d.periods;
  const line = (label: string, f: (x: typeof p[number]) => number, cls = 'row-line', total = true) => (
    <tr className={cls}>
      <td>{label}</td>
      {p.map((x) => <td key={x.start} className="num" data-no-i18n>{f(x) ? money(f(x)) : ''}</td>)}
      <td className="num" data-no-i18n>{total ? money(p.reduce((a, x) => a + f(x), 0)) : ''}</td>
    </tr>
  );
  return (
    <div className="estimate-pane" data-testid="cash-flow">
      <div className="view-toolbar estimate-toolbar">
        <span className="panel-title"><Wallet size={14} /> Cash flow</span>
        <button type="button" className="icon" title="The S-curve: the cost over time" onClick={() => showCurve()} data-testid="cash-to-curve"><CalendarRange size={14} /><span className="small">S-curve</span></button>
        <span className="group-rule" aria-hidden />
        <select value={period} onChange={(e) => useCashFlow.setState({ period: e.target.value as 'month' | 'week' })} title="The table's periods" data-testid="cash-period">
          <option value="month">By month</option>
          <option value="week">By week</option>
        </select>
        <button type="button" className="icon" title="The cash-flow table on a new sheet (by month)" onClick={() => void formOnSheet('cashflow')} data-testid="cash-sheet"><Table2 size={14} /><span className="small">Table on sheet</span></button>
        <button type="button" className="icon" title="The periods and the certificates as a workbook" onClick={() => void exportCashXlsx(name)} data-testid="cash-xlsx"><FileSpreadsheet size={14} /><span className="small">XLSX</span></button>
        <button type="button" className="icon" title="The periods as a CSV file" onClick={() => void exportCashCsv(name)} data-testid="cash-csv"><FileText size={14} /><span className="small">CSV</span></button>
      </div>
      <div className="estimate-facts" data-testid="cash-facts">
        <span title="The value the certificates add up to">Contract value</span><strong data-no-i18n>{`${money(d.contractValue)} ${d.currency}`}</strong>
        {d.advance > 0 && <><span>Advance</span><strong data-no-i18n>{money(d.advance)}</strong></>}
        <span title="The lowest the cumulative cash flow goes: the money the contractor has to find">Peak funding need</span>
        <strong className={d.peak < 0 ? 'cde-warn' : ''} data-no-i18n data-testid="cash-peak-value">{d.peakPeriod ? `${money(-d.peak)} · ${periodLabel(d.peakPeriod, d.period)}` : money(0)}</strong>
        <span className="muted">planned schedule</span>
        {d.actual && <span className="muted">actual certificates</span>}
        <span className="muted">{d.settings.vat ? 'with VAT' : 'without VAT'}</span>
      </div>
      <div className="estimate-scroll cost-time-scroll cash-scroll">
        <div className="cash-top">
          <div className="cash-chart-wrap">
            <Chart d={d} />
            <div className="cost-legend">
              <span><i className="key-bar-in" />Net cash in</span>
              <span><i className="key-bar-out" />Net cash out</span>
              <span><i className="key-cash" />Cumulative</span>
              <span><i className="key-peak" />Peak funding need</span>
            </div>
          </div>
          <Terms s={d.settings} currency={d.currency} />
        </div>
        <table className="estimate-table" data-testid="cash-table">
          <thead><tr><th />{p.map((x) => <th key={x.start} className="num" data-no-i18n>{periodLabel(x.start, d.period)}</th>)}<th className="num">Total</th></tr></thead>
          <tbody>
            {line('Cost incurred', (x) => x.cost)}
            {line('Certified', (x) => x.certified)}
            {line('Retention held', (x) => x.retention)}
            {d.advance > 0 && line('Advance recovered', (x) => x.recovered)}
            {line('Cash in', (x) => x.in, 'row-subtotal')}
            {d.settings.costSide && line('Cash out', (x) => x.out, 'row-subtotal')}
            {line('Net cash flow', (x) => x.net, 'row-total')}
            {line('Cumulative', (x) => x.cumulative, 'row-subtotal', false)}
            {line('Retention balance', (x) => x.retentionHeld, 'row-line', false)}
            {d.advance > 0 && line('Advance to recover', (x) => x.advanceLeft, 'row-line', false)}
          </tbody>
        </table>
        <table className="estimate-table cash-certificates" data-testid="cash-certificates">
          <thead><tr><th>Certificate</th><th>Date</th><th className="num">Certified</th><th className="num">Retention</th><th className="num">Advance recovered</th><th className="num">Net</th>{d.settings.vat && <th className="num">VAT</th>}<th>Paid</th></tr></thead>
          <tbody>
            {d.certificates.map((c) => (
              <tr key={c.no} className="row-line">
                <td data-no-i18n>{c.actual ? `${c.no} ✓` : c.no}</td><td data-no-i18n>{c.date}</td><td className="num" data-no-i18n>{money(c.certified)}</td><td className="num" data-no-i18n>{money(c.retention)}</td>
                <td className="num" data-no-i18n>{money(c.recovered)}</td><td className="num" data-no-i18n>{money(c.net)}</td>{d.settings.vat && <td className="num" data-no-i18n>{money(c.vat)}</td>}<td data-no-i18n>{c.due}</td>
              </tr>
            ))}
            {d.releases.map((r) => (
              <tr key={r.what} className="row-line">
                <td>{r.what === 'takeover' ? 'Retention released at taking-over' : 'Retention released after the defects period'}</td><td /><td /><td /><td />
                <td className="num" data-no-i18n>{money(r.value)}</td>{d.settings.vat && <td />}<td data-no-i18n>{r.due}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
