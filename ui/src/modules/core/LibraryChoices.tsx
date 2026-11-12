/**
 * What a type library brings that the project already has under the same name (a tester's report: nothing lost in
 * silence), shared by Manage → Type packs… and the Wall and Floor types' JSON import: each material whose definition
 * differs (its hatches, colour, parameters…) and each type that differs, with what differs and the user's choice; a
 * choice for all of them at once. The engine takes the choices (types.importFrom, architecture.importTypes: one step to
 * undo). And what an import did, counted.
 */

import { engine, execute, useSession } from '../../engine/session';
import { notify } from '../../app/store';

export interface MaterialConflict { name: string; differs: string[] }
export interface TypeConflict { key: string; name: string; label?: string; differs: string[] }
export interface LibraryChoiceState { materials: Record<string, string>; types: Record<string, string> }

export const MATERIAL_CHOICES: [string, string][] = [['keep', "Keep the project's"], ['update', 'Update from the library'], ['both', 'Keep both']];
export const TYPE_CHOICES: [string, string][] = [['update', "Update the project's"], ['both', 'Keep both'], ['skip', 'Skip']];

/** Every conflict at its default: a material kept as the project's, a type as the import does by default (`typeDefault`). */
export function defaultChoices(materials: MaterialConflict[], types: TypeConflict[], typeDefault: string): LibraryChoiceState {
  return {
    materials: Object.fromEntries(materials.map((m) => [m.name, 'keep'])),
    types: Object.fromEntries(types.map((t) => [t.key, typeDefault])),
  };
}

/** A question to the engine whose refusal the user reads (`query` answers null and says nothing). */
export async function ask<T>(name: string, args: Record<string, unknown>): Promise<T | null> {
  try { return await engine.call<T>('doc.query', { name, args }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); return null; }
}

/** A command whose refusal is always shown (a notice), not only in a dialog's last line. */
export async function run<T>(command: string, args: Record<string, unknown>): Promise<T | null> {
  const r = await execute(command, args) as T | null;
  if (r == null) notify(useSession.getState().error ?? 'The import failed.');
  return r;
}

/** What differs, each its own word (translated as such). */
const Differs = ({ list }: { list: string[] }) => (
  <span className="muted small">{list.map((d, i) => <span key={d}>{i > 0 && ', '}<span>{d}</span></span>)}</span>
);

export function LibraryChoices({ materials, types, value, onChange }: {
  materials: MaterialConflict[]; types: TypeConflict[]; value: LibraryChoiceState; onChange: (v: LibraryChoiceState) => void;
}) {
  const all = (kind: 'materials' | 'types', choice: string) => {
    if (!choice) return;
    onChange({ ...value, [kind]: Object.fromEntries(Object.keys(value[kind]).map((k) => [k, choice])) });
  };
  const pick = (kind: 'materials' | 'types', key: string, choice: string) => onChange({ ...value, [kind]: { ...value[kind], [key]: choice } });
  if (materials.length === 0 && types.length === 0) return null;
  return (
    <div className="library-choices" data-testid="library-choices">
      {materials.length > 0 && (
        <>
          <p className="eyebrow">Materials the project already has, defined otherwise</p>
          <table className="layer-table">
            <thead><tr><th>Material</th><th>What differs</th><th>
              <select value="" title="The same choice for every material" data-choice-all="materials" onChange={(e) => all('materials', e.target.value)}>
                <option value="">Apply to all…</option>
                {MATERIAL_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </th></tr></thead>
            <tbody>
              {materials.map((m) => (
                <tr key={m.name}>
                  <td data-no-i18n>{m.name}</td>
                  <td><Differs list={m.differs} /></td>
                  <td><select value={value.materials[m.name] ?? 'keep'} data-choice-material={m.name} onChange={(e) => pick('materials', m.name, e.target.value)}>
                    {MATERIAL_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                  </select></td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
      {types.length > 0 && (
        <>
          <p className="eyebrow">Types the project already has, made otherwise</p>
          <table className="layer-table">
            <thead><tr><th>Type</th><th>What differs</th><th>
              <select value="" title="The same choice for every type" data-choice-all="types" onChange={(e) => all('types', e.target.value)}>
                <option value="">Apply to all…</option>
                {TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </th></tr></thead>
            <tbody>
              {types.map((t) => (
                <tr key={t.key}>
                  <td>{t.label && <span className="muted small">{t.label} · </span>}<span data-no-i18n>{t.name}</span></td>
                  <td><Differs list={t.differs} /></td>
                  <td><select value={value.types[t.key] ?? 'both'} data-choice-type={t.name} onChange={(e) => pick('types', t.key, e.target.value)}>
                    {TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                  </select></td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
      <p className="muted small">Keep both makes the library's beside the project's, with (2) after its name. Updating changes the project's in place: what uses it follows. One step to undo.</p>
    </div>
  );
}

/** What an import did, counted (the rows it has something for). */
export interface ImportSummaryData {
  typesMade?: number; typesChanged?: number; typesSkipped?: number; typesReused?: number;
  materialsMade?: number; materialsUpdated?: number; materialsKept?: number; renamed?: string[];
}

export function ImportSummary({ r }: { r: ImportSummaryData }) {
  const rows: [string, number | undefined][] = [
    ['Types made', r.typesMade], ['Types changed', r.typesChanged], ['Types already in the project', r.typesReused], ['Types skipped', r.typesSkipped],
    ['Materials made', r.materialsMade], ['Materials updated', r.materialsUpdated], ["Materials kept as the project's", r.materialsKept],
  ];
  return (
    <dl className="fields import-summary" data-testid="import-summary">
      {rows.filter(([, n]) => n).map(([label, n]) => <span key={label} className="qrow"><dt>{label}</dt><dd data-no-i18n>{n}</dd></span>)}
      {(r.renamed?.length ?? 0) > 0 && <span className="qrow"><dt>Made beside, renamed</dt><dd data-no-i18n>{r.renamed!.join(', ')}</dd></span>}
    </dl>
  );
}
