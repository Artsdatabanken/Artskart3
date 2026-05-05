import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { LanguageService, SupportedLanguage } from '../../services/languages/language.service';

export interface MenuItem {
  label: string;
  href: string;
  ariaLabel?: string;
}

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.css']
})
export class HeaderComponent implements OnInit, OnDestroy {
  @Input() projectName: string = 'Artskart';
  @Input() menuItems: MenuItem[] = [
    { label: 'Menypunkt 1', href: '#', ariaLabel: 'Go to Menypunkt 1' },
    { label: 'Menypunkt 2', href: '#', ariaLabel: 'Go to Menypunkt 2' },
    { label: 'Menypunkt 3', href: '#', ariaLabel: 'Go to Menypunkt 3' }
  ];

  isMenuOpen = false;
  isLanguageMenuOpen = false;
  currentLanguage: SupportedLanguage = 'no';
  supportedLanguages: SupportedLanguage[] = [];

  languageNames: Record<SupportedLanguage, string> = {
    en: 'English',
    no: 'Norsk'
  };

  private destroy$ = new Subject<void>();

  constructor(private languageService: LanguageService) {}

  ngOnInit(): void {
    this.languageService.getLanguage$()
      .pipe(takeUntil(this.destroy$))
      .subscribe(lang => {
        this.currentLanguage = lang;
        this.isLanguageMenuOpen = false;
      });

    this.supportedLanguages = this.languageService.getSupportedLanguages();
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
}


