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

  readonly janMayenGroup = computed<CountyGroup | null>(() => {
    const response = this.areaResponse();
    const county = response?.counties?.janMayen;
    if (!county?.fid || !county.isCurrent) return null;
    const municipalities = this.municipalities();
    return {
      county: county as AreaDto & { fid: string },
      municipalities: municipalities.filter(
        (m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid!.padStart(2, '0'),
      ),
    };
  });

  readonly svalbardGroup = computed<CountyGroup | null>(() => {
    const response = this.areaResponse();
    const county = response?.counties?.svalbard;
    if (!county?.fid || !county.isCurrent) return null;
    const municipalities = this.municipalities();
    return {
      county: county as AreaDto & { fid: string },
      municipalities: municipalities.filter(
        (m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid!.padStart(2, '0'),
      ),
    };
  });

  readonly oceanAreaGroup = computed<CountyGroup | null>(() => {
    const response = this.areaResponse();
    const oceanAreas = response?.oceanAreas;
    if (!oceanAreas) return null;
    const areas = (oceanAreas.areas ?? []).filter(
      (a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent,
    );
    if (areas.length === 0) return null;
    return {
      county: { id: oceanAreas.id, name: oceanAreas.name, fid: 'ocean', isCurrent: true },
      municipalities: areas,
    };
  });

  /**
   * Resolves selected municipality IDs into optimized county/municipality ID sets for the API:
   * - If all municipalities under a county are selected → send county fid only
   * - If only some municipalities are selected → send those municipality fids only
   * - Directly selected county IDs (e.g. Svalbard) are always included
   */
  readonly resolvedAreaFilter = computed(() => {
    const selectedMunicipalities = this.filterState.selectedMunicipalityIds();
    const directlySelectedCounties = this.filterState.selectedCountyIds();
    const counties = this.counties();
    const allMunicipalities = this.municipalities();

    const countyIds: string[] = [...directlySelectedCounties];
    const municipalityIds: string[] = [];

    const allGroups: CountyGroup[] = [
      ...counties.map((county) => ({
        county,
        municipalities: allMunicipalities.filter(
          (m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid.padStart(2, '0'),
        ),
      })),
    ];

    const janMayen = this.janMayenGroup();
    if (janMayen) allGroups.push(janMayen);

    const svalbard = this.svalbardGroup();
    if (svalbard) allGroups.push(svalbard);

    for (const group of allGroups) {
      const groupMunicipalities = group.municipalities;
      const selectedInGroup = groupMunicipalities.filter((m) =>
        selectedMunicipalities.includes(m.fid!),
      );

      if (selectedInGroup.length === 0) continue;

      if (selectedInGroup.length === groupMunicipalities.length && group.county.fid) {
        countyIds.push(group.county.fid);
      } else {
        selectedInGroup.forEach((m) => municipalityIds.push(m.fid!));
      }
    }

    return { countyIds, municipalityIds };
  });
}
