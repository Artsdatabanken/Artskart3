import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';

export type SupportedLanguage = 'en' | 'no';

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private readonly STORAGE_KEY = 'artskart_language';
  private readonly DEFAULT_LANGUAGE: SupportedLanguage = 'no';
  private readonly SUPPORTED_LANGUAGES: SupportedLanguage[] = ['en', 'no'];

  private currentLanguage$ = new BehaviorSubject<SupportedLanguage>(this.DEFAULT_LANGUAGE);

  constructor(private translate: TranslateService) {
    this.translate.setDefaultLang('no');
    this.translate.addLangs(['en', 'no']);
  }

  initialize(): Observable<any> {
    const savedLanguage = this.getSavedLanguage();
    const language = this.isLanguageSupported(savedLanguage) ? savedLanguage : this.getDefaultLanguage();
    this.translate.use('no');

    return this.setLanguage(language);
  }

  setLanguage(lang: SupportedLanguage): Observable<any> {
    if (!this.isLanguageSupported(lang)) {
      lang = this.DEFAULT_LANGUAGE;
    }

    this.currentLanguage$.next(lang);
    this.saveLanguage(lang);

    return this.translate.use(lang).pipe(
      catchError(() => {
        return of({});
      })
    );
  }

  getLanguage(): SupportedLanguage {
    return this.currentLanguage$.value;
  }

  getLanguage$(): Observable<SupportedLanguage> {
    return this.currentLanguage$.asObservable();
  }

  getSupportedLanguages(): SupportedLanguage[] {
    return [...this.SUPPORTED_LANGUAGES];
  }

  isLanguageSupported(lang: any): lang is SupportedLanguage {
    return this.SUPPORTED_LANGUAGES.includes(lang);
  }

  private getDefaultLanguage(): SupportedLanguage {
    const browserLang = (this.translate.getBrowserLang() || '').toLowerCase();
    return this.isLanguageSupported(browserLang) ? (browserLang as SupportedLanguage) : this.DEFAULT_LANGUAGE;
  }

  private saveLanguage(lang: SupportedLanguage): void {
    try {
      localStorage.setItem(this.STORAGE_KEY, lang);
    } catch (e) {
      console.warn('Failed to save language preference to localStorage:', e);
    }
  }

  private getSavedLanguage(): SupportedLanguage | null {
    try {
      return localStorage.getItem(this.STORAGE_KEY) as SupportedLanguage | null;
    } catch (e) {
      console.warn('Failed to retrieve language preference from localStorage:', e);
      return null;
    }
  }
}
