import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageService } from '../services/languages/language.service';

export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  const lang = inject(LanguageService).getLanguage();
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
