import type DevExpress from 'devextreme/bundles/dx.all';
import type CustomStore from 'devextreme/data/custom_store';

export type DrawerOpenedStateMode = 'overlap' | 'shrink' | 'push';
export type DrawerRevealMode = 'slide' | 'expand';

export interface LegacyPointerEvent {
  preventDefault?(): void;
  stopPropagation?(): void;
}

export interface TreeViewItemClickEvent<TItem = unknown> {
  itemData?: TItem;
  node?: { selected?: boolean };
  event?: LegacyPointerEvent;
}

export interface ToolbarItemClickEvent {
  event?: LegacyPointerEvent;
}

export interface ListSelectionChangedEvent {
  component?: { option(name: string): unknown };
  value?: unknown;
}

/**
 * DevExtreme 19 widgets accept a CustomStore at runtime, but their Angular
 * input declarations omit Store from the dataSource union. Keep the original
 * store instance while exposing the compatible vendor DataSourceOptions type.
 */
export type LegacyWidgetDataSource = CustomStore & DevExpress.data.DataSourceOptions;

export function asLegacyWidgetDataSource(store: CustomStore): LegacyWidgetDataSource {
  return store as LegacyWidgetDataSource;
}
