/**
 * A material chosen from the project's (by name): only materials that exist
 * can be picked, and a new one is made in place ("New material…" asks its
 * name and creates it), so every list that names materials shows the same
 * ones and a material made in the Materials dialog is there at once.
 */

import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';

const NEW = '\u0000new';

export function MaterialPicker({ value, onChange, placeholder, allowEmpty, byId }: { value: string; onChange: (value: string) => void; placeholder?: string; allowEmpty?: boolean; byId?: boolean }) {
  const model = useSession();
  const materials = ofClass(model, 'IfcMaterial').sort((a, b) => a.name.localeCompare(b.name));
  const names = materials.map((m) => m.name);
  // By id (a Studio solid's material): the value and what is picked are material ids, shown by their names.
  const options = byId ? materials.map((m) => ({ value: m.id, label: m.name })) : names.map((n) => ({ value: n, label: n }));
  const known = options.some((o) => o.value === value);
  const pick = async (next: string) => {
    if (next !== NEW) { onChange(next); return; }
    const name = window.prompt('The new material\'s name')?.trim();
    if (!name) return;
    const existing = materials.find((m) => m.name === name);
    if (byId) {
      const id = existing?.id ?? ((await execute('material.create', { name })) as { id?: string } | undefined)?.id ?? '';
      if (id) onChange(id);
      return;
    }
    if (!existing) await execute('material.create', { name });
    onChange(name);
  };
  return (
    <select value={value} onChange={(e) => void pick(e.target.value)}>
      {(allowEmpty || !value) && <option value="">{placeholder ?? '—'}</option>}
      {!known && value && <option value={value} data-no-i18n>{value} (new)</option>}
      {/* Names are the project's data: never translated (a renderer finds them by name). */}
      {options.map((o) => <option key={o.value} value={o.value} data-no-i18n>{o.label}</option>)}
      <option value={NEW}>New material…</option>
    </select>
  );
}
