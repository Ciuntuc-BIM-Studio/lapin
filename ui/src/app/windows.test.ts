import { afterEach, describe, expect, it } from 'vitest';
import { useSectionBox } from '../viewport/sectionBox';
import { mirrorSectionBox } from './windows';

const box = { min: [0, 0, 0] as [number, number, number], max: [4, 4, 3] as [number, number, number] };

describe('the section box between windows', () => {
  afterEach(() => useSectionBox.setState({ box: null, hidden: false }));

  it('sends what changes here, and takes what comes from another window without sending it back', () => {
    const sent: unknown[] = [];
    const m = mirrorSectionBox((s) => sent.push(s));
    useSectionBox.setState({ box });
    expect(sent).toEqual([{ box, hidden: false }]);
    m.apply({ box: null, hidden: false });
    expect(useSectionBox.getState().box).toBeNull();
    expect(sent).toHaveLength(1);
    m.apply({ box, hidden: true });
    expect(useSectionBox.getState()).toMatchObject({ box, hidden: true });
    expect(sent).toHaveLength(1);
    m.stop();
  });
});
