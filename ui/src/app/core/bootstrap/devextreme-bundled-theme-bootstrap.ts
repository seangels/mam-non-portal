/**
 * DevExtreme 19 initializes a statically bundled theme while its module is
 * evaluated. With no dynamic `link[rel=dx-theme]`, there is no later theme
 * callback to await before starting Angular.
 */
export function bootstrapWithBundledDevExtremeTheme<T>(bootstrap: () => Promise<T>): Promise<T> {
  return bootstrap();
}
