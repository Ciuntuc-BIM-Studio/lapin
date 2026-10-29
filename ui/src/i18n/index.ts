/**
 * The interface's language (English, Romanian or Spanish). The interface is
 * written in English; in another language a translator walks what it shows
 * (texts, titles, placeholders) and puts each phrase it knows into that
 * language, keeping the English to put back. Drawings (SVG) and what the user
 * types are left as they are. Each language is one file (`ro.ts`, `es.ts`):
 * its phrase list and its patterns; a phrase not in it stays English.
 */

import { create } from 'zustand';
import { ES, ES_PATTERNS } from './es';
import { RO, RO_PATTERNS } from './ro';

/** A language's phrases: the exact ones, and those with a number or a name in them. */
export interface Language {
  dict: Record<string, string>;
  patterns: [RegExp, (m: RegExpExecArray) => string][];
}

const TABLES = {
  ro: { dict: RO, patterns: RO_PATTERNS },
  es: { dict: ES, patterns: ES_PATTERNS },
} satisfies Record<string, Language>;

export type Lang = 'en' | keyof typeof TABLES;

/** The languages offered, each named in itself. */
export const LANGUAGES: { id: Lang; name: string }[] = [
  { id: 'en', name: 'English' },
  { id: 'ro', name: 'Română' },
  { id: 'es', name: 'Español' },
];

const isLang = (x: unknown): x is Lang => LANGUAGES.some((l) => l.id === x);

function stored(): Lang {
  try { const v = localStorage.getItem('lapin.lang'); return isLang(v) ? v : 'en'; } catch { return 'en'; }
}

export const useLang = create<{ lang: Lang }>(() => ({ lang: stored() }));

export function setLang(lang: Lang): void {
  try { localStorage.setItem('lapin.lang', lang); } catch { /* the choice lasts this session */ }
  useLang.setState({ lang });
  retranslate();
}

/** A phrase in a language, the one chosen unless given (null: it stays as it is). */
export function translate(text: string, lang: Lang = useLang.getState().lang): string | null {
  if (lang === 'en') return null;
  const core = text.trim();
  if (!core) return null;
  const { dict, patterns } = TABLES[lang];
  const exact = dict[core];
  if (exact != null) return text.replace(core, exact);
  for (const [re, f] of patterns) {
    const m = re.exec(core);
    if (m) return text.replace(core, f(m));
  }
  return null;
}

/** For code that builds a phrase itself (a notice): the phrase in the language chosen. */
export function t(text: string): string {
  return translate(text) ?? text;
}

const ATTRS = ['title', 'placeholder', 'aria-label'];
const originals = new WeakMap<Node, string>();
const attrOriginals = new WeakMap<Element, Record<string, string>>();
let root: HTMLElement | null = null;
let observer: MutationObserver | null = null;
let busy = false;

/** Whether `now` is `english` or its translation in a language (the one shown before a switch, too). */
const shows = (now: string, english: string) => now === english || LANGUAGES.some((l) => now === translate(english, l.id));

const skip = (el: Element | null) => !!el?.closest('svg, textarea, [contenteditable], [data-no-i18n]');

function textNode(n: Text) {
  if (skip(n.parentElement)) return;
  const now = n.nodeValue ?? '';
  const known = originals.get(n);
  // The English it shows now (React may have written new English since).
  const english = known != null && shows(now, known) ? known : now;
  originals.set(n, english);
  const want = translate(english) ?? english;
  if (want !== now) n.nodeValue = want;
}

function attributes(el: Element) {
  if (skip(el)) return;
  const saved = attrOriginals.get(el) ?? {};
  for (const a of ATTRS) {
    const now = el.getAttribute(a);
    if (now == null) continue;
    const english = saved[a] != null && shows(now, saved[a]) ? saved[a] : now;
    saved[a] = english;
    const want = translate(english) ?? english;
    if (want !== now) el.setAttribute(a, want);
  }
  attrOriginals.set(el, saved);
}

function walk(node: Node) {
  if (node.nodeType === Node.TEXT_NODE) { textNode(node as Text); return; }
  if (node.nodeType !== Node.ELEMENT_NODE) return;
  const el = node as Element;
  if (el.tagName === 'svg' || el.tagName === 'TEXTAREA') return;
  attributes(el);
  for (const c of Array.from(el.childNodes)) walk(c);
}

function retranslate() {
  if (!root) return;
  busy = true;
  try { walk(root); } finally { busy = false; }
}

/** Keeps everything under `el` in the language chosen, as it changes. */
export function startTranslator(el: HTMLElement): void {
  root = el;
  observer?.disconnect();
  observer = new MutationObserver((records) => {
    if (busy) return;
    busy = true;
    try {
      for (const r of records) {
        if (r.type === 'characterData') textNode(r.target as Text);
        else if (r.type === 'attributes') attributes(r.target as Element);
        else r.addedNodes.forEach(walk);
      }
    } finally { busy = false; }
  });
  observer.observe(el, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ATTRS });
  retranslate();
}
