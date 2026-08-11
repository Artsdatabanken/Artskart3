import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'meterUnit',
  pure: true,
})
export class MeterUnitPipe implements PipeTransform {
  transform(value: number | null | undefined, lang?: string): string {
    if (value == null) return '';
    const locale = lang === 'no' ? 'nb-NO' : 'en-GB';
    return new Intl.NumberFormat(locale, { style: 'unit', unit: 'meter' }).format(value);
  }
}
