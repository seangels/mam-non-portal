import { Injectable } from '@angular/core';

@Injectable()
export class AppInfoService {
  constructor() {}

  public get title() {
    return 'Cổng quản trị mầm non';
  }

  public get currentYear() {
    return new Date().getFullYear();
  }
}
