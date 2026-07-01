import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Pipe({
  name: 'meterUnit',
  pure: true,
})
export class MeterUnitPipe implements PipeTransform {
  private readonly translateService = inject(TranslateService);

  transform(value: number | null | undefined): string {
    if (value == null) return '';
    const locale = this.translateService.currentLang === 'no' ? 'nb-NO' : 'en-GB';
    return new Intl.NumberFormat(locale, { style: 'unit', unit: 'meter' }).format(value);
  }
}
