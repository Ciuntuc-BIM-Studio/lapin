/**
 * The estimate (docs/plan-4d5d.md, stage 6) as a view: its chapters, folding, each line's code, text, unit, quantity
 * from the model, unit prices split into material, labour, equipment and transport, and value; the rates under it
 * (contributions on labour, overheads, profit, VAT), the currency and the project's EUR rate in its toolbar; the forms
 * (F3, F2, C6–C9) chosen above it, to look at, export or place on a sheet. A line clicked selects its elements; the
 * elements selected mark their lines. Lines of the user's own are added and typed here.
 */

import { Fragment, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, ChevronDown, ChevronRight, FileSpreadsheet, FileText, ListChecks, Plus, Sheet, Tags, Trash2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { FORMS, exportPdf, exportXlsx, formOnSheet, linesHolding, money, parseRate, qty, unpricedCount, useEstimate, useEstimateData, type FormDto, type LineDto } from './estimate';

/** A rate typed in the toolbar, sent when it is left or Enter is pressed. */
function RateInput({ label, value, field, title, unit = '%', testId }: { label: string; value: number; field: string; title: string; unit?: string; testId: string }) {
  const [text, setText] = useState(String(value));
  useEffect(() => setText(String(value)), [value]);
  const send = () => {
    const n = parseRate(text);
    if (n == null || n === value) { setText(String(value)); return; }
    void execute('cost.updateEstimate', { [field]: n });
  };
  return (
    <label className="estimate-rate" title={title}>{label}
      <input value={text} onChange={(e) => setText(e.target.value)} onBlur={send} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} data-testid={testId} />{unit}
    </label>
  );
}

/** A line's own cell, typed over (a line of the user's). */
function Cell({ value, onCommit, num }: { value: string; onCommit: (v: string) => void; num?: boolean }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return <input className={`estimate-cell${num ? ' num' : ''}`} value={text} onChange={(e) => setText(e.target.value)} onBlur={() => text !== value && onCommit(text)} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />;
}

/** A heading's or a total's label runs on over the empty text cells after it, as the printed form has it. */
function spans(cells: string[], style: string, numeric: Set<number>): { text: string; at: number; span: number }[] {
  const o: { text: string; at: number; span: number }[] = [];
  for (let j = 0; j < cells.length; j++) {
    let span = 1;
    if (style !== 'line' && cells[j] && !numeric.has(j)) while (j + span < cells.length && !cells[j + span]) span++;
    o.push({ text: cells[j], at: j, span });
    j += span - 1;
  }
  return o;
}

