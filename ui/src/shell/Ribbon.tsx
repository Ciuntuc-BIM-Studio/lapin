/** The top bar and the ribbon: the tabs come from the product's modules. */

import { HelpCircle, Moon, Search, Sun } from 'lucide-react';
import { useUi } from '../app/store';
import { ribbonTabs } from '../platform/modules';
import { redo, undo, useSession } from '../engine/session';

export function Ribbon() {
  const tabs = ribbonTabs();
  const tabId = useUi((s) => s.tab) ?? tabs[0]?.id;
  const tool = useUi((s) => s.tool);
  const theme = useUi((s) => s.theme);
  const doc = useSession((s) => s.doc);
  const tab = tabs.find((t) => t.id === tabId) ?? tabs[0];
  return (
    <header className="topbar">
      <div className="tabs-row">
        <button type="button" className="brand" onClick={() => useUi.setState({ screen: 'start' })} title="Start screen">Lapin</button>
        <button type="button" className="tab">File</button>
        {tabs.map((t) => (
          <button type="button" key={t.id} className={`tab${t.id === tab?.id ? ' on' : ''}`} onClick={() => useUi.setState({ tab: t.id })}>{t.label}</button>
        ))}
        <span className="grow" />
        <button type="button" className="search"><Search size={14} />Commands… <kbd>Ctrl K</kbd></button>
        <button type="button" className="icon" disabled={!doc?.canUndo} onClick={() => void undo()} title="Undo">↶</button>
        <button type="button" className="icon" disabled={!doc?.canRedo} onClick={() => void redo()} title="Redo">↷</button>
        <button type="button" className="icon" title="Theme" onClick={() => useUi.setState({ theme: theme === 'paper' ? 'graphite' : 'paper' })}>{theme === 'paper' ? <Moon size={15} /> : <Sun size={15} />}</button>
        <button type="button" className="icon" title="Help, report an issue, community"><HelpCircle size={15} /></button>
      </div>
      {tab && (
        <div className="band" role="toolbar" aria-label={tab.label}>
          {tab.groups.map((g) => (
            <div className="group" key={g.label}>
              <div className="group-tools">
                {g.tools.map((tl) => (
                  <button type="button" key={tl.id} className={`tool${tool === tl.id ? ' on' : ''}`} onClick={tl.run} title={tl.shortcut ? `${tl.label} (${tl.shortcut})` : tl.label}>
                    <tl.icon size={22} strokeWidth={1.5} />
                    <span>{tl.label}</span>
                  </button>
                ))}
              </div>
              <div className="group-label">{g.label}</div>
            </div>
          ))}
        </div>
      )}
    </header>
  );
}
