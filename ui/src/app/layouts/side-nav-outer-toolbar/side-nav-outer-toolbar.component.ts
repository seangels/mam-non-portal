import { Component, OnInit, NgModule, Input, ViewChild } from '@angular/core';
import { SideNavigationMenuModule, HeaderModule } from '../../shared/components';
import { AuthService, ScreenService } from '../../shared/services';
import { DxDrawerModule } from 'devextreme-angular/ui/drawer';
import { DxScrollViewModule, DxScrollViewComponent } from 'devextreme-angular/ui/scroll-view';
import { CommonModule } from '@angular/common';
import { buildNavigation, NavigationItem } from '../../app-navigation';
import {
  DrawerOpenedStateMode,
  DrawerRevealMode,
  TreeViewItemClickEvent
} from '../../core/models/devextreme-legacy.types';

import { Router, NavigationEnd } from '@angular/router';
import { UserRole } from 'src/app/core/models/api.models';

@Component({
  selector: 'app-side-nav-outer-toolbar',
  templateUrl: './side-nav-outer-toolbar.component.html',
  styleUrls: ['./side-nav-outer-toolbar.component.scss']
})
export class SideNavOuterToolbarComponent implements OnInit {
  @ViewChild(DxScrollViewComponent, { static: true }) scrollView!: DxScrollViewComponent;
  selectedRoute = '';

  menuOpened!: boolean;
  temporaryMenuOpened = false;

  @Input()
  title!: string;
  pageTitle?: string = '';

  menuMode: DrawerOpenedStateMode = 'shrink';
  menuRevealMode: DrawerRevealMode = 'expand';
  minMenuSize = 0;
  shaderEnabled = false;

  constructor(
    private screen: ScreenService,
    private router: Router,
    private auth: AuthService
  ) { }
  private currentRole?: UserRole;
  ngOnInit() {
    this.auth.user$.subscribe(user => this.currentRole = user?.role);

    this.menuOpened = this.screen.sizes['screen-large'];

    this.router.events.subscribe(val => {
      if (val instanceof NavigationEnd) {
        this.selectedRoute = val.urlAfterRedirects.split('?')[0];
        this.pageTitle = this.resolveTitle(this.selectedRoute);
      }
    });

    this.screen.changed.subscribe(() => this.updateDrawer());

    this.updateDrawer();
  }

  private resolveTitle(path: string): string {
    const flatten = (items: NavigationItem[]): NavigationItem[] =>
      items.flatMap(i => (i.items ? [i, ...flatten(i.items)] : [i]));

    const match = flatten(buildNavigation(this.currentRole)).find(i => i.path === path);
    return match?.text ?? '';
  }

  updateDrawer() {
    const isXSmall = this.screen.sizes['screen-x-small'];
    const isLarge = this.screen.sizes['screen-large'];

    this.menuMode = isLarge ? 'shrink' : 'overlap';
    this.menuRevealMode = isXSmall ? 'slide' : 'expand';
    this.minMenuSize = isXSmall ? 0 : 60;
    this.shaderEnabled = !isLarge;
  }

  get hideMenuAfterNavigation() {
    return this.menuMode === 'overlap' || this.temporaryMenuOpened;
  }

  get showMenuAfterClick() {
    return !this.menuOpened;
  }

  navigationChanged(event: TreeViewItemClickEvent<NavigationItem>) {
    const path = event.itemData?.path;
    const pointerEvent = event.event;

    if (path && this.menuOpened) {
      if (event.node?.selected) {
        pointerEvent?.preventDefault?.();
      } else {
        this.router.navigate([path]);
        this.scrollView.instance.scrollTo(0);
      }

      if (this.hideMenuAfterNavigation) {
        this.temporaryMenuOpened = false;
        this.menuOpened = false;
        pointerEvent?.stopPropagation?.();
      }
    } else {
      pointerEvent?.preventDefault?.();
    }
  }

  navigationClick() {
    if (this.showMenuAfterClick) {
      this.temporaryMenuOpened = true;
      this.menuOpened = true;
    }
  }
}

@NgModule({
  imports: [ SideNavigationMenuModule, DxDrawerModule, HeaderModule, DxScrollViewModule, CommonModule ],
  exports: [ SideNavOuterToolbarComponent ],
  declarations: [ SideNavOuterToolbarComponent ]
})
export class SideNavOuterToolbarModule { }
