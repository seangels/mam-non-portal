import { UserRole } from './core/models/api.models';

export interface NavigationItem {
  text: string;
  path?: string;
  icon?: string;
  items?: NavigationItem[];
}

export function buildNavigation(role?: UserRole): NavigationItem[] {
  const items: NavigationItem[] = [
    { text: 'Tổng quan', path: '/home', icon: 'home' },
    { text: 'Điểm danh', path: '/attendance', icon: 'check' }
  ];

  if (role === 'SuperAdmin' || role === 'Admin') {
    items.push(
      { text: 'Tài khoản', path: '/users', icon: 'group' },
      { text: 'Học sinh', path: '/students', icon: 'card' },
      { text: 'Nhóm', path: '/student-groups', icon: 'hierarchy' }
    );
  }

  return items;
}
