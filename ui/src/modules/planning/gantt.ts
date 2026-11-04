/**
 * The Gantt's arithmetic (docs/plan-4d5d.md, stage 1), apart from React so it can be tested: dates as day numbers, the
 * time scale and its two header rows by zoom, the rows a collapsed WBS leaves, where a bar stands, what a drag moves it
 * to, a link's type from the ends it joins, and the elbowed path of its arrow. The schedule itself (dates, floats, the
 * critical path) is the engine's; the interface only draws it and says what the user did.
 */

export type LinkType = 'FS' | 'SS' | 'FF' | 'SF';

export interface TaskDto {
  id: string; row: number; name: string; wbs: string; level: number; parent: string | null;
  summary: boolean; milestone: boolean; duration: number; start: string; finish: string; lateStart: string; lateFinish: string;
  totalFloat: number; freeFloat: number; critical: boolean; predecessors: string;
  constraint?: 'snet' | 'mso'; constraintDate?: string; wet?: boolean; kind?: string; notes?: string;
}
export interface LinkDto { id: string; from: string; to: string; type: LinkType; lag: number; critical: boolean }
export interface CalendarException { from: string; to: string; working: boolean; name: string }
export interface CalendarDto {
  id: string | null; name: string; days: number[]; dayStart: string; hours: number; holidays: 'ro' | 'none';
  exceptions: CalendarException[]; winter: { on: boolean; from: string; to: string };
}
export interface ScheduleDto {
  id: string | null; name: string; start: string; finish: string; dataDate: string | null; workingDays: number;
  calendar: CalendarDto; nonWorking: { date: string; why: string }[]; tasks: TaskDto[]; links: LinkDto[]; problem: string | null;
}

// ── Dates ────────────────────────────────────────────────────────────────────

const DAY = 86_400_000;

/** A date (2026-11-09) as its day number (days since 1970-01-01, UTC). */
export function dayOf(iso: string): number {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return Math.round(Date.UTC(y, m - 1, d) / DAY);
}

/** A day number as its date (2026-11-09). */
export function dateOf(day: number): string {
  return new Date(day * DAY).toISOString().slice(0, 10);
}

/** The ISO weekday of a day number: 1 Monday … 7 Sunday (1970-01-01 was a Thursday). */
export const weekday = (day: number) => ((((day + 3) % 7) + 7) % 7) + 1;

/** Today as a day number, in the user's own time zone. */
export function today(now = new Date()): number {
  return Math.round(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) / DAY);
}

/** A date as the interface shows it: 09.11.2026 (day first, as in Romania and most of Europe). */
export function showDate(iso: string | null | undefined): string {
  if (!iso) return '';
  const [y, m, d] = iso.slice(0, 10).split('-');
  return `${d}.${m}.${y}`;
}

/** A date typed by the user (09.11.2026, 9.11.26, 2026-11-09, 9/11/2026) as ISO, or null. */
export function parseDate(text: string): string | null {
  const t = text.trim();
  let m = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t);
  if (m) return check(+m[1], +m[2], +m[3]);
  m = /^(\d{1,2})[./-](\d{1,2})[./-](\d{2}|\d{4})$/.exec(t);
  if (m) return check(m[3].length === 2 ? 2000 + +m[3] : +m[3], +m[2], +m[1]);
  return null;
}

function check(y: number, m: number, d: number): string | null {
  const day = new Date(Date.UTC(y, m - 1, d));
  if (day.getUTCFullYear() !== y || day.getUTCMonth() !== m - 1 || day.getUTCDate() !== d) return null;
  return day.toISOString().slice(0, 10);
}

// ── The calendar, as the interface needs it while dragging ───────────────────

/** The calendar's working days as a test of a day number: the exceptions first, then the days the engine listed not worked, then the weekdays; a wet task skips the winter break. */
export function workingTest(cal: CalendarDto, nonWorking: { date: string }[]): (day: number, wet?: boolean) => boolean {
  const off = new Set(nonWorking.map((x) => dayOf(x.date)));
  const worked = cal.exceptions.filter((x) => x.working).map((x) => [dayOf(x.from), dayOf(x.to)] as const);
  const winter = cal.winter.on ? [md(cal.winter.from), md(cal.winter.to)] as const : null;
  return (day, wet = false) => {
    if (wet && winter && inWinter(day, winter[0], winter[1])) return false;
    if (worked.some(([a, b]) => day >= a && day <= b)) return true;
    if (off.has(day)) return false;
    return cal.days.includes(weekday(day));
  };
}

const md = (s: string) => { const [m, d] = s.split('-').map(Number); return m * 100 + d; };

function inWinter(day: number, from: number, to: number): boolean {
  const d = new Date(day * DAY);
  const x = (d.getUTCMonth() + 1) * 100 + d.getUTCDate();
  return from <= to ? x >= from && x < to : x >= from || x < to;
}

