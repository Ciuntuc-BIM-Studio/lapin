/**
 * A task's elements in Properties (docs/plan-4d5d.md, stage 2): how many it takes, the rule they are found by — the kinds,
 * storeys, types, materials, groups and phases, picked as a schedule's filter is, each with how many elements have it —
 * the part of them it takes (the whole, the core, the finishes, a material, a category), and the exceptions added and
 * taken out by hand. The count follows every change; each change is one step to undo. And on an element, the tasks it is in.
 */

import { useEffect, useState } from 'react';
import { ListChecks, MousePointer2, Wand2, X } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { select, useUi } from '../../app/store';
import { showDate, type TaskDto, type TaskRule } from './gantt';
import { tasksOf, toggleRule, type RuleKey } from './links';
import { assignTo, selectedElements, showTaskElements, taskOf, usePlanning, useSchedule } from './schedule';

interface Option { id: string; name: string; count: number; class?: string; category?: string | null; kind?: string }
type Options = Record<RuleKey, Option[]> & { categories: string[] };

/** The rule's options, asked again when the model changes. */
function useRuleOptions(): Options | null {
  const version = useSession((s) => s.version);
  const [o, setO] = useState<Options | null>(null);
  useEffect(() => { void query<Options>('planning.ruleOptions').then((x) => x && setO(x)); }, [version]);
  return o;
}

const HEADINGS: Record<RuleKey, string> = { classes: 'Kinds', storeys: 'Storeys', types: 'Types', materials: 'Materials', groups: 'Groups', phases: 'Phases', demolished: 'Demolished in' };
const ORDER: RuleKey[] = ['classes', 'storeys', 'types', 'materials', 'groups', 'phases', 'demolished'];

const update = (id: string, args: Record<string, unknown>) => void execute('planning.updateTask', { id, ...args });

export function TaskLinksSection({ node }: { node: NodeDto }) {
  const data = useSchedule();
  const options = useRuleOptions();
  const t = taskOf(data, node.id);
  if (!data || !t) return null;
  const rule = t.rule;
  const setRule = (r: TaskRule | null) => update(t.id, { rule: r });
  const nameOf = (id: string) => [...(options?.materials ?? [])].find((m) => m.id === id)?.name;
  return (
    // In the context 'task': its Part is a part of the elements, its Kinds the elements' classes.
    <dl className="fields task-links" data-testid="task-links" data-i18n-ctx="task">
      <dt>Elements</dt>
      <dd>
        <span data-testid="task-count">{t.summary ? `${t.count} (with the tasks under it)` : `${t.count}`}</span>
        {t.count > 0 && <button type="button" className="link" onClick={() => showTaskElements(t)} title="Select them in the view and frame them"><MousePointer2 size={12} />Select</button>}
      </dd>
      <dt title="New elements that fit the rule join the task by themselves">Rule</dt>
      <dd>
        <label className="check"><input type="checkbox" checked={!!rule} onChange={(e) => setRule(e.target.checked ? {} : null)} data-testid="task-rule-on" />Find them by a rule</label>
      </dd>
      {rule && <dd className="rule-wide">
        {!Object.keys(rule).length && <span className="muted small">every element, until you pick what it takes</span>}
        {options && <RuleEditor task={t} rule={rule} options={options} onChange={setRule} />}
      </dd>}
      <dt title="A layered wall can be built in one task and plastered in another">Part</dt>
      <dd>
        <select value={t.part ?? ''} onChange={(e) => update(t.id, { part: e.target.value || null })} data-testid="task-part">
          <option value="">Whole elements</option>
          <option value="core">Core (structure)</option>
          <option value="finishes">Finishes (layers but the core)</option>
          <option disabled>Material categories</option>
          {(options?.categories ?? []).map((c) => <option key={c} value={`category:${c}`}>{c}</option>)}
          {!!options?.materials.length && <option disabled>Materials</option>}
          {(options?.materials ?? []).map((m) => <option key={m.id} value={`material:${m.id}`} data-no-i18n>{m.name}</option>)}
          {t.part?.startsWith('material:') && !nameOf(t.part.slice(9)) && <option value={t.part}>{t.part}</option>}
        </select>
      </dd>
      <dt title="Elements put in or taken out by hand, the rule left as it is">Exceptions</dt>
      <dd>
        {!t.added && !t.removed ? <span className="muted small">none: drag the selection onto the task's row</span> : <>
          {!!t.added && <span className="task-exception">{`+${t.added} added`}<button type="button" className="icon mini" title="Forget the elements added by hand" onClick={() => update(t.id, { add: [] })}><X size={11} /></button></span>}
          {!!t.removed && <span className="task-exception">{`−${t.removed} taken out`}<button type="button" className="icon mini" title="Give the rule back the elements taken out" onClick={() => update(t.id, { remove: [] })}><X size={11} /></button></span>}
        </>}
      </dd>
      <dd className="task-link-buttons rule-wide">
        <button type="button" className="link" onClick={() => void assignTo(t.id, selectedElements())} title="The elements selected in the views, put in this task"><ListChecks size={12} />Add the selection</button>
        <button type="button" className="link" onClick={() => void assignTo(t.id, selectedElements(), true)} title="The elements selected in the views, taken out of this task">Take the selection out</button>
        <button type="button" className="link" onClick={() => { const ids = selectedElements(); if (ids.length) void execute('planning.ruleFromSelection', { id: t.id, ids }); }} title="A rule taking the kinds and storeys of the elements selected"><Wand2 size={12} />Rule from the selection</button>
      </dd>
    </dl>
  );
}

