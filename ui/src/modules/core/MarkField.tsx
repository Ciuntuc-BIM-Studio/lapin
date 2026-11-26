/**
 * A mark (forum topic 177, decision 731): a type's, at the top of its type editor as a door's or window's has it, and an
 * element's own, under its name in Properties. Each change is one engine command (element.setMark, one undo step); empty
 * clears it: an element then shows its type's mark (else its type's name), a type its name. Tags read them as {mark},
 * {type.mark} and {TypeMark}; IFC writes the type's as Pset_…Common.Reference and the element's own as its Tag.
 */

import { useEffect, useState } from 'react';
import { execute, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';

/** What an element shows as its mark without one of its own: its type's mark, else its type's name. */
export function inheritedMark(type: NodeDto | undefined | null): string {
  if (!type) return '';
  return typeof type.props.mark === 'string' && type.props.mark ? type.props.mark : type.name;
}

/** The input: written on Enter or when left, only when changed; Escape puts it back. */
function MarkInput({ value, placeholder, field, onCommit }: { value: string; placeholder?: string; field: string; onCommit: (mark: string) => void }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <input value={text} placeholder={placeholder} data-field={field} maxLength={80}
      onChange={(e) => setText(e.target.value)}
      onBlur={() => { if (text.trim() !== value) onCommit(text.trim()); }}
      onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); else if (e.key === 'Escape') { setText(value); e.preventDefault(); } }} />
  );
}

/**
 * A type's mark at the top of its editor. While the type is not yet created: nothing, or, given a `draft` (issue #147), the mark
 * typed for it, kept as typed and sent by Create type with the type; the one Mark field either way.
 */
export function TypeMarkField({ typeId, draft }: { typeId: string | null; draft?: { mark: string; setMark: (m: string) => void } }) {
  const type = useSession((s) => (typeId ? s.nodes.get(typeId) : undefined));
  if ((!typeId || !type) && draft) return (
    <label className="field-line" title="The type's mark, which tags ({mark}, {TypeMark}), schedules and IFC (its Reference) give; empty: the type's name">
      <span className="eyebrow">Mark</span>
      <input value={draft.mark} placeholder="(the type's name)" data-field="type-mark" maxLength={80} onChange={(e) => draft.setMark(e.target.value)} />
    </label>
  );
  if (!typeId || !type) return null;
  const mark = typeof type.props.mark === 'string' ? type.props.mark : '';
  return (
    <label className="field-line" title="The type's mark, which tags ({mark}, {TypeMark}), schedules and IFC (its Reference) give; empty: the type's name">
      <span className="eyebrow">Mark</span>
      <MarkInput value={mark} placeholder={type.name} field="type-mark" onCommit={(m) => void execute('element.setMark', { ids: [typeId], mark: m })} />
    </label>
  );
}

/** An element's own mark under its name in Properties, its type's shown faintly while it has none. */
export function ElementMarkField({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const type = typeOf({ nodes, relations }, node.id);
  const mark = typeof node.props.mark === 'string' ? node.props.mark : '';
  return (
    <label className="props-mark" title="Its own mark, which its tags ({mark}), schedules and IFC (its Tag) give; empty: its type's mark, else its type's name">
      <span className="eyebrow">Mark</span>
      <MarkInput value={mark} placeholder={inheritedMark(type)} field="element-mark" onCommit={(m) => void execute('element.setMark', { ids: [node.id], mark: m })} />
    </label>
  );
}

/** Whether an element in Properties has a mark of its own to edit: a built element or a type, not an annotation or the spatial structure. */
export function hasMark(node: NodeDto): boolean {
  if (!node.editable) return false;
  if (['IfcProject', 'IfcSite', 'IfcBuilding', 'IfcBuildingStorey', 'IfcAnnotation', 'IfcGrid', 'IfcSpace', 'IfcZone'].includes(node.type)) return false;
  return node.type.startsWith('Ifc') && !node.type.startsWith('IfcRel') && !!node.storeyId;
}
