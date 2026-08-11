import { includesVietnamese, normalizeVietnamese } from './vietnamese-search';

describe('Vietnamese search', () => {
  it('matches text with or without Vietnamese accents and casing', () => {
    const values = ['HS-01', 'Nguyễn Đăng An', 'Bé Đậu'];

    expect(includesVietnamese(values, 'nguyen')).toBeTrue();
    expect(includesVietnamese(values, 'NGUYỄN')).toBeTrue();
    expect(includesVietnamese(values, 'be dau')).toBeTrue();
    expect(includesVietnamese(values, 'hs-01')).toBeTrue();
  });

  it('collapses whitespace and normalizes đ', () => {
    expect(normalizeVietnamese('  Bé   Đậu  ')).toBe('be dau');
  });
});
