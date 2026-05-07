import { HttpInterceptorFn } from '@angular/common/http';

export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req.clone({
    withCredentials: true,
    setHeaders: { 'X-CSRF': '1' },
  }));
};
