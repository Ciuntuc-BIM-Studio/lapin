/**
 * Publish to store… (docs/plan-material-graphics.md, stage 3): from a saved custom standard, the manifest (id, name, version,
 * country tag, norm, licence, author, indicative, description), the .lapinstandards file and a screenshot of the reference
 * details (the sheet's SVG drawn to a PNG), saved as a zip the author's page takes; then the store's author page opens with the
 * three steps. Nothing is uploaded from here: the author sends it, the catalogue reviews it like any extension.
 */

import { useState } from 'react';
import { ExternalLink, FileArchive } from 'lucide-react';
import { notify } from '../../app/store';
import { engine, engineInBrowser } from '../../engine/session';
import { safeFileName, saveFile } from '../../engine/files';
import { ask } from './LibraryChoices';
import { AUTHOR_PAGE, PUBLISH_STEPS, buildManifest, packId, publishFiles, zipStore, type MaterialStandard } from './materialStandardsModel';

/** An SVG drawn to PNG bytes in the page's canvas (null where the page cannot: a test, a blocked canvas). */
export async function svgToPng(svg: string, width = 1200): Promise<Uint8Array | null> {
  try {
    const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
    const img = new Image();
    await new Promise<void>((ok, bad) => { img.onload = () => ok(); img.onerror = () => bad(new Error('The sheet could not be drawn.')); img.src = url; });
    const k = width / Math.max(1, img.naturalWidth || width);
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = Math.max(1, Math.round((img.naturalHeight || width * 0.6) * k));
    const c = canvas.getContext('2d');
    if (!c) return null;
    c.fillStyle = '#ffffff'; c.fillRect(0, 0, canvas.width, canvas.height);
    c.drawImage(img, 0, 0, canvas.width, canvas.height);
    URL.revokeObjectURL(url);
    const blob = await new Promise<Blob | null>((ok) => canvas.toBlob(ok, 'image/png'));
    return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
  } catch { return null; }
}

/** The author's page, in the system's browser. */
export function openAuthorPage(): void {
  if (engineInBrowser) { window.open(AUTHOR_PAGE, '_blank', 'noopener'); return; }
  void engine.call('support.open', { link: 'extensionPage', url: AUTHOR_PAGE }).catch(() => notify('The link could not be opened.'));
}

export function MaterialStandardPublish({ id, standard, onClose }: { id: string; standard: MaterialStandard; onClose: () => void }) {
  const [account, setAccount] = useState('');
  const [version, setVersion] = useState(standard.version && /^\d+\.\d+\.\d+$/.test(standard.version) ? standard.version : '1.0.0');
  const [summary, setSummary] = useState((standard.description ?? '').replace(/\s+/g, ' ').slice(0, 200));
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState<string | null>(null);
  const bad = !/^\d+\.\d+\.\d+$/.test(version);
  const make = async () => {
    setBusy(true);
    try {
      const pack = await ask<{ text: string; file: string }>('standards.export', { name: standard.name, materialStandards: [id], only: 'materialGraphics' });
      if (!pack) return;
      const sheet = await ask<{ svg?: string | string[] }>('standards.previewSheet', { id, scale: 50, drawings: false, svg: true });
      const svg = Array.isArray(sheet?.svg) ? sheet.svg[0] : sheet?.svg;
      const png = svg ? await svgToPng(svg) : null;
      const bytes = new TextEncoder().encode(pack.text);
      const fileName = `${safeFileName(packId(standard, account).replace(/^.*\./, ''), 'standard')}.lapinstandards`;
      const manifest = await buildManifest({ ...standard, version }, { account: account.trim() || undefined, version, summary: summary || undefined, fileName, bytes });
      const zip = zipStore(publishFiles(manifest, bytes, fileName, png));
      const name = await saveFile(zip, `${safeFileName(String(manifest.id), 'standard')}.zip`, 'application/zip');
      if (name) { setSaved(name); notify(`Saved ${name}.`); }
    } finally { setBusy(false); }
  };
  return (
    <section className="standard-publish" data-testid="standard-publish">
      <div className="row"><span className="eyebrow">Publish to the store</span><span className="grow" /><button type="button" className="btn compact" onClick={onClose}>Close</button></div>
      <p className="small muted">Lapin writes the manifest, the standard’s file and a screenshot into a zip; you send it on the store’s author page. Nothing is uploaded from here.</p>
      <label className="field-line"><span className="small">GitHub account</span><input data-field="publish-account" value={account} placeholder="optional" onChange={(e) => setAccount(e.target.value)} /></label>
      <label className="field-line"><span className="small">Version</span><input data-field="publish-version" value={version} aria-invalid={bad} onChange={(e) => setVersion(e.target.value)} /></label>
      <label className="field-line"><span className="small">Summary</span><input data-field="publish-summary" value={summary} onChange={(e) => setSummary(e.target.value)} /></label>
      <div className="row">
        <button type="button" className="btn primary" data-action="publish-save" disabled={busy || bad} onClick={() => void make()}><FileArchive size={13} />Save the files…</button>
        <button type="button" className="btn" data-action="publish-open" onClick={openAuthorPage}><ExternalLink size={13} />Open the author page</button>
      </div>
      {saved && <p className="small" role="status" data-testid="publish-saved">{`Saved ${saved}.`}</p>}
      <ol className="small">{PUBLISH_STEPS.map((s) => <li key={s}>{s}</li>)}</ol>
    </section>
  );
}
