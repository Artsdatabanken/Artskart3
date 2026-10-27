import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'dateRange',
})
export class DateRangePipe implements PipeTransform {
  transform(startDate?: string | null, endDate?: string | null, lang?: string): string {
    const locale = lang === 'no' ? 'nb-NO' : 'en-GB';
    const dateFormatter = new Intl.DateTimeFormat(locale, {
      day: '2-digit',
      month: 'long',
      year: 'numeric',
    });
    const dateTimeFormatter = new Intl.DateTimeFormat(locale, {
      day: '2-digit',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    });
    const start = this.formatDate(startDate, dateFormatter, dateTimeFormatter);
    const end = this.formatDate(endDate, dateFormatter, dateTimeFormatter);

    return start && end ? `${start} - ${end}` : start || end;
  }

  private formatDate(
    value: string | null | undefined,
    dateFormatter: Intl.DateTimeFormat,
    dateTimeFormatter: Intl.DateTimeFormat
  ): string {
    if (!value) return '';

    const parsed = this.parseLocalDate(value);
    if (!parsed) return '';

    return (parsed.hasTime ? dateTimeFormatter : dateFormatter).format(parsed.date);
  }

  // Verdier uten tidssone tolkes som lokal (norsk) veggklokketid, uavhengig av nettleserens tidssone.
  private parseLocalDate(value: string): { date: Date; hasTime: boolean } | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2})(?::\d{2}(?:\.\d+)?)?)?$/.exec(value);
    if (!match) return null;

    const [, year, month, day, hour, minute] = match;
    const hasTime = hour !== undefined;
    const date = new Date(Number(year), Number(month) - 1, Number(day), Number(hour ?? 0), Number(minute ?? 0));

    const isValid =
      date.getFullYear() === Number(year) &&
      date.getMonth() === Number(month) - 1 &&
      date.getDate() === Number(day) &&
      date.getHours() === Number(hour ?? 0) &&
      date.getMinutes() === Number(minute ?? 0);

    return isValid ? { date, hasTime } : null;
  }
}