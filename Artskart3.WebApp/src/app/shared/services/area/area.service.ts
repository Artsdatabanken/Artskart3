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

  /** Fullføres når områdene er lastet (eller feilet). */
  readonly areasLoaded$ = this.areas$;

  readonly counties = computed(() => {
    const response = this.areaResponse();
    if (!response?.counties) return [];
    return (response.counties.areas ?? []).filter((a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent);
  });

  readonly municipalities = computed(() => {
    const response = this.areaResponse();
    if (!response?.municipalities) return [];
    return (response.municipalities.areas ?? []).filter((a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent);
  });

  readonly countyGroups = computed<CountyGroup[]>(() => {
    const counties = this.counties();
    const municipalities = this.municipalities();
    if (counties.length === 0) return [];

    return counties.map((county) => ({
      county,
      municipalities: municipalities.filter((m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid.padStart(2, '0')),
    }));
  });

  readonly svalbardBjornoyaAndJanMayenAreas = computed<(AreaDto & { fid: string })[]>(() => {
    const response = this.areaResponse();
    if (!response?.svalbardBjørnøyaAndJanMayen) return [];
    return (response.svalbardBjørnøyaAndJanMayen.areas ?? []).filter((a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent);
  });

  readonly oceanAreaGroup = computed<CountyGroup | null>(() => {
    const response = this.areaResponse();
    const oceanAreas = response?.oceanAreas;
    if (!oceanAreas) return null;
    const areas = (oceanAreas.areas ?? []).filter((a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent);
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
   * - Municipality IDs that are not in the current area list (e.g. merged away since a
   *   filter was saved) are sent as they are, so the filter is never silently widened
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
        municipalities: allMunicipalities.filter((m) => m.fid.padStart(4, '0').substring(0, 2) === county.fid.padStart(2, '0')),
      })),
    ];

    for (const group of allGroups) {
      const groupMunicipalities = group.municipalities;
      const selectedInGroup = groupMunicipalities.filter((m) => selectedMunicipalities.includes(m.fid!));

      if (selectedInGroup.length === 0) continue;

      if (selectedInGroup.length === groupMunicipalities.length && group.county.fid) {
        countyIds.push(group.county.fid);
      } else {
        selectedInGroup.forEach((m) => municipalityIds.push(m.fid!));
      }
    }

    municipalityIds.push(...this.unknownMunicipalityIds(selectedMunicipalities));

    return { countyIds, municipalityIds };
  });

  /**
   * Den omvendte veien av `resolvedAreaFilter`: gjør fylkes- og kommune-ID-ene fra
   * et lagret filter om til valgene i filterpanelet. Et fastlandsfylke blir til alle
   * kommunene sine, slik avkrysningen i panelet forventer. Svalbard/Jan Mayen og
   * ukjente fylker beholdes som fylkes-ID-er.
   */
  toSelection(countyIds: string[] = [], municipalityIds: string[] = []): { countyIds: string[]; municipalityIds: string[] } {
    const groupsByCounty = new Map(this.countyGroups().map((group) => [group.county.fid, group]));
    const selectedCounties: string[] = [];
    const selectedMunicipalities = new Set(municipalityIds);

    for (const countyId of countyIds) {
      const group = groupsByCounty.get(countyId);
      if (group && group.municipalities.length > 0) {
        group.municipalities.forEach((m) => selectedMunicipalities.add(m.fid!));
      } else {
        selectedCounties.push(countyId);
      }
    }

    return { countyIds: selectedCounties, municipalityIds: [...selectedMunicipalities] };
  }

  readonly mainlandSelectionCount = computed(() => {
    const selectedMunicipalities = this.filterState.selectedMunicipalityIds();
    const selectedCounties = this.filterState.selectedCountyIds();
    const groups = this.countyGroups();
    if (groups.length === 0) {
      return selectedMunicipalities.length + selectedCounties.length;
    }
    let count = 0;
    for (const group of groups) {
      const municipalityFids = group.municipalities.map((m) => m.fid!);
      const selected = municipalityFids.filter((fid) => selectedMunicipalities.includes(fid)).length;
      if (selected > 0) {
        count += selected === municipalityFids.length ? 1 : selected;
      } else if (group.county.fid && selectedCounties.includes(group.county.fid)) {
        count += 1;
      }
    }

    // Områder som ikke finnes i dagens inndeling vises ikke i panelet, men filtrerer
    // fortsatt. De telles med, så filterbrikken viser at de er satt og kan fjernes.
    const knownCounties = new Set([
      ...groups.map((g) => g.county.fid),
      ...this.svalbardBjornoyaAndJanMayenAreas().map((a) => a.fid),
    ]);
    count += selectedCounties.filter((fid) => !knownCounties.has(fid)).length;
    count += this.unknownMunicipalityIds(selectedMunicipalities).length;
    return count;
  });

  /** Valgte kommuner som ikke hører til noe fylke i dagens inndeling, og derfor ikke vises i panelet. */
  private unknownMunicipalityIds(selectedMunicipalities: string[]): string[] {
    const known = new Set(this.countyGroups().flatMap((group) => group.municipalities.map((m) => m.fid)));
    return selectedMunicipalities.filter((fid) => !known.has(fid));
  }

  readonly svalbardBjornoyaAndJanMayenSelectionCount = computed(() => {
    const areas = this.svalbardBjornoyaAndJanMayenAreas();
    if (areas.length === 0) return 0;
    const fids = new Set(areas.map((a) => a.fid));
    return this.filterState.selectedCountyIds().filter((fid) => fids.has(fid)).length;
  });
}
