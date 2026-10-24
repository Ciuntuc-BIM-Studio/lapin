/**
 * The interface's language (English or Romanian). The interface is written
 * in English; in Romanian a translator walks what it shows (texts, titles,
 * placeholders) and puts each phrase it knows into Romanian, keeping the
 * English to put back. Drawings (SVG) and what the user types are left as
 * they are. The phrase list is `ro.ts`; a phrase not in it stays English.
 */

import { create } from 'zustand';
import { RO, RO_PATTERNS } from './ro';

export type Lang = 'en' | 'ro';

function stored(): Lang {
  try { return localStorage.getItem('lapin.lang') === 'ro' ? 'ro' : 'en'; } catch { return 'en'; }
}

export const useLang = create<{ lang: Lang }>(() => ({ lang: stored() }));

export function setLang(lang: Lang): void {
  try { localStorage.setItem('lapin.lang', lang); } catch { /* the choice lasts this session */ }
  useLang.setState({ lang });
  retranslate();
}

/** A phrase in the language chosen (null: it stays as it is). */
export function translate(text: string): string | null {
  const core = text.trim();
  if (!core) return null;
  const exact = RO[core];
  if (exact != null) return text.replace(core, exact);
  for (const [re, f] of RO_PATTERNS) {
    const m = re.exec(core);
    if (m) return text.replace(core, f(m));
  }
  return null;
}

/** For code that builds a phrase itself (a notice): the phrase in the language chosen. */
export function t(text: string): string {
  return useLang.getState().lang === 'ro' ? translate(text) ?? text : text;
}

const ATTRS = ['title', 'placeholder', 'aria-label'];
const originals = new WeakMap<Node, string>();
const attrOriginals = new WeakMap<Element, Record<string, string>>();
let root: HTMLElement | null = null;
let observer: MutationObserver | null = null;
let busy = false;

const skip = (el: Element | null) => !!el?.closest('svg, textarea, [contenteditable], [data-no-i18n]');

function textNode(n: Text) {
  if (skip(n.parentElement)) return;
  const ro = useLang.getState().lang === 'ro';
  const now = n.nodeValue ?? '';
  const known = originals.get(n);
  // The English it shows now (React may have written new English since).
  const english = known != null && (now === known || now === translate(known)) ? known : now;
  originals.set(n, english);
  const want = ro ? translate(english) ?? english : english;
  if (want !== now) n.nodeValue = want;
}

function attributes(el: Element) {
  if (skip(el)) return;
  const ro = useLang.getState().lang === 'ro';
  const saved = attrOriginals.get(el) ?? {};
  for (const a of ATTRS) {
    const now = el.getAttribute(a);
    if (now == null) continue;
    const english = saved[a] != null && (now === saved[a] || now === translate(saved[a])) ? saved[a] : now;
    saved[a] = english;
    const want = ro ? translate(english) ?? english : english;
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