/** The working days from one day to another, both counted (0 when the end comes first). */
export function workingDaysBetween(works: (day: number) => boolean, from: number, to: number): number {
  let n = 0;
  for (let d = from; d <= to; d++) if (works(d)) n++;
  return n;
}

// ── The time scale ───────────────────────────────────────────────────────────

/** The zooms the toolbar offers, in pixels a day. */
export const ZOOMS = { day: 28, week: 10, month: 3, quarter: 1.1 } as const;
export type Zoom = keyof typeof ZOOMS;
export const MIN_PX = 0.4, MAX_PX = 60;

/** The header's tiers for a scale: days under weeks, weeks under months, months under years, quarters under years. */
export function tierOf(pxPerDay: number): Zoom {
  if (pxPerDay >= 16) return 'day';
  if (pxPerDay >= 5) return 'week';
  if (pxPerDay >= 1.6) return 'month';
  return 'quarter';
}

export interface Tick { from: number; to: number; label: string }

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const WEEKDAYS = ['M', 'T', 'W', 'T', 'F', 'S', 'S'];

/** The names a header writes: the months (0 January) and the weekdays' letters (1 Monday); English unless the interface's language gives its own. */
export interface Names { month: (m: number) => string; weekday: (iso: number) => string }
export const ENGLISH: Names = { month: (m) => MONTHS[m], weekday: (d) => WEEKDAYS[d - 1] };

/** The month and weekday names of a language (ro: ian., L), from the browser's own tables. */
export function namesOf(lang: string): Names {
  if (lang === 'en') return ENGLISH;
  try {
    const mf = new Intl.DateTimeFormat(lang, { month: 'short', timeZone: 'UTC' });
    const wf = new Intl.DateTimeFormat(lang, { weekday: 'narrow', timeZone: 'UTC' });
    const months = Array.from({ length: 12 }, (_, m) => mf.format(new Date(Date.UTC(2026, m, 15))).replace(/\.$/, ''));
    // 5 January 2026 was a Monday.
    const days = Array.from({ length: 7 }, (_, i) => wf.format(new Date(Date.UTC(2026, 0, 5 + i))).toUpperCase());
    return { month: (m) => months[m], weekday: (d) => days[d - 1] };
  } catch {
    return ENGLISH;
  }
}

function ymd(day: number) { const d = new Date(day * DAY); return { y: d.getUTCFullYear(), m: d.getUTCMonth(), d: d.getUTCDate() }; }
const monthStart = (y: number, m: number) => Math.round(Date.UTC(y, m, 1) / DAY);

/** The header's two rows of ticks between two days (the first ending after `from`, the last starting before `to`). */
export function ticks(pxPerDay: number, from: number, to: number, names: Names = ENGLISH): { top: Tick[]; bottom: Tick[] } {
  const tier = tierOf(pxPerDay);
  const months = (): Tick[] => {
    const o: Tick[] = [];
    let { y, m } = ymd(from);
    for (let s = monthStart(y, m); s < to; ) {
      const next = monthStart(m === 11 ? y + 1 : y, (m + 1) % 12);
      o.push({ from: s, to: next, label: tier === 'month' ? names.month(m) : `${names.month(m)} ${y}` });
      s = next; if (++m === 12) { m = 0; y++; }
    }
    return o;
  };
  const years = (): Tick[] => {
    const o: Tick[] = [];
    for (let y = ymd(from).y; monthStart(y, 0) < to; y++) o.push({ from: monthStart(y, 0), to: monthStart(y + 1, 0), label: `${y}` });
    return o;
  };
  const weeks = (labelDay: boolean): Tick[] => {
    const o: Tick[] = [];
    for (let s = from - (weekday(from) - 1); s < to; s += 7) {
      const { d, m } = ymd(s);
      o.push({ from: s, to: s + 7, label: labelDay ? `${d} ${names.month(m)}` : `${d}` });
    }
    return o;
  };
  switch (tier) {
    case 'day': {
      const bottom: Tick[] = [];
      for (let s = from; s < to; s++) bottom.push({ from: s, to: s + 1, label: `${names.weekday(weekday(s))}${ymd(s).d}` });
      return { top: weeks(true), bottom };
    }
    case 'week': return { top: months(), bottom: weeks(false) };
    case 'month': return { top: years(), bottom: months() };
    default: {
      const bottom: Tick[] = [];
      for (let y = ymd(from).y; monthStart(y, 0) < to; y++) for (let q = 0; q < 4; q++) bottom.push({ from: monthStart(y, q * 3), to: monthStart(q === 3 ? y + 1 : y, (q * 3 + 3) % 12), label: `Q${q + 1}` });
      return { top: years(), bottom };
    }
  }
}

/** The days the chart spans: a little before the start and after the finish, and today. */
export function span(s: Pick<ScheduleDto, 'start' | 'finish'>, now = today()): { from: number; to: number } {
  const a = Math.min(dayOf(s.start), now), b = Math.max(dayOf(s.finish), now);
  return { from: a - 7 - (weekday(a - 7) - 1), to: b + 45 };
}

