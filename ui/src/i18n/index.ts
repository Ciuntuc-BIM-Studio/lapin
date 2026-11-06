/**
 * The interface's language (English, Romanian, Spanish, German, French,
 * Danish, Italian, Polish, Brazilian Portuguese, Lao or Simplified Chinese). The interface is
 * written in English; in another language a translator walks what it shows
 * (texts, titles, placeholders) and puts each phrase it knows into that
 * language, keeping the English to put back. Drawings (SVG) and what the user
 * types are left as they are. Each language is one file (`ro.ts`, `es.ts`,
 * `de.ts`, `fr.ts`, `da.ts`, `it.ts`, `pl.ts`, `pt.ts`, `lo.ts`, `zh.ts`):
 * its phrase list and its patterns; a phrase not in it stays English. A
 * language is loaded when it is chosen (`loadLanguage`), so the app carries
 * only the one it shows; main.tsx loads the stored one before the first draw.
 *
 * One English word with two meanings (Join: a shared session or the wall-join
 * tool) gets a context: an element marked `data-i18n-ctx="session"` looks up
 * `session|Join` before `Join`.
 */

import { create } from 'zustand';
import { rebrand } from '../platform/brand';

/** A language's phrases: the exact ones, and those with a number or a name in them. */
export interface Language {
  dict: Record<string, string>;
  patterns: [RegExp, (m: RegExpExecArray) => string][];
}

/** Each language's file, fetched the first time it is wanted. */
const LOADERS = {
  ro: () => import('./ro').then((m) => ({ dict: m.RO, patterns: m.RO_PATTERNS })),
  es: () => import('./es').then((m) => ({ dict: m.ES, patterns: m.ES_PATTERNS })),
  de: () => import('./de').then((m) => ({ dict: m.DE, patterns: m.DE_PATTERNS })),
  fr: () => import('./fr').then((m) => ({ dict: m.FR, patterns: m.FR_PATTERNS })),
  da: () => import('./da').then((m) => ({ dict: m.DA, patterns: m.DA_PATTERNS })),
  it: () => import('./it').then((m) => ({ dict: m.IT, patterns: m.IT_PATTERNS })),
  pl: () => import('./pl').then((m) => ({ dict: m.PL, patterns: m.PL_PATTERNS })),
  pt: () => import('./pt').then((m) => ({ dict: m.PT, patterns: m.PT_PATTERNS })),
  lo: () => import('./lo').then((m) => ({ dict: m.LO, patterns: m.LO_PATTERNS })),
  zh: () => import('./zh').then((m) => ({ dict: m.ZH, patterns: m.ZH_PATTERNS })),
} satisfies Record<string, () => Promise<Language>>;

export type Lang = 'en' | keyof typeof LOADERS;

/** The languages loaded so far. */
const TABLES: Partial<Record<Exclude<Lang, 'en'>, Language>> = {};

/** Loads a language's phrases (at once for English or one already here). */
export async function loadLanguage(lang: Lang): Promise<void> {
  if (lang === 'en' || TABLES[lang]) return;
  TABLES[lang] = await LOADERS[lang]();
}

/** Every language, for the tests that compare them. */
export async function loadAllLanguages(): Promise<void> {
  await Promise.all((Object.keys(LOADERS) as Lang[]).map(loadLanguage));
}

/** The languages offered, each named in itself. */
export const LANGUAGES: { id: Lang; name: string }[] = [
  { id: 'en', name: 'English' },
  { id: 'ro', name: 'Română' },
  { id: 'es', name: 'Español' },
  { id: 'de', name: 'Deutsch' },
  { id: 'fr', name: 'Français' },
  { id: 'da', name: 'Dansk' },
  { id: 'it', name: 'Italiano' },
  { id: 'pl', name: 'Polski' },
  { id: 'pt', name: 'Português' },
  { id: 'lo', name: 'ພາສາລາວ' },
  { id: 'zh', name: '简体中文' },
];

const isLang = (x: unknown): x is Lang => LANGUAGES.some((l) => l.id === x);

function stored(): Lang {
  try { const v = localStorage.getItem('lapin.lang'); return isLang(v) ? v : 'en'; } catch { return 'en'; }
}

export const useLang = create<{ lang: Lang }>(() => ({ lang: stored() }));

/** The stored language, loaded: main.tsx draws the interface once it is here (English if it cannot be fetched). */
export const languageReady: Promise<void> = loadLanguage(useLang.getState().lang).catch(() => { useLang.setState({ lang: 'en' }); });

/** The page says its language, so the browser hyphenates and reads it as such. */
const mark = (lang: Lang) => { if (typeof document !== 'undefined') document.documentElement.lang = lang; };

/** Chooses the interface's language; done once its file is loaded and the interface redrawn in it. */
export function setLang(lang: Lang): Promise<void> {
  try { localStorage.setItem('lapin.lang', lang); } catch { /* the choice lasts this session */ }
  return loadLanguage(lang).then(() => {
    useLang.setState({ lang });
    mark(lang);
    retranslate();
  });
}

/** A phrase in a language, the one chosen unless given (null: it stays as it is); `ctx` picks a meaning (`ctx|phrase`) first. */
export function translate(text: string, lang: Lang = useLang.getState().lang, ctx?: string | null): string | null {
  if (lang === 'en') return null;
  const core = text.trim();
  if (!core) return null;
  const table = TABLES[lang];
  if (!table) return null;
  const { dict, patterns } = table;
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
  return rebrand(translate(text, undefined, ctx) ?? text);
}

const ATTRS = ['title', 'placeholder', 'aria-label'];
const originals = new WeakMap<Node, string>();
const attrOriginals = new WeakMap<Element, Record<string, string>>();
let root: HTMLElement | null = null;
let observer: MutationObserver | null = null;
let busy = false;

/** Whether `now` is `english` or its translation in a language loaded (the one shown before a switch, too). */
const shows = (now: string, english: string, ctx?: string | null) => now === english || now === rebrand(english) || (Object.keys(TABLES) as Lang[]).some((l) => {
  const x = translate(english, l, ctx);
  return x != null && (now === x || now === rebrand(x));
});

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
  const want = rebrand(translate(english, undefined, ctx) ?? english);
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
    const want = rebrand(translate(english, undefined, ctx) ?? english);
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
