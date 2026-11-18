/**
 * Material standards from the store (docs/plan-material-graphics.md, stage 3; docs/plan-extensions.md): the catalogue's
 * `standards` extensions, filtered by country and norm, each previewed on the reference details before it is installed (the
 * pack is downloaded and checked, nothing is kept), installed on this computer like any pack, then used in the project (the
 * import, then the apply with its preview). A pack whose store version is newer shows "Update available": the update is the
 * user's to take, and the apply preview shows what it would change; nothing is ever updated by itself.
 */

import { useEffect, useMemo, useState } from 'react';
import { Download, RefreshCw } from 'lucide-react';
import { notify } from '../../app/store';
import { useSession } from '../../engine/session';
import { computerFiles } from '../../platform/libraries';
import { fetchPackage, install, loadCatalogue, uninstall, useExtensions } from '../../platform/extensions/runtime';
import { readStandards } from '../../platform/extensions/packs';
import type { Manifest } from '../../platform/extensions/manifest';
import { bringStandards } from './libraryKinds';
import { MaterialStandardSheet } from './MaterialStandardSheet';
import { countryOf, filterChoices, filterPacks, normOf, packStandards, packUpdates, type MaterialStandard } from './materialStandardsModel';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function MaterialStandardStore({ onUse }: { onUse: (standard: MaterialStandard) => void }) {
  const { list, loading, error, installed, busy } = useExtensions();
  const connected = useSession((s) => s.connected);
  const [search, setSearch] = useState('');
  const [country, setCountry] = useState('');
  const [norm, setNorm] = useState('');
  const [preview, setPreview] = useState<{ id: string; standard: MaterialStandard } | null>(null);
  const [working, setWorking] = useState<string | null>(null);
  useEffect(() => { if (connected && list.length === 0 && !loading) void loadCatalogue(); }, [connected]); // eslint-disable-line react-hooks/exhaustive-deps
  const choices = useMemo(() => filterChoices(list), [list]);
  const shown = useMemo(() => filterPacks(list, { search, country: country || null, norm: norm || null }), [list, search, country, norm]);
  const updates = useMemo(() => packUpdates(installed, list), [installed, list]);
  const had = (m: Manifest) => installed.find((x) => x.id === m.id);

  const act = async (m: Manifest, f: () => Promise<void>) => {
    setWorking(m.id);
    try { await f(); } catch (e) { notify(message(e)); } finally { setWorking(null); }
  };
  const show = (m: Manifest) => act(m, async () => {
    const text = new TextDecoder().decode((await fetchPackage(m)));
    readStandards(new TextEncoder().encode(text));
    const standard = packStandards(text)[0];
    if (!standard) throw new Error('The pack holds no material standard.');
    setPreview({ id: m.id, standard });
  });
  const use = (m: Manifest) => act(m, async () => {
    const file = (await computerFiles('lapinstandards')).find((f) => f.extension === m.id || f.name === m.name);
    if (!file) throw new Error('Install the pack first.');
    const standard = packStandards(file.text)[0];
    if (!standard) throw new Error('The pack holds no material standard.');
    const said = await bringStandards(file.text, `${m.name}.lapinstandards`);
    if (said !== null) onUse(standard);
  });

  return (
    <div className="standard-store" data-testid="standard-store">
      <div className="row">
        <input type="search" data-field="standard-store-search" placeholder="Search the store" value={search} onChange={(e) => setSearch(e.target.value)} />
        <select data-field="standard-store-country" aria-label="Country" value={country} onChange={(e) => setCountry(e.target.value)}>
          <option value="">Every country</option>{choices.countries.map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
        </select>
        <select data-field="standard-store-norm" aria-label="Norm" value={norm} onChange={(e) => setNorm(e.target.value)}>
          <option value="">Every norm</option>{choices.norms.map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
        </select>
        <button type="button" className="icon" title="Read the catalogue again" aria-label="Read the catalogue again" onClick={() => void loadCatalogue()}><RefreshCw size={14} /></button>
      </div>
      {loading && <p className="muted small">Reading the catalogue…</p>}
      {error && <p className="error-text small" role="alert">The catalogue could not be read: {error}</p>}
      {!loading && !error && shown.length === 0 && <p className="muted small">{list.length === 0 ? 'The store lists no material standard yet.' : 'Nothing matches.'}</p>}
      <div className="standard-store-split">
        <ul className="standard-store-list">
          {shown.map((m) => {
            const have = had(m);
            const up = updates.find((u) => u.manifest.id === m.id);
            const isBusy = working === m.id || busy === m.id;
            return (
              <li key={m.id} className="standard-store-row" data-extension={m.id} data-testid="store-standard">
                <span className="catalog-text">
                  <span className="catalog-name" data-no-i18n>{m.name.replace(/^Material standard:\s*/, '')}</span>
                  <span className="muted small" data-no-i18n>{[countryOf(m), normOf(m), `v${m.version}`, m.author.name, m.license].filter(Boolean).join(' · ')}</span>
                  <span className="muted small" data-no-i18n>{m.summary}</span>
                  {m.indicative && <span className="badge" title="Drawn from general principles: check it against the norm">Indicative</span>}
                  {up && <span className="badge warn" data-testid="store-update"><span>Update available</span><span data-no-i18n>{`: v${up.installed.version} → v${m.version}`}</span></span>}
                </span>
                <span className="row">
                  <button type="button" className="btn compact" data-action="store-preview" disabled={isBusy} onClick={() => void show(m)}>Preview</button>
                  {!have && <button type="button" className="btn compact primary" data-action="store-install" disabled={isBusy} onClick={() => void act(m, async () => { await install(m); notify(`Installed „${m.name}”.`); })}><Download size={12} />Install</button>}
                  {up && <button type="button" className="btn compact" data-action="store-update" disabled={isBusy} title="Take the newer version; the project is not changed until you use it" onClick={() => void act(m, async () => { await install(m); notify(`Updated „${m.name}” to v${m.version}.`); })}>Update</button>}
                  {have && <button type="button" className="btn compact primary" data-action="store-use" disabled={isBusy} title="Bring it into the project and apply it, with a preview" onClick={() => void use(m)}>Use in project…</button>}
                  {have && <button type="button" className="btn compact" data-action="store-uninstall" disabled={isBusy} onClick={() => void act(m, () => uninstall(m.id))}>Uninstall</button>}
                </span>
              </li>
            );
          })}
        </ul>
        {preview && (
          <div className="standard-store-preview">
            <div className="row"><span className="panel-title" data-no-i18n>{preview.standard.name}</span><span className="grow" /><button type="button" className="btn compact" onClick={() => setPreview(null)}>Close</button></div>
            <MaterialStandardSheet target={{ standard: preview.standard }} />
          </div>
        )}
      </div>
    </div>
  );
}
