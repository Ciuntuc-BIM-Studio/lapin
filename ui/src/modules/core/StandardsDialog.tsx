/**
 * Drafting standards: the project's text styles, dimension styles, axis types
 * (and the default one), tag types (a box, or a door's or window's marker
 * across its wall in plan; their leaders, decision 331), layers and layer groups; on Project, the units, line ends and the
 * section and elevation markers' sizes, each kind its own (decisions 455, 460), the standard scales and each kind's for a
 * new view (decision 492). Sizes are paper millimetres.
 * Each change is one engine command; Standard (and layer 0) always stay.
 */

import { ColourField } from '../../shell/ColourField';
import { clearStandardsTab, standardsFocus, standardsTab } from './standardsTab';
import { useEffect, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { PendingDialog } from './PendingDialog';
import { MarkerSymbolsStandards, SymbolPicker, useSymbolList } from './symbols/SlotPicker';
import { LineTypesTab } from './lineTypes/LineTypesTab';
import { FrameLinesStandards } from './frameLine';
import { useLineTypes } from './lineTypes/lineTypes';
import { lineTypeNames, withCurrent } from './lineTypes/lineTypeModel';
import { useSystems } from './classifications/classifications';
import { classificationFields } from './classifications/classModel';
import { StandardsFirmFolders, StandardsImportPanel, StandardsLibraryButtons, type PackRead } from './StandardsLibrary';
import { BuildupEditor } from './BuildupEditor';
import { LeaderEditor, type LeaderEntry } from './LeaderEditor';
import { MarkerEditor, type MarkerEntry } from './MarkerEditor';
import { BUILDUP_DEFAULTS, type BuildupEntry } from './buildupTag';
import { EXT_SUPPRESS, EXT_SUPPRESS_LABELS } from './dimensionLines';
import { TagRulesEditor } from './TagRulesEditor';
import type { TagRuleDto } from './tagRules';
import { FieldPicker } from '../../shell/FieldPicker';
import { parseDecimal } from '../../shell/parseDecimal';
import { openingFormat } from './openingFormat';

type Entry = Record<string, string | number | boolean | null | undefined> & { id: string; name: string };
type Book = { textStyles: Entry[]; dimStyles: Entry[]; axisStyles: Entry[]; tagTypes: Entry[]; defaultAxisStyle: string; layers: Entry[]; layerGroups: Entry[]; units?: string; lineCap?: string; sectionMarkSize?: number; sectionMarkText?: number; elevationMarkSize?: number; elevationMarkText?: number; areaRounding?: number; suppressZeros?: boolean; levelDecimals?: number; levelZeros?: string; scales?: number[]; scaleChoices?: number[]; planScale?: number; sectionScale?: number; elevationScale?: number; tagRules?: TagRuleDto[]; tableStyles?: { id: string; name: string; cells?: boolean; table?: { rows?: unknown[]; cols?: unknown[] } }[] };
type Col = { key: string; label: string; title?: string; kind: 'text' | 'mm' | 'number' | 'decimals' | 'bool' | 'color' | 'choice' | 'style' | 'group'; choices?: string[]; lineType?: boolean; labels?: Record<string, string> };
type Tab = { id: 'text' | 'dim' | 'axis' | 'tag' | 'layer'; label: string; list: keyof Book; cols: Col[]; fresh: Record<string, unknown> };

/** How a dimension writes an opening's height under its line (decision 338; one line and custom: forum topic 229). */
const OPENING_HEIGHTS = ['off', 'number', 'h', 'hp', 'slash', 'line', 'custom'] as const;
/** The opening text's tokens, one phrase for the translations. */
const OPENING_TEXT_HELP = 'When Opening height is custom: {h} the height, {p} the sill, | starts a new line, [ ] is written only when there is a sill (a door has none); e.g. H {h}[, S {p}]. Empty: h={h}|p={p}';
const LINE_TYPES = ['continuous', 'dashed', 'dotted', 'dashdot', 'dashdotdot'];
/** How a choice reads where its value alone would not say it. */
const CHOICE_LABELS: Record<string, string> = { project: 'as the project', across: 'perpendicular to the tag line', along: 'along the tag line', buildup: 'build-up',
  // An opening's height under a dimension line across it (report #92, decision 338).
  // One line, or the style's own format with its prefixes (forum topic 229).
  off: 'off', number: '150', h: 'h=150', hp: 'h=150 over p=90', slash: '150/90', line: 'h=150, p=90 (one line)', custom: 'custom (Opening text)',
  // A dimension's trailing zeros (forum #81).
  keep: 'keep (2.500)', suppress: 'suppress (2.5)' };
/**
 * A dimension style's extension lines and dimension line (issue #66): which extension lines a string leaves out (a dimension's
 * own choice in its properties goes before), a fixed length back from the dimension line, and each line's pen, colour and
 * line type. Empty: as before (from the gap, 0.13 mm, the style's colour, continuous).
 */
const DIM_LINE_COLS: Col[] = [
  { key: 'extSuppress', label: 'Extension lines off', title: "Leave out a string's first extension line, its last, or both; a dimension's own choice in its properties goes before", kind: 'choice', choices: [...EXT_SUPPRESS], labels: EXT_SUPPRESS_LABELS },
  { key: 'extFixed', label: 'Fixed extension mm', title: 'Every extension line this long on paper, measured back from the dimension line (never nearer the point than the gap); empty: from the gap', kind: 'mm' },
  { key: 'extWeight', label: 'Extension pen mm', title: 'The extension lines\' pen on paper; empty: 0.13 mm', kind: 'mm' },
  { key: 'extColor', label: 'Extension colour', kind: 'color' },
  { key: 'extLineType', label: 'Extension line', kind: 'choice', choices: LINE_TYPES, lineType: true },
  { key: 'dimLineWeight', label: 'Dimension line pen mm', title: 'The dimension line\'s pen on paper (its arrows too); empty: 0.13 mm', kind: 'mm' },
  { key: 'dimLineColor', label: 'Dimension line colour', title: "The dimension line's colour, its arrows and ticks too; empty: the style's", kind: 'color' },
  { key: 'dimLineType', label: 'Dimension line', kind: 'choice', choices: LINE_TYPES, lineType: true },
  // The values' own colour (forum topic 173): over the text style's; empty: the text style's colour, else the style's.
  { key: 'textColor', label: 'Text colour', title: "The values' colour; empty: the colour of its text style (Text tab), else the style's", kind: 'color' },
];
/** The dimension style fields left empty to say "as before" (sent as null). */
const NULLABLE = new Set(['textLeader', 'extFixed', 'extWeight', 'dimLineWeight', 'openingText']);
/** The colours that may be cleared back to the style's. */
const CLEARABLE = new Set(['extColor', 'dimLineColor', 'textColor']);
const TABS: Tab[] = [
  { id: 'text', label: 'Text', list: 'textStyles', fresh: { name: 'Text', height: 2.5 }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'height', label: 'Height mm', kind: 'mm' }, { key: 'font', label: 'Font', kind: 'choice', choices: ['sans', 'serif', 'mono'] },
    { key: 'bold', label: 'B', kind: 'bool' }, { key: 'italic', label: 'I', kind: 'bool' }, { key: 'color', label: 'Colour', kind: 'color' }] },
  { id: 'dim', label: 'Dimensions', list: 'dimStyles', fresh: { name: 'Dimension' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'arrow', label: 'Arrow', kind: 'choice', choices: ['tick', 'arrow', 'dot', 'none'] },
    { key: 'arrowSize', label: 'Arrow mm', kind: 'mm' }, { key: 'extGap', label: 'Gap mm', kind: 'mm' }, { key: 'extOver', label: 'Over mm', kind: 'mm' },
    { key: 'placement', label: 'Value', kind: 'choice', choices: ['above', 'centered', 'below'] }, { key: 'unit', label: 'Unit', kind: 'choice', choices: ['project', 'm', 'cm', 'mm', 'ft-in', 'in'] },
    { key: 'precision', label: 'Decimals', kind: 'decimals' },
    // Trailing zeros dropped (2.500 → 2.5, 3.000 → 3) or kept, or as the project's standards say (forum #81).
    { key: 'zeros', label: 'Trailing zeros', title: 'Whether a value drops its trailing zeros (2.500 → 2.5, 3.000 → 3) or keeps them; as the project: the Project tab says', kind: 'choice', choices: ['project', 'keep', 'suppress'] },
    // A segment across a door, a window or an opening writes its height under the line, and its sill in some forms (decision 338).
    { key: 'openingHeight', label: 'Opening height', title: 'Under a dimension segment across a door, window or opening: its height (and its sill), read from the model', kind: 'choice', choices: [...OPENING_HEIGHTS] },
    // Its own format, prefixes and all, when Opening height is custom (forum topic 229).
    { key: 'openingText', label: 'Opening text', title: OPENING_TEXT_HELP, kind: 'text' },
    // Values that do not fit between their witness lines moved clear of their neighbours, with a leader past a distance (forum #78).
    { key: 'autoText', label: 'Text placement', title: 'A value that does not fit between its witness lines moves just outside its segment, or above and below the line in turn, clear of the other values; as the project: Annotate → Dimension rules… says', kind: 'choice', choices: ['project', 'on', 'off'] },
    { key: 'textLeader', label: 'Leader beyond mm', title: 'A value moved further than this (mm on paper) from its place gets a leader to its segment; empty: as the project', kind: 'mm' },
    // The extension lines and the dimension line as AutoCAD's dimension styles have them (issue #66); empty: as before.
    ...DIM_LINE_COLS] },
  { id: 'axis', label: 'Grid axes', list: 'axisStyles', fresh: { name: 'Axis' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'bubble', label: 'Bubble', kind: 'choice', choices: ['circle', 'square'] },
    { key: 'ends', label: 'Ends', kind: 'choice', choices: ['both', 'start', 'end', 'none'] }, { key: 'size', label: 'Size mm', kind: 'mm' },
    { key: 'lineType', label: 'Line', kind: 'choice', choices: LINE_TYPES }, { key: 'firstLabel', label: 'First', kind: 'text' }, { key: 'color', label: 'Colour', kind: 'color' }] },
  // A tag's template reads the element: {mark}, {name}, {type}, {class}, a door's or window's {width}, {height}, {sill} (in the unit given), a room's {number}, {area}, {perimeter}, {height}, {volume}, {function}; "|" starts a new line.
  { id: 'tag', label: 'Tags', list: 'tagTypes', fresh: { name: 'Tag', template: '{mark}', leaderShape: 'elbow' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'shape', label: 'Shape', kind: 'choice', choices: ['box', 'opening', 'room', 'buildup'] },
    // Built as a mark is (decision 737): a prefix and a suffix round the template, a formula of fields.
    { key: 'prefix', label: 'Prefix', title: 'Written before the text (its first line); plain text', kind: 'text' },
    { key: 'template', label: 'Text (outside)', kind: 'text' },
    { key: 'suffix', label: 'Suffix', title: 'Written after the text (its last line); plain text', kind: 'text' },
    { key: 'inner', label: 'Inside', kind: 'text' },
    { key: 'unit', label: 'Unit', kind: 'choice', choices: ['cm', 'mm', 'm', 'ft-in', 'in', 'project'] }, { key: 'reach', label: 'Past faces mm', kind: 'mm' },
    // A door's or window's marker: its texts across its line (along the wall) or along it, always upright (decision 292).
    { key: 'textOrientation', label: 'Text direction', kind: 'choice', choices: ['across', 'along'] },
    { key: 'textStyle', label: 'Text', kind: 'style' }, { key: 'classes', label: 'Classes', kind: 'text' }] },
  { id: 'layer', label: 'Layers', list: 'layers', fresh: { name: 'Layer' }, cols: [
    { key: 'name', label: 'Name', kind: 'text' }, { key: 'color', label: 'Colour', kind: 'color' }, { key: 'lineWeight', label: 'Pen mm', kind: 'mm' },
    { key: 'lineType', label: 'Line', kind: 'choice', choices: LINE_TYPES }, { key: 'group', label: 'Group', kind: 'group' }, { key: 'visible', label: 'On', kind: 'bool' }, { key: 'locked', label: 'Lock', kind: 'bool' }] },
];

