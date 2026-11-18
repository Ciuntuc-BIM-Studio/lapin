/**
 * Manage → Material graphics sets (docs/plan-material-graphics.md, stage 4). A set lists, in order, which materials draw how:
 * an entry matches a material by its id, by a word of its name, by its category, or all, and gives what the material draws in a
 * cut, a plan and an elevation (the material editor's own fields, by scale too). The most particular entry wins; ties go to the
 * first in the list. A view style names a set. Saved by `materialGraphicsSet.update`, one undo step.
 */

import { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, Copy, FileDown, FileUp, GripVertical, Pencil, Plus, Trash2, X } from 'lucide-react';
import { Dialog } from '../../shell/Dialog';
import { confirmAction, promptText } from '../../shell/prompts';
import { notify, useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { ask, run } from './LibraryChoices';
import { StandardsImportPanel, type PackRead } from './StandardsLibrary';
import { MaterialPicker } from './MaterialPicker';
import { MaterialGraphicsPreview } from './MaterialGraphicsPreview';
import { MATERIAL_KINDS, Row, useGraphicsPrefs, type HatchPattern } from './MaterialGraphicsEditor';
import { RuleGraphicsFields } from './MaterialStandardEditor';
import { useViewStyles } from './viewStyles';
import { useMaterialGraphicsSets } from './materialGraphicsSets';
import type { MaterialKind } from './materialGraphicsModel';
import type { StandardsPack } from './standardsLibraryModel';
import {
  MATCH_MODES, copyName, duplicateEntry, emptyEntry, entriesProblem, entryLabel, matchFor, modeOf, moveEntry, moveEntryTo, patchEntryKind, removeEntryAt, specificity,
  updateEntry, usedBy, type GraphicsSetInfo, type SetEntry,
} from './materialGraphicsSetsModel';

function EntryEditor({ entry, categories, hatches, onChange }: { entry: SetEntry; categories: string[]; hatches: HatchPattern[]; onChange: (f: (e: SetEntry) => SetEntry) => void }) {
  const model = useSession();
  const materials = useMemo(() => ofClass(model, 'IfcMaterial').sort((a, b) => a.name.localeCompare(b.name)), [model]);
  const [kind, setKind] = useState<MaterialKind>('section');
  const prefs = useGraphicsPrefs();
  const [sample, setSample] = useState('');
  const mode = modeOf(entry.match);
  // The preview material: the one picked, else the entry's own, else the first.
  const previewId = sample || entry.match.material || materials[0]?.id || '';
  return (
    <div className="rule-editor" data-testid="set-entry-editor">
      <Row label="Matches" title="Which materials this entry takes" set={false} onReset={() => undefined}>
        <select data-field="set-match-mode" value={mode} onChange={(e) => onChange((x) => ({ ...x, match: matchFor(e.target.value as never, x.match, categories) }))}>
          {MATCH_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
        </select>
      </Row>
      {mode === 'material' && (
        <Row label="Material" set={false} onReset={() => undefined}>
          <MaterialPicker byId value={entry.match.material ?? ''} placeholder="pick a material" onChange={(id) => onChange((x) => ({ ...x, match: { material: id } }))} />
        </Row>
      )}
      {mode === 'name' && (
        <Row label="Word in the name" title="A word or a small expression found in the material's name: letters of any case, accents ignored" set={false} onReset={() => undefined}>
          <input data-field="set-match-name" value={entry.match.materialName ?? ''} placeholder="brick" onChange={(e) => onChange((x) => ({ ...x, match: { materialName: e.target.value } }))} />
        </Row>
      )}
      {mode === 'category' && (
        <Row label="Category" set={false} onReset={() => undefined}>
          <select data-field="set-match-category" value={entry.match.category ?? ''} onChange={(e) => onChange((x) => ({ ...x, match: { category: e.target.value } }))}>
            {categories.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </Row>
      )}
      <div className="gfx-kinds" role="tablist">
        {MATERIAL_KINDS.map(({ kind: k, label }) => (
          <button key={k} type="button" role="tab" aria-selected={kind === k} data-kind={k} className={`tab gfx-kind${kind === k ? ' on' : ''}`} onClick={() => setKind(k)}>
            <span>{label}</span>{entry[k] && <span className="muted small" aria-hidden> •</span>}
          </button>
        ))}
        {entry[kind] && <button type="button" className="icon mini" title="This entry says nothing of it" aria-label="This entry says nothing of it" onClick={() => onChange((x) => patchEntryKind(x, kind, null))}><X size={11} /></button>}
      </div>
      <RuleGraphicsFields kind={kind} gfx={entry[kind]} hatches={hatches} onPatch={(p) => onChange((x) => patchEntryKind(x, kind, p))} />
      {materials.length > 0 && (
        <>
          <div className="row"><span className="eyebrow">Preview on</span>
            <select aria-label="Preview material" data-testid="set-preview-material" value={previewId} onChange={(e) => setSample(e.target.value)}>
              {materials.map((m) => <option key={m.id} value={m.id} data-no-i18n>{m.name}</option>)}
            </select></div>
          <MaterialGraphicsPreview id={previewId} kind={kind} draft={(entry[kind] ?? {}) as Record<string, unknown>} scale={prefs.scale} layout={prefs.layout}
            onScale={(scale) => useGraphicsPrefs.setState({ scale })} onLayout={(layout) => useGraphicsPrefs.setState({ layout })} />
        </>
      )}
    </div>
  );
}

function SetEditor({ set, categories, onClose }: { set: GraphicsSetInfo; categories: string[]; onClose: () => void }) {
  const nodes = useSession((s) => s.nodes);
  const [name, setName] = useState(set.name);
  const [entries, setEntries] = useState<SetEntry[]>(set.entries);
  const [original, setOriginal] = useState(() => JSON.stringify([set.name, set.entries]));
  const [sel, setSel] = useState(0);
  const [dragFrom, setDragFrom] = useState<number | null>(null);
  const [hatches, setHatches] = useState<HatchPattern[]>([]);
  useEffect(() => { void query<HatchPattern[]>('hatch.list').then((r) => r && setHatches(r)); }, []);
  const materialName = (id: string) => nodes.get(id)?.name;
  const dirty = JSON.stringify([name, entries]) !== original;
  const problem = !name.trim() ? 'A set needs a name.' : entriesProblem(entries);
  const at = Math.min(sel, entries.length - 1);
  const entry = entries[at];
  const edit = (f: (l: SetEntry[]) => SetEntry[]) => setEntries((l) => f(l));
  const save = async () => {
    const r = await run('materialGraphicsSet.update', { id: set.id, name: name.trim(), entries });
    if (r !== null) { notify(`Saved „${name.trim()}”.`); setOriginal(JSON.stringify([name, entries])); }
  };
  const close = () => { if (!dirty || confirmAction('Close without saving the changes?')) onClose(); };
  return (
    <div className="standard-editor" data-testid="set-editor">
      <div className="row">
        <input data-field="set-name" aria-label="Name" value={name} onChange={(e) => setName(e.target.value)} />
        {set.builtIn && <span className="badge" data-testid="set-builtin">Built in</span>}
        <span className="grow" />
        <button type="button" className="btn primary" data-action="set-save" disabled={!dirty || !!problem} title={problem ?? undefined} onClick={() => void save()}>Save</button>
        <button type="button" className="btn" onClick={close}>Close</button>
      </div>
      {problem && <p className="small error-text" role="alert">{problem}</p>}
      <p className="muted small" data-testid="set-rule-note">Each material takes the most particular entry that matches it: a material first, then a word in its name, then its category, then every material. Between entries equally particular, the first in the list wins.</p>
      <div className="standard-split">
        <div className="standard-rules">
          <div className="row"><span className="eyebrow">Entries</span><span className="grow" />
            <button type="button" className="btn compact" data-action="set-entry-add" onClick={() => { edit((l) => [...l, emptyEntry()]); setSel(entries.length); }}><Plus size={12} />Add an entry</button></div>
          <ol className="rule-list" data-testid="set-entry-list">
            {entries.map((e, i) => (
              <li key={i} className={`rule-row${i === at ? ' on' : ''}`} data-entry={i} draggable onDragStart={() => setDragFrom(i)} onDragOver={(ev) => ev.preventDefault()}
                onDrop={() => { if (dragFrom !== null) { edit((l) => moveEntryTo(l, dragFrom, i)); setSel(i); } setDragFrom(null); }}>
                <GripVertical size={12} className="muted" aria-hidden />
                <button type="button" className="tree-item" onClick={() => setSel(i)}><span className="muted small">{i + 1}</span> <span data-no-i18n>{entryLabel(e, materialName)}</span>
                  <span className="muted small" aria-hidden> {'●'.repeat(specificity(e.match) + 1)}</span></button>
                <button type="button" className="icon mini" aria-label="Move up" title="Earlier" disabled={i === 0} onClick={() => { edit((l) => moveEntry(l, i, -1)); setSel(i - 1); }}><ArrowUp size={11} /></button>
                <button type="button" className="icon mini" aria-label="Move down" title="Later" disabled={i === entries.length - 1} onClick={() => { edit((l) => moveEntry(l, i, 1)); setSel(i + 1); }}><ArrowDown size={11} /></button>
                <button type="button" className="icon mini" aria-label="Duplicate the entry" title="Duplicate the entry" onClick={() => { edit((l) => duplicateEntry(l, i)); setSel(i + 1); }}><Copy size={11} /></button>
                <button type="button" className="icon mini" aria-label="Remove the entry" title="Remove the entry" onClick={() => { edit((l) => removeEntryAt(l, i)); setSel(Math.max(0, i - 1)); }}><Trash2 size={11} /></button>
              </li>
            ))}
          </ol>
          {entries.length === 0 && <p className="muted small">The set has no entries: its views draw every material as the material says.</p>}
        </div>
        {entry && <EntryEditor key={at} entry={entry} categories={categories} hatches={hatches} onChange={(f) => edit((l) => updateEntry(l, at, f))} />}
      </div>
    </div>
  );
}

export function MaterialGraphicsSetsDialog() {
  const { sets, categories } = useMaterialGraphicsSets();
  const styles = useViewStyles();
  const [editing, setEditing] = useState<string | null>(null);
  const [importing, setImporting] = useState<PackRead | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const styleName = (id: string) => styles.find((s) => s.id === id)?.name;
  const names = sets.map((s) => s.name);
  const editedSet = sets.find((s) => s.id === editing);

  const create = async (from?: GraphicsSetInfo) => {
    const name = promptText(from ? 'The copy\'s name' : 'The new set\'s name', from ? copyName(from.name, names) : '')?.trim();
    if (!name) return;
    const r = await run<{ id: string }>('materialGraphicsSet.create', { name, ...(from ? { from: from.id } : { entries: [emptyEntry()] }) });
    if (r?.id) setEditing(r.id);
  };
  const rename = async (s: GraphicsSetInfo) => {
    const name = promptText('The set\'s new name', s.name)?.trim();
    if (name && name !== s.name) await run('materialGraphicsSet.update', { id: s.id, name });
  };
  const remove = async (s: GraphicsSetInfo) => {
    const used = usedBy(s, styleName);
    const text = used.length ? 'Delete the set „{name}”, used by {styles}? Those styles will draw materials as the materials say.' : 'Delete the set „{name}”?';
    if (confirmAction(text, { name: s.name, styles: used.join(', ') })) await run('materialGraphicsSet.delete', { id: s.id });
  };
  const exportOne = async (s: GraphicsSetInfo) => {
    const r = await ask<{ text: string; file: string }>('standards.export', { name: s.name, materialGraphicsSets: [s.id], only: 'materialGraphics' });
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, `${s.name}.lapinstandards`), 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importFile = async () => {
    const f = await pickFile('Drafting standards (.lapinstandards)', 'lapinstandards');
    if (!f) return;
    const text = new TextDecoder().decode(f.bytes);
    const pack = await ask<StandardsPack>('standards.readPack', { text });
    if (pack) setImporting({ text, pack });
  };

  return (
    <Dialog title="Material graphics sets" onClose={close} className="wide material-standards-dialog" dataDialog="materialGraphicsSets">
      <p className="muted small">A set tells how materials draw in a view that takes it, over what the materials say themselves: concrete solid grey in a structural view, finishes without hatch. A view style names its set (Manage → View styles).</p>
      {editedSet && <SetEditor key={editedSet.id} set={editedSet} categories={categories} onClose={() => setEditing(null)} />}
      {!editedSet && importing && <StandardsImportPanel read={importing} onDone={() => setImporting(null)} />}
      {!editedSet && !importing && (
        <>
          <div className="row">
            <button type="button" className="btn compact" data-action="set-new" onClick={() => void create()}><Plus size={13} />New set</button>
            <button type="button" className="btn compact" data-action="set-import" onClick={() => void importFile()}><FileUp size={13} />Import…</button>
          </div>
          <ul className="standard-list" data-testid="set-list">
            {sets.map((s) => {
              const used = usedBy(s, styleName);
              return (
                <li key={s.id} className="standard-row" data-set={s.id} data-testid="set-row">
                  <span className="catalog-text">
                    <span className="catalog-name"><span data-no-i18n>{s.name}</span>{s.builtIn && <span className="badge" data-testid="set-badge-builtin">Built in</span>}{s.builtIn && s.edited && <span className="badge">Edited</span>}</span>
                    <span className="muted small">{s.entries.length === 1 ? '1 entry' : `${s.entries.length} entries`}</span>
                    <span className="muted small" data-testid="set-used-by">{used.length ? <><span>Used by</span> <span data-no-i18n>{used.join(', ')}</span></> : 'Used by no style'}</span>
                  </span>
                  <span className="row">
                    <button type="button" className="icon" title="Edit" aria-label="Edit" data-action="set-edit" onClick={() => setEditing(s.id)}><Pencil size={13} /></button>
                    <button type="button" className="btn compact" data-action="set-rename" onClick={() => void rename(s)}>Rename</button>
                    <button type="button" className="icon" title="Duplicate" aria-label="Duplicate" data-action="set-duplicate" onClick={() => void create(s)}><Copy size={13} /></button>
                    <button type="button" className="icon" title="Export…" aria-label="Export…" data-action="set-export" onClick={() => void exportOne(s)}><FileDown size={13} /></button>
                    <button type="button" className="icon" title="Delete" aria-label="Delete" data-action="set-delete" onClick={() => void remove(s)}><Trash2 size={13} /></button>
                  </span>
                </li>
              );
            })}
          </ul>
          {sets.length === 0 && <p className="muted small">The project has no material graphics set yet.</p>}
        </>
      )}
    </Dialog>
  );
}
