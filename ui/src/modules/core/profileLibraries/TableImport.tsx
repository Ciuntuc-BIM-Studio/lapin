/**
 * A profile table (CSV or XLSX) matched to Lapin's fields (docs/plan-profile-libraries.md), as the price lists' are:
 * its first rows under their headings, each column's meaning and unit guessed by the engine from the heading (English,
 * Romanian, German, French; the American axes, x the strong one, told apart from EN's) and changed here; the kind for the
 * rows that do not say theirs, the material, the library's name. The choice is remembered on this computer for the next
 * file of the same layout.
 */

import { useState } from 'react';
import { engine } from '../../../engine/session';
import { toBase64 } from '../../../engine/files';
import { fieldOf, KIND_LABELS, MATERIAL_LABELS, MATERIALS, rememberChoice, rememberedChoice, setColumn, type ColumnChoice, type LibraryJson } from './model';

export interface TableRead {
  header: number; rows: number; convention: string; system: string; layout: string; headings: string[]; sample: string[][];
  map: Record<string, { index: number; unit: string; factor: number; alternates?: number[] } | null>;
  fields: { key: string; dimension: string }[]; units: Record<string, string[]>; kinds: string[];
}

export interface ImportReport { library: LibraryJson; skipped: number; notes: string[]; checks: CheckDto[]; checkCount: number }
export interface CheckDto { profile: string; key: string; given: number; computed: number; difference: number; unit?: string | null }

/** What each field means, as the dialog names it. */
export const FIELD_LABELS: Record<string, string> = {
  name: 'Name (designation)', family: 'Family', kind: 'Kind (W, IPE, HSS…)', h: 'Depth h (a tube’s diameter)', b: 'Width b', tw: 'Web thickness tw', tf: 'Flange thickness tf',
  t: 'Thickness t (one wall)', r1: 'Root radius r1', r2: 'Toe radius r2', lip: 'Lip c', b2: 'Bottom flange width', tf2: 'Bottom flange thickness', slope: 'Flange slope',
  A: 'Area A', Iy: 'Iy (strong axis)', Iz: 'Iz (weak axis)', Wely: 'Wel,y', Welz: 'Wel,z', Wply: 'Wpl,y', Wplz: 'Wpl,z', iy: 'iy (radius of gyration)', iz: 'iz (radius of gyration)',
  It: 'It (torsion)', Iw: 'Iw (warping)', Iu: 'Iu', Iv: 'Iv', mass: 'Mass per metre', paint: 'Paint area per metre', standard: 'Standard', material: 'Material',
};

function toChoice(map: TableRead['map']): Record<string, ColumnChoice | null> {
  const o: Record<string, ColumnChoice | null> = {};
  for (const [f, c] of Object.entries(map)) o[f] = c ? { index: c.index, unit: c.unit, alternates: c.alternates } : null;
  return o;
}

