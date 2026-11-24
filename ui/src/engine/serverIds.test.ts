import { beforeEach, describe, expect, it, vi } from 'vitest';

const tab = new Map<string, string>();
vi.stubGlobal('window', {
  sessionStorage: { getItem: (k: string) => tab.get(k) ?? null, setItem: (k: string, v: string) => void tab.set(k, v), removeItem: (k: string) => void tab.delete(k) },
  location: { origin: 'https://cde.example.org' },
});
const fetch = vi.fn();
vi.stubGlobal('fetch', (...a: unknown[]) => fetch(...a));
vi.mock('./session', () => ({ engineInBrowser: true }));
const remoteCall = vi.fn();
vi.mock('./remote', () => ({ remoteCall: (...a: unknown[]) => remoteCall(...a) }));
const cde = await import('./cdeServer');
const atrium = await import('./atrium');
const ids = await import('./serverIds');
type Finding = import('./checks').Finding;

const answer = (status: number, body: unknown) => ({ ok: status < 300, status, headers: new Headers({ 'content-type': 'application/json' }), json: async () => body }) as unknown as Response;
const DESK = 'https://other.example.org';

beforeEach(() => {
  fetch.mockReset(); remoteCall.mockReset(); tab.clear();
  cde.resetCdeForTests();
  cde.keepCdeTokens('A1', 'R1');
  atrium.forgetFeatures();
});

const finding = (code: string, severity: Finding['severity'], details: Record<string, unknown>, message = 'The server says so.'): Finding =>
  ({ check: 'IDS', code, severity, message, page: null, bbox: null, field: null, details });