/** The markers' sizes on the Project tab (decision 460): a section's and an elevation's apart, each its symbol and its name. */
type MarkKey = 'sectionMarkSize' | 'sectionMarkText' | 'elevationMarkSize' | 'elevationMarkText';
const MARK_ROWS: { key: MarkKey; field: string; size: boolean; label: string; title: string }[] = [
  { key: 'sectionMarkSize', field: 'section-mark-size', size: true, label: 'Section marker size', title: "The size of the section markers' arrows (0.7 of it), in paper mm, in plans, sections, elevations, sheets and PDF; a section's own size in its properties goes before it" },
  { key: 'sectionMarkText', field: 'section-mark-text', size: false, label: 'Section marker text', title: "The height of the section markers' names, in paper mm; empty: the Standard text style's" },
  { key: 'elevationMarkSize', field: 'elevation-mark-size', size: true, label: 'Elevation marker size', title: "The elevation markers' circle across, in paper mm, in plans, sheets and PDF; an elevation's own size in its properties goes before it" },
  { key: 'elevationMarkText', field: 'elevation-mark-text', size: false, label: 'Elevation marker text', title: "The height of the elevation markers' names, in paper mm; empty: the Standard text style's" },
];

/** The scale each kind of new view takes (forum #30, decision 492); empty: 1:100. */
type ScaleKey = 'planScale' | 'sectionScale' | 'elevationScale';
const SCALE_ROWS: { key: ScaleKey; field: string; label: string }[] = [
  { key: 'planScale', field: 'plan-scale', label: 'New plans at 1:' },
  { key: 'sectionScale', field: 'section-scale', label: 'New sections at 1:' },
  { key: 'elevationScale', field: 'elevation-scale', label: 'New elevations at 1:' },
];
/** A list of scales as typed: 1:50, 100; 200 (empty: none). */
const scaleList = (text: string): number[] => text.split(/[,;\s]+/).map((x) => Number(x.replace(/^1[:/]/, ''))).filter((n) => Number.isFinite(n) && n > 0);