export function TableImport({ file, read, onDone, onCancel }: { file: { name: string; bytes: Uint8Array }; read: TableRead; onDone: (r: ImportReport) => void; onCancel: () => void }) {
  const remembered = rememberedChoice(read.layout);
  const [map, setMap] = useState<Record<string, ColumnChoice | null>>(remembered?.map ?? toChoice(read.map));
  const [header, setHeader] = useState(Math.max(0, read.header));
  const [kind, setKind] = useState<string>(remembered?.kind ?? '');
  const [material, setMaterial] = useState<string>(remembered?.material ?? 'steel');
  const [name, setName] = useState(file.name.replace(/\.[^.]+$/, ''));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const dimension = (f: string) => read.fields.find((x) => x.key === f)?.dimension ?? 'text';
  const unitsOf = (f: string) => read.units[dimension(f)] ?? [''];
  const ok = map.name != null;
  const go = async () => {
    setBusy(true); setError(null);
    try {
      const r = await engine.call<LibraryJson & { skipped: number; notes: string[]; checks: CheckDto[]; checkCount: number }>('doc.query', {
        name: 'profiles.importTable', args: { data: toBase64(file.bytes), name, header, map, kind: kind || null, material },
      });
      rememberChoice(read.layout, { map, kind: kind || null, material });
      const { skipped, notes, checks, checkCount, ...library } = r;
      onDone({ library: { ...library, name: name.trim() || library.name }, skipped, notes, checks, checkCount });
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  return (
    <div className="price-matching" data-testid="profile-table-matching">
      <p className="muted small">Each column's meaning and unit are guessed from its heading. Change what is wrong: a profile needs its name and the sizes its kind is built of; the values the table gives are kept and checked against Lapin's.</p>
      <p className="small">
        <span className="muted">Axes:</span> <span>{read.convention === 'american' ? 'American (x the strong axis: Ix is read as Iy)' : 'European (y the strong axis)'}</span>
        {' · '}<span className="muted">Units:</span> <span>{read.system === 'imperial' ? 'inches and pounds' : 'metric'}</span>
        {remembered && <> · <span className="tag">the columns as you matched them last time</span></>}
      </p>
      <div className="price-sample-wrap">
        <table className="price-sample">
          <thead>
            <tr>{read.headings.map((h, i) => {
              const field = fieldOf(map, i);
              return (
                <th key={i}>
                  <select value={field ?? ''} data-testid={`profile-column-${i}`} onChange={(e) => {
                    const f = e.target.value || null;
                    setMap(setColumn(map, i, f, f ? unitsOf(f)[0] ?? '' : ''));
                  }}>
                    <option value="">Not used</option>
                    {read.fields.map((f) => <option key={f.key} value={f.key}>{FIELD_LABELS[f.key] ?? f.key}</option>)}
                  </select>
                  {field && dimension(field) !== 'text' && (
                    <select value={map[field]?.unit ?? ''} data-testid={`profile-unit-${i}`} onChange={(e) => setMap({ ...map, [field]: { ...map[field]!, unit: e.target.value } })}>
                      {[...new Set([map[field]?.unit ?? '', ...unitsOf(field)])].filter(Boolean).map((u) => <option key={u} value={u} data-no-i18n>{u}</option>)}
                    </select>
                  )}
                  <div className="muted small" data-no-i18n>{h}</div>
                </th>
              );
            })}</tr>
          </thead>
          <tbody>{read.sample.map((r, i) => <tr key={i}>{read.headings.map((_, j) => <td key={j} data-no-i18n>{r[j] ?? ''}</td>)}</tr>)}</tbody>
        </table>
      </div>
      <div className="row wrap">
        <label title="The row the headings are on (the first row is 1)">Headings on row <input type="number" min={1} value={header + 1} onChange={(e) => setHeader(Math.max(0, Number(e.target.value) - 1))} style={{ width: 56 }} /></label>
        <label title="For the rows whose kind neither a column nor the name says">Kind of the others <select value={kind} onChange={(e) => setKind(e.target.value)} data-testid="profile-table-kind">
          <option value="">(from each row)</option>
          {read.kinds.map((k) => <option key={k} value={k}>{KIND_LABELS[k] ?? k}</option>)}
        </select></label>
        <label>Material <select value={material} onChange={(e) => setMaterial(e.target.value)}>{MATERIALS.map((m) => <option key={m} value={m}>{MATERIAL_LABELS[m]}</option>)}</select></label>
        <label>Library <input value={name} onChange={(e) => setName(e.target.value)} data-testid="profile-table-name" /></label>
        <span className="grow" />
        <span className="muted small" data-no-i18n>{`${read.rows}`}</span><span className="muted small">rows in the file</span>
      </div>
      {error && <p className="error-text" role="alert" data-no-i18n>{error}</p>}
      <div className="row">
        <span className="grow" />
        <button type="button" className="btn" onClick={onCancel}>Back</button>
        <button type="button" className="btn primary" disabled={!ok || busy} onClick={() => void go()} data-testid="profile-table-go">Import</button>
      </div>
    </div>
  );
}
