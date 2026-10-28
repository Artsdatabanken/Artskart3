import { Pipe, PipeTransform } from '@angular/core';
import { localeFromLanguage } from '@shared/helpers/locale/locale';

@Pipe({
  name: 'formatNumber',
  pure: true,
})
export class FormatNumberPipe implements PipeTransform {
  transform(value: number | null | undefined, lang: string): string {
    if (value == null) return '';
    const locale = localeFromLanguage(lang);
    return new Intl.NumberFormat(locale).format(value);
  }
}
