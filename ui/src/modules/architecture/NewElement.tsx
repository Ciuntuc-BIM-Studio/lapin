/**
 * What a placing tool puts down, shown in Properties while the tool is on (a tester's report, decision 247):
 * the type it uses, chosen there before the first click, what that type is (its layers and thickness, a door's
 * or window's size), how many elements already use it, and the way to its types' dialog. The choice is the
 * tool's own type (wallTypeId, doorTypeId…), as Create similar and Draw with it set it.
 */

import type { ComponentType } from 'react';
import { Settings2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import type { TypedTool } from '../../platform/module';
import { useUi, type UiState } from '../../app/store';
import { useSession } from '../../engine/session';
import { materialName, mm, ofClass, relatedOf, typeLayers } from '../../engine/model';

type TypeField = 'wallTypeId' | 'doorTypeId' | 'windowTypeId' | 'slabTypeId' | 'ceilingTypeId' | 'skirtingTypeId' | 'curtainTypeId' | 'stairTypeId' | 'rampTypeId';

/** A tool's types for the quick slots (decision 260) and the catalog (decision 263): the class, its choice kept in `field`, edited in `dialog`, drawn by `thumb`. */
export const typedBy = (cls: string, field: TypeField, dialog?: string, thumb?: ComponentType<{ type: NodeDto }>): TypedTool => ({
  cls, get: () => useUi.getState()[field] as string | null, set: (id) => useUi.setState({ [field]: id } as Partial<UiState>), dialog, thumb,
  about: (t) => { const { nodes, relations } = useSession.getState(); return about({ nodes, relations }, t); },
});

interface Spec { width?: number; height?: number; sillHeight?: number; profile?: string; thickness?: number; material?: string }

/** A type in a few words: a layered type's layers and thickness, a door's or window's size and sill. */
function about(model: { nodes: Map<string, NodeDto>; relations: Parameters<typeof typeLayers>[0]['relations'] }, t: NodeDto): string | null {
  const layers = typeLayers(model, t.id);
  if (layers.length) {
    const total = layers.reduce((s, l) => s + l.thickness, 0);
    return `${layers.map((l) => `${materialName(model, l.material)} ${mm(l.thickness)}${l.core ? '*' : ''}`).join(' · ')} = ${mm(total)}`;
  }
  const spec = t.props.spec as Spec | undefined;
  // A skirting's: its profile, height × thickness and material.
  if (spec?.profile && spec.height && spec.thickness) return `${spec.profile} · ${mm(spec.height)} × ${mm(spec.thickness)}${spec.material ? ` · ${spec.material}` : ''}`;
  if (spec?.width && spec.height) return `${mm(spec.width)} × ${mm(spec.height)}${spec.sillHeight ? ` · sill ${mm(spec.sillHeight)}` : ''}`;
  return null;
}

/** The Properties section of a tool placing elements of a type class, its choice kept in `field`, its types edited in `dialog`. */
export function newElement(cls: string, field: TypeField, dialog: string): ComponentType {
  return function NewElementType() {
    const nodes = useSession((s) => s.nodes);
    const relations = useSession((s) => s.relations);
    const chosen = useUi((s) => s[field] as string | null);
    const model = { nodes, relations };
    const types = ofClass(model, cls);
    // None chosen yet: the tool takes the project's first.
    const current = types.find((t) => t.id === chosen) ?? types[0];
    const text = current ? about(model, current) : null;
    return (
      <div className="pad new-element" data-field="new-element">
        <label className="field-line"><span className="eyebrow">Type</span>
          {types.length
            ? <select value={current?.id ?? ''} onChange={(e) => useUi.setState({ [field]: e.target.value } as Partial<UiState>)}>
                {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
              </select>
            : <span className="muted small">The first one is made as you place it.</span>}
        </label>
        {text && <p className="small" data-no-i18n>{text}</p>}
        {current && <p className="muted small"><span>Elements of this type:</span> {relatedOf(model, current.id, 'IfcRelDefinesByType').length}</p>}
        <button type="button" className="btn small" onClick={() => useUi.setState({ dialog })}><Settings2 size={13} /> Edit types…</button>
      </div>
    );
  };
}
