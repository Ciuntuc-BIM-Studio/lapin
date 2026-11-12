/**
 * Convert to… (docs/plan-user-extensibility.md §7.4): the drafted lines, polylines, arcs and circles selected in a view made walls,
 * floors, rooms, railings, beams, footings, columns or bars by the converters this edition has, their options set, what each shape
 * would become shown before, the drafting kept or removed, one step to undo.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { mergeOptions } from '../../core/tables/tableModel';
import { convertDrafting, convertedMessage, listConverters, previewConversion, useConvert, type Converter, type ConvertPreview } from './convert';
import { OptionFields, PreviewList } from './OptionFields';

export function ConvertDialog() {
  const { viewId, ids } = useConvert();
  const [converters, setConverters] = useState<Converter[] | null>(null);
  const [converterId, setConverterId] = useState('');
  const [values, setValues] = useState<Record<string, unknown>>({});
  const [keep, setKeep] = useState(false);
  const [preview, setPreview] = useState<ConvertPreview | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  const converter = converters?.find((c) => c.id === converterId) ?? null;

  useEffect(() => {
    if (!viewId) return;
    void listConverters(viewId).then((r) => {
      setConverters(r.value?.converters ?? []);
      if (r.error) setProblem(r.error);
      const first = r.value?.converters[0];
      if (first) { setConverterId(first.id); setValues(mergeOptions(first.options, undefined)); }
    });
  }, [viewId]);

  const choose = (id: string) => { setConverterId(id); const c = converters?.find((x) => x.id === id); setValues(mergeOptions(c?.options ?? [], undefined)); };
  const clean = useMemo(() => Object.fromEntries(Object.entries(values).filter(([, v]) => v !== undefined)), [values]);

  useEffect(() => {
    if (!viewId || !converter) { setPreview(null); return; }
    let live = true;
    const t = setTimeout(() => {
      void previewConversion(viewId, { ids, converter: converter.id, options: clean }).then((r) => { if (!live) return; setPreview(r.value); setProblem(r.error); });
    }, 150);
    return () => { live = false; clearTimeout(t); };
  }, [viewId, converter, ids, clean]);

  const run = async () => {
    if (!viewId || !converter || busy) return;
    setBusy(true);
    const r = await convertDrafting(viewId, { ids, converter: converter.id, options: clean }, keep);
    setBusy(false);
    if ('error' in r) { setProblem(r.error); notify(r.error); return; }
    notify(convertedMessage(r));
    if (r.ids.length) useUi.setState({ selected: r.ids, selection: r.ids[0], dialog: null }); else close();
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Convert drafting">
      <div className="dialog convert-dialog" data-dialog="convert">
        <header className="dialog-head"><span className="panel-title">Convert to…</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {converters && converters.length === 0 && <p className="muted">This view's drafting has nothing it can be converted to in this edition.</p>}
          {converters && converters.length > 0 && (
            <>
              <p className="muted small"><span data-no-i18n>{ids.length}</span> <span>shapes selected.</span></p>
              <dl className="fields">
                <dt>Convert to</dt>
                <dd>
                  <select value={converterId} data-field="convert-converter" onChange={(e) => choose(e.target.value)}>
                    {converters.map((c) => <option key={c.id} value={c.id} data-no-i18n>{c.label}</option>)}
                  </select>
                </dd>
              </dl>
              {converter?.description && <p className="muted small" data-no-i18n>{converter.description}</p>}
              {converter && <OptionFields options={converter.options} values={values} onChange={(k, v) => setValues({ ...values, [k]: v })} />}
              {preview && (
                <div data-testid="convert-preview">
                  <p><strong data-field="convert-ok">{preview.ok}</strong> <span>can be converted</span>{preview.left ? <>, <strong className="row-bad" data-field="convert-left">{preview.left}</strong> <span>cannot</span></> : ''} · <span data-no-i18n>{preview.steps}</span> <span>steps</span></p>
                  <PreviewList shapes={preview.shapes} />
                </div>
              )}
              {problem && <p className="row-bad" data-testid="convert-problem">{problem}</p>}
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <label title="The drafted shapes stay under the new elements, as an underlay to check them against"><input type="checkbox" data-field="convert-keep" checked={keep} onChange={(e) => setKeep(e.target.checked)} /> Keep the drafting</label>
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-field="convert-run" disabled={!converter || busy || !preview || preview.ok === 0} onClick={() => void run()}>{busy ? 'Converting…' : 'Convert'}</button>
        </footer>
      </div>
    </div>
  );
}
