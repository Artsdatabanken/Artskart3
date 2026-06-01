import { Pipe, PipeTransform, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AreaService } from '../services/area/area.service';

@Pipe({
  name: 'areaName',
})
export class AreaNamePipe implements PipeTransform {
  private readonly areaService = inject(AreaService);

  transform(fid: string | null | undefined): Observable<string> {
    if (!fid) return new Observable((sub) => sub.next(''));

    return this.areaService.getAreas().pipe(
      map((areaTypes) => {
        for (const type of areaTypes) {
          const match = type.areas?.find((a) => a.fid === fid);
          if (match?.name) return match.name;
        }
        return fid;
      }),
    );
  }
}
