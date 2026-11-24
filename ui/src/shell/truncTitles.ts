/**
 * Long labels and values in the properties panel are cut with an ellipsis; the full text is in the tooltip (forum topic 257).
 * One listener on the document: pointing at a label, a dropdown or a text field of the panel that shows less than it holds
 * puts what it holds in its `title` (a label that has an explanation keeps it, after the label).
 */

const MARK = 'truncTitle';

/** The text a cut element holds, or null when it all shows. */
export function fullTextOf(el: HTMLElement): string | null {
  if (el instanceof HTMLSelectElement) {
    const text = el.selectedOptions[0]?.text ?? '';
    return text && el.scrollWidth > el.clientWidth ? text : null;
  }
  if (el instanceof HTMLInputElement) return el.value && el.scrollWidth > el.clientWidth ? el.value : null;
  const text = el.textContent?.trim() ?? '';
  return text && el.scrollWidth > el.clientWidth ? text : null;
}

function onOver(e: MouseEvent): void {
  const t = e.target;
  if (!(t instanceof HTMLElement)) return;
  const el = t.closest<HTMLElement>('.props .fields dt, .props .fields dd, .props .fields select, .props .fields input[type=text], .props .fields input:not([type])');
  if (!el) return;
  // The title the element was given (an explanation) is kept apart from the one put here, so a value that changed is read again.
  if (el.dataset[MARK] === undefined) el.dataset[MARK] = el.getAttribute('title') ?? '';
  const base = el.dataset[MARK];
  const full = fullTextOf(el);
  const next = full && !base.includes(full) ? (base ? `${full}: ${base}` : full) : base;
  if (next) el.setAttribute('title', next); else el.removeAttribute('title');
}

let installed = false;

/** Listens once, for the whole app. */
export function installTruncationTitles(): void {
  if (installed || typeof document === 'undefined') return;
  installed = true;
  document.addEventListener('mouseover', onOver);
}
