/**
 * Cost → Map articles… (docs/plan-4d5d.md, stage 6, step 4.2): the project's types that elements use, and the materials
 * of their layers, each with the article that prices it — what it has, what was chosen for one of that name before (on
 * this computer), or the article suggested by its words — the part of the element it measures (a layered type: its
 * core, so its finishes are priced by the materials' rules) and the measure. Confirmed, the choices go into the project
 * as one step and are remembered for the next projects.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { choiceKey, remember, remembered, startingChoice, type Choice } from './estimate';

interface ArticleDto { code: string; basis?: string; part?: string | null; factor?: number; waste?: number; chapter?: string }
interface Suggested { code: string; name: string; unit: string; part: string | null; basis: string }
interface TypeRow { id: string; name: string; class: string; label: string; count: number; layered: boolean; articles: ArticleDto[]; suggested?: Suggested }
interface MaterialRow { id: string; name: string; category?: string | null; count: number; rule?: { id: string; code: string; basis: string; own: boolean; category?: string | null } | null; suggested?: Suggested }
interface MappingDto { types: TypeRow[]; materials: MaterialRow[]; untyped: { class: string; label: string; count: number }[]; lists: number }
interface PriceRow { code: string; name: string; unit: string }

export const BASES: { id: string; label: string }[] = [
  { id: 'auto', label: "By the price's unit" }, { id: 'volume', label: 'Net volume' }, { id: 'grossVolume', label: 'Gross volume' },
  { id: 'area', label: 'Net area' }, { id: 'grossArea', label: 'Gross area' }, { id: 'length', label: 'Length' }, { id: 'count', label: 'Count' }, { id: 'weight', label: 'Weight' },
];

interface Row { key: string; code: string; part: string | null; basis: string; from: string; on: boolean }

export function MapArticlesDialog() {
  const version = useSession((s) => s.version);
  const [data, setData] = useState<MappingDto | null>(null);
  const [prices, setPrices] = useState<PriceRow[]>([]);
  const [rows, setRows] = useState<Record<string, Row>>({});
  useEffect(() => { void query<MappingDto>('cost.mapping').then((d) => setData(d ?? null)); }, [version]);
  useEffect(() => { void query<PriceRow[]>('cost.priceRows', { limit: 5000 }).then((r) => setPrices(r ?? [])); }, [version]);
  const codes = useMemo(() => new Set(prices.map((p) => p.code.toLowerCase())), [prices]);
  useEffect(() => {
    if (!data) return;
    const past = remembered();
    const o: Record<string, Row> = {};
    for (const t of data.types) {
      const now = t.articles[0] ? { code: t.articles[0].code, part: t.articles[0].part ?? null, basis: t.articles[0].basis ?? 'auto' } : null;
      const start = startingChoice(now, past[choiceKey('type', t.name, t.class)] ?? null, t.suggested ? { code: t.suggested.code, part: t.suggested.part, basis: 'auto' } : null, (c) => codes.has(c.toLowerCase()));
      o[`type:${t.id}`] = { key: t.id, code: start.choice?.code ?? '', part: start.choice?.part ?? (t.layered ? 'core' : null), basis: start.choice?.basis ?? 'auto', from: start.from, on: start.from === 'remembered' || start.from === 'suggested' };
    }
    for (const m of data.materials) {
      const now = m.rule?.own ? { code: m.rule.code, basis: m.rule.basis } : null;
      const start = startingChoice(now, past[choiceKey('material', m.name)] ?? null, m.suggested ? { code: m.suggested.code, basis: 'auto' } : null, (c) => codes.has(c.toLowerCase()));
      o[`material:${m.id}`] = { key: m.id, code: start.choice?.code ?? '', part: null, basis: start.choice?.basis ?? 'auto', from: start.from, on: start.from === 'remembered' };
    }
    setRows(o);
  }, [data, codes]);
  const close = () => useUi.setState({ dialog: null });
  if (!data) return null;
  const set = (k: string, change: Partial<Row>) => setRows((r) => ({ ...r, [k]: { ...r[k], ...change, on: true } }));
  const nameOf = (code: string) => prices.find((p) => p.code.toLowerCase() === code.toLowerCase());
  const apply = async () => {
    const types = data.types.filter((t) => rows[`type:${t.id}`]?.on).map((t) => {
      const r = rows[`type:${t.id}`];
      const rest = t.articles.slice(1);
      return { id: t.id, articles: r.code.trim() ? [{ code: r.code.trim(), part: r.part, basis: r.basis }, ...rest] : rest };
    });
    const rules = data.materials.filter((m) => rows[`material:${m.id}`]?.on).map((m) => ({ material: m.id, code: rows[`material:${m.id}`].code.trim(), basis: rows[`material:${m.id}`].basis }));
    if (!types.length && !rules.length) { close(); return; }
    const r = await execute('cost.applyMapping', { types, rules });
    if (!r) return;
    const keep: Record<string, Choice> = {};
    for (const t of data.types) { const x = rows[`type:${t.id}`]; if (x?.on && x.code.trim()) keep[choiceKey('type', t.name, t.class)] = { code: x.code.trim(), part: x.part, basis: x.basis }; }
    for (const m of data.materials) { const x = rows[`material:${m.id}`]; if (x?.on && x.code.trim()) keep[choiceKey('material', m.name)] = { code: x.code.trim(), basis: x.basis }; }
    remember(keep);
    notify('Articles mapped: one step to undo. The choices are remembered for your next projects.');
    close();
  };
  const fromLabel = (from: string) => from === 'remembered' ? 'as chosen before' : from === 'suggested' ? 'suggested' : '';
  const codeInput = (k: string, r: Row) => (
    <span className="map-code">
      <input list="cost-codes" value={r.code} onChange={(e) => set(k, { code: e.target.value })} placeholder="Article code" data-testid="map-code" data-no-i18n />
      {r.code && <span className="muted small" data-no-i18n>{nameOf(r.code)?.name ?? ''}</span>}
      {r.code && !nameOf(r.code) && <span className="error-text small">not in the lists</span>}
    </span>
  );
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide map-articles" role="dialog" aria-label="Map articles" data-testid="map-articles" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Map articles</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {data.lists === 0 && <p className="error-text small">No price list in the project yet: Cost → Price lists… imports yours (the suggestions come from it).</p>}
          <p className="muted small">An article on a type prices its elements; a rule on a material prices every layer of it, in any type. A layered type's article measures its core, so its finishes are priced by their materials. The rows ticked are applied.</p>
          <datalist id="cost-codes">{prices.map((p) => <option key={p.code} value={p.code}>{`${p.name} (${p.unit})`}</option>)}</datalist>
          <h4 className="eyebrow">Types</h4>
          <table className="map-table" data-testid="map-types">
            <thead><tr><th /><th>Type</th><th>Elements</th><th>Article</th><th data-i18n-ctx="task">Part</th><th data-i18n-ctx="cost">Measure</th><th /></tr></thead>
            <tbody>
              {data.types.map((t) => {
                const k = `type:${t.id}`;
                const r = rows[k];
                if (!r) return null;
                return (
                  <tr key={t.id} className={r.on ? 'on' : ''} data-type={t.id}>
                    <td><input type="checkbox" checked={r.on} onChange={(e) => setRows((x) => ({ ...x, [k]: { ...r, on: e.target.checked } }))} /></td>
                    <td><span data-no-i18n>{t.name}</span> <span className="muted small">{t.label}</span></td>
                    <td className="num" data-no-i18n>{t.count}</td>
                    <td>{codeInput(k, r)}</td>
                    <td>
                      <select value={r.part ?? ''} onChange={(e) => set(k, { part: e.target.value || null })}>
                        <option value="">Whole element</option>
                        {t.layered && <option value="core">Core (structure)</option>}
                        {t.layered && <option value="finishes">Finishes (layers but the core)</option>}
                      </select>
                    </td>
                    <td><select value={r.basis} onChange={(e) => set(k, { basis: e.target.value })}>{BASES.map((b) => <option key={b.id} value={b.id}>{b.label}</option>)}</select></td>
                    <td className="muted small">{fromLabel(r.from)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {data.untyped.length > 0 && <p className="muted small">Without a type, not mapped here (a line of your own prices them): <span data-no-i18n>{data.untyped.map((u) => `${u.label} × ${u.count}`).join(', ')}</span></p>}
          <h4 className="eyebrow">Materials (rules)</h4>
          <table className="map-table" data-testid="map-materials">
            <thead><tr><th /><th>Material</th><th>Elements</th><th>Article</th><th /><th data-i18n-ctx="cost">Measure</th><th /></tr></thead>
            <tbody>
              {data.materials.map((m) => {
                const k = `material:${m.id}`;
                const r = rows[k];
                if (!r) return null;
                return (
                  <tr key={m.id} className={r.on ? 'on' : ''} data-material={m.id}>
                    <td><input type="checkbox" checked={r.on} onChange={(e) => setRows((x) => ({ ...x, [k]: { ...r, on: e.target.checked } }))} /></td>
                    <td><span data-no-i18n>{m.name}</span> {m.category && <span className="muted small" data-no-i18n>{m.category}</span>}</td>
                    <td className="num" data-no-i18n>{m.count}</td>
                    <td>{codeInput(k, r)}</td>
                    <td className="muted small">{m.rule && !m.rule.own ? <>by its category: <span data-no-i18n>{m.rule.code}</span></> : ''}</td>
                    <td><select value={r.basis} onChange={(e) => set(k, { basis: e.target.value })}>{BASES.map((b) => <option key={b.id} value={b.id}>{b.label}</option>)}</select></td>
                    <td className="muted small">{fromLabel(r.from)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <footer className="dialog-foot">
          <span className="muted small">An empty code takes the article or the rule off.</span>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" onClick={() => void apply()} data-testid="map-apply">Apply</button>
        </footer>
      </div>
    </div>
  );
}
