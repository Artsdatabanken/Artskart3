import { Pipe, PipeTransform } from '@angular/core';
import { localeFromLanguage } from '@shared/helpers/locale/locale';

@Pipe({
  name: 'localeDate',
  pure: true,
})
export class LocaleDatePipe implements PipeTransform {
  transform(value: string | null | undefined, lang: string): string {
    if (!value) return '';
    const date = new Date(value);
    if (isNaN(date.getTime())) return '';
    const locale = localeFromLanguage(lang);
    return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' }).format(date);
  }
}
