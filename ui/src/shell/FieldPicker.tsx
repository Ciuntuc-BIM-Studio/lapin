/**
 * A searchable picker of the project's fields (`fields.model`): the groups the engine names (Lapin, type, quantities,
 * parameters, each property set of the files, the office's field names, scopes), each field with how many elements have
 * it and a few of its values. The typed text may also be a field of its own (a formula's name, a template), taken as it is.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { create } from 'zustand';
import { ChevronDown } from 'lucide-react';
import { useSession } from '../engine/session';
import { fieldModel, searchGroups, type FieldGroupDef, type FieldModel } from './fieldSchedule';
import { FACETS, TEXT_FACET, fieldLabel } from './conditionGroup';

/** The model, asked once per model version and shared by every picker open. */
const useModel = create<{ version: number; model: FieldModel | null; load: (version: number) => void }>((set, get) => ({
  version: -1, model: null,
  load: (version) => {
    if (get().version === version) return;
    set({ version });
    void fieldModel().then((model) => { if (get().version === version) set({ model }); });
  },
}));

export function useFieldModel(): FieldModel | null {
  const version = useSession((s) => s.version);
  const { model, load } = useModel();
  useEffect(() => { load(version); }, [version, load]);
  return model;
}

/** The facets of an element, listed before the project's fields when a picker asks for them (conditions on elements). */
const FACET_GROUP: FieldGroupDef = {
  id: 'facets', label: 'Element',
  fields: [...FACETS, [TEXT_FACET, 'Parameter (as text)…']].map(([token, label]) => ({ token, label, kind: 'text' as const, count: 0, examples: [] })),
};

export function FieldPicker({ value, onPick, placeholder = 'Pick a field…', testId, facets = false, extra }: { value: string; onPick: (token: string) => void; placeholder?: string; testId?: string; facets?: boolean; extra?: FieldGroupDef[] }) {
  const model = useFieldModel();
  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState('');
  const box = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState<{ top?: number; bottom?: number; left: number; maxH: number } | null>(null);
  // Opened over everything (the dialog's body scrolls): downward when there is room, else upward.
  const toggle = () => {
    const r = box.current?.getBoundingClientRect();
    if (r && !open) {
      const below = window.innerHeight - r.bottom - 8, above = r.top - 8;
      setPos(below >= 260 || below >= above ? { top: r.bottom + 2, left: Math.min(r.left, window.innerWidth - 350), maxH: Math.min(360, below) } : { bottom: window.innerHeight - r.top + 2, left: Math.min(r.left, window.innerWidth - 350), maxH: Math.min(360, above) });
    }
    setOpen(!open);
  };
  useEffect(() => {
    if (!open) return;
    const away = (e: PointerEvent) => { if (!box.current?.contains(e.target as Node)) setOpen(false); };
    window.addEventListener('pointerdown', away);
    return () => window.removeEventListener('pointerdown', away);
  }, [open]);
  // Groups the caller adds first (a parts schedule's parts' own fields).
  const groups = useMemo(() => searchGroups([...(extra ?? []), ...(facets ? [FACET_GROUP] : []), ...(model?.groups ?? [])], typed), [model, typed, facets, extra]);
  const pick = (token: string) => { onPick(token); setOpen(false); setTyped(''); };
  const total = groups.reduce((n, g) => n + g.fields.length, 0);
  return (
    <div className="field-picker" ref={box}>
      <button type="button" className="field-picker-button" onClick={toggle} data-testid={testId} aria-expanded={open}>
        <span className={value ? '' : 'muted'} data-no-i18n={value && !value.startsWith('@') ? true : undefined}>{value ? (facets ? fieldLabel(value) : value) : placeholder}</span><ChevronDown size={12} />
      </button>
      {open && (
        <div className="menu field-picker-menu" role="listbox" style={pos ? { ...pos, maxHeight: pos.maxH } : undefined}>
          <input autoFocus type="search" placeholder="Search the fields…" value={typed} onChange={(e) => setTyped(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter' && typed.trim()) pick(total === 1 ? groups[0].fields[0].token : typed.trim()); if (e.key === 'Escape') setOpen(false); }} />
          {!model && <p className="muted small">Reading the fields…</p>}
          {model && model.elements === 0 && <p className="muted small">The model has no elements yet.</p>}
          <div className="field-picker-list">
            {groups.map((g) => (
              <section key={g.id}>
                <h5 className="eyebrow" data-no-i18n={g.id === 'facets' ? undefined : true}>{g.label}</h5>
                {g.fields.map((f) => (
                  <button type="button" key={f.token} role="option" className="field-picker-item" title={f.description ?? undefined} onClick={() => pick(f.token)} data-field-token={f.token}>
                    {g.id === 'facets' ? <span>{f.label}</span> : <>
                      <span data-no-i18n>{f.token}</span>
                      <span className="muted small" data-no-i18n>{`${f.count}${f.examples?.length ? ` · ${f.examples.slice(0, 3).join(', ')}` : ''}`}</span>
                    </>}
                  </button>
                ))}
              </section>
            ))}
          </div>
          {typed.trim() && <button type="button" className="field-picker-item" onClick={() => pick(typed.trim())}><span data-no-i18n>{typed.trim()}</span><span className="muted small">as typed</span></button>}
        </div>
      )}
    </div>
  );
}
