import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LanguageService } from '../services/languages/language.service';


@Injectable()
export class LanguageInterceptor implements HttpInterceptor {
  constructor(private languageService: LanguageService) {}

  intercept(
    request: HttpRequest<any>,
    next: HttpHandler
  ): Observable<HttpEvent<any>> {
    const currentLanguage = this.languageService.getLanguage();

    const languageRequest = request.clone({
      setHeaders: {
        'Accept-Language': currentLanguage
      }
    });

    return next.handle(languageRequest);
  }
}
