/**
 * Convert layers… (docs/plan-user-extensibility.md §7.5): the drafting layers of a view (a DXF's A-WALL, A-COLS…), each given the
 * conversion it becomes (walls, columns, bars…) and its options, what every shape would become shown before, one step to undo; the
 * drafting is kept by default, as the underlay the elements were made from. The map is kept on this computer by layer name and goes
 * to a .lapinmapping file and comes back from one.
 */

import { t as tr } from '../../../i18n';
import { useEffect, useMemo, useState } from 'react';
import { FileDown, FileUp, SlidersHorizontal, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { pickFile, saveFile } from '../../../engine/files';
import { layerMappingText, mergeOptions, parseMappingFile, recallLayer, rememberLayers, type LayerEntry } from '../../core/tables/tableModel';
import { convertDrafting, convertedMessage, listConverters, previewConversion, useConvert, type Converter, type ConvertPreview, type DraftLayer } from './convert';
import { OptionFields, PreviewList } from './OptionFields';

type Row = LayerEntry & { shapes: number };

export function ConvertLayersDialog() {
  const { viewId } = useConvert();
  const [converters, setConverters] = useState<Converter[] | null>(null);
  const [rows, setRows] = useState<Row[]>([]);
  const [open, setOpen] = useState<string | null>(null);
  const [keep, setKeep] = useState(true);
  const [preview, setPreview] = useState<ConvertPreview | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    if (!viewId) return;
    void listConverters(viewId).then((r) => {
      const cs = r.value?.converters ?? [];
      setConverters(cs);
      if (r.error) setProblem(r.error);
      setRows((r.value?.layers ?? []).map((l: DraftLayer) => {
        const got = recallLayer(l.layer);
        const conv = got?.converter && cs.some((c) => c.id === got.converter) ? got.converter : null;
        return { layer: l.layer, shapes: l.shapes, converter: conv, options: mergeOptions(cs.find((c) => c.id === conv)?.options ?? [], got?.options) };
      }));
    });
  }, [viewId]);

  const set = (layer: string, patch: Partial<Row>) => setRows((rs) => rs.map((r) => (r.layer === layer ? { ...r, ...patch } : r)));
  const choose = (layer: string, id: string) => set(layer, { converter: id || null, options: mergeOptions(converters?.find((c) => c.id === id)?.options ?? [], undefined) });
  const map = useMemo(() => rows.filter((r) => r.converter).map((r) => ({ layer: r.layer, converter: r.converter!, options: Object.fromEntries(Object.entries(r.options).filter(([, v]) => v !== undefined)) })), [rows]);

  useEffect(() => {
    if (!viewId || map.length === 0) { setPreview(null); return; }
    let live = true;
    const t = setTimeout(() => { void previewConversion(viewId, { map }).then((r) => { if (!live) return; setPreview(r.value); setProblem(r.error); }); }, 200);
    return () => { live = false; clearTimeout(t); };
  }, [viewId, map]);

  const run = async () => {
    if (!viewId || busy || map.length === 0) return;
    setBusy(true);
    const r = await convertDrafting(viewId, { map }, keep);
    setBusy(false);
    if ('error' in r) { setProblem(r.error); notify(r.error); return; }
    rememberLayers(rows.map(({ layer, converter, options }) => ({ layer, converter, options })));
    notify(convertedMessage(r));
    if (r.ids.length) useUi.setState({ selected: r.ids, selection: r.ids[0], dialog: null }); else close();
  };

  const exportMap = async () => {
    const entries = rows.map(({ layer, converter, options }) => ({ layer, converter, options }));
    if (await saveFile(new TextEncoder().encode(layerMappingText(entries)), 'layers.lapinmapping', 'application/json')) notify('The layer map was saved as a .lapinmapping file.');
  };
  const importMap = async () => {
    const f = await pickFile('Mapping (.lapinmapping)', ['lapinmapping', 'json']);
    if (!f) return;
    try {
      const m = parseMappingFile(new TextDecoder().decode(f.bytes));
      if (m.kind !== 'layers') throw new Error('This mapping is for a table, not for drafting layers.');
      const by = new Map(m.layers.map((l) => [l.layer.toLowerCase(), l]));
      setRows((rs) => rs.map((r) => {
        const l = by.get(r.layer.toLowerCase());
        if (!l) return r;
        const conv = l.converter && converters?.some((c) => c.id === l.converter) ? l.converter : null;
        return { ...r, converter: conv, options: mergeOptions(converters?.find((c) => c.id === conv)?.options ?? [], l.options) };
      }));
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Convert layers">
      <div className="dialog wide convert-dialog" data-dialog="convert-layers">
        <header className="dialog-head"><span className="panel-title">Convert layers…</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {converters && converters.length === 0 && <p className="muted">This view's drafting has nothing it can be converted to in this edition.</p>}
          {rows.length === 0 && converters && converters.length > 0 && <p className="muted">This view has no drafted lines, polylines, arcs or circles.</p>}
          {rows.length > 0 && converters && converters.length > 0 && (
            <>
              <p className="muted small">Give each layer what it becomes; layers left as they are stay drafting. The map is kept on this computer by layer name.</p>
              <table className="layer-table" data-testid="convert-layers">
                <thead><tr><th data-i18n-ctx="cad">Layer</th><th className="num">Shapes</th><th>Becomes</th><th /></tr></thead>
                <tbody>
                  {rows.map((r) => {
                    const conv = converters.find((c) => c.id === r.converter);
                    return [
                      <tr key={r.layer} data-layer={r.layer}>
                        <td data-no-i18n>{r.layer}</td>
                        <td className="num" data-no-i18n>{r.shapes}</td>
                        <td>
                          <select value={r.converter ?? ''} data-field={`layer-converter-${r.layer}`} onChange={(e) => choose(r.layer, e.target.value)}>
                            <option value="">(leave as drafting)</option>
                            {converters.map((c) => <option key={c.id} value={c.id} data-no-i18n>{tr(c.label)}</option>)}
                          </select>
                        </td>
                        <td>{conv && conv.options.length > 0 && <button type="button" className={`btn mini${open === r.layer ? ' primary' : ''}`} data-field={`layer-options-${r.layer}`} title="This conversion's options" onClick={() => setOpen(open === r.layer ? null : r.layer)}><SlidersHorizontal size={12} />Options</button>}</td>
                      </tr>,
                      open === r.layer && conv ? <tr key={`${r.layer}-o`}><td colSpan={4}><OptionFields options={conv.options} values={r.options} onChange={(k, v) => set(r.layer, { options: { ...r.options, [k]: v } })} /></td></tr> : null,
                    ];
                  })}
                </tbody>
              </table>
              <div className="pair">
                <button type="button" className="btn mini" data-field="layers-export" onClick={() => void exportMap()}><FileDown size={12} />Export map…</button>
                <button type="button" className="btn mini" data-field="layers-import" onClick={() => void importMap()}><FileUp size={12} />Import map…</button>
              </div>
              {preview && (
                <div data-testid="convert-preview">
                  <p><strong data-field="convert-ok">{preview.ok}</strong> <span>shapes can be converted</span>{preview.left ? <>, <strong className="row-bad" data-field="convert-left">{preview.left}</strong> <span>cannot</span></> : ''} · <span data-no-i18n>{preview.steps}</span> <span>steps</span></p>
                  <PreviewList shapes={preview.shapes.filter((s) => s.error).concat(preview.shapes.filter((s) => !s.error).slice(0, 20))} />
                </div>
              )}
              {problem && <p className="row-bad" data-testid="convert-problem">{problem}</p>}
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <label title="The drafted shapes stay as the underlay the elements were made from"><input type="checkbox" data-field="layers-keep" checked={keep} onChange={(e) => setKeep(e.target.checked)} /> Keep the drafting</label>
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-field="layers-run" disabled={busy || map.length === 0 || !preview || preview.ok === 0} onClick={() => void run()}>{busy ? 'Converting…' : 'Convert'}</button>
        </footer>
      </div>
    </div>
  );
}
