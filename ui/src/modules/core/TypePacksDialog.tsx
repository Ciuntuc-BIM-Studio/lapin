/**
 * Manage → Type packs… (docs/plan-extensions.md, §9.3): the project's types, any kind (walls, floors, doors, windows,
 * columns, beams, stairs…), chosen and exported as a .lapintypes with what they are made of (layers, materials,
 * profiles, parameters), the file a types extension is; and a .lapintypes read, its types chosen and brought into the
 * project in one step to undo (types.importFrom: materials matched by name or made, an identical type reused, one of
 * the same name that differs added as „Name (2)”). What the project already has under the same name and is defined
 * otherwise (a material's hatches, a type's layers) is listed before, with a choice each (LibraryChoices): nothing is
 * lost in silence; what the import did is counted.
 */

import { useEffect, useMemo, useState } from 'react';
import { FileDown, FileUp, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { pickFile, saveFile } from '../../engine/files';
import { query, useSession } from '../../engine/session';
import { readTypePack } from '../../platform/extensions/packs';
import { PendingDialog } from './PendingDialog';
import { FirmFolderFiles } from './FirmFolderFiles';
import { exportTypes, saveTypesToLibrary, type LibraryTarget } from './typeLibrary';
import { ask, defaultChoices, ImportSummary, LibraryChoices, run, type ImportSummaryData, type LibraryChoiceState, type MaterialConflict } from './LibraryChoices';

interface ProjectType { id: string; class: string; label: string; name: string; count: number }
interface PackType { ref: string; class: string; label: string; name: string; exists: boolean; differs?: string[] }
interface ImportResult { created: number; reused: number; updated: number; skipped: number; materials: number; materialsUpdated: number; materialsKept: number; materialsRenamed: string[]; types: { name: string; status: string }[] }

/** The pack's types the project has by name, made otherwise. */
const conflictsOf = (types: PackType[]) => types.filter((t) => (t.differs?.length ?? 0) > 0).map((t) => ({ key: t.ref, name: t.name, label: t.label, differs: t.differs! }));

const byKind = <T extends { label: string; name: string }>(list: T[]) => [...list].sort((a, b) => a.label.localeCompare(b.label) || a.name.localeCompare(b.name));

export function TypePacksDialog() {
  const nodes = useSession((s) => s.nodes);
  const [types, setTypes] = useState<ProjectType[] | null>(null);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [name, setName] = useState('');
  const [pack, setPack] = useState<{ file: string; name: string | null; text: string; types: PackType[]; materials: MaterialConflict[] } | null>(null);
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const [choices, setChoices] = useState<LibraryChoiceState>({ materials: {}, types: {} });
  const [summary, setSummary] = useState<ImportSummaryData | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    void query<{ types: ProjectType[] }>('types.list').then((r) => { if (live) setTypes(byKind(r?.types ?? [])); });
    return () => { live = false; };
  }, [nodes]);
  const packTypes = useMemo(() => byKind(pack?.types ?? []), [pack]);
  if (!types) return <PendingDialog title="Type packs" />;

  const toggle = (set: Set<string>, id: string) => { const n = new Set(set); if (n.has(id)) n.delete(id); else n.add(id); return n; };
  const exportPack = async () => {
    const r = await ask<{ text: string; file: string }>('types.export', { ids: [...picked], name: name.trim() || undefined });
    if (!r) return;
    if (await saveFile(new TextEncoder().encode(r.text), r.file, 'application/json')) notify('The types were exported as a .lapintypes, with their layers, materials and profiles: the file a types extension is.');
  };
  const openPack = async (given?: { name: string; bytes: Uint8Array }) => {
    setSummary(null);
    const f = given ?? await pickFile('Type pack (.lapintypes)', ['lapintypes', 'json']);
    if (!f) return;
    try {
      const { text } = readTypePack(f.bytes);
      // A refusal (not a pack, a newer one, a kind of type this edition lacks) is said, not swallowed.
      const r = await ask<{ name: string | null; types: PackType[]; materials?: MaterialConflict[] }>('types.readPack', { text });
      if (!r) return;
      const materials = r.materials ?? [];
      setPack({ file: f.name, name: r.name, text, types: r.types, materials });
      setChosen(new Set(r.types.map((t) => t.ref)));
      setChoices(defaultChoices(materials, conflictsOf(r.types), 'both'));
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const keepHere = async (target: LibraryTarget) => { await saveTypesToLibrary([...picked], name.trim() || 'Types', target); };
  const bring = async () => {
    if (!pack) return;
    const types = Object.fromEntries(Object.entries(choices.types).filter(([ref]) => chosen.has(ref)));
    const r = await run<ImportResult>('types.importFrom', { text: pack.text, refs: [...chosen], materials: choices.materials, types });
    if (!r) return;
    setSummary({ typesMade: r.created, typesReused: r.reused, typesChanged: r.updated, typesSkipped: r.skipped, materialsMade: r.materials, materialsUpdated: r.materialsUpdated, materialsKept: r.materialsKept,
      renamed: [...r.types.filter((t) => t.status === 'renamed').map((t) => t.name), ...r.materialsRenamed] });
    if (r.created + r.updated === 0 && r.reused === 0) notify("No type was imported: the file's types were all skipped.");
    else notify(`Types added to the project: ${r.created} new, ${r.reused} already in it.`);
    setPack(null);
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Type packs" data-dialog="type-packs" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Type packs</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {pack ? (
            <>
              <p className="eyebrow" data-no-i18n>{pack.name ?? pack.file}</p>
              <p className="muted small">Choose the types to add. Their materials are the project's when it has one of the same name, else made; a type already in the project the same is reused, one of the same name that differs is added beside it with (2) after its name. One step to undo.</p>
              <table className="layer-table type-packs" data-testid="type-pack-types">
                <thead><tr><th /><th>Kind</th><th>Type</th><th /></tr></thead>
                <tbody>
                  {packTypes.map((t) => (
                    <tr key={t.ref}>
                      <td><input type="checkbox" checked={chosen.has(t.ref)} onChange={() => setChosen(toggle(chosen, t.ref))} data-pack-type={t.name} /></td>
                      <td>{t.label}</td>
                      <td data-no-i18n>{t.name}</td>
                      <td className="muted small">{t.exists ? 'a type of this name is in the project' : ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <LibraryChoices materials={pack.materials} types={conflictsOf(packTypes).filter((t) => chosen.has(t.key))} value={choices} onChange={setChoices} />
            </>
          ) : (
            <>
              <p className="muted small">Choose types of the project and export them as a .lapintypes, with their layers, materials, profiles and parameters: another project imports them here, and a types extension of Lapin Extensions is such a file.</p>
              {types.length === 0 ? <p className="muted">The project has no types yet.</p> : (
                <table className="layer-table type-packs" data-testid="project-types">
                  <thead><tr><th /><th>Kind</th><th>Type</th><th className="num">Elements</th></tr></thead>
                  <tbody>
                    {types.map((t) => (
                      <tr key={t.id} className={picked.has(t.id) ? 'changed' : ''}>
                        <td><input type="checkbox" checked={picked.has(t.id)} onChange={() => setPicked(toggle(picked, t.id))} data-project-type={t.name} /></td>
                        <td>{t.label}</td>
                        <td data-no-i18n>{t.name}</td>
                        <td className="num" data-no-i18n>{t.count}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              {summary && <ImportSummary r={summary} />}
              <FirmFolderFiles ext="lapintypes" onPick={(f) => openPack(f)}
                save={{ disabled: picked.size === 0, get: async () => { const r = await exportTypes([...picked], name.trim() || undefined); return r ? { text: r.text, name: name.trim() || r.file.replace(/\.[^.]+$/, '') } : null; } }} />
              <label className="row small">Pack name <input className="grow" value={name} onChange={(e) => setName(e.target.value)} placeholder="Types" data-field="type-pack-name" /></label>
            </>
          )}
        </div>
        <footer className="dialog-foot">
          {pack ? (
            <>
              <button type="button" className="btn" onClick={() => setPack(null)}>Back</button>
              <span className="grow" />
              <button type="button" className="btn primary" disabled={chosen.size === 0} onClick={() => void bring()} data-action="type-pack-import">Add to the project</button>
            </>
          ) : (
            <>
              <button type="button" className="btn" onClick={() => void openPack()} data-action="type-pack-open"><FileUp size={13} /> Import types…</button>
              <span className="grow" />
              <button type="button" className="btn" disabled={picked.size === 0} onClick={() => void keepHere({ kind: 'computer' })} data-action="type-pack-keep" title="Kept in this computer's type packs: any project here can add them (Catalog)">Save to this computer</button>
              <button type="button" className="btn primary" disabled={picked.size === 0} onClick={() => void exportPack()} data-action="type-pack-export"><FileDown size={13} /> Export types…</button>
            </>
          )}
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
