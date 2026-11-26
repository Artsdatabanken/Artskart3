import { ApplicationConfig, ErrorHandler, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { TitleStrategy } from '@angular/router';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { from, map, Observable, throwError } from 'rxjs';
import { ApplicationinsightsAngularpluginErrorService } from '@microsoft/applicationinsights-angularplugin-js';
import { routes } from './app.routes';
import { languageInterceptor } from './shared/interceptors/language.interceptor';
import { csrfInterceptor } from './shared/interceptors/csrf.interceptor';
import { anonymousUserInterceptor } from './shared/interceptors/anonymous-user.interceptor';
import { unauthorizedInterceptor } from './shared/interceptors/unauthorized.interceptor';
import { LanguageService, SupportedLanguage } from './shared/services/languages/language.service';
import { LoggingService } from './shared/logging.service';
import { AreasService } from './core/services/areas/areas.service';
import { TranslatedTitleStrategy } from './shared/router/translated-title.strategy';

// Imported rather than fetched so each language becomes its own content-hashed chunk.
const translationFiles: Record<SupportedLanguage, () => Promise<{ default: TranslationObject }>> = {
  no: () => import('../assets/languages/no.json'),
  en: () => import('../assets/languages/en.json')
};

class BundledTranslateLoader implements TranslateLoader {
  getTranslation(lang: string): Observable<TranslationObject> {
    if (!Object.hasOwn(translationFiles, lang)) {
      return throwError(() => new Error(`No translation file for language '${lang}'`));
    }
    return from(translationFiles[lang as SupportedLanguage]()).pipe(map((module) => module.default));
  }
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withXhr(), withInterceptors([languageInterceptor, csrfInterceptor, anonymousUserInterceptor, unauthorizedInterceptor])),
    provideTranslateService({
      fallbackLang: 'no',
      loader: {
        provide: TranslateLoader,
        useClass: BundledTranslateLoader
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
