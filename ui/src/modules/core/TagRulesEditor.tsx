/**
 * The tag rules on the Tags tab of the drafting standards (issue #72): per class, in order, a condition (a formula over the
 * element's fields: width > 1.2, FireRating != "") and the tag type it gives; the first of its class that holds wins over the
 * type a tag was made with, unless the tag's own type was chosen in its properties. Each change is one engine command.
 */

import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react';
import { execute } from '../../engine/session';
import { moveIndex, ruleClasses, rulesByClass, type TagRuleDto } from './tagRules';

type TypeEntry = { id: string; name: string; shape?: unknown; classes?: unknown };

const RULES_HELP = 'Per class, the first rule whose condition holds gives the tag type, over the one a tag was made with; a tag whose type is chosen in its properties keeps it. A condition reads the element as {= …} does: width > 1.2 (metres), FireRating != "", IsExternal.';

export function TagRulesEditor({ rules, types }: { rules: TagRuleDto[]; types: TypeEntry[] }) {
  const usable = types.filter((t) => t.shape !== 'buildup');
  const classes = ruleClasses(types, rules);
  const set = (entry: Partial<TagRuleDto> & { id?: string }) => void execute('standards.set', { kind: 'tagRule', entry });
  const add = () => {
    const type = usable.find((t) => t.shape === 'opening') ?? usable[0];
    if (type) set({ class: 'IfcDoor', when: 'width > 1.2', tagType: type.id });
  };
  const move = (id: string, by: -1 | 1) => {
    const index = moveIndex(rules, id, by);
    if (index != null) void execute('standards.move', { kind: 'tagRule', id, index });
  };
  return (
    <section className="tag-rules" data-tag-rules>
      <h4 className="panel-title">Tag rules</h4>
      <p className="muted small">{RULES_HELP}</p>
      {rules.length > 0 && (
        <table className="standards-table">
          <thead><tr><th>Class</th><th title="A formula over the element's fields; it holds when it is true (a number not 0, a text not empty)">Condition</th><th>Tag type</th><th /></tr></thead>
          <tbody>
            {rulesByClass(rules).flatMap(([, list]) => list).map((r) => (
              <tr key={r.id} data-tag-rule={r.id}>
                <td><select value={r.class} data-field="tag-rule-class" onChange={(e) => set({ id: r.id, class: e.target.value })}>
                  {classes.map((c) => <option key={c} value={c} data-no-i18n>{c}</option>)}
                </select></td>
                <td><input key={`${r.id}${r.when}`} defaultValue={r.when} data-field="tag-rule-when" placeholder="width > 1.2" style={{ minWidth: 220 }}
                  onBlur={(e) => { const t = e.target.value.trim(); if (t && t !== r.when) set({ id: r.id, when: t }); }} /></td>
                <td><select value={r.tagType} data-field="tag-rule-type" onChange={(e) => set({ id: r.id, tagType: e.target.value })}>
                  {!usable.some((t) => t.id === r.tagType) && <option value={r.tagType} data-no-i18n>{r.tagType}</option>}
                  {usable.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select></td>
                <td className="row-tools">
                  <button type="button" className="icon mini" title="Move up: tried before the rule above" disabled={moveIndex(rules, r.id, -1) == null} onClick={() => move(r.id, -1)}><ArrowUp size={12} /></button>
                  <button type="button" className="icon mini" title="Move down: tried after the rule below" disabled={moveIndex(rules, r.id, 1) == null} onClick={() => move(r.id, 1)}><ArrowDown size={12} /></button>
                  <button type="button" className="icon mini" title="Remove" onClick={() => void execute('standards.remove', { kind: 'tagRule', id: r.id })}><Trash2 size={12} /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="row">
        <button type="button" className="btn" data-field="tag-rule-new" disabled={usable.length === 0} onClick={add}><Plus size={14} />New rule</button>
      </div>
    </section>
  );
}
