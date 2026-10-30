/**
 * The interface's language (English, Romanian, Spanish, German, French or
 * Danish). The interface is
 * written in English; in another language a translator walks what it shows
 * (texts, titles, placeholders) and puts each phrase it knows into that
 * language, keeping the English to put back. Drawings (SVG) and what the user
 * types are left as they are. Each language is one file (`ro.ts`, `es.ts`,
 * `de.ts`, `fr.ts`, `da.ts`):
 * its phrase list and its patterns; a phrase not in it stays English.
 *
 * One English word with two meanings (Join: a shared session or the wall-join
 * tool) gets a context: an element marked `data-i18n-ctx="session"` looks up
 * `session|Join` before `Join`.
 */

import { create } from 'zustand';
import { DA, DA_PATTERNS } from './da';
import { DE, DE_PATTERNS } from './de';
import { ES, ES_PATTERNS } from './es';
import { FR, FR_PATTERNS } from './fr';
import { RO, RO_PATTERNS } from './ro';

/** A language's phrases: the exact ones, and those with a number or a name in them. */
export interface Language {
  dict: Record<string, string>;
  patterns: [RegExp, (m: RegExpExecArray) => string][];
}

const TABLES = {
  ro: { dict: RO, patterns: RO_PATTERNS },
  es: { dict: ES, patterns: ES_PATTERNS },
  de: { dict: DE, patterns: DE_PATTERNS },
  fr: { dict: FR, patterns: FR_PATTERNS },
  da: { dict: DA, patterns: DA_PATTERNS },
} satisfies Record<string, Language>;

export type Lang = 'en' | keyof typeof TABLES;

/** The languages offered, each named in itself. */
export const LANGUAGES: { id: Lang; name: string }[] = [
  { id: 'en', name: 'English' },
  { id: 'ro', name: 'Română' },
  { id: 'es', name: 'Español' },
  { id: 'de', name: 'Deutsch' },
  { id: 'fr', name: 'Français' },
  { id: 'da', name: 'Dansk' },
];

const isLang = (x: unknown): x is Lang => LANGUAGES.some((l) => l.id === x);

function stored(): Lang {
  try { const v = localStorage.getItem('lapin.lang'); return isLang(v) ? v : 'en'; } catch { return 'en'; }
}

export const useLang = create<{ lang: Lang }>(() => ({ lang: stored() }));

/** The page says its language, so the browser hyphenates and reads it as such. */
const mark = (lang: Lang) => { if (typeof document !== 'undefined') document.documentElement.lang = lang; };

export function setLang(lang: Lang): void {
  try { localStorage.setItem('lapin.lang', lang); } catch { /* the choice lasts this session */ }
  useLang.setState({ lang });
  mark(lang);
  retranslate();
}

/** A phrase in a language, the one chosen unless given (null: it stays as it is); `ctx` picks a meaning (`ctx|phrase`) first. */
export function translate(text: string, lang: Lang = useLang.getState().lang, ctx?: string | null): string | null {
  if (lang === 'en') return null;
  const core = text.trim();
  if (!core) return null;
  const { dict, patterns } = TABLES[lang];
  const exact = (ctx ? dict[`${ctx}|${core}`] : undefined) ?? dict[core];
  if (exact != null) return text.replace(core, exact);
  for (const [re, f] of patterns) {
    const m = re.exec(core);
    if (m) return text.replace(core, f(m));
  }
  return null;
}

/** For code that builds a phrase itself (a notice): the phrase in the language chosen. */
export function t(text: string, ctx?: string): string {
  return translate(text, undefined, ctx) ?? text;
}

const ATTRS = ['title', 'placeholder', 'aria-label'];
const originals = new WeakMap<Node, string>();
const attrOriginals = new WeakMap<Element, Record<string, string>>();
let root: HTMLElement | null = null;
let observer: MutationObserver | null = null;
let busy = false;

/** Whether `now` is `english` or its translation in a language (the one shown before a switch, too). */
const shows = (now: string, english: string, ctx?: string | null) => now === english || LANGUAGES.some((l) => now === translate(english, l.id, ctx));

/** The meaning an element's phrases take (`data-i18n-ctx` on it or around it). */
const ctxOf = (el: Element | null) => el?.closest('[data-i18n-ctx]')?.getAttribute('data-i18n-ctx') ?? null;

const skip = (el: Element | null) => !!el?.closest('svg, textarea, [contenteditable], [data-no-i18n]');

function textNode(n: Text) {
  if (skip(n.parentElement)) return;
  const now = n.nodeValue ?? '';
  const known = originals.get(n);
  const ctx = ctxOf(n.parentElement);
  // The English it shows now (React may have written new English since).
  const english = known != null && shows(now, known, ctx) ? known : now;
  originals.set(n, english);
  const want = translate(english, undefined, ctx) ?? english;
  if (want !== now) n.nodeValue = want;
}

function attributes(el: Element) {
  if (skip(el)) return;
  const saved = attrOriginals.get(el) ?? {};
  const ctx = ctxOf(el);
  for (const a of ATTRS) {
    const now = el.getAttribute(a);
    if (now == null) continue;
    const english = saved[a] != null && shows(now, saved[a], ctx) ? saved[a] : now;
    saved[a] = english;
    const want = translate(english, undefined, ctx) ?? english;
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
  mark(useLang.getState().lang);
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
