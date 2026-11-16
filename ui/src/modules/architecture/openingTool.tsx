/**
 * One Opening tool (audit 2026-11-15, S14): its first choice is what to put in the hole, by host. In a wall: a door, a window,
 * a bay window, or an opening of a shape with nothing in it (rectangle, arches, semicircle, rounded corners, circle); in a floor:
 * an opening of its own outline (a stairwell, a shaft); in a roof: a roof window or a dormer. Each kind is the tool it was, the same
 * gesture and the same engine command; the old tools (Door, Window, Bay window, Wall opening, Floor opening, Roof window, Dormer)
 * stay as tools (`legacyTools`) that open this one set to their kind, so their ids, letters, quick slots and My tools keep working.
 */
import { create } from 'zustand';
import { useUi } from '../../app/store';
import { activate, useToolTick } from '../../platform/modules';
import type { ToolDef } from '../../platform/module';
import { DoorIcon } from '../../icons';

export type OpeningKind = 'door' | 'window' | 'bayWindow' | 'opening' | 'floor' | 'outlineHole' | 'roofWindow' | 'dormer';
export type OpeningHost = 'wall' | 'floor' | 'roof';

/** The kinds, in the picker's order, each with its host. */
export const OPENING_KINDS: readonly { kind: OpeningKind; label: string; host: OpeningHost }[] = [
  { kind: 'door', label: 'Door', host: 'wall' },
  { kind: 'window', label: 'Window', host: 'wall' },
  { kind: 'bayWindow', label: 'Bay window', host: 'wall' },
  { kind: 'opening', label: 'Opening of a shape', host: 'wall' },
  { kind: 'floor', label: 'Floor opening', host: 'floor' },
  // A hole in a floor's or a ceiling's own outline (audit S4), not an element of its own.
  { kind: 'outlineHole', label: 'Hole in the outline', host: 'floor' },
  { kind: 'roofWindow', label: 'Roof window', host: 'roof' },
  { kind: 'dormer', label: 'Dormer', host: 'roof' },
];
const HOSTS: { host: OpeningHost; label: string }[] = [{ host: 'wall', label: 'In a wall' }, { host: 'floor', label: 'In a floor' }, { host: 'roof', label: 'In a roof' }];

export const hostOf = (kind: OpeningKind): OpeningHost => OPENING_KINDS.find((k) => k.kind === kind)?.host ?? 'wall';

export const useOpeningKind = create<{ kind: OpeningKind }>(() => ({ kind: 'door' }));
export const openingKind = (): OpeningKind => useOpeningKind.getState().kind;
/** The kind the tool puts next: the views draw again and a gesture half made starts over (the tool's `mode`). */
export function setOpeningKind(kind: OpeningKind): void {
  if (useOpeningKind.getState().kind === kind) return;
  useOpeningKind.setState({ kind });
  useToolTick.setState((s) => ({ n: s.n + 1 }));
}

/** What the Opening tool puts, grouped by host: in the command line's bar and at the head of Properties. */
function KindPicker() {
  const kind = useOpeningKind((s) => s.kind);
  return (
    <span className="cad-layer on">
      <select value={kind} data-testid="opening-kind" title="What it puts: a door, a window or an opening of a shape in a wall; an opening in a floor; a roof window or a dormer in a roof"
        onChange={(e) => setOpeningKind(e.target.value as OpeningKind)}>
        {HOSTS.map((h) => (
          <optgroup key={h.host} label={h.label}>
            {OPENING_KINDS.filter((k) => k.host === h.host).map((k) => <option key={k.kind} value={k.kind}>{k.label}</option>)}
          </optgroup>
        ))}
      </select>
    </span>
  );
}

/** The Opening tool over one tool per kind, and the kinds' old tools as its aliases. */
export function openingTool(parts: Record<OpeningKind, ToolDef>): { tool: ToolDef; aliases: ToolDef[] } {
  const part = () => parts[openingKind()];
  function Options() {
    const Own = parts[useOpeningKind((s) => s.kind)].options;
    return <><KindPicker />{Own && <Own />}</>;
  }
  function Properties() {
    const Own = parts[useOpeningKind((s) => s.kind)].properties;
    return <><div className="pad new-element"><label className="field-line"><span className="eyebrow">Opening</span><KindPicker /></label></div>{Own && <Own />}</>;
  }
  const tool: ToolDef = {
    id: 'architecture.opening', label: 'Opening', icon: DoorIcon,
    get gesture() { return part().gesture; },
    get typed() { return part().typed; },
    get space() { return part().space; },
    mode: () => openingKind(),
    options: Options,
    properties: Properties,
    complete: async (input, ctx) => { await part().complete?.(input, ctx); },
  };
  const aliases = OPENING_KINDS.map(({ kind }): ToolDef => {
    const t = parts[kind];
    return {
      id: t.id, label: t.label, icon: t.icon, shortcut: t.shortcut, typed: t.typed, properties: t.properties,
      run: () => { setOpeningKind(kind); if (useUi.getState().tool !== tool.id) activate(tool); },
    };
  });
  return { tool, aliases };
}
