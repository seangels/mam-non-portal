import { Injectable } from '@angular/core';
import { CanDeactivate } from '@angular/router';

export interface PendingChangesAware {
  canLeave(): boolean | Promise<boolean>;
}

@Injectable({ providedIn: 'root' })
export class PendingChangesGuard implements CanDeactivate<PendingChangesAware> {
  canDeactivate(component: PendingChangesAware): boolean | Promise<boolean> {
    return component.canLeave();
  }
}
