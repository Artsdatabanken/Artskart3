import { Pipe, PipeTransform, inject } from '@angular/core';
import { AreaService } from '../services/area/area.service';

@Pipe({
  name: 'areaName',
})
export class AreaNamePipe implements PipeTransform {
  private readonly areaService = inject(AreaService);

  transform(fid: string | null | undefined): string {
    if (!fid) return '';

    const municipalities = this.areaService.municipalities();
    const match = municipalities.find((a) => a.fid === fid);
    if (match?.name) return match.name;

    const counties = this.areaService.counties();
    const countyMatch = counties.find((a) => a.fid === fid);
    if (countyMatch?.name) return countyMatch.name;

    return fid;
  }
}
