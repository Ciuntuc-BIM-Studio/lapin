/**
 * View → Keyboard shortcuts… (KS): every command the keyboard reaches, by
 * tab, with its bindings. Click + and press a chord (Ctrl+Shift+W, F4) or
 * type a sequence's letters and press Enter; × takes a binding away. A
 * binding another command has is shown with it, to reassign or not. Reset a
 * command or all of them to the preset, choose the preset (Revit-style,
 * Lapin classic), take the shortcuts to a file and back.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Download, Plus, RotateCcw, Search, Upload, X } from 'lucide-react';
import { useUi, notify } from '../app/store';
import {
  chordOf, conflicts, exportKeymap, formatBinding, importKeymap, isSequence, PRESETS, resetAll, resetCommand, setKeymap, useKeymap,
  usersOf, withBinding, withoutBinding, withPreset, type Binding, type PresetId,
} from '../app/shortcuts';
import { commands, findCommandDef, useBindings } from '../app/commands';

/** Listening for a binding: a chord at once, or letters ended by Enter. */
function useCapture(active: boolean, onDone: (b: Binding | null) => void): string {
  const [typed, setTyped] = useState('');
  const done = useRef(onDone);
  done.current = onDone;
  useEffect(() => {
    if (!active) { setTyped(''); return; }
    let letters = '';
    const onKey = (e: KeyboardEvent) => {
      e.preventDefault();
      e.stopImmediatePropagation();
      if (e.key === 'Escape') { done.current(null); return; }
      if (e.key === 'Enter') { done.current(letters ? letters : null); return; }
      if (e.key === 'Backspace' && letters) { letters = letters.slice(0, -1); setTyped(letters); return; }
      const plainLetter = /^[a-z]$/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey;
      if (plainLetter) {
        letters = (letters + e.key.toUpperCase()).slice(0, 3);
        setTyped(letters);
        if (letters.length === 3) done.current(letters);
        return;
      }
      if (letters) return;
      const chord = chordOf(e);
      if (chord) done.current(chord);
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [active]);
  return typed;
}

export function ShortcutsDialog() {
  const state = useKeymap();
  const bindings = useBindings();
  const all = useMemo(() => commands(), []);
  const [filter, setFilter] = useState('');
  const [adding, setAdding] = useState<string | null>(null);
  const [clash, setClash] = useState<{ id: string; binding: Binding; users: string[] } | null>(null);
  const file = useRef<HTMLInputElement>(null);
  const close = () => useUi.setState({ dialog: null });
  const shared = conflicts(bindings);

  const typed = useCapture(adding !== null, (b) => {
    const id = adding;
    setAdding(null);
    if (!b || !id) return;
    const users = usersOf(bindings, b, id);
    if (users.length) { setClash({ id, binding: b, users }); return; }
    setKeymap(withBinding(commands(), state, id, b));
  });

  const q = filter.trim().toLowerCase();
  const shown = all.filter((c) => !q || c.label.toLowerCase().includes(q) || c.category.toLowerCase().includes(q) || c.id.includes(q)
    || (bindings[c.id] ?? []).some((b) => b.toLowerCase().includes(q) || formatBinding(b).toLowerCase().includes(q)));
  const categories = [...new Set(shown.map((c) => c.category))];
  const name = (id: string) => findCommandDef(id)?.label ?? id;

  const choosePreset = (p: PresetId) => {
    if (Object.keys(state.overrides).length && !window.confirm('Your own changes to the shortcuts go, and the preset’s come. Go on?')) return;
    setKeymap(withPreset(p));
  };
  const exportFile = () => {
    const blob = new Blob([exportKeymap(state)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob); a.download = 'lapin-shortcuts.json'; a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  };
  const importFile = async (f: File) => {
    const s = importKeymap(await f.text());
    if (!s) { notify('That is not a Lapin shortcuts file.'); return; }
    setKeymap(s);
    notify('The shortcuts were read from the file.');
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Keyboard shortcuts" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog wide shortcuts-dialog">
        <header className="dialog-head">
          <span className="panel-title">Keyboard shortcuts</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="shortcuts-bar">
          <label className="props-filter"><Search size={12} /><input placeholder="Find a command or a key" value={filter} autoFocus onChange={(e) => setFilter(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') { if (filter) { e.stopPropagation(); setFilter(''); } else close(); } }} /></label>
          <label className="row small"><span className="eyebrow">Preset</span>
            <select value={state.preset} onChange={(e) => choosePreset(e.target.value as PresetId)} aria-label="Preset">
              {(Object.keys(PRESETS) as PresetId[]).map((p) => <option key={p} value={p}>{PRESETS[p].label}</option>)}
            </select>
          </label>
          <span className="grow" />
          <button type="button" className="btn" disabled={!Object.keys(state.overrides).length} onClick={() => setKeymap(resetAll(state))}><RotateCcw size={13} />Reset all</button>
          <button type="button" className="btn" onClick={exportFile}><Download size={13} />Export…</button>
          <button type="button" className="btn" onClick={() => file.current?.click()}><Upload size={13} />Import…</button>
          <input ref={file} type="file" accept=".json,application/json" hidden onChange={(e) => { const f = e.target.files?.[0]; if (f) void importFile(f); e.target.value = ''; }} />
        </div>
        {clash && (
          <div className="shortcut-clash" role="alert">
            <span><kbd data-no-i18n>{formatBinding(clash.binding)}</kbd> <span>is already used by</span> <b>{clash.users.map(name).join(', ')}</b>.</span>
            <span className="grow" />
            <button type="button" className="btn primary" onClick={() => { setKeymap(withBinding(commands(), state, clash.id, clash.binding, true)); setClash(null); }}>Reassign to {name(clash.id)}</button>
            <button type="button" className="btn" onClick={() => { setKeymap(withBinding(commands(), state, clash.id, clash.binding)); setClash(null); }}>Keep both</button>
            <button type="button" className="btn" onClick={() => setClash(null)}>Cancel</button>
          </div>
        )}
        <div className="dialog-body shortcuts-list">
          <p className="muted small">Letters (WA) are typed in a view with nothing in a field and the command line empty; a command line command that starts with the same letters makes them wait a moment, or run with Enter. Esc always cancels.</p>
          {categories.map((cat) => (
            <section key={cat} className="shortcut-group">
              <h4 className="eyebrow">{cat}</h4>
              <table>
                <tbody>
                  {shown.filter((c) => c.category === cat).map((c) => {
                    const list = bindings[c.id] ?? [];
                    const mine = c.id in state.overrides;
                    return (
                      <tr key={c.id} className={adding === c.id ? 'adding' : undefined} data-command={c.id}>
                        <td className="cmd-name">{c.label}{c.group && <span className="muted small"> · {c.group}</span>}</td>
                        <td><div className="cmd-keys">
                          {(c.fixed ?? []).map((b) => <span key={b} className="key-chip fixed" title="Part of how Lapin works: not changed here"><kbd data-no-i18n>{formatBinding(b)}</kbd></span>)}
                          {list.map((b) => (
                            <span key={b} className={`key-chip${shared[b] ? ' warn' : ''}${isSequence(b) ? ' seq' : ''}`} title={shared[b] ? `Also used by ${shared[b].filter((x) => x !== c.id).map(name).join(', ')}` : undefined}>
                              <kbd data-no-i18n>{formatBinding(b)}</kbd>
                              <button type="button" className="icon mini" title="Remove this shortcut" onClick={() => setKeymap(withoutBinding(commands(), state, c.id, b))}><X size={10} /></button>
                            </span>
                          ))}
                          {adding === c.id
                            ? <span className="key-chip capture" data-no-i18n>{typed || '…'}</span>
                            : <button type="button" className="icon mini add-key" title="Add a shortcut: press the keys, or type letters and Enter" onClick={() => { setClash(null); setAdding(c.id); }}><Plus size={12} /></button>}
                          {adding === c.id && <span className="muted small"><span>Press the keys, or type letters and Enter · Esc cancels</span></span>}
                        </div></td>
                        <td className="cmd-reset">{mine && <button type="button" className="icon mini" title="Back to the preset's shortcuts" onClick={() => setKeymap(resetCommand(state, c.id))}><RotateCcw size={11} /></button>}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </section>
          ))}
        </div>
      </div>
    </div>
  );
}