/** The scale that shows the whole schedule in a width (pixels a day, within the zooms' bounds). */
export function fitScale(s: Pick<ScheduleDto, 'start' | 'finish'>, width: number): number {
  const days = Math.max(7, dayOf(s.finish) - dayOf(s.start) + 14);
  return Math.min(MAX_PX, Math.max(MIN_PX, width / days));
}

// ── Rows and bars ────────────────────────────────────────────────────────────

/** The tasks shown: those with no collapsed summary above them, in row order. */
export function visibleRows(tasks: TaskDto[], collapsed: ReadonlySet<string>): TaskDto[] {
  if (collapsed.size === 0) return tasks;
  const parent = new Map(tasks.map((t) => [t.id, t.parent]));
  const hidden = (t: TaskDto) => { for (let p = t.parent; p; p = parent.get(p) ?? null) if (collapsed.has(p)) return true; return false; };
  return tasks.filter((t) => !hidden(t));
}

export interface Bar { x: number; w: number; y: number; h: number }

/** Where a task's bar stands: from the start of its first day to the end of its last, in its row (a milestone a point at its day's start). */
export function barOf(t: Pick<TaskDto, 'start' | 'finish' | 'milestone'>, row: number, origin: number, pxPerDay: number, rowH: number): Bar {
  const x = (dayOf(t.start) - origin) * pxPerDay;
  const w = t.milestone ? 0 : (dayOf(t.finish) + 1 - dayOf(t.start)) * pxPerDay;
  const h = Math.round(rowH * 0.5);
  return { x, w, y: row * rowH + Math.round((rowH - h) / 2), h };
}

/** A drag along the time scale in whole days. */
export const dragDays = (dx: number, pxPerDay: number) => Math.round(dx / pxPerDay);

/** A bar dragged by `days`: its new start (a summary, a milestone moved the same way). */
export const movedStart = (t: Pick<TaskDto, 'start'>, days: number) => dateOf(dayOf(t.start) + days);

/** A bar's end dragged by `days`: its new last day, never before its first. */
export const movedFinish = (t: Pick<TaskDto, 'start' | 'finish'>, days: number) => dateOf(Math.max(dayOf(t.start), dayOf(t.finish) + days));

/** Which end of a bar a point is nearer: the start's half or the finish's. */
export const endAt = (bar: Bar, x: number): 'start' | 'finish' => (x < bar.x + bar.w / 2 ? 'start' : 'finish');

/** A link's type from the ends it joins: finish to start FS, start to start SS, finish to finish FF, start to finish SF. */
export function linkType(from: 'start' | 'finish', to: 'start' | 'finish'): LinkType {
  if (from === 'finish') return to === 'start' ? 'FS' : 'FF';
  return to === 'start' ? 'SS' : 'SF';
}

/**
 * A link's arrow as a polyline: out of its predecessor's end (left of a start, right of a finish), into its successor's
 * end from the side it faces, elbowed through the gap between the rows when the two ends overlap in time.
 */
export function arrowPoints(from: Bar, to: Bar, type: LinkType, rowH: number, out = 8): [number, number][] {
  const fromStart = type[0] === 'S', toStart = type[1] === 'S';
  const x1 = fromStart ? from.x : from.x + from.w, y1 = from.y + from.h / 2;
  const x2 = toStart ? to.x : to.x + to.w, y2 = to.y + to.h / 2;
  const bx = x1 + (fromStart ? -out : out);
  const ex = x2 + (toStart ? -out : out);
  if ((toStart && bx <= x2 - 2) || (!toStart && bx >= x2 + 2)) {
    // Room to turn straight down (or up) onto the successor's row.
    return [[x1, y1], [bx, y1], [bx, y2], [x2, y2]];
  }
  const midY = y2 > y1 ? to.y - (rowH - to.h) / 2 : to.y + to.h + (rowH - to.h) / 2;
  return [[x1, y1], [bx, y1], [bx, midY], [ex, midY], [ex, y2], [x2, y2]];
}

/** The visible slice of a long list of rows: the first and the last shown, with a few spare either side. */
export function rowWindow(scrollTop: number, height: number, rowH: number, count: number, spare = 6): [number, number] {
  const first = Math.max(0, Math.floor(scrollTop / rowH) - spare);
  const last = Math.min(count - 1, Math.ceil((scrollTop + height) / rowH) + spare);
  return [first, last];
}

/** A link's label in the predecessors' manner: FS, SS+2d, FF−1d. */
export function linkLabel(l: Pick<LinkDto, 'type' | 'lag'>): string {
  return `${l.type}${l.lag > 0 ? `+${l.lag}d` : l.lag < 0 ? `−${-l.lag}d` : ''}`;
}
