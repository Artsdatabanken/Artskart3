import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Pipe({
  name: 'localeDateTime',
  pure: true,
})
export class LocaleDateTimePipe implements PipeTransform {
  private readonly translateService = inject(TranslateService);

  transform(value: string | null | undefined): string {
    if (!value) return '';
    const date = new Date(value);
    if (isNaN(date.getTime())) return '';
    const locale = this.translateService.currentLang === 'no' ? 'nb-NO' : 'en-GB';
    return new Intl.DateTimeFormat(locale, {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  }
}
