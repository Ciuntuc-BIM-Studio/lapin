/**
 * Place on sheet… (forum topic 207): a view's right-click (in it, on its tab, in the project browser) places it on a sheet
 * chosen here, searched by number or name, or on a new one made with it (one undo step: sheet.create with the view). It goes
 * where a view placed with no point goes (the middle of the paper left of the title block), and the sheet opens (the view
 * stays a tab away), unless "Open the sheet after placing" is off. A view already on a sheet says which, and opens it.
 */

import { useState } from 'react';
import { create } from 'zustand';
import { FilePlus, FileText } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import { Dialog } from './Dialog';
import { matchingSheets, paperForNew, placedOn, sheetLabel } from './browserFind';

export const usePlaceOnSheet = create<{ viewId: string | null }>(() => ({ viewId: null }));

const OPEN_KEY = 'lapin.placeOnSheet.open';
const openAfter = (): boolean => { try { return localStorage.getItem(OPEN_KEY) !== '0'; } catch { return true; } };

/** Opens the picker for a view. */
export function openPlaceOnSheet(viewId: string): void {
  usePlaceOnSheet.setState({ viewId });
  useUi.setState({ dialog: 'placeOnSheet' });
}

const showSheet = (id: string) => useUi.setState({ view: 'sheet', sheetId: id, selection: id, selected: [id] });

export function PlaceOnSheetDialog() {
  const viewId = usePlaceOnSheet((s) => s.viewId);
  const nodes = useSession((s) => s.nodes);
  const [typed, setTyped] = useState('');
  const [open, setOpen] = useState(openAfter);
  const close = () => useUi.setState({ dialog: null });
  if (!viewId) return null;
  const name = viewId.startsWith('plan:') ? nodes.get(viewId.slice(5))?.name ?? 'Plan' : nodes.get(viewId)?.name ?? 'View';
  const already = placedOn(nodes).get(viewId) ?? [];
  const sheets = matchingSheets(nodes, typed);
  const done = (sheetId: string | null | undefined) => {
    if (!sheetId) return;
    close();
    const s = useSession.getState().nodes.get(sheetId);
    if (open) showSheet(sheetId);
    else if (s) notify(`Placed on sheet ${sheetLabel(s)}.`);
  };
  const onto = (sheetId: string) => void execute('sheet.addViewport', { id: sheetId, viewId }).then((r) => done(r ? sheetId : null));
  const onNew = () => {
    const discipline = typeof nodes.get(viewId)?.props.discipline === 'string' ? nodes.get(viewId)!.props.discipline : undefined;
    void execute('sheet.create', { name, ...paperForNew(nodes), ...(discipline ? { discipline } : {}), viewId }).then((r) => done((r as { id?: string } | null)?.id));
  };
  const keep = (on: boolean) => { setOpen(on); try { localStorage.setItem(OPEN_KEY, on ? '1' : '0'); } catch { /* a convenience only */ } };
  return (
    <Dialog title="Place on sheet" onClose={close} className="place-on-sheet" testId="place-on-sheet" dataDialog="placeOnSheet">
      <p className="small" data-no-i18n="">{name}</p>
      {already.length > 0 ? (
        <>
          <p className="small">{already.length === 1 ? `Already on sheet ${sheetLabel(already[0])}.` : `Already on sheets ${already.map(sheetLabel).join(', ')}.`}</p>
          <p className="muted small">A view goes on one sheet: take it off that sheet to place it on another.</p>
          <div className="place-sheet-list">
            {already.map((s) => (
              <button type="button" key={s.id} className="tree-item" data-testid="place-open-sheet" onClick={() => { close(); showSheet(s.id); }}>
                <FileText size={13} /> Open the sheet <span data-no-i18n="">{sheetLabel(s)}</span>
              </button>
            ))}
          </div>
        </>
      ) : (
        <>
          <input type="search" autoFocus value={typed} placeholder="Search the sheets (number or name)" data-testid="place-sheet-search"
            onChange={(e) => setTyped(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); if (sheets[0]) onto(sheets[0].id); else onNew(); } }} />
          <div className="place-sheet-list" role="listbox" aria-label="Sheets">
            <button type="button" className="tree-item" data-testid="place-new-sheet" title="New sheet with this view on it, named after it" onClick={onNew}><FilePlus size={13} /> New sheet…</button>
            {sheets.map((s) => (
              <button type="button" key={s.id} role="option" aria-selected={false} className="tree-item" data-no-i18n="" data-sheet-id={s.id} onClick={() => onto(s.id)}>
                <FileText size={13} /> {sheetLabel(s)}
              </button>
            ))}
            {sheets.length === 0 && typed.trim() && <p className="muted small pad">No sheet matches.</p>}
          </div>
          <label className="small check"><input type="checkbox" checked={open} onChange={(e) => keep(e.target.checked)} /> Open the sheet after placing</label>
        </>
      )}
    </Dialog>
  );
}
