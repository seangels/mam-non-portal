import { Injectable } from '@angular/core';

@Injectable()
export class AppInfoService {
  constructor() {}

  public get title() {
    return 'Mầm Non Admin';
  }

  public get currentYear() {
    return new Date().getFullYear();
  }
}
