import { describe, expect, it } from 'vitest';
import { asksTrust, EMPTY_TRUST, remember, sha256, trustQuestion } from './trust';

describe('trust in a project’s scripts (decision 489, stage 3)', () => {
  it('hashes the code as the engine keeps it: UTF-8, SHA-256, lowercase hex', async () => {
    expect(await sha256('')).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
    expect(await sha256('print("ș")\n')).toMatch(/^[0-9a-f]{64}$/);
    expect(await sha256('a')).not.toBe(await sha256('a '));
  });

  it('asks before a project’s script runs, until its code is trusted; a change asks again', async () => {
    const project = { kind: 'project', id: 'S1', name: 'walls.py' } as const;
    const code = await sha256('x = 1\n');
    expect(asksTrust(project, code, EMPTY_TRUST)).toBe(true);
    // Run once remembers nothing.
    const once = remember(EMPTY_TRUST, code, 'once');
    expect(asksTrust(project, code, once)).toBe(true);
    const trusted = remember(EMPTY_TRUST, code, 'trust');
    expect(asksTrust(project, code, trusted)).toBe(false);
    expect(asksTrust(project, await sha256('x = 2\n'), trusted)).toBe(true);
    // The user's own scripts and new code never ask.
    expect(asksTrust({ kind: 'mine', name: 'a.py' }, code, EMPTY_TRUST)).toBe(false);
    expect(asksTrust({ kind: 'new' }, code, EMPTY_TRUST)).toBe(false);
  });

  it('says where the script came from', () => {
    expect(trustQuestion('house.lapin', 'walls.py')).toBe('„walls.py” came with the project „house.lapin”.');
    expect(trustQuestion(null, 'walls.py')).toBe('„walls.py” came with this project.');
  });
});
