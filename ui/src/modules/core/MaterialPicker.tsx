/**
 * A material chosen from the project's (by name): only materials that exist
 * can be picked, and a new one is made in place ("New material…" asks its
 * name and creates it), so every list that names materials shows the same
 * ones and a material made in the Materials dialog is there at once.
 */

import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';

const NEW = '\u0000new';

export function MaterialPicker({ value, onChange, placeholder, allowEmpty }: { value: string; onChange: (name: string) => void; placeholder?: string; allowEmpty?: boolean }) {
  const model = useSession();
  const names = ofClass(model, 'IfcMaterial').map((m) => m.name).sort((a, b) => a.localeCompare(b));
  const known = names.includes(value);
  const pick = async (next: string) => {
    if (next !== NEW) { onChange(next); return; }
    const name = window.prompt('The new material\'s name')?.trim();
    if (!name) return;
    if (!names.includes(name)) await execute('material.create', { name });
    onChange(name);
  };
  return (
    <select value={known || !value ? value : value} onChange={(e) => void pick(e.target.value)}>
      {(allowEmpty || !value) && <option value="">{placeholder ?? '—'}</option>}
      {!known && value && <option value={value}>{value} (new)</option>}
      {names.map((n) => <option key={n} value={n}>{n}</option>)}
      <option value={NEW}>New material…</option>
    </select>
  );
}
