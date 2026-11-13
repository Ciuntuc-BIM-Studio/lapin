/**
 * „Save to library” at a type configurator (docs/plan-user-extensibility.md §5.2): the type chosen written as a .lapintypes
 * (with its layers, materials, profiles and parameters) to this computer's type packs or a writable firm folder, so the same
 * configured type is one click away in any other project (Catalog, Manage → Type packs…).
 */

import { useState } from 'react';
import { BookMarked } from 'lucide-react';
import { useSession } from '../../engine/session';
import { libraryTargets, saveTypesToLibrary } from './typeLibrary';
import { useLibraries } from './profileLibraries/store';

export function SaveTypeToLibrary({ ids, name }: { ids: string[]; name?: string }) {
  useLibraries((s) => s.version);
  const targets = libraryTargets();
  const [to, setTo] = useState('computer');
  const target = targets.find((t) => t.id === to) ?? targets[0];
  return (
    <span className="row small save-type-library" data-field="save-type-library">
      {targets.length > 1 && <select value={target.id} onChange={(e) => setTo(e.target.value)} data-field="save-type-target" aria-label="Library">{targets.map((t) => <option key={t.id} value={t.id}>{t.id === 'computer' ? 'This computer' : t.label}</option>)}</select>}
      <button type="button" className="btn small" disabled={ids.length === 0} data-action="save-type-library" title="Saves this type, with its layers, materials and parameters, as a .lapintypes to reuse in other projects"
        onClick={() => void saveTypesToLibrary(ids, name ?? useSession.getState().nodes.get(ids[0])?.name ?? 'Types', target.target)}><BookMarked size={13} />Save to library</button>
    </span>
  );
}
