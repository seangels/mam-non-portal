import { ElementRef } from '@angular/core';
import { UserPanelComponent } from './user-panel.component';

describe('UserPanelComponent context menu', () => {
  it('targets the containing user button element', () => {
    const button = document.createElement('div');
    button.className = 'user-button';
    const host = document.createElement('app-user-panel');
    button.appendChild(host);

    const component = new UserPanelComponent(new ElementRef(host));

    expect(component.contextMenuTarget).toBe(button);
  });
});
