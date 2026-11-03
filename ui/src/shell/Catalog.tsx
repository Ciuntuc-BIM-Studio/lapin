/**
 * The catalog (decision 263), the browser's third tab: the project's types
 * for each tool that places typed elements (walls, doors, windows, floors…),
 * each drawn small (its layers, its elevation, its profile) with what it is
 * in a few words. A click makes it the tool's type and starts the tool; a
 * card dragged onto a quick slot puts the tool with that type there.
 */

import { useState } from 'react';
import { Settings2 } from 'lucide-react';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { slotOf, TOOL_DRAG } from '../app/hotbar';
import { activate, allTools, typesOf } from '../platform/modules';
import type { ToolDef } from '../platform/module';

const KIND_KEY = 'lapin.catalog.kind';

/** One tool per type class, the shortest name standing for it (Wall, not Curved wall), in the tools' order. */
export function catalogKinds(tools: ToolDef[] = allTools()): ToolDef[] {
  const by = new Map<string, ToolDef>();
  for (const t of tools) {
    if (!t.typed) continue;
    const had = by.get(t.typed.cls);
    if (!had || t.label.length < had.label.length) by.set(t.typed.cls, t);
  }
  return [...by.values()];
}

export function Catalog() {
  useSession((s) => s.nodes);
  useUi((s) => [s.wallTypeId, s.doorTypeId, s.windowTypeId, s.slabTypeId, s.stairTypeId, s.rampTypeId, s.ceilingTypeId, s.skirtingTypeId, s.curtainTypeId, s.memberTypeId].join());
  const kinds = catalogKinds();
  const [kindId, setKindId] = useState<string | null>(() => { try { return localStorage.getItem(KIND_KEY); } catch { return null; } });
  const tool = kinds.find((k) => k.id === kindId) ?? kinds[0];
  if (!tool?.typed) return <p className="muted small pad">This edition places no typed elements.</p>;
  const typed = tool.typed;
  const pick = (id: string) => { setKindId(id); try { localStorage.setItem(KIND_KEY, id); } catch { /* a convenience only */ } };
  const types = typesOf(tool);
  const current = typed.get() ?? types[0]?.id ?? null;
  const nodes = useSession.getState().nodes;
  const Thumb = typed.thumb;
  return (
    <div className="catalog" data-testid="catalog">
      <div className="catalog-kinds" role="tablist" aria-label="Catalog">
        {kinds.map((k) => (
          <button type="button" key={k.id} role="tab" aria-selected={k.id === tool.id} className={`kind-chip${k.id === tool.id ? ' on' : ''}`} data-kind={k.id} onClick={() => pick(k.id)}>{k.label}</button>
        ))}
      </div>
      {types.length === 0 && <p className="muted small pad">No types yet: the first is made as you place one.</p>}
      <div className="catalog-cards">
        {types.map((x) => {
          const node = nodes.get(x.id)!;
          const about = typed.about?.(node);
          return (
            <button type="button" key={x.id} className={`catalog-card${x.id === current ? ' on' : ''}`} data-type={x.name}
              title={`${tool.label} · ${x.name}`} draggable
              onDragStart={(e) => { e.dataTransfer.setData(TOOL_DRAG, slotOf(tool.id, x.name)); e.dataTransfer.effectAllowed = 'copy'; }}
              onClick={() => { typed.set(x.id); activate(tool); }}>
              <span className="catalog-thumb">{Thumb ? <Thumb type={node} /> : <tool.icon size={26} strokeWidth={1.3} />}</span>
              <span className="catalog-text">
                <span className="catalog-name" data-no-i18n>{x.name}</span>
                {about && <span className="muted small" data-no-i18n>{about}</span>}
              </span>
            </button>
          );
        })}
      </div>
      <p className="muted small pad">A click places it; drag a card onto a quick slot to keep it there.</p>
      {typed.dialog && <button type="button" className="btn small catalog-edit" onClick={() => useUi.setState({ dialog: typed.dialog! })}><Settings2 size={13} /> Edit types…</button>}
    </div>
  );
}
