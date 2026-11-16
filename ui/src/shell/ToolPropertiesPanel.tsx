/**
 * Tool Properties tab (forum #122): opens automatically when a tool with settings is chosen
 * and restores the previous tab when the tool finishes. Shows what the new element will be: the tool's own
 * properties panel (its type, its values), else its type picker. The tool's options (reference line, height,
 * line/arc…) stay in the command line's bar only, never shown twice (the user's choice, 16.11.2026).
 */

import { Settings2 } from 'lucide-react';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { findTool, typesOf } from '../platform/modules';

export function ToolPropertiesPanel() {
  useSession((s) => s.nodes);
  const toolId = useUi((s) => s.tool);
  useUi((s) => [s.wallTypeId, s.doorTypeId, s.windowTypeId, s.slabTypeId, s.stairTypeId, s.rampTypeId, s.ceilingTypeId, s.skirtingTypeId, s.curtainTypeId, s.memberTypeId].join());
  const tool = findTool(toolId);
  if (!tool) {
    return (
      <aside className="panel props" aria-label="Tool Properties">
        <div className="panel-body" data-testid="tool-properties-panel">
          <p className="muted small">Pick a tool to see its settings here.</p>
        </div>
      </aside>
    );
  }
  const typed = tool.typed;
  const types = typed ? typesOf(tool) : [];
  const currentType = typed ? (typed.get() ?? types[0]?.id ?? '') : '';
  const Props = tool.properties;
  // The same panel as Properties (aside.props), so the new element's fields look and are found the same way.
  return (
    <aside className="panel props" aria-label="Tool Properties">
    <div className="panel-body tool-properties" data-testid="tool-properties-panel">
      <div className="row between" style={{ marginBottom: 8 }}>
        <span className="row" style={{ gap: 6 }}>
          <tool.icon size={15} />
          <b>{tool.label}</b>
        </span>
        {tool.shortcut && <span className="muted small" data-no-i18n>{tool.shortcut}</span>}
      </div>
      {Props ? <Props /> : typed && (
        <div className="part-section">
          <div className="part-title">Type</div>
          <div className="row" style={{ gap: 4 }}>
            <select className="input small grow" value={currentType} onChange={(e) => typed.set(e.target.value || null)} data-testid="tool-props-type">
              {types.length === 0 && <option value="">Default</option>}
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
            {typed.dialog && (
              <button type="button" className="icon" title="Edit types…" onClick={() => useUi.setState({ dialog: typed.dialog! })}>
                <Settings2 size={14} />
              </button>
            )}
          </div>
        </div>
      )}
    </div>
    </aside>
  );
}
