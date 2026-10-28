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

/** A binary file the user picks, as base64 (a DXF: its code page is read by the engine), or null when cancelled. */
export function pickBinary(accept: string): Promise<{ name: string; data: string } | null> {
  return pickFiles(accept, false).then(async (files) => {
    const f = files[0]?.file;
    if (!f) return null;
    const bytes = new Uint8Array(await f.arrayBuffer());
    let s = '';
    for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
    return { name: f.name.replace(/\.[^.]+$/, ''), data: btoa(s) };
  });
}

/** Files the user picks (several when `multiple`: an image and its world file). */
export function pickFiles(accept: string, multiple = true): Promise<{ file: File }[]> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.multiple = multiple;
    input.onchange = () => resolve([...(input.files ?? [])].map((file) => ({ file })));
    input.click();
  });
}
