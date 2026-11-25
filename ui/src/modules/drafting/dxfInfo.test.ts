/** The DXF import dialog's first question: the engine's answer, or why there is none, never neither (forum topic 217). */

import { describe, expect, it, vi } from 'vitest';

vi.mock('../../engine/session', () => ({ engine: { call: vi.fn() } }));
vi.mock('../../engine/client', () => ({ LONG_TIMEOUT_MS: 600_000 }));

import { engine } from '../../engine/session';
import { NO_ANSWER, readDxfInfo } from './dxfInfo';

const call = engine.call as unknown as ReturnType<typeof vi.fn>;

describe('reading a DXF for the import dialog', () => {
  it('asks the engine with the long wait and gives its answer', async () => {
    call.mockResolvedValueOnce({ version: 'AC1032' });
    const r = await readDxfInfo<{ version: string }>({ name: 'plan.dxf', path: 'C:\\Výkresy\\plan.dxf' }, 'v1');
    expect(r).toEqual({ info: { version: 'AC1032' } });
    expect(call).toHaveBeenCalledWith('doc.query', { name: 'drafting.dxfInfo', args: { name: 'plan.dxf', path: 'C:\\Výkresy\\plan.dxf', viewId: 'v1' } }, { timeoutMs: 600_000 });
  });

  it('says why when the engine refuses the file', async () => {
    call.mockRejectedValueOnce(new Error('This is a binary DXF.'));
    expect(await readDxfInfo({ name: 'b.dxf', path: '/b.dxf' }, 'v1')).toEqual({ problem: NO_ANSWER, detail: 'This is a binary DXF.' });
  });

  it('says so when the engine does not answer in time, or answers nothing', async () => {
    call.mockRejectedValueOnce(new Error('The engine did not answer doc.query within 600 s.'));
    expect(await readDxfInfo({ name: 'big.dxf', path: '/big.dxf' }, 'v1')).toMatchObject({ problem: NO_ANSWER });
    call.mockResolvedValueOnce(null);
    expect(await readDxfInfo({ name: 'x.dxf', data: 'AA==' }, 'v1')).toEqual({ problem: NO_ANSWER });
  });
});

describe('what the dialog says, in every language', () => {
  it('translates the general sentence and the engine’s reasons, the block named kept', async () => {
    const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
    await loadAllLanguages();
    const block = 'The drawing unfolds into more than 2,000,000 entities through its block „Okno-1” (blocks inserted inside blocks, or an array insert), more than Lapin brings in at once. Purge or explode what is not needed in the CAD program, then import it again.';
    const phrases = [
      NO_ANSWER,
      'This is a binary DXF; Lapin reads DXF files saved as text (ASCII). Save it again as DXF without the Binary option (in AutoCAD: Save As → Tools → Options → DXF Options), then import it.',
      'This is a DWG file, not a DXF. Save it as DXF (in AutoCAD: Save As → AutoCAD DXF), then import it.',
      'This file is not a DXF Lapin can read: it has no DXF sections (HEADER, ENTITIES…).',
      'The drawing has more than 2,000,000 entities, more than Lapin brings in at once. Split it, or purge what is not needed in the CAD program, then import it again.',
    ];
    for (const { id } of LANGUAGES.filter((l) => l.id !== 'en')) {
      for (const p of phrases) expect(translate(p, id), `${id}: ${p}`).toBeTruthy();
      expect(translate(block, id), id).toContain('Okno-1');
    }
    // Loading every language takes about 5 s on a busy machine, the default limit.
  }, 30000);
});
