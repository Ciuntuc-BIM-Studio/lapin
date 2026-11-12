/**
 * Manage → Type packs… (docs/plan-extensions.md, §9.3): the project's types, any kind (walls, floors, doors, windows,
 * columns, beams, stairs…), chosen and exported as a .lapintypes with what they are made of (layers, materials,
 * profiles, parameters), the file a types extension is; and a .lapintypes read, its types chosen and brought into the
 * project in one step to undo (types.importFrom: materials matched by name or made, an identical type reused, one of
 * the same name that differs added as „Name (2)”).
 */

import { useEffect, useMemo, useState } from 'react';
import { FileDown, FileUp, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { pickFile, saveFile } from '../../engine/files';
import { execute, query, useSession } from '../../engine/session';
import { readTypePack } from '../../platform/extensions/packs';
import { PendingDialog } from './PendingDialog';

interface ProjectType { id: string; class: string; label: string; name: string; count: number }
interface PackType { ref: string; class: string; label: string; name: string; exists: boolean }
interface ImportResult { created: number; reused: number; materials: number }

const byKind = <T extends { label: string; name: string }>(list: T[]) => [...list].sort((a, b) => a.label.localeCompare(b.label) || a.name.localeCompare(b.name));

export function TypePacksDialog() {
  const nodes = useSession((s) => s.nodes);
  const [types, setTypes] = useState<ProjectType[] | null>(null);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [name, setName] = useState('');
  const [pack, setPack] = useState<{ file: string; name: string | null; text: string; types: PackType[] } | null>(null);
  const [chosen, setChosen] = useState<Set<string>>(new Set());
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
    const r = await query<{ text: string; file: string }>('types.export', { ids: [...picked], name: name.trim() || undefined });
    if (!r) return;
    if (await saveFile(new TextEncoder().encode(r.text), r.file, 'application/json')) notify('The types were exported as a .lapintypes, with their layers, materials and profiles: the file a types extension is.');
  };
  const openPack = async () => {
    const f = await pickFile('Type pack (.lapintypes)', ['lapintypes', 'json']);
    if (!f) return;
    try {
      const { text } = readTypePack(f.bytes);
      const r = await query<{ name: string | null; types: PackType[] }>('types.readPack', { text });
      if (!r) return;
      setPack({ file: f.name, name: r.name, text, types: r.types });
      setChosen(new Set(r.types.map((t) => t.ref)));
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const bring = async () => {
    if (!pack) return;
    const r = await execute('types.importFrom', { text: pack.text, refs: [...chosen] }) as ImportResult | null;
    if (!r) return;
    notify(`Types added to the project: ${r.created} new, ${r.reused} already in it.`);
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
              <button type="button" className="btn primary" disabled={picked.size === 0} onClick={() => void exportPack()} data-action="type-pack-export"><FileDown size={13} /> Export types…</button>
            </>
          )}
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
