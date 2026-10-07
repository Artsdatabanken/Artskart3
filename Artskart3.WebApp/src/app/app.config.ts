import { ApplicationConfig, ErrorHandler, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { HttpBackend, HttpClient, provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { TitleStrategy } from '@angular/router';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import { ApplicationinsightsAngularpluginErrorService } from '@microsoft/applicationinsights-angularplugin-js';
import { routes } from './app.routes';
import { languageInterceptor } from './shared/interceptors/language.interceptor';
import { csrfInterceptor } from './shared/interceptors/csrf.interceptor';
import { unauthorizedInterceptor } from './shared/interceptors/unauthorized.interceptor';
import { LanguageService } from './shared/services/languages/language.service';
import { LoggingService } from './shared/logging.service';
import { AreasService } from './core/services/areas/areas.service';
import { TranslatedTitleStrategy } from './shared/router/translated-title.strategy';

class CustomTranslateLoader implements TranslateLoader {
  private http: HttpClient;

  constructor(handler: HttpBackend) {
    // HttpClient created from HttpBackend bypasses all interceptors,
    // avoiding circular dependency with languageInterceptor
    this.http = new HttpClient(handler);
  }

  getTranslation(lang: string): Observable<TranslationObject> {
    return this.http.get<TranslationObject>(`/assets/languages/${lang}.json`);
  }
}

export function HttpLoaderFactory(handler: HttpBackend): TranslateLoader {
  return new CustomTranslateLoader(handler);
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withXhr(), withInterceptors([languageInterceptor, csrfInterceptor, unauthorizedInterceptor])),
    provideTranslateService({
      fallbackLang: 'no',
      loader: {
        provide: TranslateLoader,
        useFactory: HttpLoaderFactory,
        deps: [HttpBackend]
      }
    }),
    AreasService,
    {
      provide: TitleStrategy,
      useClass: TranslatedTitleStrategy
    },
    provideAppInitializer(() => inject(LanguageService).initialize()),
    LoggingService,
    {
      provide: ErrorHandler,
      useClass: ApplicationinsightsAngularpluginErrorService
    }
  ]
};
