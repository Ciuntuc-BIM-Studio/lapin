/** A converter's options as fields: a type of its class, a length (in the project's unit), a choice, a yes or no, a number. */

import { useSession } from '../../../engine/session';
import { LengthField, NumberField } from '../../../shell/cells';
import type { ConvOption } from '../../core/tables/tableModel';

export function OptionFields({ options, values, onChange }: { options: ConvOption[]; values: Record<string, unknown>; onChange: (key: string, value: unknown) => void }) {
  const nodes = useSession((s) => s.nodes);
  if (!options.length) return <p className="muted small">This conversion has no options.</p>;
  return (
    <div className="opt-grid" data-testid="convert-options">
      {options.map((o) => {
        const v = values[o.key];
        const field = (() => {
          switch (o.kind) {
            case 'type': {
              const types = [...nodes.values()].filter((n) => n.type === o.typeClass).sort((a, b) => a.name.localeCompare(b.name));
              return (
                <select value={typeof v === 'string' ? v : ''} data-field={`opt-${o.key}`} onChange={(e) => onChange(o.key, e.target.value || undefined)}>
                  <option value="">(the first the project has)</option>
                  {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
                </select>
              );
            }
            case 'length': return <LengthField metres={typeof v === 'number' ? v : null} testId={`opt-${o.key}`} onCommit={(m) => onChange(o.key, m ?? undefined)} />;
            case 'choice':
              return (
                <select value={v == null ? '' : String(v)} data-field={`opt-${o.key}`} onChange={(e) => onChange(o.key, e.target.value)}>
                  {(o.choices ?? []).map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              );
            case 'bool': return <input type="checkbox" data-field={`opt-${o.key}`} checked={v === true} onChange={(e) => onChange(o.key, e.target.checked)} />;
            default: return <NumberField value={typeof v === 'number' ? v : null} testId={`opt-${o.key}`} onCommit={(n) => onChange(o.key, n ?? undefined)} />;
          }
        })();
        return [<label key={`l${o.key}`} data-no-i18n>{o.label}</label>, <div key={`f${o.key}`}>{field}</div>];
      })}
    </div>
  );
}

export const PreviewList = ({ shapes, label }: { shapes: { id: string; error: string | null; steps: string[] }[]; label?: (id: string) => string }) => (
  <div className="scroll-x" style={{ maxHeight: 180 }}>
    <table className="layer-table" data-testid="convert-shapes">
      <tbody>
        {shapes.slice(0, 200).map((s, i) => (
          <tr key={s.id} className={s.error ? 'row-bad' : ''}>
            <td className="num">{i + 1}</td>
            {label && <td data-no-i18n>{label(s.id)}</td>}
            <td data-no-i18n>{s.error ?? s.steps.join(' · ')}</td>
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);
