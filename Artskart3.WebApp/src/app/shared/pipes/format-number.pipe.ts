import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'formatNumber',
  pure: true,
})
export class FormatNumberPipe implements PipeTransform {
  transform(value: number | null | undefined, lang?: string): string {
    if (value == null) return '';
    const locale = lang === 'no' ? 'nb-NO' : 'en-GB';
    return new Intl.NumberFormat(locale).format(value);
  }
}