/** The rule's conditions as chips, each value on or off, with how many elements have it. */
function RuleEditor({ rule, options, onChange }: { task: TaskDto; rule: TaskRule; options: Options; onChange: (r: TaskRule) => void }) {
  return (
    <div className="rule-editor" data-testid="rule-editor">
      {ORDER.filter((k) => (options[k]?.length ?? 0) > (k === 'phases' ? 1 : 0) || (rule[k]?.length ?? 0) > 0).map((k) => (
        <div key={k} className="rule-condition">
          <span className="eyebrow">{HEADINGS[k]}</span>
          <span className="chips">
            {(options[k] ?? []).map((o) => (
              <button type="button" key={o.id} className={`chip${rule[k]?.includes(o.id) ? ' on' : ''}`} onClick={() => onChange(toggleRule(rule, k, o.id))}
                data-rule={`${k}:${o.id}`} title={`${o.count}`}>
                <span data-no-i18n={k === 'classes' || k === 'phases' || k === 'demolished' ? undefined : true}>{o.name}</span><span className="muted small" data-no-i18n>{` ${o.count}`}</span>
              </button>
            ))}
            {(rule[k] ?? []).filter((id) => !(options[k] ?? []).some((o) => o.id === id)).map((id) => (
              <button type="button" key={id} className="chip on" onClick={() => onChange(toggleRule(rule, k, id))} title="Not in the model now" data-no-i18n>{id}</button>
            ))}
          </span>
        </div>
      ))}
    </div>
  );
}

/** On an element: the tasks it is in, each to open or to take it out of. */
export function ElementTasksSection({ node }: { node: NodeDto }) {
  const data = useSchedule();
  const tasks = data ? tasksOf(data.tasks, node.id) : [];
  const temp = data?.temporaries?.find((x) => x.id === node.id);
  if (temp && data) {
    // A formwork or a scaffold: no task of its own, on site while its hosts' tasks need it.
    const hosts = temp.tasks.map((id) => data.tasks.find((x) => x.id === id)).filter((x): x is TaskDto => !!x);
    return (
      <dl className="fields" data-testid="element-temporary">
        <dt title="From the first task on what it serves to its striking (a formwork) or the last task on its walls (a scaffold)">On site</dt>
        <dd data-no-i18n>{`${showDate(temp.from)} – ${showDate(temp.to)}`}</dd>
        <dt>Lives by</dt>
        <dd>{hosts.map((h) => <div key={h.id}><button type="button" className="link" onClick={() => { select(h.id); useUi.setState({ selected: [h.id] }); }} data-no-i18n>{h.name}</button></div>)}</dd>
      </dl>
    );
  }
  if (!tasks.length) return null;
  return (
    <dl className="fields" data-testid="element-tasks">
      {tasks.map((t) => (
        <div key={t.id} className="element-task">
          <dt data-no-i18n>{t.wbs}</dt>
          <dd>
            <button type="button" className="link" onClick={() => { select(t.id); useUi.setState({ selected: [t.id] }); }} title={`${showDate(t.start)} – ${showDate(t.finish)}`} data-no-i18n>{t.name}</button>
            <button type="button" className="icon mini" title="Take it out of this task" onClick={() => void assignTo(t.id, [node.id], true)}><X size={11} /></button>
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** Whether an element is in a task (the section shows only then). */
export const inSomeTask = (node: NodeDto): boolean => node.type !== 'IfcTask'
  && (!!usePlanning.getState().data?.tasks.some((t) => !t.summary && t.elements?.includes(node.id)) || !!usePlanning.getState().data?.temporaries?.some((x) => x.id === node.id));
