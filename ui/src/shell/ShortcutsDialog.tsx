/**
 * View → Keyboard shortcuts… (KS): every command the keyboard reaches, by
 * tab, with its bindings. Click + and press a chord (Ctrl+Shift+W, F4) or
 * type a sequence's letters and press Enter; × takes a binding away. A
 * binding another command has is shown with it, to reassign or not. Reset a
 * command or all of them to the preset, choose the preset (Revit-style,
 * Lapin classic), take the shortcuts to a file and back.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Download, Pencil, Plus, RotateCcw, Search, Upload, X } from 'lucide-react';
import { useUi, notify } from '../app/store';
import {
  aliasClash, captureKey, conflicts, normalizeAlias, exportKeymap, formatBinding, importKeymap, isSequence, PRESETS, resetAll, resetCommand, setKeymap, useKeymap,
  usersOf, withBinding, withoutBinding, withPreset, type Binding, type PresetId,
} from '../app/shortcuts';
import { cliNames, commands, current, findCommandDef, useBindings } from '../app/commands';
import { COMMANDS } from '../viewport/cad/session';
import { t } from '../i18n';
import { isDesktop, saveFile } from '../engine/files';

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
      const step = captureKey(letters, e);
      if (!step) return;
      if (step.kind === 'done') done.current(step.binding);
      else if (step.kind === 'letters') { letters = step.letters; setTyped(letters); }
      else if (!letters) setTyped(step.text);
    };
    // A modifier let go before its key: nothing held is shown any more.
    const onUp = () => { if (!letters) setTyped(''); };
    window.addEventListener('keyup', onUp, true);
    window.addEventListener('keydown', onKey, true);
    return () => { window.removeEventListener('keydown', onKey, true); window.removeEventListener('keyup', onUp, true); };
  }, [active]);
  return typed;
}

/** Every command an alias may run: the command line's own (`cad:NAME`) and the app's and ribbon's (their registry ids). */
function aliasTargets(): { value: string; label: string; group: string }[] {
  return [
    ...COMMANDS.map((c) => ({ value: `cad:${c.names[0]}`, label: `${c.label} (${c.names[0]})`, group: 'Command line commands' })),
    ...commands().map((c) => ({ value: c.id, label: c.label, group: c.category })),
  ];
}

const targetName = (value: string) => {
  if (value.startsWith('cad:')) { const c = COMMANDS.find((x) => x.names[0] === value.slice(4)); return c ? `${c.label} (${c.names[0]})` : value.slice(4); }
  return findCommandDef(value)?.label ?? value;
};

