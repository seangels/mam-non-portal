export function normalizeVietnamese(value: string): string {
  return value
    .toLocaleLowerCase('vi-VN')
    .replace(/đ/g, 'd')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

export function includesVietnamese(haystack: Array<string | null | undefined>, search: string): boolean {
  const normalizedSearch = normalizeVietnamese(search);
  if (!normalizedSearch) {
    return true;
  }
  return haystack.some(value => normalizeVietnamese(value ?? '').includes(normalizedSearch));
}
