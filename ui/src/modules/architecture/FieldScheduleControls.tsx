/** A field schedule's own controls in its toolbar: Configure… (its editor), the template (a file, or kept on this computer), the field names. */

import { Settings2 } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { query } from '../../engine/session';
import { saveFile } from '../../engine/files';
import { keepTemplate } from '../../shell/fieldSchedule';

interface Exported { text: string; file: string; template: { name?: string } }

export function FieldScheduleControls({ id }: { id: string }) {
  const template = async (how: string) => {
    const r = await query<Exported>('schedule.exportTemplate', { id });
    if (!r) return;
    if (how === 'file') {
      const saved = await saveFile(new TextEncoder().encode(r.text), r.file, 'application/json');
      if (saved) notify(`Exported ${saved}.`);
    } else if (how === 'keep') {
      await keepTemplate(r.file.replace(/\.lapinschedule$/, ''), r.text);
      notify('Kept in this computer\'s library: any project can make a schedule from it.');
    }
  };
  return <>
    <button type="button" className="icon" title="What the schedule lists, how it groups, its columns" data-testid="schedule-configure" onClick={() => useUi.setState({ dialog: 'fieldSchedule' })}><Settings2 size={14} /><span className="small">Configure…</span></button>
    <select value="" title="Keep the schedule's setup as a template, for other projects" data-testid="schedule-template" onChange={(e) => { const v = e.target.value; if (v) void template(v); }}>
      <option value="">Template…</option>
      <option value="file">Save as template…</option>
      <option value="keep">Keep in this computer's library</option>
    </select>
    <button type="button" className="icon" title="The names your office gives to fields" data-testid="schedule-aliases" onClick={() => useUi.setState({ dialog: 'fieldAliases' })}><span className="small">Field names…</span></button>
  </>;
}
