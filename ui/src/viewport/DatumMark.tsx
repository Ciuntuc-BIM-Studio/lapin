/** The temporary reference point's mark (viewport/datum.ts): a circle with a cross, at its place in the view (screen px). */
export function DatumMark({ at, to }: { at: { x: number; y: number }; to?: { x: number; y: number } | null }) {
  return (
    <g className="plan-preview datum-mark" data-testid="datum-mark" pointerEvents="none" aria-hidden="true">
      {to && <line x1={at.x} y1={at.y} x2={to.x} y2={to.y} strokeDasharray="4 4" />}
      <circle cx={at.x} cy={at.y} r={5} fill="none" />
      <path d={`M${at.x - 8},${at.y}H${at.x + 8}M${at.x},${at.y - 8}V${at.y + 8}`} />
    </g>
  );
}
