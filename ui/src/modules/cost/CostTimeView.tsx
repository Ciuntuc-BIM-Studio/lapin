/**
 * Cost over time (docs/plan-4d5d.md, stage 7) as a view: the S-curve — the planned cumulative cost over the schedule,
 * the baseline's when one is saved, the work done's to the status date — over the months' bars (planned and done), the
 * budget, what the tasks carry of it and the cost to date above it, and the month table by chapter under it. Placed on a
 * sheet (the curve, or the month table as a form), or exported as a workbook and a PDF.
 */

import { Fragment, useMemo } from 'react';
import { CalendarRange, FileSpreadsheet, FileText, Sheet, Table2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { formOnSheet, money, useEstimateData } from './estimate';
import { chartOf, curveOnSheet, exportCostTimePdf, exportCostTimeXlsx, isCurve, shortMoney, useOverTime, type OverTimeDto } from './costTime';

const W = 900, H = 300;
const BOX = { x0: 64, x1: W - 56, y0: 18, y1: H - 28 };

const showMonth = (m: string) => `${m.slice(5, 7)}.${m.slice(2, 4)}`;

function Chart({ d }: { d: OverTimeDto }) {
  const c = useMemo(() => chartOf(d, BOX), [d]);
  const every = Math.max(1, Math.ceil(c.bars.length * 44 / (BOX.x1 - BOX.x0)));
  return (
    <svg className="cost-chart" viewBox={`0 0 ${W} ${H}`} role="img" aria-label="S-curve" data-testid="cost-chart">
      {c.cum.values.map((v) => (
        <g key={`c${v}`}>
          <line className="grid" x1={BOX.x0} x2={BOX.x1} y1={c.y(v)} y2={c.y(v)} />
          <text className="tick" x={BOX.x0 - 6} y={c.y(v) + 4} textAnchor="end" data-no-i18n>{shortMoney(v)}</text>
        </g>
      ))}
      {c.month.values.map((v) => <text key={`m${v}`} className="tick" x={BOX.x1 + 6} y={c.ym(v) + 4} data-no-i18n>{shortMoney(v)}</text>)}
      {c.bars.map((b, i) => (
        <g key={b.month}>
          {b.planned.h > 0 && <rect className="bar-planned" x={b.planned.x} y={b.planned.y} width={b.planned.w} height={b.planned.h}><title>{`${showMonth(b.month)}: ${money(d.months[i].planned)}`}</title></rect>}
          {b.done.h > 0 && <rect className="bar-done" x={b.done.x} y={b.done.y} width={b.done.w} height={b.done.h}><title>{`${showMonth(b.month)}: ${money(d.months[i].done)}`}</title></rect>}
          {i % every === 0 && <text className="tick" x={b.mid} y={BOX.y1 + 16} textAnchor="middle" data-no-i18n>{showMonth(b.month)}</text>}
        </g>
      ))}
      <line className="axis" x1={BOX.x0} x2={BOX.x0} y1={BOX.y0} y2={BOX.y1} />
      <line className="axis" x1={BOX.x1} x2={BOX.x1} y1={BOX.y0} y2={BOX.y1} />
      <line className="axis" x1={BOX.x0} x2={BOX.x1} y1={BOX.y1} y2={BOX.y1} />
      {c.baseline && <path className="curve-baseline" d={c.baseline} data-testid="cost-curve-baseline" />}
      <path className="curve-planned" d={c.planned} data-testid="cost-curve-planned" />
      {c.done && <path className="curve-done" d={c.done} data-testid="cost-curve-done" />}
      {c.status != null && <line className="status" x1={c.status} x2={c.status} y1={BOX.y0} y2={BOX.y1} />}
    </svg>
  );
}

export function CostTimeView() {
  const d = useOverTime();
  const estimate = useEstimateData();
  if (!d) return <div className="estimate-pane" data-testid="cost-time"><p className="muted estimate-empty">Reading the estimate…</p></div>;
  if (!isCurve(d)) {
    return (
      <div className="estimate-pane" data-testid="cost-time">
        <div className="estimate-empty"><p data-testid="cost-time-why">{d.why}</p></div>
      </div>
    );
  }
  const name = estimate?.name ?? 'Estimate';
  return (
    <div className="estimate-pane" data-testid="cost-time">
      <div className="view-toolbar estimate-toolbar">
        <span className="panel-title"><CalendarRange size={14} /> Cost over time</span>
        <span className="group-rule" aria-hidden />
        <button type="button" className="icon" title="The S-curve on a new sheet" onClick={() => void curveOnSheet()} data-testid="cost-time-sheet"><Sheet size={14} /><span className="small">Place on sheet</span></button>
        <button type="button" className="icon" title="The month table on a new sheet" onClick={() => void formOnSheet('monthly')} data-testid="cost-time-table-sheet"><Table2 size={14} /><span className="small">Month table on sheet</span></button>
        <button type="button" className="icon" title="The curve a day, the month table and the tasks' costs as a workbook" onClick={() => void exportCostTimeXlsx(name)} data-testid="cost-time-xlsx"><FileSpreadsheet size={14} /><span className="small">XLSX</span></button>
        <button type="button" className="icon" title="The curve and the month table as a PDF, ready to print" onClick={() => void exportCostTimePdf(name)} data-testid="cost-time-pdf"><FileText size={14} /><span className="small">PDF</span></button>
      </div>
      <div className="estimate-facts" data-testid="cost-time-facts">
        <span title="The estimate's direct costs">Budget at completion</span><strong data-no-i18n>{`${money(d.budget)} ${d.currency}`}</strong>
        <span title="Each task's cost times how much of it is done">Cost to date</span><strong data-no-i18n data-testid="cost-time-to-date">{money(d.toDate)}</strong>
        {d.unscheduled > 0 && <button type="button" className="link cde-warn" title="Lines whose elements no task builds, and lines of your own in no task" onClick={() => useUi.setState({ view: 'estimate' })} data-testid="cost-time-unscheduled"><span>Not scheduled</span><span data-no-i18n>{` ${money(d.unscheduled)}`}</span></button>}
        <span className="muted" data-no-i18n>{d.schedule}</span>
        <span className="muted">direct costs</span>
      </div>
      <div className="estimate-scroll cost-time-scroll">
        <Chart d={d} />
        <div className="cost-legend">
          <span><i className="key-planned" />Planned, cumulative</span>
          {d.baseline && <span><i className="key-baseline" />Baseline, cumulative</span>}
          <span><i className="key-done" />Done, cumulative</span>
          <span><i className="key-bar-planned" />Planned a month</span>
          <span><i className="key-bar-done" />Done a month</span>
        </div>
        <table className="estimate-table" data-testid="cost-time-table">
          <thead>
            <tr><th>Chapter</th>{d.months.map((m) => <th key={m.month} className="num" data-no-i18n>{showMonth(m.month)}</th>)}<th className="num">Not scheduled</th><th className="num">Total</th></tr>
          </thead>
          <tbody>
            {d.chapters.map((c) => (
              <Fragment key={c.name}>
                <tr className="row-line"><td data-no-i18n>{c.name}</td>{c.months.map((v, i) => <td key={i} className="num" data-no-i18n>{v ? money(v) : ''}</td>)}<td className="num" data-no-i18n>{c.notScheduled ? money(c.notScheduled) : ''}</td><td className="num" data-no-i18n>{money(c.total)}</td></tr>
              </Fragment>
            ))}
            <tr className="row-total"><td>Planned a month</td>{d.months.map((m) => <td key={m.month} className="num" data-no-i18n>{money(m.planned)}</td>)}<td className="num" data-no-i18n>{money(d.unscheduled)}</td><td className="num" data-no-i18n>{money(d.budget)}</td></tr>
            <tr className="row-subtotal"><td>Planned, cumulative</td>{d.months.map((m) => <td key={m.month} className="num" data-no-i18n>{money(m.plannedCum)}</td>)}<td /><td /></tr>
            {d.toDate > 0 && <>
              <tr className="row-total"><td>Done a month</td>{d.months.map((m) => <td key={m.month} className="num" data-no-i18n>{m.done ? money(m.done) : ''}</td>)}<td /><td className="num" data-no-i18n>{money(d.toDate)}</td></tr>
              <tr className="row-subtotal"><td>Done, cumulative</td>{d.months.map((m) => <td key={m.month} className="num" data-no-i18n>{m.month <= (d.doneThrough ?? '').slice(0, 7) ? money(m.doneCum) : ''}</td>)}<td /><td /></tr>
            </>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
