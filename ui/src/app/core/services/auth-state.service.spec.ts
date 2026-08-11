import { AuthStateService } from './auth-state.service';

describe('AuthStateService', () => {
  const user = {
    id: '7dfe9bc6-d78e-4b1d-879d-f3b01e6f63ba',
    email: 'admin@example.com',
    fullName: 'Admin',
    role: 'Admin' as const,
    status: 'Active' as const
  };

  it('keeps tokens in memory and clears the complete session', () => {
    const state = new AuthStateService();
    state.setSession('access-token', user, 'csrf-token');

    expect(state.accessToken).toBe('access-token');
    expect(state.csrfToken).toBe('csrf-token');
    expect(state.user).toEqual(user);

    state.clear();
    expect(state.accessToken).toBeNull();
    expect(state.csrfToken).toBeNull();
    expect(state.user).toBeNull();
  });
});
