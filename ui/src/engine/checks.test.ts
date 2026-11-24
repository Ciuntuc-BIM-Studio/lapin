import { describe, expect, it, vi } from 'vitest';
import {
  PT_PER_MM, badMember, bboxToFractions, bboxToPixels, checksDone, describeFinding, factText, fileLevel, findingsBySeverity, fitScale, onPage,
  parseExpected, pollChecks, splitLines, splitList, tryNaming, type ChecksReport, type Finding,
} from './checks';

const report = (status: ChecksReport['status']): ChecksReport => ({ status, run: 1, requested: null, requestedBy: null, finished: null, error: null, summary: null });
const finding = (o: Partial<Finding>): Finding => ({ check: 'V1', code: 'V1-01', severity: 'error', message: 'English.', page: null, bbox: null, field: null, details: null, ...o });

describe('the poller', () => {
  it('reads until the checks are no longer pending, waiting longer each time', async () => {
    const answers = [report('pending'), report('pending'), report('pending'), report('done')];
    const get = vi.fn(async () => ({ id: 's1', checks: answers.shift()! }));
    const waits: number[] = [];
    const seen: string[] = [];
    const s = await pollChecks(get, { delays: [100, 200, 400], sleep: async (ms) => { waits.push(ms); }, onUpdate: (x) => seen.push(x.checks!.status) });
    expect(s?.checks?.status).toBe('done');
    expect(get).toHaveBeenCalledTimes(4);
    expect(waits).toEqual([100, 200, 400]);
    expect(seen).toEqual(['pending', 'pending', 'pending', 'done']);
  });

  it('ends on failed, and at once for a submission without checks (an older server)', async () => {
    expect((await pollChecks(async () => ({ checks: report('failed') }), { sleep: async () => {} }))?.checks?.status).toBe('failed');
    const get = vi.fn(async () => ({ checks: null }));
    expect(await pollChecks(get, { sleep: async () => {} })).toEqual({ checks: null });
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('stops when the dialog closes, and gives up after its time', async () => {
    const stop = new AbortController();
    let n = 0;
    const s = await pollChecks(async () => { if (++n === 2) stop.abort(); return { checks: report('pending') }; }, { signal: stop.signal, sleep: async () => {} });
    expect(s).toBeNull();
    expect(n).toBe(2);
    let t = 0;
    const late = await pollChecks(async () => ({ checks: report('pending') }), { sleep: async (ms) => { t += ms; }, now: () => t, maxWaitMs: 5000, delays: [1000] });
    expect(late).toBeNull();
    expect(t).toBe(5000);
  });

  it('rides over a few failed reads, then says the error', async () => {
    let n = 0;
    const s = await pollChecks(async () => { if (++n < 3) throw new Error('offline'); return { checks: report('done') }; }, { sleep: async () => {} });
    expect(s?.checks?.status).toBe('done');
    await expect(pollChecks(async () => { throw new Error('gone'); }, { sleep: async () => {}, retries: 2 })).rejects.toThrow('gone');
  });

  it('the real wait ends early when stopped', async () => {
    const stop = new AbortController();
    const p = pollChecks(async () => ({ checks: report('pending') }), { signal: stop.signal, delays: [60_000] });
    setTimeout(() => stop.abort(), 10);
    expect(await p).toBeNull();
  });
});

describe('a finding\'s box on the page', () => {
  it('is mm from the top-left corner × 72/25.4 × the scale (pdf.js\'s viewport)', () => {
    expect(PT_PER_MM).toBeCloseTo(2.834645669, 8);
    const b = bboxToPixels([25.4, 50.8, 76.2, 63.5], 1.5);
    expect(b.left).toBeCloseTo(108, 6);
    expect(b.top).toBeCloseTo(216, 6);
    expect(b.width).toBeCloseTo(216, 6);
    expect(b.height).toBeCloseTo(54, 6);
    // An A3 landscape page (420 × 297 mm) at scale 1 is 1190.55 × 841.89 points; its bottom-right corner is there.
    const corner = bboxToPixels([0, 0, 420, 297], 1);
    expect(corner.width).toBeCloseTo(1190.55, 2);
    expect(corner.height).toBeCloseTo(841.89, 2);
  });

  it('is ordered whichever corners come first, and as fractions of the page for any rendering', () => {
    expect(bboxToPixels([76.2, 63.5, 25.4, 50.8], 1)).toEqual(bboxToPixels([25.4, 50.8, 76.2, 63.5], 1));
    const f = bboxToFractions([42, 29.7, 84, 59.4], { width: 420, height: 297 });
    for (const v of [f.left, f.top, f.width, f.height]) expect(v).toBeCloseTo(0.1, 12);
  });

  it('fits a page into the width shown', () => {
    const s = fitScale(420, 760);
    expect(420 * PT_PER_MM * s).toBeCloseTo(760, 6);
    expect(fitScale(0, 760)).toBe(1);
  });

  it('draws on a page only the findings with a box there; the others are listed', () => {
    const fs = [finding({ page: 1, bbox: [1, 2, 3, 4] }), finding({ page: 1 }), finding({ page: 2, bbox: [1, 1, 2, 2] }), finding({ page: null })];
    expect(onPage(fs, 1)).toEqual({ drawn: [fs[0]], listed: [fs[1]] });
    expect(fileLevel(fs)).toEqual([fs[3]]);
  });
});

describe('the findings in words', () => {
  it('says each code from its code, field and details, not from the server\'s English', () => {
    const w = describeFinding(finding({ code: 'V1-01', message: 'ignored', details: { code: 'EL24-X', pattern: 'EL24-.*' } }));
    expect(w.text).toBe('The code does not match the project\'s naming rule.');
    expect(w.facts.map(factText)).toEqual(['Code: EL24-X', 'Naming rule: EL24-.*']);
    expect(describeFinding(finding({ code: 'V1-03', details: { revision: 'P02', submission: 'P03' } })).facts.map(factText)).toEqual(['In the name: P02', 'Submission: P03']);
    expect(describeFinding(finding({ code: 'V1-04', details: { revision: 'P01', issued: 'P03', tag: 'refs/tags/issue/4-P03' } })).facts.map(factText))
      .toEqual(['Revision: P01', 'Issued: P03', 'Issue tag: 4-P03']);
  });

  it('a field is named in words when it is the default title block\'s, by its name otherwise', () => {
    const w = describeFinding(finding({ code: 'V2-03', field: 'drawnBy' }));
    expect(w.text).toBe('A required title-block field is missing.');
    expect(w.facts).toEqual([{ label: 'Field', value: 'Drawn by', translate: true }]);
    expect(describeFinding(finding({ code: 'V2-03', field: 'specialitate' })).facts).toEqual([{ label: 'Field', value: 'specialitate' }]);
  });

  it('a severity-dependent code says which, and similarities are percentages', () => {
    expect(describeFinding(finding({ code: 'V1-05', severity: 'error', details: { revision: 'P01', expected: 'P02' } })).text).toBe('The revision is lower than the register expects.');
    expect(describeFinding(finding({ code: 'V1-05', severity: 'warning' })).text).toBe('The revision is higher than the register expects.');
    const v24 = describeFinding(finding({ code: 'V2-04', severity: 'warning', field: 'client', details: { expected: 'CNCF CFR SA', similarity: 0.874 } }));
    expect(v24.text).toMatch(/close to the expected one/);
    expect(v24.facts.map(factText)).toEqual(['Field: Client', 'Expected: CNCF CFR SA', 'Similarity: 87 %']);
    expect(describeFinding(finding({ code: 'V5-01', field: 'project', details: { expected: 'Electrificare', similarity: 0.41 } })).facts.map(factText))
      .toEqual(['Field: Project name', 'Expected: Electrificare', 'Similarity: 41 %']);
  });

  it('every code of the contract has its words', () => {
    const codes = ['R-01', 'R-02', 'R-03', ...Array.from({ length: 10 }, (_, i) => `V1-${String(i + 1).padStart(2, '0')}`), ...Array.from({ length: 8 }, (_, i) => `V2-0${i + 1}`), 'V3-01', 'V3-02', 'V4-01', 'V5-01', 'V5-02', 'V5-03', 'V5-04'];
    for (const code of codes) expect(describeFinding(finding({ code, message: 'SERVER' })).text, code).not.toBe('SERVER');
    // An unknown code (a newer server) keeps the server's English.
    expect(describeFinding(finding({ code: 'V9-01', message: 'Something new.' })).text).toBe('Something new.');
  });

  it('keywords, spans and other details', () => {
    expect(describeFinding(finding({ code: 'V5-04', severity: 'warning', details: { keyword: 'Caransebeș', inTitleBlock: true } })).facts)
      .toEqual([{ label: 'Keyword', value: 'Caransebeș' }, { label: 'In the title block', value: 'yes', translate: true }]);
    expect(describeFinding(finding({ code: 'V3-01', details: { objects: 3, span: 0.012 } })).facts.map(factText)).toEqual(['Objects: 3', 'Area covered: 1 %']);
    expect(describeFinding(finding({ code: 'V1-06', details: { tag: 'refs/tags/issue/2-P01', sha256: 'abcdef0123456789abcdef' } })).facts.map(factText)).toEqual(['Issue tag: 2-P01', 'SHA-256: abcdef012345']);
    expect(describeFinding(finding({ code: 'R-01', severity: 'warning', details: { reason: 'encrypted', extra: 2 } })).facts.map(factText)).toEqual(['Reason: encrypted', 'extra: 2']);
  });

  it('groups a file\'s findings by severity, by page then code', () => {
    const fs = [finding({ code: 'V4-01', page: 2 }), finding({ code: 'V5-04', severity: 'warning', page: 1 }), finding({ code: 'V1-08', page: 1 }), finding({ code: 'R-03', severity: 'info' })];
    const g = findingsBySeverity(fs);
    expect(g.error.map((f) => f.code)).toEqual(['V1-08', 'V4-01']);
    expect(g.warning.map((f) => f.code)).toEqual(['V5-04']);
    expect(g.info.map((f) => f.code)).toEqual(['R-03']);
  });

  it('reads a checks_done event\'s counts', () => {
    expect(checksDone('{"run":2,"status":"done","errors":1,"warnings":3,"infos":0}')).toEqual({ run: 2, status: 'done', errors: 1, warnings: 3, infos: 0 });
    expect(checksDone('not json')).toBeNull();
    expect(checksDone(null)).toBeNull();
  });
});

describe('the check settings', () => {
  it('tries the naming rule on a code as the server does: whole, with the project group', () => {
    const rule = '(?<project>EL24)-[A-Z]{3}-L2-[A-Z0-9]{2}-[A-Z]{2}-[A-Z]-\\d{4}';
    expect(tryNaming(rule, 'EL24-BVC-L2-XX-PL-E-0105')).toEqual({ matches: true, project: 'EL24', error: null });
    expect(tryNaming(rule, 'EL24-BVC-L2-XX-PL-E-0105-old').matches).toBe(false);
    expect(tryNaming('[A-Z]+-\\d+', 'PRJ-12')).toEqual({ matches: true, project: 'PRJ', error: null });
    expect(tryNaming('(', 'x').error).toBeTruthy();
    expect(tryNaming('', 'anything').matches).toBe(true);
  });

  it('names the setting a 400 is about from the server\'s message', () => {
    expect(badMember('naming does not compile: Invalid pattern')).toEqual({ key: 'naming' });
    expect(badMember('checks.futureDays is 0 to 3650.')).toEqual({ key: 'futureDays' });
    expect(badMember('checks.foreignKeywords: each is 1 to 100 characters.')).toEqual({ key: 'foreignKeywords' });
    expect(badMember('checks.titleBlock.region.width is a size in millimetres.')).toEqual({ key: 'titleBlock.region' });
    expect(badMember('checks.titleBlock.minLabels is 1 to 40.')).toEqual({ key: 'titleBlock.minLabels' });
    expect(badMember('checks.titleBlock.fields is an array of 1 to 40 fields.')).toEqual({ key: 'titleBlock.fields' });
    expect(badMember('Field date: zone is [x0, y0, x1, y1] with x1 > x0 and y1 > y0.')).toEqual({ key: 'titleBlock.fields', field: 'date' });
    expect(badMember('Two fields are named code.')).toEqual({ key: 'titleBlock.fields', field: 'code' });
    expect(badMember("A field's name is 1–40 letters, digits or _.")).toEqual({ key: 'titleBlock.fields' });
    expect(badMember('checks.enabled lists V1 to V5.')).toEqual({ key: 'enabled' });
    expect(badMember('Something else.')).toBeNull();
  });

  it('reads lists and expected values as typed', () => {
    expect(splitList('DRAWING NO, COD PLANȘĂ;  ,DWG NO\nDRAWING NO')).toEqual(['DRAWING NO', 'COD PLANȘĂ', 'DWG NO']);
    expect(splitLines('EL22\n Caransebeș, km 12 \n\nEL22')).toEqual(['EL22', 'Caransebeș, km 12']);
    expect(parseExpected('client = CNCF CFR SA\ndesigner: BIMVoice SRL\nbroken line\n= x')).toEqual({ client: 'CNCF CFR SA', designer: 'BIMVoice SRL' });
  });
});
