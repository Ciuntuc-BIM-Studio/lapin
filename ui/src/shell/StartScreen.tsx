/** The start screen, Atrium's way: the actions on the left, the recent projects on the right. */

import { FilePlus2, FolderOpen, Link2 } from 'lucide-react';
import { useUi } from '../app/store';
import { modules, productInfo } from '../platform/modules';
import { newProject } from '../engine/session';

const RECENT = [
  { name: 'Casa P+1 Huși', file: 'casa-p1-husi.lapin', when: '23 Oct 2026 · 18:20', elements: '214 elements · 2 levels' },
  { name: 'Bloc P+4', file: 'bloc-p4.lapin', when: '21 Oct 2026 · 09:05', elements: '1,219 elements · 6 levels' },
  { name: 'Hală depozit', file: 'hala-depozit.lapin', when: '18 Oct 2026 · 16:40', elements: '87 elements · 1 level' },
];

export function StartScreen() {
  const open = () => useUi.setState({ screen: 'workspace', tab: null });
  return (
    <div className="start">
      <section className="start-left">
        <h1 className="start-brand">Lapin</h1>
        <p className="muted">BIM authoring for architects and engineers</p>
        <button type="button" className="btn primary big" onClick={() => { void newProject(); open(); }}><FilePlus2 size={18} />New project</button>
        <button type="button" className="btn big" onClick={open}><FolderOpen size={18} />Open project…</button>
        <button type="button" className="btn big" onClick={open}><Link2 size={18} />Link an IFC model…</button>
        <p className="eyebrow">Start here</p>
        <p className="muted small">A new project starts with a ground floor and its plan. Draw walls with <kbd>WA</kbd>, doors with <kbd>DR</kbd>; every element is native IFC.</p>
        <p className="eyebrow">This edition</p>
        <p className="muted small">{productInfo.name}: {modules.map((m) => m.name).join(' · ')}</p>
      </section>
      <section className="start-right">
        <p className="eyebrow">Recent</p>
        {RECENT.map((r) => (
          <button type="button" key={r.file} className="recent" onClick={open}>
            <span className="thumb" aria-hidden="true" />
            <span className="recent-body">
              <span className="recent-name">{r.name}</span>
              <span className="muted small">{r.file} · {r.elements}</span>
            </span>
            <span className="muted small">{r.when}</span>
          </button>
        ))}
      </section>
      <footer className="start-foot muted small">
        <span>Lapin · Ciuntuc BIM Studio</span>
        <span>Ctrl+N new · Ctrl+O open · drop a .lapin or .ifc here</span>
      </footer>
    </div>
  );
}
