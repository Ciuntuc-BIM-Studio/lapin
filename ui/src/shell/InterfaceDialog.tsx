/**
 * View → Interface… (Ctrl+.): the three presets as one choice, and each part
 * of the interface on its own; a part changed on its own makes the choice
 * Custom. Applied at once, kept for this user.
 */

import { X } from 'lucide-react';
import { useUi } from '../app/store';
import { CHOICES, choosePart, choosePreset, useInterface, type InterfaceParts, type InterfacePreset } from '../app/interface';
import { bindingText } from '../app/commands';

const PRESET_LABELS: Record<InterfacePreset, { label: string; note: string }> = {
  comfortable: { label: 'Comfortable', note: 'The ribbon with names, a command line under each drawing, the HUD over the view.' },
  compact: { label: 'Compact', note: 'An icon band, one status line for the prompt and the command line, the quick tools on the view’s edge.' },
  focus: { label: 'Focus', note: 'The tabs and a command line: the ribbon drops from its tab, the panels hide (Tab brings them back).' },
};

const PARTS: { key: keyof InterfaceParts; label: string; choices: Record<string, string> }[] = [
  { key: 'ribbon', label: 'Ribbon', choices: { large: 'Large with names', icons: 'Icons only', dropdown: 'Opens from its tab' } },
  { key: 'quickTools', label: 'Quick tools', choices: { floating: 'Floating at the bottom', docked: 'Docked on the left edge', off: 'Off (the number keys still work)' } },
  { key: 'commandLine', label: 'Command line', choices: { own: 'Its own line', status: 'In the status line' } },
  { key: 'hud', label: 'HUD', choices: { over: 'Over the view', status: 'In the status line', off: 'Off' } },
  { key: 'prompt', label: 'Tool prompt and inputs', choices: { banner: 'Banner over the view', commandLine: 'In the command line' } },
  { key: 'keyPlan', label: 'Key plan', choices: { shown: 'Shown', button: 'A button' } },
  { key: 'panels', label: 'Panels', choices: { docked: 'Docked', browserRail: 'Browser folds to a rail', bothRail: 'Both fold', hidden: 'Hidden' } },
  { key: 'rows', label: 'Property rows', choices: { normal: 'Normal', dense: 'Dense (22 px)' } },
];

export function InterfaceDialog() {
  const s = useInterface();
  const close = () => useUi.setState({ dialog: null });
  const keys = bindingText('app.interface');
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Interface" onMouseDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="dialog interface-dialog">
        <header className="dialog-head">
          <span className="panel-title">Interface</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="seg presets" role="radiogroup" aria-label="Mode">
            {(Object.keys(PRESET_LABELS) as InterfacePreset[]).map((p) => (
              <button type="button" key={p} role="radio" aria-checked={s.preset === p} className={`seg-item${s.preset === p ? ' on' : ''}`} onClick={() => choosePreset(p)}>
                <b>{PRESET_LABELS[p].label}</b><span className="muted small">{PRESET_LABELS[p].note}</span>
              </button>
            ))}
          </div>
          {s.preset === 'custom' && <p className="muted small">Custom: the parts below as you set them.</p>}
          <div className="interface-parts">
            {PARTS.map((part) => (
              <div className="part-row" key={part.key}>
                <span className="part-label">{part.label}</span>
                <div className="seg small" role="radiogroup" aria-label={part.label}>
                  {(CHOICES[part.key] as readonly string[]).map((v) => (
                    <button type="button" key={v} role="radio" aria-checked={s.parts[part.key] === v} className={`seg-item${s.parts[part.key] === v ? ' on' : ''}`}
                      onClick={() => choosePart(part.key, v as never)}>{part.choices[v]}</button>
                  ))}
                </div>
              </div>
            ))}
          </div>
          <p className="muted small"><span>The side panels fold in every mode: the button on their header, a double-click on their edge, or the keyboard.</span> <kbd data-no-i18n>{bindingText('app.toggleBrowser') || '—'}</kbd> <kbd data-no-i18n>{bindingText('app.toggleProps') || '—'}</kbd></p>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'shortcuts' })}>Keyboard shortcuts…</button>
          <span className="grow" />
          {keys && <span className="muted small"><span>This dialog:</span> <kbd data-no-i18n>{keys}</kbd></span>}
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
