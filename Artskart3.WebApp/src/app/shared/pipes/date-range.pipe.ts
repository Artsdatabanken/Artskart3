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

    const date = this.parseLocalDate(value);
    return date ? formatter.format(date) : '';
  }

  private parseLocalDate(value: string): Date | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (!match) return null;

    const [, year, month, day] = match;
    const date = new Date(Number(year), Number(month) - 1, Number(day));

    return date.getFullYear() === Number(year) &&
      date.getMonth() === Number(month) - 1 &&
      date.getDate() === Number(day)
      ? date
      : null;
  }
}