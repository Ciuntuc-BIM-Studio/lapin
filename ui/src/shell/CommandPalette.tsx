/**
 * Commands… (Ctrl+K): every command by its name, with its shortcuts from the
 * registry, recent commands first when opened empty, fuzzy matching and tool
 * icons; type to narrow, the arrows to choose, Enter to run.
 */

import { useMemo, useState } from 'react';
import { Clock, Search } from 'lucide-react';
import { useUi } from '../app/store';
import { bindingText, commands, runCommand, useBindings } from '../app/commands';
import { findTool } from '../platform/modules';
import { t } from '../i18n';

const RECENT_KEY = 'lapin.palette.recent';
const MAX_RECENT = 8;

function readRecent(): string[] {
  try {
    const raw = localStorage.getItem(RECENT_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string').slice(0, MAX_RECENT) : [];
  } catch { return []; }
}

function noteRecent(id: string): void {
  const next = [id, ...readRecent().filter((x) => x !== id)].slice(0, MAX_RECENT);
  try { localStorage.setItem(RECENT_KEY, JSON.stringify(next)); } catch { /* session only */ }
}

/** Subsequence fuzzy match: every character of `word` appears in `text` in order. */
export function fuzzyWord(text: string, word: string): boolean {
  if (text.includes(word)) return true;
  let i = 0;
  for (let j = 0; j < text.length && i < word.length; j++) if (text[j] === word[i]) i++;
  return i === word.length;
}

/** Score a command against query words: lower is better (0 = exact prefix, 1 = substring, 2 = fuzzy, -1 = no match). */
export function matchScore(text: string, words: readonly string[]): number {
  if (!words.length) return 0;
  let score = 0;
  for (const w of words) {
    if (text.startsWith(w)) score += 0;
    else if (text.includes(w)) score += 1;
    else if (fuzzyWord(text, w)) score += 2;
    else return -1;
  }
  return score;
}

export function CommandPalette() {
  useBindings();
  const all = useMemo(() => commands().filter((c) => c.id !== 'app.palette' && !c.id.startsWith('hotbar.')), []);
  const recent = useMemo(() => readRecent(), []);
  const recentRank = useMemo(() => new Map(recent.map((id, i) => [id, i])), [recent]);
  const [q, setQ] = useState('');
  const [at, setAt] = useState(0);
  const close = () => useUi.setState({ dialog: null });
  const words = q.trim().toLowerCase().split(/\s+/).filter(Boolean);
  // A command matches when each word is in its name or its tab (substring or fuzzy), in English or in the language shown.
  const list = useMemo(() => {
    if (!words.length) {
      const rec = recent.map((id) => all.find((c) => c.id === id)).filter((c): c is (typeof all)[number] => Boolean(c));
      const rest = all.filter((c) => !recentRank.has(c.id));
      return [...rec, ...rest].slice(0, 60);
    }
    const scored: { c: (typeof all)[number]; s: number }[] = [];
    for (const c of all) {
      const text = `${c.label} ${c.category} ${c.group ?? ''} ${t(c.label)} ${t(c.category)} ${bindingText(c.id)}`.toLowerCase();
      const s = matchScore(text, words);
      if (s >= 0) scored.push({ c, s });
    }
    scored.sort((a, b) => a.s - b.s || (recentRank.get(a.c.id) ?? 999) - (recentRank.get(b.c.id) ?? 999));
    return scored.slice(0, 60).map((x) => x.c);
  }, [all, words, recent, recentRank]);
  const run = (id: string) => { noteRecent(id); close(); setTimeout(() => runCommand(id), 0); };
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
          {list.map((c, i) => {
            const tool = findTool(c.id);
            const Icon = tool?.icon;
            const isRecent = !words.length && recentRank.has(c.id);
            return (
              <li key={c.id} role="option" aria-selected={i === at} className={i === at ? 'on' : undefined} onMouseEnter={() => setAt(i)} onMouseDown={(e) => { e.preventDefault(); run(c.id); }}>
                <span className="palette-icon" aria-hidden>{Icon ? <Icon size={14} strokeWidth={1.6} /> : isRecent ? <Clock size={13} className="muted" /> : null}</span>
                <span>{c.label}</span>
                <span className="muted small"><span>{c.category}</span>{c.group && <> · <span>{c.group}</span></>}</span>
                <span className="grow" />
                {bindingText(c.id) && <kbd data-no-i18n>{bindingText(c.id)}</kbd>}
              </li>
            );
          })}
          {list.length === 0 && <li className="muted">No command by that name.</li>}
        </ul>
      </div>
    </div>
  );
}
