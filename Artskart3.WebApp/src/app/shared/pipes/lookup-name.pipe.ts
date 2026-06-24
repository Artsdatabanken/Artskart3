import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'lookupName',
  pure: true,
})
export class LookupNamePipe implements PipeTransform {
  transform(id: number | null | undefined, map: Map<number, string>): string {
    if (id == null) return '';
    return map.get(id) ?? '';
  }
}
