/** "New from example…": the bundled example graphs in a small list (name, one line about it); picking one saves a copy into the project. */

import { useEffect, useRef, useState } from 'react';
import { Shapes } from 'lucide-react';
import { scriptIcon } from '../../shell/toolIcons';
import { EXAMPLES } from './examples';
import { newGraphFromExample } from './store';

export function ExampleMenu({ labelled = false }: { labelled?: boolean }) {
  const [open, setOpen] = useState(false);
  const host = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const away = (e: PointerEvent) => { if (!host.current?.contains(e.target as Node)) setOpen(false); };
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    window.addEventListener('pointerdown', away);
    window.addEventListener('keydown', esc);
    return () => { window.removeEventListener('pointerdown', away); window.removeEventListener('keydown', esc); };
  }, [open]);
  return (
    <div className="graphs-examples" ref={host}>
      <button type="button" className={labelled ? undefined : 'icon'} title="New from example…" data-action="graph-new-example" onClick={() => setOpen(!open)}>
        <Shapes size={14} />{labelled && <> <span>New from example…</span></>}
      </button>
      {open && (
        <div className="graphs-example-list" role="menu" data-field="graph-examples">
          {EXAMPLES.map((e) => {
            const Icon = scriptIcon(e.icon, e.name);
            return (
              <button key={e.id} type="button" role="menuitem" data-example={e.id} onClick={() => { setOpen(false); void newGraphFromExample(e.id); }}>
                <Icon size={18} />
                <span className="graphs-example-text"><strong>{e.name}</strong><span className="muted small">{e.description}</span></span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
