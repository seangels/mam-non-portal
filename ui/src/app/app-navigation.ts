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
    { text: 'Điểm danh', path: '/attendance', icon: 'check' },
    { text: 'DS Đánh giá', path: '/assessments', icon: 'paste' }
  ];

  if (role === 'SuperAdmin' || role === 'Admin') {
    items.push(
      { text: 'Giáo viên', path: '/teachers', icon: 'user' },
      { text: 'Học sinh', path: '/students', icon: 'card' },
      { text: 'Nhóm', path: '/student-groups', icon: 'hierarchy' }
    );
  }

  if (role === 'SuperAdmin') {
    items.splice(items.length, 0, { text: 'Tài khoản quản trị', path: '/users', icon: 'group' });
  }


  return items;
}
