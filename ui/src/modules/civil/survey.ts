import { query } from '../../engine/session';
/** Survey files: picking one (xyz or PENZD points). */

/** A text file the user picks (a survey: xyz or PENZD), or null when cancelled. */
export function pickText(accept: string): Promise<{ name: string; text: string } | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.onchange = () => {
      const f = input.files?.[0];
      if (!f) { resolve(null); return; }
      void f.text().then((text) => resolve({ name: f.name.replace(/\.[^.]+$/, ''), text }));
    };
    input.click();
  });
}

/** What a survey file can be. */
export const SURVEY = '.csv,.txt,.xyz,.pts';

/** The LandXML of the project's terrains, saved by the browser. */
export async function downloadLandXml(ids?: string[]): Promise<void> {
  const r = await query<{ name: string; text: string }>('civil.landXml', ids ? { ids } : {});
  if (!r) return;
  const url = URL.createObjectURL(new Blob([r.text], { type: 'application/xml' }));
  const a = document.createElement('a');
  a.href = url; a.download = r.name; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
