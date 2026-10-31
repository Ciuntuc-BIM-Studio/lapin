/**
 * View → Sections… (docs/sections.md, decision 223): which sections are
 * marked in the view in front, grouped by their marker layer. A checkbox per
 * layer and per section hides or shows the marker in this view alone; each
 * section's layer is chosen here (an existing one, or a new one); a count
 * says in how many views each is hidden.
 */

import { Fragment, useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { groupByLayer, hiddenInText, type SectionMarker, type SectionMarkers } from './sectionMarkers';

const DEFAULT_LAYER = 'Sections';
const NEW_LAYER = '\u0000new';

export function SectionsDialog() {
  const nodes = useSession((s) => s.nodes);
  const [target, setTarget] = useState<string | null>(null);
  const [d, setD] = useState<SectionMarkers | null>(null);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    void frontView().then((id) => {
      const kind = id ? nodes.get(id)?.props.kind : null;
      if (kind !== 'plan' && kind !== 'section' && kind !== 'elevation') { notify('Open a plan, a section or an elevation first.'); close(); } else setTarget(id);
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  useEffect(() => { if (target) void query<SectionMarkers>('view.sectionMarkers', { viewId: target }).then(setD); }, [target, nodes]);
  if (!target || !d) return null;
  const view = nodes.get(target);
  const groups = groupByLayer(d);

  const setLayer = (s: SectionMarker, value: string) => {
    let layer: string | null = value;
    if (value === NEW_LAYER) {
      const name = window.prompt("New layer's name");
      if (!name?.trim()) return;
      layer = name.trim();
    }
    void execute('element.update', { id: s.id, attrs: { markerLayer: layer === DEFAULT_LAYER ? null : layer } });
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Section markers" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Section markers · <span data-no-i18n>{view?.name ?? 'View'}</span></span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">The markers this view shows, by layer. A section sits on a marker layer; this view hides a layer, or a section alone.</p>
          {d.sections.length === 0 ? <p className="muted">No sections yet: draw one in a plan with View → Section.</p> : (
            <table className="layer-table">
              <thead><tr><th>Shown in this view</th><th>Section</th><th>Layer</th><th /></tr></thead>
              <tbody>
                {groups.map(({ layer, sections }) => (
                  <Fragment key={layer.name}>
                    <tr className="group-row">
                      <td><input type="checkbox" checked={layer.shown} onChange={(e) => void execute('view.showSectionLayer', { viewId: target, layer: layer.name, shown: e.target.checked })} /></td>
                      <td colSpan={3}><strong data-no-i18n>{layer.name}</strong> <span className="muted small" data-no-i18n>· {sections.length}</span></td>
                    </tr>
                    {sections.map((s) => (
                      <tr key={s.id}>
                        <td><input type="checkbox" checked={s.shown} disabled={!layer.shown} title={layer.shown ? undefined : 'Its layer is hidden in this view'}
                          onChange={(e) => void execute('view.showSection', { viewId: target, sectionId: s.id, shown: e.target.checked })} /></td>
                        <td className="indent" data-no-i18n>{s.name}</td>
                        <td>
                          <select value={s.layer} onChange={(e) => setLayer(s, e.target.value)}>
                            {d.layers.map((l) => <option key={l.name} value={l.name}>{l.name}</option>)}
                            <option value={NEW_LAYER}>New layer…</option>
                          </select>
                        </td>
                        <td className="muted small">{hiddenInText(s.hiddenIn)}</td>
                      </tr>
                    ))}
                  </Fragment>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
