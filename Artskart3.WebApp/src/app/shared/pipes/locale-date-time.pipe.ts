import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'localeDateTime',
  pure: true,
})
export class LocaleDateTimePipe implements PipeTransform {
  transform(value: string | null | undefined, lang?: string): string {
    if (!value) return '';
    const date = new Date(value);
    if (isNaN(date.getTime())) return '';
    const locale = lang === 'no' ? 'nb-NO' : 'en-GB';
    return new Intl.DateTimeFormat(locale, {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  }
}
