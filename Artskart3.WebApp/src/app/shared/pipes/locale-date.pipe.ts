import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'localeDate',
  pure: true,
})
export class LocaleDatePipe implements PipeTransform {
  transform(value: string | null | undefined, lang?: string): string {
    if (!value) return '';
    const date = new Date(value);
    if (isNaN(date.getTime())) return '';
    const locale = lang === 'no' ? 'nb-NO' : 'en-GB';
    return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' }).format(date);
  }
}
