/**
 * The type a column, beam or pad footing is placed with: the project's default unless one is picked in the command line's bar, or
 * set by Create similar from an element (audit 2026-11-15: Create similar for every class that has a placing tool).
 */
import { create } from 'zustand';
import { useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';

export type PickKind = 'column' | 'beam' | 'pad';
const CLASS: Record<PickKind, string> = { column: 'IfcColumnType', beam: 'IfcBeamType', pad: 'IfcFootingType' };

export const useTypePick = create<Record<PickKind, string | null>>(() => ({ column: null, beam: null, pad: null }));

const typesFor = (kind: PickKind, model: Parameters<typeof ofClass>[0]): NodeDto[] =>
  ofClass(model, CLASS[kind]).filter((t) => kind !== 'pad' || t.props.kind === 'pad');

/** The engine's typeId for the next one, when one is picked and still in the project. */
export function typeArg(kind: PickKind): { typeId?: string } {
  const id = useTypePick.getState()[kind];
  return id && useSession.getState().nodes.has(id) ? { typeId: id } : {};
}

/** The type picker of a tool's bar. */
export function TypeOption({ kind }: { kind: PickKind }) {
  const id = useTypePick((s) => s[kind]);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const types = typesFor(kind, { nodes, relations });
  if (!types.length) return null;
  return (
    <span className="cad-layer on">
      <select aria-label="Type" data-testid={`structure-type-${kind}`} title="The type it is placed with: the project's default, or one of the project's types"
        value={id && types.some((t) => t.id === id) ? id : ''} onChange={(e) => useTypePick.setState({ [kind]: e.target.value || null })}>
        <option value="">Default type</option>
        {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
      </select>
    </span>
  );
}
