import { Component, Input, OnInit, OnDestroy, CUSTOM_ELEMENTS_SCHEMA, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { LanguageService, SupportedLanguage } from '../../services/languages/language.service';
import { AuthService } from '../../services/auth/auth.service';

export interface MenuItem {
  label: string;
  href: string;
  ariaLabel?: string;
}

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.css']
})
export class HeaderComponent implements OnInit, OnDestroy {
  @Input() projectName = 'Artskart';
  @Input() menuItems: MenuItem[] = [];

  isMenuOpen = false;
  isLanguageMenuOpen = false;
  isDarkMode = false;
  currentLanguage: SupportedLanguage = 'no';
  supportedLanguages: SupportedLanguage[] = [];

  languageNames: Record<SupportedLanguage, string> = {
    en: 'English',
    no: 'Norsk'
  };

  private destroy$ = new Subject<void>();

  private readonly languageService = inject(LanguageService);
  readonly authService = inject(AuthService);

  ngOnInit(): void {
    this.languageService.getLanguage$()
      .pipe(takeUntil(this.destroy$))
      .subscribe(lang => {
        this.currentLanguage = lang;
        this.isLanguageMenuOpen = false;
      });

    this.supportedLanguages = this.languageService.getSupportedLanguages();

    const stored = localStorage.getItem('theme');
    const prefersDark =
      stored === 'dark' ||
      (!stored && window.matchMedia('(prefers-color-scheme: dark)').matches);
    this.isDarkMode = prefersDark;
    document.documentElement.setAttribute('data-theme', prefersDark ? 'dark' : 'light');
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  toggleBurgerMenu(): void {
    this.isMenuOpen = !this.isMenuOpen;
    if (this.isMenuOpen) {
      this.isLanguageMenuOpen = false;
    }
  }

  closeBurgerMenu(): void {
    this.isMenuOpen = false;
  }

  onMenuItemClick(): void {
    if (this.isMenuOpen) {
      this.closeBurgerMenu();
    }
  }

  toggleLanguageMenu(): void {
    this.isLanguageMenuOpen = !this.isLanguageMenuOpen;
  }

  closeLanguageMenu(): void {
    this.isLanguageMenuOpen = false;
  }

  changeLanguage(lang: SupportedLanguage): void {
    this.languageService.setLanguage(lang)
      .pipe(takeUntil(this.destroy$))
      .subscribe(
        () => {
          this.closeLanguageMenu();
        }
      );
  }

  getLanguageName(lang: SupportedLanguage): string {
    return this.languageNames[lang] || lang;
  }

  toggleDarkMode(): void {
    this.isDarkMode = !this.isDarkMode;
    if (this.isDarkMode) {
      document.documentElement.setAttribute('data-theme', 'dark');
      localStorage.setItem('theme', 'dark');
    } else {
      document.documentElement.setAttribute('data-theme', 'light');
      localStorage.setItem('theme', 'light');
    }
  }
}


