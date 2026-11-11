/**
 * The page's look: Lapin's (ui/src/theme.css) — paper and graphite, the same tokens, small spaced capitals for labels,
 * thin rules, square corners, one accent. Inline: a page from a file loads no stylesheet and no font.
 */
export const VIEWER_CSS = `
:root, [data-theme="paper"] {
  --bg: #f6f3ec; --panel: #fdfbf7; --raised: #ffffff; --wash: #efeae0; --rule: #e2dccf; --rule-strong: #d6cebf;
  --ink: #1e2a3a; --ink-muted: #6b6357; --accent: #b5532d; --accent-ink: #ffffff; --accent-wash: #f1e3d9;
  --hud: rgba(253, 251, 247, 0.92); --view: #eef0f2; color-scheme: light;
}
[data-theme="graphite"] {
  --bg: #161513; --panel: #1c1b19; --raised: #22201d; --wash: #2c2a26; --rule: #34312c; --rule-strong: #4a463f;
  --ink: #e9e4da; --ink-muted: #a39b8c; --accent: #e07a4f; --accent-ink: #1c1b19; --accent-wash: #3a2c24;
  --hud: rgba(22, 21, 19, 0.88); --view: #1f1e1c; color-scheme: dark;
}
* { box-sizing: border-box; }
html, body { height: 100%; margin: 0; overflow: hidden; }
body { background: var(--bg); color: var(--ink); font: 13px/1.4 ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Noto Sans Lao", sans-serif; -webkit-font-smoothing: antialiased; }
button, input, select { font: inherit; color: inherit; }
#lapin-start { display: none; }
.lp { position: fixed; inset: 0; display: grid; grid-template-rows: auto 1fr; }
.lp-head { display: flex; align-items: center; gap: 14px; padding: 8px 14px; border-bottom: 1px solid var(--rule); background: var(--panel); min-width: 0; }
.lp-mark { display: inline-grid; place-items: center; width: 24px; height: 24px; border-radius: 4px; background: var(--accent); color: var(--accent-ink); font-weight: 900; font-size: 14px; flex: none; }
.lp-title { min-width: 0; }
.lp-title b { display: block; font-size: 15px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.lp-title span { color: var(--ink-muted); font-size: 11.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; display: block; }
.lp-tabs { display: flex; gap: 2px; margin-left: 8px; }
.lp-tab { padding: 6px 12px; border: 1px solid transparent; border-radius: 3px; background: none; cursor: pointer; font-size: 11px; font-weight: 600; letter-spacing: 0.12em; text-transform: uppercase; color: var(--ink-muted); }
.lp-tab:hover { color: var(--ink); }
.lp-tab[aria-selected="true"] { color: var(--ink); border-color: var(--rule-strong); background: var(--raised); }
.lp-grow { flex: 1; }
.lp-open { font-size: 12px; color: var(--ink-muted); white-space: nowrap; }
.lp-open b { color: var(--ink); }
.lp-btn { display: inline-flex; align-items: center; gap: 6px; padding: 5px 10px; border: 1px solid var(--rule-strong); border-radius: 3px; background: var(--panel); cursor: pointer; font-size: 12px; white-space: nowrap; }
.lp-btn:hover { border-color: var(--accent); }
.lp-btn:disabled { opacity: .5; cursor: default; border-color: var(--rule); }
.lp-main { display: grid; grid-template-columns: 1fr 320px; min-height: 0; }
.lp-pane { position: relative; min-width: 0; min-height: 0; overflow: hidden; background: var(--view); }
.lp-side { border-left: 1px solid var(--rule); background: var(--panel); overflow: auto; padding: 12px 14px; }
.lp-eyebrow { font-size: 10.5px; font-weight: 600; letter-spacing: 0.14em; text-transform: uppercase; color: var(--ink-muted); margin: 14px 0 6px; }
.lp-eyebrow:first-child { margin-top: 0; }
.lp-muted { color: var(--ink-muted); }
.lp-props { display: grid; grid-template-columns: minmax(90px, 40%) 1fr; gap: 3px 10px; margin: 0; font-size: 12.5px; }
.lp-props dt { color: var(--ink-muted); overflow-wrap: anywhere; }
.lp-props dd { margin: 0; overflow-wrap: anywhere; }
.lp-hud { position: absolute; left: 10px; top: 10px; display: flex; flex-wrap: wrap; gap: 6px; align-items: center; padding: 6px; background: var(--hud); border: 1px solid var(--rule); border-radius: 3px; max-width: calc(100% - 20px); }
.lp-hud select { padding: 4px 6px; border: 1px solid var(--rule-strong); border-radius: 3px; background: var(--panel); }
.lp-hud label { display: inline-flex; align-items: center; gap: 4px; font-size: 12px; }
.lp-canvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; touch-action: none; }
.lp-empty { position: absolute; inset: 0; display: grid; place-items: center; padding: 24px; text-align: center; color: var(--ink-muted); }
.lp-draw { display: grid; grid-template-columns: 240px 1fr; height: 100%; min-height: 0; }
.lp-list { border-right: 1px solid var(--rule); background: var(--panel); overflow: auto; padding: 10px 8px; }
.lp-list button { display: block; width: 100%; text-align: left; padding: 5px 8px; border: none; border-radius: 3px; background: none; cursor: pointer; font-size: 12.5px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lp-list button:hover { background: var(--wash); }
.lp-list button[aria-current="true"] { background: var(--accent-wash); color: var(--ink); font-weight: 600; }
.lp-paperbox { position: relative; overflow: hidden; touch-action: none; cursor: grab; }
.lp-paperbox.lp-dragging { cursor: grabbing; }
.lp-paper { position: absolute; left: 0; top: 0; transform-origin: 0 0; background: #ffffff; box-shadow: 0 1px 4px rgba(0,0,0,.25); }
.lp-paper svg { display: block; width: 100%; height: 100%; }
.lp-paper .lp-pick path { cursor: pointer; }
.lp-paper .lp-pick path:hover { fill-opacity: .12; stroke-opacity: .25; fill: #b5532d; stroke: #b5532d; }
.lp-paper .lp-pick path.lp-on { fill-opacity: .28; stroke-opacity: .55; fill: #b5532d; stroke: #b5532d; }
.lp-tablebox { height: 100%; overflow: auto; padding: 14px 18px 30px; background: var(--bg); }
.lp-tablehead { display: flex; flex-wrap: wrap; gap: 10px; align-items: baseline; margin-bottom: 8px; }
.lp-tablehead h2 { margin: 0; font-size: 16px; }
.lp-tag { display: inline-block; padding: 1px 7px; border-radius: 3px; background: var(--accent-wash); color: var(--accent); font-size: 11px; font-weight: 600; letter-spacing: .06em; text-transform: uppercase; }
.lp-filter { padding: 4px 8px; border: 1px solid var(--rule-strong); border-radius: 3px; background: var(--panel); min-width: 160px; }
table.lp-table { border-collapse: collapse; width: 100%; font-size: 12.5px; background: var(--panel); }
.lp-table th { position: sticky; top: 0; background: var(--wash); text-align: left; font-size: 10.5px; letter-spacing: .1em; text-transform: uppercase; color: var(--ink-muted); padding: 6px 8px; border-bottom: 1px solid var(--rule-strong); cursor: pointer; user-select: none; white-space: nowrap; }
.lp-table th[aria-sort] { color: var(--ink); }
.lp-table td { padding: 4px 8px; border-bottom: 1px solid var(--rule); vertical-align: top; }
.lp-table td.lp-num, .lp-table th.lp-num { text-align: right; font-variant-numeric: tabular-nums; }
.lp-table tr.lp-heading td { font-weight: 700; background: var(--wash); }
.lp-table tbody tr { cursor: pointer; }
.lp-table tbody tr:hover td { background: var(--accent-wash); }
.lp-table tbody tr.lp-on td { background: var(--accent-wash); }
.lp-table tbody tr.lp-on td:first-child { box-shadow: inset 3px 0 0 var(--accent); }
.lp-totals { margin: 12px 0 0 auto; border-collapse: collapse; font-size: 13px; }
.lp-totals td { padding: 3px 8px; }
.lp-totals td:last-child { text-align: right; font-variant-numeric: tabular-nums; }
.lp-totals tr:last-child td { font-weight: 700; border-top: 1px solid var(--rule-strong); }
@media (max-width: 760px) {
  .lp-main { grid-template-columns: 1fr; grid-template-rows: 1fr 40%; }
  .lp-side { border-left: none; border-top: 1px solid var(--rule); }
  .lp-draw { grid-template-columns: 1fr; grid-template-rows: auto 1fr; }
  .lp-list { max-height: 30vh; border-right: none; border-bottom: 1px solid var(--rule); }
  .lp-open { display: none; }
}
@media print {
  .lp-head, .lp-side, .lp-list, .lp-hud { display: none !important; }
  .lp, .lp-main, .lp-draw { position: static; display: block; }
  .lp-paper { position: static; transform: none !important; box-shadow: none; width: 100% !important; height: auto !important; }
}
`;
