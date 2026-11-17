/**
 * File → Import types from a project… (issue #74, decision 669): another Lapin project's types, any kind the type packs carry
 * (walls, floors, roofs, doors, windows, columns, beams, stairs…), listed with the materials each needs and what each is to the
 * open project, chosen and brought in in one step to undo (library.importProjectTypes). The source is only read, by the engine
 * (library.projectTypes: a model of its own, brought up to date in memory), never written to. The rules are a .lapintypes': a
 * material is the project's when it has one of the same name, a type of the same name that differs is kept beside as „Name (2)”;
 * a type the project has under the very GlobalId is that type, replaced in place when it changed. What differs is chosen for
 * (LibraryChoices), as in Manage → Type packs….
 */

import { useEffect, useMemo, useState } from 'react';
import { notify, useUi } from '../../app/store';
import { engine } from '../../engine/session';
import { LONG_TIMEOUT_MS } from '../../engine/client';
import { Dialog } from '../../shell/Dialog';
import { STATUS_LABELS, type LibraryTally } from '../../platform/libraryFile';
import { tallySentence } from '../../platform/ImportPreview';
import { PendingDialog } from './PendingDialog';
import { LibraryChoices, run, type LibraryChoiceState } from './LibraryChoices';
import { byKind, defaultProjectChoices, importArgs, projectTypesFile, typeConflicts, type ProjectTypesRead } from './projectTypes';

const TITLE = 'Import types from a project';

export function ProjectTypesDialog() {
  const [read, setRead] = useState<ProjectTypesRead | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const [choices, setChoices] = useState<LibraryChoiceState>({ materials: {}, types: {} });
  const [busy, setBusy] = useState(false);
  const file = projectTypesFile.current;
  const close = () => { projectTypesFile.current = null; useUi.setState({ dialog: null }); };

  useEffect(() => {
    if (!file) { close(); return; }
    let live = true;
    void engine.call<ProjectTypesRead>('library.projectTypes', file, { timeoutMs: LONG_TIMEOUT_MS }).then((r) => {
      if (!live) return;
      setRead(r);
      setChosen(new Set(r.types.map((t) => t.ref)));
      setChoices(defaultProjectChoices(r));
    }, (e: unknown) => { if (live) setError(e instanceof Error ? e.message : String(e)); });
    return () => { live = false; };
  }, [file]);

  const types = useMemo(() => byKind(read?.types ?? []), [read]);
  if (!read) return <PendingDialog title={TITLE} error={error} onClose={close} />;

  const toggle = (ref: string) => { const n = new Set(chosen); if (n.has(ref)) n.delete(ref); else n.add(ref); setChosen(n); };
  const bring = async () => {
    setBusy(true);
    const r = await run<LibraryTally & { created: number; updated: number; reused: number }>('library.importProjectTypes', importArgs(read, chosen, choices));
    setBusy(false);
    if (!r) return;
    notify(tallySentence(r));
    close();
  };

  return (
    <Dialog title={<><span>{TITLE}</span> · <span data-no-i18n>{read.name ?? file?.name}</span></>} onClose={close} className="wide" dataDialog="project-types" testId="project-types"
      footer={<>
        <button type="button" className="btn" onClick={() => setChosen(new Set(types.map((t) => t.ref)))} data-action="project-types-all">All</button>
        <button type="button" className="btn" onClick={() => setChosen(new Set())} data-action="project-types-none">None</button>
        <span className="grow" />
        <button type="button" className="btn" onClick={close}>Close</button>
        <button type="button" className="btn primary" disabled={busy || chosen.size === 0} onClick={() => void bring()} data-action="project-types-import">Add to the project</button>
      </>}>
      <p className="muted small">Choose the types to bring in, with the materials each needs. The project they come from is only read, never changed.</p>
      <p className="muted small">A type already here under the same GlobalId is that type: replaced in place when it changed. One of the same name is added beside it with (2) after its name. A material is the project's when it has one of the same name. One step to undo.</p>
      <table className="layer-table type-packs" data-testid="project-types-list">
        <thead><tr><th /><th>Kind</th><th>Type</th><th>Materials</th><th className="num">Elements</th><th>Status</th></tr></thead>
        <tbody>
          {types.map((t) => (
            <tr key={t.ref} data-status={t.status}>
              <td><input type="checkbox" checked={chosen.has(t.ref)} onChange={() => toggle(t.ref)} data-source-type={t.name} /></td>
              <td>{t.label}</td>
              <td data-no-i18n>{t.name}</td>
              <td className="muted small" data-no-i18n>{t.materials.join(', ')}</td>
              <td className="num" data-no-i18n>{t.count}</td>
              <td>{STATUS_LABELS[t.status]}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <LibraryChoices materials={read.materials} types={typeConflicts(types).filter((t) => chosen.has(t.key))} value={choices} onChange={setChoices} />
    </Dialog>
  );
}
