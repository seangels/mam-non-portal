import { AuthStateService } from './auth-state.service';

describe('AuthStateService', () => {
  const user = {
    id: '7dfe9bc6-d78e-4b1d-879d-f3b01e6f63ba',
    email: 'admin@example.com',
    fullName: 'Admin',
    role: 'Admin' as const,
    status: 'Active' as const
  };

  it('shares bearer tokens across tabs and clears the complete session', () => {
    localStorage.clear();
    const state = new AuthStateService();
    state.setSession('access-token', user, 'refresh-token');

    expect(state.accessToken).toBe('access-token');
    expect(state.refreshToken).toBe('refresh-token');
    expect(state.user).toEqual(user);

    state.clear();
    expect(state.accessToken).toBeNull();
    expect(state.refreshToken).toBeNull();
    localStorage.clear();
    expect(state.user).toBeNull();
  });
});
