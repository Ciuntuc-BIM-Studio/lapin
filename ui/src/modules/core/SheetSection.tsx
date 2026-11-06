/**
 * A sheet in the inspector: its discipline (the browser's set it sits in), its title block (the project's default or one of
 * its own, decision 340) and its title block fields (empty: the project's name, today's date).
 */

import { useEffect, useState } from 'react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { t as tr } from '../../i18n';
import { DisciplinePicker } from './DisciplinePicker';
import { listTitleBlocks, setSheetTitleBlock, type TbList } from './titleBlocks/titleBlocks';
import { titleBlocksDialog } from './titleBlocks/TitleBlocksDialog';

// The revision and the status are set by an issue (Manage → Issue…) and may be typed; the phase is the design stage the sheet belongs to.
const FIELDS: [string, string][] = [['project', 'Project'], ['client', 'Client'], ['drawnBy', 'Drawn by'], ['checkedBy', 'Checked by'], ['date', 'Date'], ['phase', 'Phase'], ['revision', 'Revision'], ['status', 'Status']];

export function SheetSection({ node }: { node: NodeDto }) {
  const tb = (node.props.titleBlock as Record<string, string> | undefined) ?? {};
  const version = useSession((s) => s.version);
  const [blocks, setBlocks] = useState<TbList | null>(null);
  useEffect(() => { void listTitleBlocks().then(setBlocks); }, [version]);
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
      {FIELDS.map(([key, label]) => (
        <span key={key} style={{ display: 'contents' }}>
          <dt>{label}</dt>
          <dd><input key={`${node.id}${key}${tb[key] ?? ''}`} defaultValue={tb[key] ?? ''} disabled={!node.editable}
            onBlur={(e) => e.target.value !== (tb[key] ?? '') && void execute('sheet.titleBlock', { id: node.id, fields: { [key]: e.target.value } })} /></dd>
        </span>
      ))}
    </dl>
  );
}
