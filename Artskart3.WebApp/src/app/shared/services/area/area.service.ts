import { HttpClient } from '@angular/common/http';
import { Injectable, inject, computed } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, shareReplay } from 'rxjs';
import { AreaResponseDto, AreaDto } from '../../types/api.types';
import { FilterStateService } from '../filter-state/filter-state.service';

export interface CountyGroup {
  county: AreaDto;
  municipalities: AreaDto[];
}

@Injectable({
  providedIn: 'root',
})
export class AreaService {
  private readonly http = inject(HttpClient);
  private readonly filterState = inject(FilterStateService);
  private readonly endpoint = '/api/Lookup/Areas';

  private readonly areas$ = this.http.get<AreaResponseDto>(this.endpoint).pipe(
    catchError(() => of(undefined)),
    shareReplay(1),
  );

  private readonly areaResponse = toSignal(this.areas$, { initialValue: undefined });

  readonly counties = computed(() => {
    const response = this.areaResponse();
    if (!response?.counties) return [];
    return (response.counties.fastlandsNorge ?? []).filter(
      (a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent,
    );
  });

  readonly municipalities = computed(() => {
    const response = this.areaResponse();
    if (!response?.municipalities) return [];
    return (response.municipalities.areas ?? []).filter(
      (a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent,
    );
  });

  readonly countyGroups = computed<CountyGroup[]>(() => {
    const counties = this.counties();
    const municipalities = this.municipalities();
    if (counties.length === 0) return [];

    return counties.map((county) => ({
      county,
      municipalities: municipalities.filter(
        (m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid.padStart(2, '0'),
      ),
    }));
  });

  /**
   * Resolves selected municipality IDs into optimized county/municipality ID sets for the API:
   * - If all municipalities under a county are selected → send county fid only
   * - If only some municipalities are selected → send those municipality fids only
   */
  readonly resolvedAreaFilter = computed(() => {
    const selectedMunicipalities = this.filterState.selectedMunicipalityIds();
    const counties = this.counties();
    const allMunicipalities = this.municipalities();

    const countyIds: string[] = [];
    const municipalityIds: string[] = [];

    for (const county of counties) {
      const countyMunicipalities = allMunicipalities.filter(
        (m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid.padStart(2, '0'),
      );
      const selectedInCounty = countyMunicipalities.filter((m) =>
        selectedMunicipalities.includes(m.fid),
      );

      if (selectedInCounty.length === 0) continue;

      if (selectedInCounty.length === countyMunicipalities.length) {
        countyIds.push(county.fid);
      } else {
        selectedInCounty.forEach((m) => municipalityIds.push(m.fid));
      }
    }

    return { countyIds, municipalityIds };
  });
}