describe('IDS on the server (W6c)', () => {
  it('shows only when the server lists ids among its features', async () => {
    remoteCall.mockResolvedValueOnce({ status: 200, body: { features: ['submissions', 'checks', 'register'] }, etag: null });
    expect((await atrium.features(DESK)).includes('ids')).toBe(false);
    atrium.forgetFeatures();
    remoteCall.mockResolvedValueOnce({ status: 200, body: { features: ['submissions', 'checks', 'register', 'ids'] }, etag: null });
    expect((await atrium.features(DESK)).includes('ids')).toBe(true);
    atrium.forgetFeatures();
    remoteCall.mockResolvedValueOnce({ status: 404, body: null, etag: null });
    expect(await atrium.features(DESK)).toEqual([]);
  });

  it('puts an IDS file as its bytes: the page\'s fetch in front of the server, remote.atrium with base64 on the desktop', async () => {
    cde.useCde.setState({ server: 'https://cde.example.org' });
    const xml = new TextEncoder().encode('<ids/>');
    fetch.mockImplementationOnce(async (url: string, init?: RequestInit) => {
      expect(url).toBe('/atrium/1.0/projects/p%201/ids/LOIN%20arhitectur%C4%83.ids');
      expect(init?.method).toBe('PUT');
      expect((init?.headers as Record<string, string>)['content-type']).toBe('application/xml');
      expect(init?.body).toBe(xml);
      return answer(201, { name: 'LOIN arhitectură.ids', specifications: 3 });
    });
    expect((await ids.putIds('https://cde.example.org', 'p 1', 'LOIN arhitectură.ids', xml)).specifications).toBe(3);
    remoteCall.mockResolvedValueOnce({ status: 200, body: { name: 'a.ids', specifications: 1 }, etag: null });
    await ids.putIds(DESK, 'p1', 'a.ids', xml);
    expect(remoteCall).toHaveBeenCalledWith('remote.atrium', { url: DESK, method: 'PUT', path: 'atrium/1.0/projects/p1/ids/a.ids', data: btoa('<ids/>'), contentType: 'application/xml' }, DESK);
  });

  it('a refused IDS file keeps its problems, each with its line', async () => {
    remoteCall.mockResolvedValueOnce({ status: 400, body: { code: 'bad_ids', message: 'Not IDS 1.0.', problems: ['Line 12: The cardinality „sometimes” is not one of required, optional, prohibited.', 'No line here.'] }, etag: null });
    const e = await ids.putIds(DESK, 'p1', 'bad.ids', new Uint8Array([60])).catch((x) => x);
    expect(e).toBeInstanceOf(atrium.AtriumError);
    expect(e.code).toBe('bad_ids');
    expect(e.message).toBe('Not an IDS 1.0 file.');
    expect(ids.problemLines(e.body.problems)).toEqual([
      { line: 12, text: 'The cardinality „sometimes” is not one of required, optional, prohibited.' },
      { line: null, text: 'No line here.' },
    ]);
    expect(ids.problemLines(undefined)).toEqual([]);
  });

  it('lists, views, deletes and dry-runs by the server\'s paths', async () => {
    remoteCall.mockResolvedValue({ status: 200, body: { files: [], settings: { enabled: true, blocking: true }, specifications: [], commit: 'c', ids: null }, etag: null });
    await ids.listIds(DESK, 'p1');
    await ids.viewIds(DESK, 'p1', 'a b.ids');
    await ids.deleteIds(DESK, 'p1', 'a b.ids');
    await ids.dryRunIds(DESK, 'p1', 'abc123');
    expect(remoteCall.mock.calls.map((c) => [c[1].method, c[1].path])).toEqual([
      ['GET', 'atrium/1.0/projects/p1/ids'], ['GET', 'atrium/1.0/projects/p1/ids/a%20b.ids?view=json'],
      ['DELETE', 'atrium/1.0/projects/p1/ids/a%20b.ids'], ['POST', 'atrium/1.0/projects/p1/ids/run?commit=abc123'],
    ]);
  });

  it('a switch sends the whole ids object to PUT rules, the other member kept', async () => {
    expect(atrium.idsRule({ ids: { enabled: true, blocking: true } }, { blocking: false })).toEqual({ ids: { enabled: true, blocking: false } });
    expect(atrium.idsRule({ ids: { enabled: false, blocking: false } }, { enabled: true })).toEqual({ ids: { enabled: true, blocking: false } });
    expect(atrium.idsRule({}, { enabled: false })).toEqual({ ids: { enabled: false, blocking: true } });
    remoteCall.mockResolvedValueOnce({ status: 200, body: { role: 'owner', ids: { enabled: true, blocking: false } }, etag: null });
    await atrium.putRules(DESK, 'p1', atrium.idsRule({ ids: { enabled: true, blocking: true } }, { blocking: false }));
    expect(remoteCall).toHaveBeenCalledWith('remote.atrium', { url: DESK, method: 'PUT', path: 'atrium/1.0/projects/p1/rules', body: { ids: { enabled: true, blocking: false } } }, DESK);
  });

  it('says each code from its details, the server\'s English only for a code it does not know', () => {
    expect(ids.describeIds(finding('IDS-02', 'info', { commit: '0123456789abcdef' }))).toEqual({
      text: 'The commit has no IFC copy on the server: IDS did not run on the model.', facts: [{ label: 'Commit', value: '01234567' }],
    });
    expect(ids.describeIds(finding('IDS-03', 'warning', { model: 'file', name: 'A.ifc', reason: 'Not an IFC STEP file.' })).facts)
      .toEqual([{ label: 'Model', value: 'A.ifc' }, { label: 'Reason', value: 'Not an IFC STEP file.' }]);
    expect(ids.describeIds(finding('IDS-04', 'warning', { model: 'viewer', name: null, size: 300 * 1048576, limit: 256 * 1048576 })).facts)
      .toEqual([{ label: 'Model', value: 'IFC copy of the commit', translate: true }, { label: 'Size', value: '300 MB / 256 MB' }]);
    expect(ids.describeIds(finding('IDS-05', 'warning', { model: 'viewer', name: null, seconds: 120, checked: 3, total: 5 })).facts[1]).toEqual({ label: 'Checked', value: '3 / 5 · 120 s' });
    expect(ids.describeIds(finding('IDS-06', 'warning', { ids: 'old.ids', problems: ['Line 2: x', 'Line 3: y'] }))).toEqual({
      text: 'A stored IDS file no longer reads: it was not run.', facts: [{ label: 'IDS file', value: 'old.ids' }, { label: 'Problems', value: 'Line 2: x · Line 3: y' }],
    });
    expect(ids.describeIds(finding('IDS-99', 'warning', {}, 'Something new.')).text).toBe('Something new.');
  });

  it('groups the report by IDS file and model, each failing specification with its IDS-01 details, the notices apart', () => {
    const spec = (index: number, status: 'pass' | 'fail' | 'skipped') => ({ index, name: `S${index}`, identifier: null, usage: 'required', status, applicable: 4, passed: status === 'fail' ? 1 : 4, failed: status === 'fail' ? 3 : 0 });
    const fail = (model: 'viewer' | 'file', name: string | null) => finding('IDS-01', 'error', {
      ids: 'L.ids', model, name, specification: { index: 1, name: 'S1' }, requirements: ['Property Pset_WallCommon.FireRating (IFCLABEL)'],
      applicable: 4, passed: 1, failed: 3, reason: null, globalIds: ['g1', 'g2'], globalIdsTotal: 3, more: 1,
      failures: [{ globalId: 'g1', class: 'IFCWALL', name: 'Perete', requirement: 'r', actual: 'no Pset_WallCommon.FireRating', expected: 'Pset_WallCommon.FireRating as IFCLABEL' }],
    });
    const g = ids.idsGroups({
      enabled: true, blocking: true, files: [{ name: 'L.ids', title: null, specifications: 3 }],
      results: [
        { ids: 'L.ids', model: 'viewer', name: null, specifications: [spec(0, 'pass'), spec(1, 'fail'), spec(2, 'skipped')] },
        { ids: 'L.ids', model: 'file', name: 'A.ifc', specifications: [spec(0, 'pass'), spec(1, 'fail')] },
      ],
      summary: { specifications: 5, passed: 2, failed: 2, skipped: 1 },
      findings: [fail('file', 'A.ifc'), finding('IDS-03', 'warning', { model: 'file', name: 'B.ifc', reason: 'x' }), fail('viewer', null)],
    });
    expect(g.notices.map((f) => f.code)).toEqual(['IDS-03']);
    expect(g.results[0].specs.map((s) => s.fail != null)).toEqual([false, true, false]);
    const v = g.results[0].specs[1].fail!;
    expect([v.finding.details?.model, v.globalIds, v.globalIdsTotal, v.more, v.failures[0].expected]).toEqual(['viewer', ['g1', 'g2'], 3, 1, 'Pset_WallCommon.FireRating as IFCLABEL']);
    expect(g.results[1].specs[1].fail?.finding.details?.name).toBe('A.ifc');
  });
});