function FormTable({ form }: { form: string }) {
  const version = useSession((s) => s.version);
  const [data, setData] = useState<FormDto | null>(null);
  useEffect(() => { void query<FormDto>('cost.form', { form }).then((d) => setData(d ?? null)); }, [form, version]);
  if (!data) return <p className="muted estimate-empty">Reading the estimate…</p>;
  const numeric = new Set(data.numeric);
  return (
    <div className="estimate-form" data-testid="estimate-form">
      <h3 data-no-i18n>{data.title}</h3>
      <p className="muted small" data-no-i18n>{data.subtitle}</p>
      <table className="estimate-table form">
        <thead><tr>{data.columns.map((c, i) => <th key={i} className={numeric.has(i) ? 'num' : ''} data-no-i18n>{c}</th>)}</tr></thead>
        <tbody>
          {data.rows.map((r, i) => (
            <tr key={i} className={`row-${r.style}`}>{spans(r.cells, r.style, numeric).map(({ text, at, span }) => <td key={at} colSpan={span} className={numeric.has(at) ? 'num' : ''} data-no-i18n>{text}</td>)}</tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function EstimateView() {
  const data = useEstimateData();
  const form = useEstimate((s) => s.form);
  const selected = useUi((s) => s.selected);
  const nodes = useSession((s) => s.nodes);
  // The work schedule's tasks, for a line of the user's to be done in (stage 7: its cost then goes on the S-curve).
  const tasks = useMemo(() => [...nodes.values()].filter((n) => n.type === 'IfcTask'), [nodes]);
  const [folded, setFolded] = useState<Set<string>>(new Set());
  const holding = useMemo(() => linesHolding(data, selected), [data, selected]);
  if (!data) {
    return (
      <div className="estimate-pane" data-testid="estimate">
        <div className="estimate-empty">
          <p>No estimate yet. Cost → Estimate makes one from the model: the articles on its types and the rules on its materials, priced by your price lists.</p>
          <button type="button" className="btn primary" onClick={() => void execute('cost.createEstimate', {})} data-testid="estimate-create">New estimate</button>
        </div>
      </div>
    );
  }
  const s = data.settings;
  const other = s.equivalent;
  const unpriced = unpricedCount(data);
  const lines = data.chapters.reduce((n, c) => n + c.lines.length, 0);
  const pick = (l: LineDto) => l.elements.length && useUi.setState({ selected: l.elements, selection: l.elements[0] });
  const toggle = (name: string) => setFolded((f) => { const n = new Set(f); if (n.has(name)) n.delete(name); else n.add(name); return n; });
  const update = (line: string, field: string, text: string) => {
    const numeric = ['qty', 'm', 'l', 'e', 't'].includes(field);
    const v = numeric ? parseRate(text) : text;
    if (numeric && v == null) return;
    void execute('cost.updateLine', { line, [field]: v });
  };
  const total = (label: string, v: number, strong = false, testId?: string, rate?: number) => (
    <tr className={strong ? 'row-total' : 'row-subtotal'} data-testid={testId}>
      <td colSpan={10}>{label}{rate != null && <span data-no-i18n>{` ${rate} %`}</span>}</td><td className="num" data-no-i18n>{money(v)}</td>{other && <td className="num" data-no-i18n>{money(v / (s.currency === 'RON' ? s.eurRate : 1 / s.eurRate))}</td>}
    </tr>
  );
  return (
    <div className="estimate-pane" data-testid="estimate">
      <div className="view-toolbar estimate-toolbar">
        <input className="estimate-name" key={data.name} defaultValue={data.name} onBlur={(e) => { if (e.target.value.trim() && e.target.value !== data.name) void execute('cost.updateEstimate', { name: e.target.value }); }} title="The estimate's name" data-no-i18n />
        <select value={form} onChange={(e) => useEstimate.setState({ form: e.target.value })} title="The estimate, or one of its Romanian forms" data-testid="estimate-form-pick">
          {FORMS.map((f) => <option key={f.id} value={f.id}>{f.label}</option>)}
        </select>
        <select value={s.grouping} onChange={(e) => void execute('cost.updateEstimate', { grouping: e.target.value })} title="How the chapters are made" data-testid="estimate-grouping">
          <option value="chapter">By kind of work</option>
          <option value="storey">By storey and kind of work</option>
        </select>
        <select value={s.priceLists[0] ?? ''} onChange={(e) => void execute('cost.updateEstimate', { priceLists: e.target.value ? [e.target.value] : [] })} title="The price list the estimate prices by; all: the first that has the code" data-testid="estimate-list">
          <option value="">All price lists</option>
          {data.lists.map((l) => <option key={l.id} value={l.id} data-no-i18n>{l.name}</option>)}
        </select>
        <span className="chips" role="radiogroup" aria-label="Currency">
          {(['RON', 'EUR'] as const).map((c) => <button type="button" key={c} className={`chip${s.currency === c ? ' on' : ''}`} onClick={() => void execute('cost.updateEstimate', { currency: c })} data-testid={`estimate-currency-${c}`} data-no-i18n>{c}</button>)}
        </span>
        <RateInput label="1 EUR =" value={s.eurRate} field="eurRate" unit=" RON" title="The project's exchange rate: how many RON one EUR is" testId="estimate-rate" />
        <label className="estimate-rate" title={s.other === 'EUR' ? 'Show every value in EUR too' : 'Show every value in RON too'}><input type="checkbox" checked={s.equivalent} onChange={(e) => void execute('cost.updateEstimate', { equivalent: e.target.checked })} data-testid="estimate-equivalent" />{s.other}</label>
        <span className="group-rule" aria-hidden />
        <RateInput label="CAM" value={s.labourTax} field="labourTax" title="The contributions on labour (contribuția asiguratorie pentru muncă), on the labour's value" testId="estimate-cam" />
        <RateInput label="Overheads" value={s.overheads} field="overheads" title="Overheads (cheltuieli indirecte), on the direct costs" testId="estimate-overheads" />
        <RateInput label="Profit" value={s.profit} field="profit" title="Profit, on the direct costs and overheads" testId="estimate-profit" />
        <RateInput label="VAT" value={s.vat} field="vat" title="VAT, on the value without VAT" testId="estimate-vat" />
        <span className="group-rule" aria-hidden />
        <button type="button" className="icon" title="Place this table on a new sheet" onClick={() => void formOnSheet(form)} data-testid="estimate-sheet"><Sheet size={14} /><span className="small">Place on sheet</span></button>
        <button type="button" className="icon" title="The estimate and its forms as a workbook, a sheet each" onClick={() => void exportXlsx()} data-testid="estimate-xlsx"><FileSpreadsheet size={14} /><span className="small">XLSX</span></button>
        <button type="button" className="icon" title="The estimate and its forms as a PDF, ready to print" onClick={() => void exportPdf()} data-testid="estimate-pdf"><FileText size={14} /><span className="small">PDF</span></button>
        <button type="button" className="icon" title="Your price lists: import, the library on this computer, the template" onClick={() => useUi.setState({ dialog: 'cost.prices' })}><ListChecks size={14} /><span className="small">Price lists…</span></button>
        <button type="button" className="icon" title="The project's types and materials, each with its article" onClick={() => useUi.setState({ dialog: 'cost.map' })}><Tags size={14} /><span className="small">Map articles…</span></button>
      </div>
      <div className="estimate-facts">
        <span>{`${lines} line(s)`}</span>
        <span data-no-i18n>{`${money(data.totals.total)} ${s.currency}`}</span>
        <span className="muted">with VAT</span>
        {unpriced.types > 0 && <button type="button" className="link cde-warn" onClick={() => useUi.setState({ dialog: 'cost.map' })} title="Give them an article: Map articles…" data-testid="estimate-unpriced"><AlertTriangle size={12} />{`${unpriced.types} type(s) unpriced, ${unpriced.elements} element(s)`}</button>}
        {data.missing.length > 0 && <span className="error-text" data-testid="estimate-missing">No price in the lists for <span data-no-i18n>{data.missing.join(', ')}</span></span>}
        {!s.derive && <label className="muted" title="An imported estimate keeps its own lines; on, the model's lines are added"><input type="checkbox" checked={false} onChange={() => void execute('cost.updateEstimate', { derive: true })} />Lines from the model</label>}
      </div>
      {form !== 'estimate' ? <FormTable form={form} /> : (
        <div className="estimate-scroll">
          <table className="estimate-table" data-testid="estimate-table">
            <thead>
              <tr>
                <th className="c-no" data-i18n-ctx="row">No.</th><th className="c-code">Code</th><th className="c-name">Description</th><th>Unit</th><th className="num">Quantity</th>
                <th className="num">Material</th><th className="num">Labour</th><th className="num">Equipment</th><th className="num">Transport</th><th className="num">Unit price</th><th className="num">Value</th>
                {other && <th className="num" data-no-i18n>{`Value (${s.other})`}</th>}
              </tr>
            </thead>
            <tbody>
              {data.chapters.map((c, ci) => (
                <Fragment key={c.name}>
                  <tr className="row-chapter" onClick={() => toggle(c.name)}>
                    <td>{ci + 1}</td><td>{folded.has(c.name) ? <ChevronRight size={12} /> : <ChevronDown size={12} />}</td><td colSpan={8}>{c.name}</td><td className="num" data-no-i18n>{money(c.direct)}</td>{other && <td />}
                  </tr>
                  {!folded.has(c.name) && c.lines.map((l, li) => (
                    <tr key={l.key} className={`row-line${holding.has(l.key) ? ' holds' : ''}${l.priced ? '' : ' unpriced'}${l.manual ? ' manual' : ''}`} onClick={(e) => !(e.target as HTMLElement).closest('input,button') && pick(l)} data-testid="estimate-line" data-code={l.code}>
                      <td data-no-i18n>{`${ci + 1}.${li + 1}`}</td>
                      <td data-no-i18n>{l.manual ? <Cell value={l.code} onCommit={(v) => update(l.manual!, 'code', v)} /> : l.code}</td>
                      <td data-no-i18n title={l.elements.length ? 'Click to select its elements' : undefined}>{l.manual ? <Cell value={l.name} onCommit={(v) => update(l.manual!, 'name', v)} /> : l.name}{!l.priced && <>{' '}<span className="error-text small">no price</span></>}
                        {l.manual && tasks.length > 0 && (
                          <select className="estimate-task" value={l.tasks?.[0] ?? ''} onChange={(e) => void execute('cost.updateLine', { line: l.manual, tasks: e.target.value ? [e.target.value] : [] })} title="The task its work is done in: its cost goes on the S-curve with it" data-testid="estimate-line-task">
                            <option value="">In no task</option>
                            {tasks.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
                          </select>
                        )}</td>
                      <td data-no-i18n>{l.manual ? <Cell value={l.unit} onCommit={(v) => update(l.manual!, 'unit', v)} /> : l.unit}</td>
                      <td className="num" data-no-i18n>{l.manual ? <Cell num value={String(l.qty)} onCommit={(v) => update(l.manual!, 'qty', v)} /> : qty(l.qty)}</td>
                      {(['m', 'l', 'e', 't'] as const).map((k) => <td key={k} className="num" data-no-i18n>{l.manual ? <Cell num value={String(l[k])} onCommit={(v) => update(l.manual!, k, v)} /> : money(l[k])}</td>)}
                      <td className="num" data-no-i18n>{money(l.unitPrice)}</td>
                      <td className="num" data-no-i18n>{money(l.value)}{l.manual && <button type="button" className="icon mini" title="Remove this line" onClick={() => void execute('cost.removeLine', { line: l.manual })}><Trash2 size={11} /></button>}</td>
                      {other && <td className="num" data-no-i18n>{money(l.other)}</td>}
                    </tr>
                  ))}
                </Fragment>
              ))}
              <tr className="row-add"><td colSpan={other ? 12 : 11}><button type="button" className="link" onClick={() => void execute('cost.addLine', { name: 'New line' })} data-testid="estimate-add-line"><Plus size={12} />A line of your own (work the model does not have)</button></td></tr>
              {total('Direct costs', data.totals.direct, true, 'estimate-direct')}
              {total('Contributions on labour (CAM)', data.totals.labourTax, false, undefined, s.labourTax)}
              {total('Overheads', data.totals.overheads, false, undefined, s.overheads)}
              {total('Profit', data.totals.profit, false, undefined, s.profit)}
              {total('Total without VAT', data.totals.net, true)}
              {total('VAT', data.totals.vat, false, undefined, s.vat)}
              {total('Total with VAT', data.totals.total, true, 'estimate-total')}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
