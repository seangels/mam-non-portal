import { Component, NgModule, Output, Input, EventEmitter, ElementRef, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { buildNavigation, NavigationItem } from '../../../app-navigation';
import { TreeViewItemClickEvent } from '../../../core/models/devextreme-legacy.types';
import { AuthService } from '../../services';
import { Subscription } from 'rxjs';

import * as events from 'devextreme/events';

@Component({
  selector: 'app-side-navigation-menu',
  templateUrl: './side-navigation-menu.component.html',
  styleUrls: ['./side-navigation-menu.component.scss']
})
export class SideNavigationMenuComponent implements AfterViewInit, OnDestroy {
  @Output()
  selectedItemChanged = new EventEmitter<TreeViewItemClickEvent<NavigationItem>>();

  @Output()
  openMenu = new EventEmitter<any>();

  private _selectedItem!: String;
  @Input()
  set selectedItem(value: String) {
    this._selectedItem = value;
  }

  get selectedItemValue(): String {
    return this._selectedItem;
  }

  private readonly userSubscription: Subscription;
  private _items: NavigationItem[] = [];
  get items() {
    return this._items;
  }

  private _compactMode = false;
  @Input()
  get compactMode() {
    return this._compactMode;
  }
  set compactMode(val) {
    this._compactMode = val;
  }

  constructor(private elementRef: ElementRef, private readonly auth: AuthService) {
    this.userSubscription = this.auth.user$.subscribe(user => {
      this._items = buildNavigation(user?.role).map(item => ({ ...item, expanded: !this._compactMode }));
    });
  }

  onNavigationClick(event: MouseEvent, item: NavigationItem): void {
    this.selectedItemChanged.emit({ itemData: item, event });
  }

  ngAfterViewInit() {
    events.on(this.elementRef.nativeElement, 'dxclick', (e: Event) => {
      this.openMenu.next(e);
    });
  }

  ngOnDestroy() {
    events.off(this.elementRef.nativeElement, 'dxclick');
    this.userSubscription.unsubscribe();
  }
}

@NgModule({
  imports: [ CommonModule, RouterModule ],
  declarations: [ SideNavigationMenuComponent ],
  exports: [ SideNavigationMenuComponent ]
})
export class SideNavigationMenuModule { }
