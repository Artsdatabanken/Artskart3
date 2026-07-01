import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);

  return authService.getSession().pipe(
    map((session) => {
      if (session !== null) {
        return true;
      }
      authService.login();
      return false;
    }),
  );
};
