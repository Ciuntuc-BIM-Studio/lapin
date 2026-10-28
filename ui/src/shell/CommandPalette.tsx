/**
 * Commands… (Ctrl+K): every command by its name, with its shortcuts from the
 * registry; type to narrow, the arrows to choose, Enter to run.
 */

import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { useUi } from '../app/store';
import { bindingText, commands, runCommand, useBindings } from '../app/commands';
import { t } from '../i18n';

export function CommandPalette() {
  useBindings();
  const all = useMemo(() => commands().filter((c) => c.id !== 'app.palette' && !c.id.startsWith('hotbar.')), []);
  const [q, setQ] = useState('');
  const [at, setAt] = useState(0);
  const close = () => useUi.setState({ dialog: null });
  const words = q.trim().toLowerCase().split(/\s+/).filter(Boolean);
  // A command matches when each word is in its name or its tab, in English or in the language shown.
  const list = all.filter((c) => {
    const text = `${c.label} ${c.category} ${c.group ?? ''} ${t(c.label)} ${t(c.category)} ${bindingText(c.id)}`.toLowerCase();
    return words.every((w) => text.includes(w));
  }).slice(0, 60);
  const run = (id: string) => { close(); setTimeout(() => runCommand(id), 0); };
  return (
    <div className="dialog-backdrop palette-backdrop" role="dialog" aria-label="Commands" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog palette">
        <label className="palette-input"><Search size={14} />
          <input autoFocus placeholder="Type a command" value={q} onChange={(e) => { setQ(e.target.value); setAt(0); }}
            onKeyDown={(e) => {
              if (e.key === 'Escape') { e.preventDefault(); close(); }
              if (e.key === 'ArrowDown') { e.preventDefault(); setAt((i) => Math.min(list.length - 1, i + 1)); }
              if (e.key === 'ArrowUp') { e.preventDefault(); setAt((i) => Math.max(0, i - 1)); }
              if (e.key === 'Enter' && list[at]) { e.preventDefault(); run(list[at].id); }
            }} />
        </label>
        <ul className="palette-list" role="listbox">
          {list.map((c, i) => (
            <li key={c.id} role="option" aria-selected={i === at} className={i === at ? 'on' : undefined} onMouseEnter={() => setAt(i)} onMouseDown={(e) => { e.preventDefault(); run(c.id); }}>
              <span>{c.label}</span>
              <span className="muted small"><span>{c.category}</span>{c.group && <> · <span>{c.group}</span></>}</span>
              <span className="grow" />
              {bindingText(c.id) && <kbd data-no-i18n>{bindingText(c.id)}</kbd>}
            </li>
          ))}
          {list.length === 0 && <li className="muted">No command by that name.</li>}
        </ul>
      </div>
    </div>
  );
}