/** The user's command aliases (decision 473): add, change or remove one; what each one shadows is shown. */
function AliasesPanel() {
  const state = useKeymap();
  const [alias, setAlias] = useState('');
  const [find, setFind] = useState('');
  const [target, setTarget] = useState('');
  const targets = useMemo(aliasTargets, []);
  const q = find.trim().toLowerCase();
  const shown = targets.filter((x) => !q || x.label.toLowerCase().includes(q) || x.value.toLowerCase().includes(q) || x.group.toLowerCase().includes(q));
  const name = normalizeAlias(alias);
  const label = (id: string) => (id.startsWith('cad:') ? targetName(id) : findCommandDef(id)?.label ?? id);
  const clashOf = (a: string) => aliasClash(a, cliNames(), current().map);
  const notes = (a: string): string[] => {
    const c = clashOf(a);
    return [
      c.builtIn ? `${t('Replaces the command line’s own name for')} ${label(`cad:${c.builtIn}`)}` : '',
      c.sequence ? `${t('Same letters as the shortcut of')} ${label(c.sequence)}${state.letters ? ` (${t('the shortcut waits a moment; Enter runs the alias')})` : ''}` : '',
      ...(state.letters ? c.waits.map((seq) => `${t('Makes the shortcut of')} ${label(current().map.sequences[seq])} ${t('wait a moment; Enter runs the alias')}`) : []),
    ].filter(Boolean);
  };
  const add = () => {
    if (!name || !target) return;
    setKeymap({ ...state, aliases: { ...state.aliases, [name]: target } });
    setAlias(''); setTarget(''); setFind('');
  };
  const remove = (a: string) => { const aliases = { ...state.aliases }; delete aliases[a]; setKeymap({ ...state, aliases }); };
  const edit = (a: string) => { setAlias(a); setTarget(state.aliases[a]); setFind(''); };
  const rows = Object.keys(state.aliases).sort();
  return (
    <div className="dialog-body shortcuts-list aliases-panel" data-testid="aliases-panel">
      <p className="muted small">Your own short names for commands. Type one in the command line and press Enter or Space. Yours come before the built-in ones; the ` key gives the command line all typing, so nothing else takes the letters.</p>
      <table><tbody>
        {rows.map((a) => (
          <tr key={a} data-alias={a}>
            <td className="cmd-name"><kbd data-no-i18n>{a}</kbd></td>
            <td>{targetName(state.aliases[a])}{!findCommandDef(state.aliases[a]) && !state.aliases[a].startsWith('cad:') && <span className="muted small"> · <span>No such command</span></span>}
              {notes(a).map((n) => <div key={n} className="muted small alias-note" data-no-i18n>{n}</div>)}</td>
            <td className="cmd-reset">
              <button type="button" className="icon mini" title="Change this alias" onClick={() => edit(a)}><Pencil size={11} /></button>
              <button type="button" className="icon mini" title="Remove this alias" onClick={() => remove(a)}><X size={11} /></button>
            </td>
          </tr>
        ))}
        {!rows.length && <tr><td colSpan={3} className="muted small">No aliases yet.</td></tr>}
      </tbody></table>
      <h4 className="eyebrow">Add an alias</h4>
      <div className="alias-form">
        <input data-field="alias-name" placeholder="Alias" maxLength={12} value={alias} onChange={(e) => setAlias(e.target.value.replace(/[^A-Za-z0-9]/g, '').toUpperCase())} />
        <input data-field="alias-find" placeholder="Find a command" value={find} onChange={(e) => setFind(e.target.value)} />
        <select data-field="alias-target" size={6} value={target} onChange={(e) => setTarget(e.target.value)} aria-label="Command">
          {[...new Set(shown.map((x) => x.group))].map((g) => (
            <optgroup key={g} label={g}>{shown.filter((x) => x.group === g).map((x) => <option key={x.value} value={x.value} data-no-i18n>{x.label}</option>)}</optgroup>
          ))}
        </select>
        <button type="button" className="btn primary" data-field="alias-add" disabled={!name || !target} onClick={add}>Add alias</button>
      </div>
      <p className="muted small">An alias is 1 to 12 letters or digits, the first a letter.</p>
      {name && notes(name).map((n) => <p key={n} className="small alias-note" data-no-i18n>{n}</p>)}
    </div>
  );
}

export function ShortcutsDialog() {
  const [tab, setTab] = useState<'keys' | 'aliases'>('keys');
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
    setKeymap(withPreset(p, state));
  };
  const exportFile = () => { void saveFile(new TextEncoder().encode(exportKeymap(state)), 'lapin-shortcuts.json', 'application/json'); };
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
          <nav className="prefs-nav row" aria-label="Pages">
            <button type="button" className={`prefs-tab${tab === 'keys' ? ' on' : ''}`} data-field="shortcuts-tab-keys" onClick={() => setTab('keys')}>Shortcuts</button>
            <button type="button" className={`prefs-tab${tab === 'aliases' ? ' on' : ''}`} data-field="shortcuts-tab-aliases" onClick={() => setTab('aliases')}>Command aliases</button>
          </nav>
          <label className="row small"><input type="checkbox" data-field="letters-toggle" checked={state.letters} onChange={(e) => setKeymap({ ...state, letters: e.target.checked })} /> Letter shortcuts (Revit-style)</label>
        </div>
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
            <button type="button" className="btn primary" onClick={() => { setKeymap(withBinding(commands(), state, clash.id, clash.binding, true)); setClash(null); }}>{`Reassign to ${name(clash.id)}`}</button>
            <button type="button" className="btn" onClick={() => { setKeymap(withBinding(commands(), state, clash.id, clash.binding)); setClash(null); }}>Keep both</button>
            <button type="button" className="btn" onClick={() => setClash(null)}>Cancel</button>
          </div>
        )}
        {tab === 'aliases' && <AliasesPanel />}
        {tab === 'keys' && <div className="dialog-body shortcuts-list">
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
                          {adding === c.id && <span className="muted small"><span>Press the keys, or type letters and Enter · Esc cancels</span>{!isDesktop() && <> <span>In a browser, the keys the browser keeps (Ctrl+W, Ctrl+T, Ctrl+N) never reach Lapin.</span></>}</span>}
                        </div></td>
                        <td className="cmd-reset">{mine && <button type="button" className="icon mini" title="Back to the preset's shortcuts" onClick={() => setKeymap(resetCommand(state, c.id))}><RotateCcw size={11} /></button>}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </section>
          ))}
        </div>}
      </div>
    </div>
  );
}
