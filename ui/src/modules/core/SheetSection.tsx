/**
 * A sheet in the inspector: its discipline (the browser's set it sits in), its title block (the project's default or one of
 * its own, decision 340) and its title block fields: the usual ones, then every other {sheet.*} field its title block uses
 * (forum 90), each empty one showing greyed what prints instead (the project information's, the project's name, today's date).
 */

import { useEffect, useState } from 'react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { t as tr } from '../../i18n';
import { DisciplinePicker } from './DisciplinePicker';
import { listTitleBlocks, setSheetTitleBlock, type TbList } from './titleBlocks/titleBlocks';
import { titleBlocksDialog } from './titleBlocks/TitleBlocksDialog';
import { VIEW_LEVEL_MODES, viewDatumPatch } from '../../shell/levelDatum';

/** A field the sheet types for its title block: its own value, and what prints when it is left empty. */
export interface SheetTbField { key: string; label: string; value?: string | null; otherwise?: string | null }

// The revision and the status are set by an issue (Manage → Issue…) and may be typed; the phase is the design stage the sheet belongs to.
// Shown until the engine says which fields the sheet's title block uses.
const FIELDS: SheetTbField[] = [['project', 'Project'], ['client', 'Client'], ['drawnBy', 'Drawn by'], ['checkedBy', 'Checked by'], ['date', 'Date'], ['phase', 'Phase'], ['revision', 'Revision'], ['status', 'Status']]
  .map(([key, label]) => ({ key, label }));

export function SheetSection({ node }: { node: NodeDto }) {
  const tb = (node.props.titleBlock as Record<string, string> | undefined) ?? {};
  const version = useSession((s) => s.version);
  const [blocks, setBlocks] = useState<TbList | null>(null);
  useEffect(() => { void listTitleBlocks().then(setBlocks); }, [version]);
  const [fields, setFields] = useState<SheetTbField[] | null>(null);
  useEffect(() => {
    let live = true;
    void query<{ fields: SheetTbField[] }>('sheet.titleBlockFields', { id: node.id }).then((r) => { if (live && r) setFields(r.fields); });
    return () => { live = false; };
  }, [node.id, version]);
  const own = typeof node.props.titleBlockId === 'string' ? node.props.titleBlockId : '';
  const def = blocks?.items.find((b) => b.id === blocks.default);
  return (
    <dl className="fields">
      <dt title="The browser files the sheet under this discipline's set">Discipline</dt>
      <dd><DisciplinePicker node={node} /></dd>
      <dt title="The sheet's own title block, or the project's default (Manage → Title blocks…)">Title block</dt>
      <dd className="row">
        <select value={own} disabled={!node.editable} data-field="sheet-title-block" onChange={(e) => void setSheetTitleBlock(node.id, e.target.value || null)}>
          <option value="">{`${tr('Project default')} (${def?.name ?? 'Lapin standard'})`}</option>
          {(blocks?.items ?? []).map((b) => <option key={b.id} value={b.id} data-no-i18n>{b.name}</option>)}
        </select>
        <button type="button" className="btn mini" title="Edit the title blocks: Manage → Title blocks…" data-field="sheet-title-blocks"
          onClick={() => { titleBlocksDialog.open = own || blocks?.default || null; useUi.setState({ dialog: 'titleBlocks' }); }}>…</button>
      </dd>
      <dt title="How every view placed on the sheet writes its levels: each view keeps it, and may change it again">Levels in its views</dt>
      <dd>
        <select value="" disabled={!node.editable} data-field="sheet-level-datum" onChange={(e) => { if (e.target.value) void execute('view.levelDatum', viewDatumPatch({ sheet: node.id }, e.target.value === 'project' ? '' : e.target.value)); }}>
          <option value="">Set every view…</option>
          <option value="project">as the project's</option>
          {VIEW_LEVEL_MODES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        </select>
      </dd>
      {(fields ?? FIELDS).map(({ key, label, otherwise }) => (
        <span key={key} style={{ display: 'contents' }}>
          {/* A field of the title block's own is named by its key, as typed in the designer. */}
          <dt title={`{sheet.${key}}`} {...(label === key ? { 'data-no-i18n': true } : {})}>{label}</dt>
          <dd><input key={`${node.id}${key}${tb[key] ?? ''}`} defaultValue={tb[key] ?? ''} disabled={!node.editable} data-field={`sheet-tb-${key}`}
            placeholder={otherwise ?? ''} data-no-i18n
            onBlur={(e) => e.target.value !== (tb[key] ?? '') && void execute('sheet.titleBlock', { id: node.id, fields: { [key]: e.target.value } })} /></dd>
        </span>
      ))}
    </dl>
  );
}
