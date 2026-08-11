import { formatDateOnly, fromDateOnly, toDateOnly } from './date-only';

describe('DateOnly helpers', () => {
  it('keeps an API date-only string unchanged', () => {
    expect(toDateOnly('2026-08-01')).toBe('2026-08-01');
  });

  it('creates and formats a local calendar date without UTC conversion', () => {
    const value = fromDateOnly('2026-08-01');
    expect(value.getFullYear()).toBe(2026);
    expect(value.getMonth()).toBe(7);
    expect(value.getDate()).toBe(1);
    expect(toDateOnly(value)).toBe('2026-08-01');
    expect(formatDateOnly('2026-08-01')).toBe('1/8/2026');
  });
});
