import { Pipe, PipeTransform } from '@angular/core';
import { localeFromLanguage } from '@shared/helpers/locale/locale';

@Pipe({
  name: 'formatFileSize',
  pure: true,
})
export class FormatFileSizePipe implements PipeTransform {
  transform(bytes: number | null | undefined, lang: string): string {
    if (!bytes) return '-';
    if (bytes < 1024 ** 2) return '< 1 MB';
    const locale = localeFromLanguage(lang);
    const { unit, value } =
      bytes >= 1024 ** 3
        ? { unit: 'gigabyte' as const, value: bytes / 1024 ** 3 }
        : { unit: 'megabyte' as const, value: bytes / 1024 ** 2 };
    return new Intl.NumberFormat(locale, {
      style: 'unit',
      unit,
      maximumFractionDigits: 1,
    }).format(value);
  }
}
