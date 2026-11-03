/**
 * View → Quick tools… (decision 259): the nine quick slots of the current
 * workspace, each a tool chosen from the ribbon's tabs or empty, and the
 * modules' own set again; a tool that places typed elements takes one of the
 * project's types too (decision 260), else the tool's current one. The same as
 * dragging a ribbon tool onto a slot or a slot's right-click menu; applied at
 * once, kept for this user.
 */

import { X } from 'lucide-react';
import { useUi } from '../app/store';
import { useHotbars } from '../app/hotbar';
import { useInterface, WORKSPACE_LABELS } from '../app/interface';
import { defaultHotbar, hotbarIds, putInSlot, quickSlot, resetSlots, ribbonTabs, typeSlot, typesOf } from '../platform/modules';
import { parseSlot } from '../app/hotbar';
import { useSession } from '../engine/session';
import type { ToolDef } from '../platform/module';

/** The tools to choose from: each ribbon tab's, then the quick tools no tab has. */
function choices(): { label: string; tools: ToolDef[] }[] {
  const seen = new Set<string>();
  const out: { label: string; tools: ToolDef[] }[] = [];
  const take = (label: string, tools: ToolDef[]) => {
    const fresh = tools.filter((t) => !seen.has(t.id) && (t.gesture || t.run));
    for (const t of fresh) seen.add(t.id);
    if (fresh.length) out.push({ label, tools: fresh });
  };
  take('Quick tools', defaultHotbar().filter((t) => !ribbonTabs().some((tab) => tab.groups.some((g) => [...g.tools, ...(g.small ?? [])].some((x) => x.id === t.id)))));
  for (const tab of ribbonTabs()) take(tab.label, tab.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])]));
  return out;
}

export function QuickToolsDialog() {
  const hotbars = useHotbars();
  const workspace = useInterface((s) => s.workspace);
  useSession((s) => s.nodes);
  const slots = hotbarIds(hotbars, workspace);
  const groups = choices();
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Quick tools" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog quick-tools-dialog">
        <header className="dialog-head">
          <span className="panel-title">Quick tools</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small"><span>The tools the keys 1–9 start, for the workspace</span> <b>{WORKSPACE_LABELS[workspace]}</b>. <span>Drag a tool from the ribbon onto a slot, or one slot onto another to swap them; right-click a slot for its menu.</span></p>
          <div className="quick-slots">
            {slots.map((id, i) => {
              const q = quickSlot(id);
              const t = q?.tool ?? null;
              const types = typesOf(t);
              return (
                <div className="quick-slot-row" key={i}>
                  <kbd data-no-i18n>{i + 1}</kbd>
                  <span className="quick-slot-icon">{t ? <t.icon size={18} strokeWidth={1.5} /> : null}</span>
                  <select aria-label={`Quick tool ${i + 1}`} data-slot={i + 1} value={parseSlot(id)?.tool ?? ''} onChange={(e) => putInSlot(i, e.target.value || null)}>
                    <option value="">(empty)</option>
                    {groups.map((g) => (
                      <optgroup key={g.label} label={g.label}>
                        {g.tools.map((tool) => <option key={tool.id} value={tool.id}>{tool.label}</option>)}
                      </optgroup>
                    ))}
                  </select>
                  {t?.typed
                    ? <select aria-label={`Type of quick tool ${i + 1}`} data-slot-type={i + 1} className="quick-slot-type" value={q!.typeName ?? ''} onChange={(e) => typeSlot(i, e.target.value || null)}>
                        <option value="">The tool's current type</option>
                        {q!.typeName && !q!.typeId && <option value={q!.typeName} data-no-i18n>{q!.typeName} (not in this project)</option>}
                        {types.map((x) => <option key={x.id} value={x.name} data-no-i18n>{x.name}</option>)}
                      </select>
                    : <span />}
                </div>
              );
            })}
          </div>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={resetSlots}>Reset to the edition's</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
