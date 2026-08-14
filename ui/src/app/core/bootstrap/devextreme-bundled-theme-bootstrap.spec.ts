import { bootstrapWithBundledDevExtremeTheme } from './devextreme-bundled-theme-bootstrap';

describe('DevExtreme bundled-theme bootstrap', () => {
  it('starts Angular immediately because DevExtreme 19 has no dynamic theme link to await', async () => {
    let bootstrapCalls = 0;

    const result = bootstrapWithBundledDevExtremeTheme(() => {
      bootstrapCalls += 1;
      return Promise.resolve('started');
    });

    expect(bootstrapCalls).toBe(1);
    await expectAsync(result).toBeResolvedTo('started');
  });
});
