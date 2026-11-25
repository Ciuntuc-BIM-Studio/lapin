/**
 * Unified 3D Cursor Precision Pill (docs/plan-ux-game-like.md, P3):
 * shared cursor-side numeric & snap pill for Place3D, Move3D and AxisDrag3D,
 * with semantic snap/axis badge, invalid-input indicator and live math
 * expression preview (e.g. "6000/3" -> "= 2000 mm").
 */

import type { RefObject } from 'react';
import { formatLength, parseLength } from './liveInput';

/** Evaluates simple arithmetic expressions (+, -, *, /) for live preview before Enter. */
export function evalMathPreview(text: string): string | null {
  const t = text.trim();
  if (!t || !/[+\-*/]/.test(t)) return null;
  // Support basic arithmetic with optional trailing unit (e.g., "6000/3", "2400+350").
  const clean = t.replace(/,/g, '.').replace(/\s+/g, '');
  if (/^[0-9.]+(\*[0-9.]+|\/[0-9.]+|[+-][0-9.]+)+$/.test(clean)) {
    try {
      const tokens = clean.match(/[0-9.]+|[+\-*/]/g);
      if (!tokens || tokens.length < 3) return null;
      // Pass 1: * and /
      const stack: (number | string)[] = [Number(tokens[0])];
      for (let i = 1; i < tokens.length; i += 2) {
        const op = tokens[i], rhs = Number(tokens[i + 1]);
        if (!Number.isFinite(rhs)) return null;
        if (op === '*' || op === '/') {
          const lhs = stack.pop() as number;
          if (op === '/' && Math.abs(rhs) < 1e-12) return null;
          stack.push(op === '*' ? lhs * rhs : lhs / rhs);
        } else {
          stack.push(op, rhs);
        }
      }
      // Pass 2: + and -
      let acc = stack[0] as number;
      for (let i = 1; i < stack.length; i += 2) {
        const op = stack[i] as string, rhs = stack[i + 1] as number;
        acc = op === '+' ? acc + rhs : acc - rhs;
      }
      if (!Number.isFinite(acc)) return null;
      const m = parseLength(String(Math.round(acc * 1e6) / 1e6));
      return m != null ? `= ${formatLength(m)}` : null;
    } catch { return null; }
  }
  return null;
}

export function PrecisionPill({
  x, y, text, placeholder, bad, badge, badgeColor, ariaLabel, dataTestAttr, inputRef,
  onChange, onEnter, onKeyDown, dataContainerAttr,
}: {
  x: number;
  y: number;
  text: string;
  placeholder: string;
  bad?: boolean;
  badge?: string | null;
  badgeColor?: string | null;
  ariaLabel: string;
  dataTestAttr?: Record<string, string>;
  dataContainerAttr?: Record<string, string>;
  inputRef?: RefObject<HTMLInputElement>;
  onChange: (value: string) => void;
  onEnter: () => void;
  onKeyDown?: (e: React.KeyboardEvent<HTMLInputElement>) => void;
}) {
  const preview = !bad ? evalMathPreview(text) : null;
  return (
    <div className="axis-drag-value precision-pill" style={{ left: x, top: y }} {...dataContainerAttr}>
      {badge && <span className={`pill-badge ${badgeColor ?? ''}`} style={badgeColor?.startsWith('#') || badgeColor?.startsWith('var') ? { color: badgeColor } : undefined}>{badge}</span>}
      <input
        ref={inputRef}
        value={text}
        placeholder={placeholder}
        aria-label={ariaLabel}
        spellCheck={false}
        className={bad ? 'bad' : undefined}
        {...dataTestAttr}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => {
          onKeyDown?.(e);
          if (e.defaultPrevented) return;
          if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); onEnter(); }
        }}
      />
      {preview && <span className="pill-preview" data-no-i18n>{preview}</span>}
    </div>
  );
}