/** How a tag's template reads the element (report #64), one phrase for the translations. */
const TAG_HELP = 'A template reads the element: {mark}, {name}, {type}, {class}, any parameter by its name ({Fire rating}) or a standard one ({Pset_WallCommon.FireRating}); | starts a new line.';
/** A tag's text built as a mark is (decision 737), one phrase for the translations. */
const TAG_BUILD_HELP = 'Build the text as a mark: a prefix, the template with its fields and formulas ({= n | 00}, {storey.code}), a suffix. + Field… adds a field; Manage → Mark rules… builds the {mark} itself.';
/** The Layers tab's layers are the drafting layers (forum #168, decision 714), one phrase for the translations. */
const LAYER_HELP = 'These are the project\'s drafting layers, the same as Draft → Layers and the layer command: lines drawn by layer take their colour, pen and line type.';
/** A build-up tag's own rule, one phrase for the translations. */
const BUILDUP_HELP = 'A build-up tag writes the element\'s layers: its header, its line once per layer, its footer. Open it with Build-up… to set them.';

export function StandardsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [book, setBook] = useState<Book | null>(null);
  const [tab, setTab] = useState<Tab>(() => TABS.find((t) => t.id === standardsTab()) ?? TABS[0]);
  const [group, setGroup] = useState('');
  const [project, setProject] = useState(() => standardsTab() === 'project');
  const [lines, setLines] = useState(false);
  const [tables, setTables] = useState(false);
  const [pack, setPack] = useState<PackRead | null>(null);
  const lineTypes = useLineTypes();
  const systems = useSystems();
  const symbols = useSymbolList();
  const [editing, setEditing] = useState<string | null>(null);
  // A door's or window's marker opened for its layout (forum topic 205).
  const [marker, setMarker] = useState<string | null>(null);
  // The ready-made markers after national conventions (forum topic 205), offered beside New.
  const [presets, setPresets] = useState<{ id: string; name: string }[]>([]);
  // The style it was opened on from an annotation's properties (forum topic 173): picked out in its row, scrolled to.
  const [focus] = useState(() => standardsFocus());
  const focused = useRef<HTMLTableRowElement | null>(null);
  useEffect(() => { clearStandardsTab(); }, []);
  const ready = book != null;
  useEffect(() => { if (ready) focused.current?.scrollIntoView?.({ block: 'nearest' }); }, [ready]);
  useEffect(() => {
    let live = true;
    void query<Book>('standards.get').then((b) => { if (live && b) setBook(b); });
    return () => { live = false; };
  }, [nodes]);
  // The architecture module's project default for new openings' sills (null: not in this edition).
  const [openingSillFrom, setOpeningSillFrom] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    void query<{ sillFrom?: string }>('architecture.openingDefaults').then((d) => { if (live && d) setOpeningSillFrom(d.sillFrom === 'finished' ? 'finished' : 'structural'); }).catch(() => undefined);
    return () => { live = false; };
  }, []);
  useEffect(() => {
    let live = true;
    void query<{ id: string; name: string }[]>('standards.tagPresets').then((p) => { if (live && Array.isArray(p)) setPresets(p); }).catch(() => undefined);
    return () => { live = false; };
  }, []);
  if (!book) return <PendingDialog title="Drafting standards" />;
  const set = (entry: Record<string, unknown>, extra: Record<string, unknown> = {}) => void execute('standards.set', { kind: tab.id, entry, ...extra });
  const fixed = (e: Entry) => (tab.id === 'layer' ? e.id === '0' : e.id === 'standard');

  const cell = (e: Entry, c: Col) => {
    const v = e[c.key];
    // A tag type made a build-up takes the build-up's defaults (its template too, unless it has one of its own) and opens its editor.
    const put = (value: unknown) => {
      if (tab.id === 'tag' && c.key === 'shape' && value === 'buildup' && e.shape !== 'buildup') {
        const { template, ...rest } = BUILDUP_DEFAULTS;
        set({ id: e.id, shape: 'buildup', ...rest, template: !e.template || e.template === '{mark}' ? template : e.template });
        setEditing(e.id);
        return;
      }
      // An opening text typed is the style's format: Opening height becomes custom with it, in the same undo step (forum topic 229).
      if (tab.id === 'dim' && c.key === 'openingText' && typeof value === 'string' && value && e.openingHeight !== 'custom') { set({ id: e.id, openingText: value, openingHeight: 'custom' }); return; }
      set({ id: e.id, [c.key]: value });
    };
    switch (c.kind) {
      case 'bool': return <input type="checkbox" checked={v === true || (c.key === 'visible' && v !== false)} onChange={(ev) => put(ev.target.checked)} />;
      // A dimension style's extension or dimension line colour (issue #66) may be cleared: the style's colour again.
      // A dimension style's text colour shows its text style's until it has its own (forum topic 173).
      case 'color': return CLEARABLE.has(c.key)
        ? <span className="colour-clearable" style={{ display: 'inline-flex', alignItems: 'center', gap: 2 }}><ColourField value={typeof v === 'string' ? v : String((c.key === 'textColor' ? book.textStyles.find((t) => t.id === String(e.textStyle ?? 'standard'))?.color : null) ?? e.color ?? '#000000')} onChange={(v) => put(v)} />
          {typeof v === 'string' && <button type="button" className="icon" title={c.key === 'textColor' ? "As its text style's colour" : "As the style's colour"} onClick={() => put(null)}><X size={12} /></button>}</span>
        : <ColourField value={typeof v === 'string' ? v : '#18181b'} onChange={(v) => put(v)} />;
      case 'choice': {
        // A line type is chosen among Lapin's and the project's own (Line types tab).
        // A layer's, or a dimension style's extension or dimension line (issue #66), left empty is continuous (as before).
        const isLine = c.key === 'lineType' || c.lineType === true;
        const unset = isLine && (tab.id === 'layer' || c.lineType === true);
        const names = isLine ? withCurrent(lineTypeNames(lineTypes, c.choices), String(v ?? '') || null).filter((x) => !(unset && x === 'continuous')) : c.choices!;
        return <select value={String(v ?? (c.key === 'extSuppress' ? 'none' : ''))} onChange={(ev) => put(c.lineType && !ev.target.value ? null : ev.target.value)}>{unset && <option value="">continuous</option>}{names.map((x) => <option key={x} value={x}>{c.labels?.[x] ?? CHOICE_LABELS[x] ?? x}</option>)}</select>;
      }
      case 'style': return <select value={String(v ?? 'standard')} onChange={(ev) => put(ev.target.value)}>{book.textStyles.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select>;
      case 'group': return <select value={String(v ?? '')} onChange={(ev) => put(ev.target.value || null)}><option value="">—</option>{book.layerGroups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select>;
      // Decimals: empty is the unit's usual (none in mm, one in cm, two in m, 1/16" in imperial), kept as −1.
      case 'decimals': return <input className="num" key={`${e.id}${c.key}${v}`} defaultValue={typeof v === 'number' && v >= 0 ? String(v) : ''} placeholder="auto"
        title="Empty: the unit's usual (none in mm, one in cm, two in m; 1/16&quot; in feet and inches, where 1 is 1/8&quot; and 3 is 1/32&quot;)"
        onBlur={(ev) => {
          const t = ev.target.value.trim();
          const n = t === '' ? -1 : parseDecimal(t, { min: -1, integer: true });
          const stored = typeof v === 'number' ? v : -1;
          if (n === null) ev.target.value = stored >= 0 ? String(stored) : '';
          else if (n !== stored) put(n);
        }} />;
      default: {
        const numeric = c.kind === 'mm' || c.kind === 'number';
        // An opening text shows the format its Opening height stands for until it has its own (forum topic 229).
        const hint = c.key === 'openingText' ? openingFormat(String(e.openingHeight ?? 'off')) ?? '' : undefined;
        return <input className={numeric ? 'num' : undefined} key={`${e.id}${c.key}${v}`} defaultValue={v == null ? '' : String(v)} placeholder={hint}
          onBlur={(ev) => {
            const t = ev.target.value.trim();
            if (t === String(v ?? '')) return;
            // A style's leader distance left empty: the project's (forum #78).
            if (NULLABLE.has(c.key) && t === '') { put(null); return; }
            if (numeric) { const n = parseDecimal(t, { min: 0 }); if (n === null) ev.target.value = v == null ? '' : String(v); else put(n); }
            else if (t || c.key === 'inner' || c.key === 'classes') put(t);
          }} />;
      }
    }
  };

  // A marker size typed in the Project tab: empty clears it (the default), a number within its range sets it.
  const markOption = (input: HTMLInputElement, key: MarkKey, was: number | undefined, min: number, max: number) => {
    const t = input.value.trim();
    if (t === '') { if (was != null) void execute('standards.options', { [key]: null }); return; }
    const n = parseDecimal(t, { min, max });
    if (n === null) input.value = was == null ? '' : String(was);
    else if (n !== was) void execute('standards.options', { [key]: n });
  };
  const rows = book[tab.list] as Entry[];
  const options = (
    <dl className="fields standards-options">
      <dt title="Lengths are shown and typed in this unit everywhere; the engine keeps metres">Units</dt>
      <dd><select value={String(book.units ?? 'mm')} onChange={(e) => void execute('standards.options', { units: e.target.value })}>
        {[['mm', 'millimetres'], ['cm', 'centimetres'], ['m', 'metres'], ['ft-in', 'feet and inches (12\'-3 1/2")'], ['in', 'inches']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select></dd>
      {/* The step the areas of spaces are shown to, half up (report #76, decision 471): stamps, legends, the room schedule, documents; the model and IFC keep the exact area. */}
      <dt title="Room areas in plans, sheets, schedules and documents, rounded half up (as the Nordic countries write them: x,0 or x,5 m²); the model, quantities and IFC keep the exact area">Area rounding</dt>
      <dd><select data-field="areaRounding" value={String(book.areaRounding ?? '')} onChange={(e) => void execute('standards.options', { areaRounding: e.target.value ? Number(e.target.value) : null })}>
        {[['', 'none (to 0.01 m²)'], ['0.1', 'to 0.1 m²'], ['0.5', 'to 0.5 m²'], ['1', 'to 1 m²']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select></dd>
      {/* Dimensions drop their trailing zeros (2.500 → 2.5) unless their style keeps them (forum #81). */}
      <dt title="Dimension values drop their trailing zeros (2.500 → 2.5, 3.000 → 3), unless a dimension style keeps them">Suppress trailing zeros</dt>
      <dd><input type="checkbox" data-field="suppressZeros" checked={book.suppressZeros === true} onChange={(e) => void execute('standards.options', { suppressZeros: e.target.checked })} /></dd>
      {/* Levels (+3.00) wherever a drawing writes one: their decimals and trailing zeros (follow-up to forum #81). */}
      <dt title="Level marks, spot levels and level dimensions, base and survey points, terrain levels: the decimals they are written with (metres)">Level decimals</dt>
      <dd><select data-field="levelDecimals" value={String(book.levelDecimals ?? 2)} onChange={(e) => void execute('standards.options', { levelDecimals: Number(e.target.value) })}>
        {['0', '1', '2', '3', '4'].map((v) => <option key={v} value={v}>{v === '0' ? '+3' : `+${(3).toFixed(Number(v))}`}</option>)}
      </select></dd>
      <dt title="Whether levels drop their trailing zeros (+3.150 → +3.15, +3.00 → +3) or keep them; as the project: as Suppress trailing zeros says">Level trailing zeros</dt>
      <dd><select data-field="levelZeros" value={String(book.levelZeros ?? 'project')} onChange={(e) => void execute('standards.options', { levelZeros: e.target.value })}>
        {[['project', 'as the project'], ['keep', 'keep (+3.150)'], ['suppress', 'suppress (+3.15)']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select></dd>
      {/* Forum topic 192, decision 749: what a new door's, window's or opening's sill is measured from (the architecture module's own project setting). */}
      {openingSillFrom != null && <>
        <dt title="What the sill of a new door, window or wall opening is measured from: the wall's structural base, or the finished floor above it (set on the wall or its wall type). Each opening keeps its own; this is what new ones take">Door and window sills measured from</dt>
        <dd><select data-field="openingSillFrom" value={openingSillFrom} onChange={(e) => { const v = e.target.value; setOpeningSillFrom(v); void execute('architecture.setOpeningDefaults', { sillFrom: v }); }}>
          <option value="structural">Structural level</option><option value="finished">Finished floor</option>
        </select></dd>
      </>}
      <dt title="How lines of true weight end and meet, on screen and on paper: round, or square caps with mitred corners">Line ends</dt>
      <dd><select value={String(book.lineCap ?? 'round')} onChange={(e) => void execute('standards.options', { lineCap: e.target.value })}>
        <option value="round">round</option><option value="square">square (mitred)</option>
      </select></dd>
      {/* The project's standard scales, offered by every view's scale picker, and the scale each kind of new view takes (forum #30, decision 492). */}
      <dt title="The scales every view's scale picker offers (its corner, beside the zoom); empty: the usual ones">Standard scales</dt>
      <dd><input data-field="standard-scales" key={`scales${(book.scales ?? []).join(',')}`} defaultValue={(book.scales ?? []).join(', ')} placeholder={(book.scaleChoices ?? []).join(', ')}
        onBlur={(e) => {
          const list = scaleList(e.target.value);
          if (list.join(',') !== (book.scales ?? []).join(',')) void execute('standards.options', { scales: list.length ? list : null });
        }} /></dd>
      {SCALE_ROWS.map((row) => {
        const was = book[row.key];
        return [
          <dt key={`${row.key}t`} title="The scale a new view of this kind takes; a view's own, in its corner or its properties, changes it afterwards">{row.label}</dt>,
          <dd key={`${row.key}d`}><input className="num" data-field={row.field} key={`${row.key}${was ?? ''}`} defaultValue={was == null ? '' : String(was)} placeholder="100"
            onBlur={(e) => {
              const t = e.target.value.trim().replace(/^1[:/]/, '');
              if (t === '') { if (was != null) void execute('standards.options', { [row.key]: null }); return; }
              const n = parseDecimal(t, { min: 1, max: 10000 });
              if (n === null) e.target.value = was == null ? '' : String(was);
              else if (n !== was) void execute('standards.options', { [row.key]: n });
            }} /></dd>,
        ];
      })}
      {/* The section markers' and the elevation markers' symbol size and text height, each kind its own (decisions 455, 460); a marker's own, in its properties, goes before. */}
      {MARK_ROWS.map((row) => {
        const was = book[row.key];
        const standard = Number(book.textStyles.find((t) => t.id === 'standard')?.height ?? 2.5);
        return [
          <dt key={`${row.key}t`} title={row.title}>{row.label}</dt>,
          <dd key={`${row.key}d`}><input className="num" data-field={row.field} key={`${row.key}${was ?? ''}`} defaultValue={was == null ? '' : String(was)} placeholder={row.size ? '10' : String(standard)}
            onBlur={(e) => markOption(e.target, row.key, was, row.size ? 1 : 0.5, row.size ? 50 : 20)} /> mm</dd>,
        ];
      })}
      <MarkerSymbolsStandards />
      <FrameLinesStandards book={book as unknown as Record<string, unknown>} />
    </dl>
  );
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Drafting standards">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Drafting standards</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="tabs-inline">
            {TABS.map((t) => <button key={t.id} type="button" className={`tab-chip${t.id === tab.id && !project && !lines && !tables ? ' on' : ''}`} onClick={() => { setTab(t); setProject(false); setLines(false); setTables(false); setPack(null); }}>{t.label}</button>)}
            <button type="button" className={`tab-chip${lines ? ' on' : ''}`} data-field="standards-tab-lines" onClick={() => { setLines(true); setProject(false); setTables(false); setPack(null); }}>Line types</button>
            {/* The drafting tables' styles of the user's own (issue #71, stage 3). */}
            <button type="button" className={`tab-chip${tables ? ' on' : ''}`} data-field="standards-tab-tables" onClick={() => { setTables(true); setLines(false); setProject(false); setPack(null); }}>Tables</button>
            <button type="button" className={`tab-chip${project ? ' on' : ''}`} onClick={() => { setProject(true); setLines(false); setTables(false); setPack(null); }}>Project</button>
            {/* How materials draw in cuts, plans and facades: the material standards (stage 3 of the material graphics). */}
            <button type="button" className="tab-chip" data-field="standards-tab-materials" title="How materials draw in cuts, plans and facades: Lapin's, ISO, DIN, BS, your own or the store's" onClick={() => useUi.setState({ dialog: 'materialStandards' })}>Materials</button>
            <span className="grow" />
            <StandardsLibraryButtons onRead={setPack} />
          </div>
          {pack ? <StandardsImportPanel read={pack} onDone={() => setPack(null)} /> : lines ? <LineTypesTab /> : tables ? <TableStylesTab styles={book.tableStyles ?? []} /> : project ? options : tab.id === 'tag' && rows.some((r) => r.id === marker)
            ? <MarkerEditor entry={rows.find((r) => r.id === marker) as unknown as MarkerEntry} onClose={() => setMarker(null)} />
            : tab.id === 'tag' && rows.some((r) => r.id === editing && r.shape === 'buildup')
            ? <BuildupEditor entry={rows.find((r) => r.id === editing) as unknown as BuildupEntry} onClose={() => setEditing(null)} />
            : tab.id === 'tag' && rows.some((r) => r.id === editing)
            ? <LeaderEditor entry={rows.find((r) => r.id === editing) as unknown as LeaderEntry} onClose={() => setEditing(null)} />
            : <>
          <table className="standards-table">
            <thead><tr>{tab.id === 'axis' && <th title="New axes and the BIM grids use it">Default</th>}{tab.cols.map((c) => <th key={c.key} title={c.title}>{c.label}</th>)}{tab.id === 'tag' && <th />}<th /></tr></thead>
            <tbody>
              {rows.map((e) => (
                <tr key={e.id} className={e.id === focus ? 'lit' : undefined} data-focused={e.id === focus ? true : undefined} ref={e.id === focus ? focused : undefined}>
                  {tab.id === 'axis' && <td><input type="radio" name="default-axis" checked={book.defaultAxisStyle === e.id} onChange={() => set({ id: e.id }, { default: true })} /></td>}
                  {tab.cols.map((c) => <td key={c.key}>{cell(e, c)}</td>)}
                  {tab.id === 'tag' && <td>{e.shape === 'buildup'
                    ? <button type="button" className={`btn mini${editing === e.id ? ' on' : ''}`} data-field="tag-shape-buildup" title="Its header, line per layer, footer, order, leaders and width, with a preview" onClick={() => setEditing(editing === e.id ? null : e.id)}>Build-up…</button>
                    : <>{e.shape === 'opening'
                      ? <button type="button" className="btn mini" data-field={`tag-marker-${e.id}`} title="Its layout: the line across the wall, or a line from the frame's middle to a bubble with the mark, texts over and under it" onClick={() => setMarker(e.id)}>Marker…</button>
                      : <button type="button" className="btn mini" data-field="tag-leader" title="Its leader: straight from a box, or an elbow under the text, with a preview" onClick={() => setEditing(e.id)}>Leader…</button>}
                      {/* A door's or window's marker too (issue #72): the symbol at its line's exterior end, instead of its outer text. */}
                      <span title={e.shape === 'opening'
                        ? "A symbol of your own drawn at the marker's exterior end instead of its outer text: its texts' {fields} read the door or window ({mark}, {width}, {height}, {sill}), as the template's do (Manage → Symbols… to draw one)"
                        : "A symbol of your own drawn instead of the text: its texts' {fields} read the element, as the template's do (Manage → Symbols… to draw one)"}>
                        <SymbolPicker field={`tag-symbol-${e.id}`} value={typeof e.symbol === 'string' ? e.symbol : null} list={symbols} category="annotation" noneLabel="Text only"
                          onChange={(id) => set({ id: e.id, symbol: id })} />
                      </span>
                      {/* Any field the element has, added to the text (decision 737), as the title blocks' fields are. */}
                      <span className="tag-field-picker" title="Add a field to the text: any parameter, the storey's, the type's, a quantity">
                        <FieldPicker value="" placeholder="+ Field…" testId={`tag-field-${e.id}`}
                          onPick={(t) => set({ id: e.id, template: `${String(e.template ?? '')}${t.startsWith('{') ? t : `{${t}}`}` })} />
                      </span>
                      {systems.length > 0 && (
                        <select className="mini" value="" data-field="tag-class-field" title="Add a classification field to the text: {classification.code}, {classification.title}, or a system's own code"
                          onChange={(ev) => { if (ev.target.value) set({ id: e.id, template: `${String(e.template ?? '')}${e.template ? ' ' : ''}{${ev.target.value}}` }); }}>
                          <option value="">+ Classification…</option>
                          {classificationFields(systems).map(([k, l]) => <option key={k} value={k} data-no-i18n={k.split('.').length > 1 && k !== 'classification.code' && k !== 'classification.title' ? true : undefined}>{l}</option>)}
                        </select>
                      )}</>}</td>}
                  <td className="row-tools">{!fixed(e) && <button type="button" className="icon mini" title="Remove" onClick={() => void execute('standards.remove', { kind: tab.id, id: e.id })}><Trash2 size={12} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {tab.id === 'tag' && <p className="muted small" data-tag-help>{TAG_HELP}</p>}
          {tab.id === 'tag' && <p className="muted small" data-tag-build-help>{TAG_BUILD_HELP}</p>}
          {tab.id === 'tag' && <p className="muted small">{BUILDUP_HELP}</p>}
          {tab.id === 'layer' && <p className="muted small" data-layer-help>{LAYER_HELP}</p>}
          {tab.id === 'tag' && systems.length > 0 && <p className="muted small">{'A tag may also read the classification: {classification.code}, {classification.title} or a system\'s own code, {classification.<System>}; the + Classification… list adds one.'}</p>}
          <div className="row">
            <button type="button" className="btn" onClick={() => set({ ...tab.fresh, name: `${tab.fresh.name} ${rows.length + 1}` })}><Plus size={14} />New</button>
            {/* A ready-made door or window marker after a national convention (forum topic 205), added as the project's own. */}
            {tab.id === 'tag' && presets.length > 0 && (
              <select className="mini" value="" data-field="tag-preset" title="Add a ready-made door or window marker after a national convention, as your own to change"
                onChange={(ev) => { if (ev.target.value) void execute('standards.addTagPreset', { preset: ev.target.value }); }}>
                <option value="">+ Built-in marker…</option>
                {presets.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            )}
            {tab.id === 'layer' && (
              <>
                <span className="grow" />
                <span className="muted small">Groups:</span>
                {book.layerGroups.map((g) => (
                  <span key={g.id} className="chip">{g.name}
                    <button type="button" className="icon mini" title="Remove the group (its layers stay)" onClick={() => void execute('standards.remove', { kind: 'layerGroup', id: g.id })}><X size={10} /></button>
                  </span>
                ))}
                <input value={group} placeholder="New group" onChange={(e) => setGroup(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter' && group.trim()) { void execute('standards.set', { kind: 'layerGroup', entry: { name: group.trim() } }); setGroup(''); } }} />
              </>
            )}
          </div>
          {/* The tag rules (issue #72): per class, a condition and the tag type it gives, the first that holds winning. */}
          {tab.id === 'tag' && <TagRulesEditor rules={book.tagRules ?? []} types={rows} />}
          </>}
          {!pack && !lines && !tables && <StandardsFirmFolders onRead={setPack} />}
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
      </div>
    </div>
  );
}

/**
 * The drafting tables' styles of the user's own (issue #71, stage 3): each saved from a table's editor (Save as style…), its
 * name changed or removed here; offered beside the built-in presets when a table is made or restyled, and carried by a
 * standards file with the rest. A style kept with its cells is a template: a new table copies it whole.
 */
function TableStylesTab({ styles }: { styles: { id: string; name: string; cells?: boolean; table?: { rows?: unknown[]; cols?: unknown[] } }[] }) {
  return (
    <>
      <table className="standards-table" data-testid="table-styles">
        <thead><tr><th>Name</th><th>Size</th><th title="A new table in this style copies its cells too">With its cells</th><th /></tr></thead>
        <tbody>
          {styles.map((s) => (
            <tr key={s.id}>
              <td><input key={s.name} defaultValue={s.name} data-no-i18n
                onBlur={(e) => { const name = e.target.value.trim(); if (name && name !== s.name) void execute('standards.set', { kind: 'table', entry: { id: s.id, name } }); }}
                onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /></td>
              <td data-no-i18n>{`${s.table?.rows?.length ?? 0} × ${s.table?.cols?.length ?? 0}`}</td>
              <td><input type="checkbox" checked={!!s.cells} onChange={(e) => void execute('standards.set', { kind: 'table', entry: { id: s.id, cells: e.target.checked } })} /></td>
              <td className="row-tools"><button type="button" className="icon mini" title="Remove" onClick={() => void execute('standards.remove', { kind: 'table', id: s.id })}><Trash2 size={12} /></button></td>
            </tr>
          ))}
        </tbody>
      </table>
      {styles.length === 0 && <p className="muted small">No table styles yet.</p>}
      <p className="muted small">Save a table as a style from its editor (Save as style…): its look, its columns' widths and rows' heights, its header rows and, if asked, its cells. The built-in presets stay as they are.</p>
    </>
  );
}
