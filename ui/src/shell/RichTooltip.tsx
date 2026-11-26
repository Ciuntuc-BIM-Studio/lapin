/**
 * Rich Animated Ribbon Tooltips (docs/plan-ux-game-like.md, P9):
 * on extended hover (~600 ms) over a ribbon tool, shows a compact card with
 * the tool's name, shortcut keycap, and a 3-second looping inline SVG gesture
 * diagram (twoPoints / point / action). Zero external binary assets.
 */

import type { ToolDef } from '../platform/module';

function GestureDiagram({ kind }: { kind: string }) {
  if (kind === 'twoPoints' || kind === 'polyline' || kind === 'polygon') {
    return (
      <svg viewBox="0 0 120 40" className="rich-tip-svg" aria-hidden="true">
        <line x1="16" y1="26" x2="102" y2="14" className="rich-tip-guide" />
        <circle cx="16" cy="26" r="3.5" className="rich-tip-pt1" />
        <circle cx="102" cy="14" r="3.5" className="rich-tip-pt2" />
        <line x1="16" y1="26" x2="102" y2="14" className="rich-tip-draw" />
      </svg>
    );
  }
  if (kind === 'rectangle') {
    return (
      <svg viewBox="0 0 120 40" className="rich-tip-svg" aria-hidden="true">
        <rect x="24" y="8" width="72" height="24" className="rich-tip-draw" />
        <circle cx="24" cy="8" r="3.5" className="rich-tip-pt1" />
        <circle cx="96" cy="32" r="3.5" className="rich-tip-pt2" />
      </svg>
    );
  }
  return (
    <svg viewBox="0 0 120 40" className="rich-tip-svg" aria-hidden="true">
      <circle cx="60" cy="20" r="10" className="rich-tip-pulse" />
      <circle cx="60" cy="20" r="3.5" className="rich-tip-pt1" />
    </svg>
  );
}

export function RichTooltip({ tool, shortcut, x, y }: { tool: ToolDef; shortcut?: string; x: number; y: number }) {
  const kind = tool.gesture?.kind ?? 'action';
  const caption = kind === 'twoPoints'
    ? 'Click start → Click end or type length'
    : kind === 'rectangle'
      ? 'Click corner → Opposite corner or W,H'
      : kind === 'point'
        ? 'Click a snapped point on the plan or 3D'
        : 'Click to run on model or selection';
  const left = typeof window !== 'undefined' ? Math.min(x, window.innerWidth - 230) : x;
  return (
    <div className="rich-tooltip" role="tooltip" style={{ left, top: y }} data-testid="rich-tooltip">
      <div className="rich-tip-head">
        <b>{tool.label}</b>
        {shortcut && <kbd data-no-i18n>{shortcut}</kbd>}
      </div>
      <GestureDiagram kind={kind} />
      <span className="muted small">{caption}</span>
    </div>
  );
}
