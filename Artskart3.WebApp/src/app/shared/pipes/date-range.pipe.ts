import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'dateRange',
})
export class DateRangePipe implements PipeTransform {
  transform(startDate?: string | null, endDate?: string | null, lang?: string): string {
    const locale = lang === 'no' ? 'nb-NO' : 'en-GB';
    const formatter = new Intl.DateTimeFormat(locale, {
      day: '2-digit',
      month: 'long',
      year: 'numeric',
    });
    const start = this.formatDate(startDate, formatter);
    const end = this.formatDate(endDate, formatter);

    return start && end ? `${start} - ${end}` : start || end;
  }

  private formatDate(value: string | null | undefined, formatter: Intl.DateTimeFormat): string {
    if (!value) return '';

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : formatter.format(date);
  }
}