/**
 * A door's or window's data as free text (report #76): U-value, g-value, safety glass, fire rating, fittings, and a few
 * parameters of the user's own ("sauna window", "frosted glass"). On a type, or on some elements, whose own value then wins
 * over the type's. They are the user's parameters (the schedules, the tags and the properties panel read them as any),
 * the fire rating in IFC's common set, the rest in Lapin_Joinery.
 */

import { useState } from 'react';
import { Database, Plus, Trash2, X } from 'lucide-react';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';

/** The set the data is written to in IFC; the fire rating is the class's common set's. */
const SET = 'Lapin_Joinery';

export const DATA_FIELDS = [
  { key: 'uValue', name: 'U-value', label: 'U-value', hint: 'W/m²K: a number is written to IFC as ThermalTransmittance, any text is kept as it is' },
  { key: 'gValue', name: 'g-value', label: 'g-value', hint: 'The glazing’s solar factor, e.g. 0.52' },
  { key: 'safetyGlass', name: 'Safety glass', label: 'Safety glass', hint: 'Free text, e.g. laminated inside, tempered both sides' },
  { key: 'fireRating', name: 'FireRating', label: 'Fire rating', hint: 'Free text, e.g. EI 30, EI2 60-C5' },
  { key: 'fittings', name: 'Fittings', label: 'Fittings', hint: 'Hinges, locks, handles, closers' },
] as const;

interface Stored { name: string; value?: unknown; pset?: string }
const paramsOf = (n: NodeDto | undefined): Stored[] => (Array.isArray(n?.props.params) ? n!.props.params as unknown as Stored[] : []);
const text = (p: Stored | undefined): string => (p?.value == null ? '' : String(p.value));

/** The data an entity holds itself: the fixed fields by key, and the extras in order. */
export function dataOf(n: NodeDto | undefined): { fixed: Record<string, string>; extras: { name: string; value: string }[] } {
  const list = paramsOf(n);
  const squash = (s: string) => s.replace(/[^\p{L}\p{N}]/gu, '').toLowerCase();
  const fixed: Record<string, string> = {};
  for (const f of DATA_FIELDS) fixed[f.key] = text(list.find((p) => squash(p.name) === squash(f.name) && (f.key === 'fireRating' ? /^Pset_(Door|Window)Common$/.test(p.pset ?? '') : p.pset === SET)));
  const extras = list.filter((p) => p.pset === SET && !DATA_FIELDS.some((f) => squash(f.name) === squash(p.name))).map((p) => ({ name: p.name, value: text(p) }));
  return { fixed, extras };
}

/** The "Data…" button and, when pressed, its box. `type`: editing a type; else `ids`, elements whose own value wins over `typeNode`'s. */
export function JoineryDataButton({ type, ids, typeNode, own }: { type?: NodeDto | null; ids?: string[]; typeNode?: NodeDto | null; own?: NodeDto }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" className="btn" data-testid="joinery-data" title="U-value, g-value, safety glass, fire rating, fittings and parameters of your own" onClick={() => setOpen(true)}><Database size={14} />Data…</button>
      {open && <JoineryDataDialog type={type} ids={ids} typeNode={typeNode} own={own} onClose={() => setOpen(false)} />}
    </>
  );
}

function JoineryDataDialog({ type, ids, typeNode, own, onClose }: { type?: NodeDto | null; ids?: string[]; typeNode?: NodeDto | null; own?: NodeDto; onClose: () => void }) {
  const onType = Boolean(type);
  const held = dataOf(onType ? type! : own);
  const inherited = onType ? null : dataOf(typeNode ?? undefined);
  const [fixed, setFixed] = useState<Record<string, string>>(held.fixed);
  const [extras, setExtras] = useState(held.extras);
  const save = async () => {
    // An element keeps only what differs from its type's; the rest falls back to it.
    const data: Record<string, string> = {};
    for (const f of DATA_FIELDS) data[f.key] = inherited && fixed[f.key] === inherited.fixed[f.key] ? '' : fixed[f.key]!.trim();
    const args = { data, extras: extras.filter((x) => x.name.trim()).map((x) => ({ name: x.name.trim(), value: x.value })) };
    await execute('architecture.setJoineryData', onType ? { typeId: type!.id, ...args } : { ids, ...args });
    onClose();
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Door and window data" style={{ zIndex: 60 }}>
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">{onType ? 'Data of the type' : 'Data of the selected'}</span>
          <button type="button" className="icon" onClick={onClose} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="joinery-form" data-testid="joinery-data-form">
            {DATA_FIELDS.map((f) => (
              <label key={f.key} className="field-line" title={f.hint}><span className="eyebrow">{f.label}</span>
                <input data-field={f.key} value={fixed[f.key]} placeholder={inherited?.fixed[f.key] || ''} onChange={(e) => setFixed({ ...fixed, [f.key]: e.target.value })} />
              </label>
            ))}
            <p className="eyebrow">More data</p>
            {extras.map((x, i) => (
              <div key={i} className="row" data-testid="joinery-extra">
                <input data-field="extra-name" value={x.name} placeholder="Name" onChange={(e) => setExtras(extras.map((y, j) => (j === i ? { ...y, name: e.target.value } : y)))} />
                <input data-field="extra-value" value={x.value} placeholder="Value" onChange={(e) => setExtras(extras.map((y, j) => (j === i ? { ...y, value: e.target.value } : y)))} />
                <button type="button" className="icon" title="Remove" onClick={() => setExtras(extras.filter((_, j) => j !== i))}><Trash2 size={13} /></button>
              </div>
            ))}
            <button type="button" className="btn" data-testid="joinery-extra-add" onClick={() => setExtras([...extras, { name: '', value: '' }])}><Plus size={13} />Add data</button>
            <p className="muted small">{'Free text. In the schedules and in tags as {U-value}, {Fire rating}…; in IFC the fire rating and a U-value that is a number are Pset_WindowCommon / Pset_DoorCommon’s, the rest is in Lapin_Joinery.'}</p>
          </div>
          <div className="row joinery-actions">
            <span className="grow" />
            <button type="button" className="btn" onClick={onClose}>Cancel</button>
            <button type="button" className="btn primary" data-testid="joinery-data-save" onClick={() => void save()}>Save data</button>
          </div>
        </div>
      </div>
    </div>
  );
}
