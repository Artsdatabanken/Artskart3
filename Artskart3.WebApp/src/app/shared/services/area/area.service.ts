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
    return (response.counties.areas ?? []).filter(
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

  readonly svalbardBjornoyaAndJanMayenAreas = computed<(AreaDto & { fid: string })[]>(() => {
    const response = this.areaResponse();
    if (!response?.svalbardBjørnøyaAndJanMayen) return [];
    return (response.svalbardBjørnøyaAndJanMayen.areas ?? []).filter(
      (a): a is AreaDto & { fid: string } => !!a.fid && !!a.isCurrent,
    );
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

  /**
   * Antall aktive områdevalg på fastlandet slik chip-en skal telle dem:
   * et fylke der alle kommunene er valgt teller som 1 (kommunene er implisitt
   * valgt), mens et delvis valgt fylke teller sine valgte kommuner. Speiler
   * resolvedAreaFilter sin optimalisering.
   */
  readonly mainlandSelectionCount = computed(() => {
    const selectedMunicipalities = this.filterState.selectedMunicipalityIds();
    const selectedCounties = this.filterState.selectedCountyIds();
    const groups = this.countyGroups();
    if (groups.length === 0) {
      // Ikke lastet ennå — eller requesten feilet permanent (catchError). Vi kan
      // ikke skille fastlandsfylker fra Svalbard-områder, så alt telles som
      // fastland: bedre en chip med for høyt tall enn valg som ikke kan fjernes.
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
    return count;
  });

  /** Antall valgte områder under Svalbard, Bjørnøya og Jan Mayen (direkte fylkesvalg). */
  readonly svalbardBjornoyaAndJanMayenSelectionCount = computed(() => {
    const areas = this.svalbardBjornoyaAndJanMayenAreas();
    // Ukjent før lasting (og etter feilet request): tell ingenting. Uten listen
    // vet vi verken hvilke fider som hører til eller kan fjerne dem — en chip
    // ville vist feil tall og hatt en clear-knapp som ikke gjorde noe.
    if (areas.length === 0) return 0;
    const fids = new Set(areas.map((a) => a.fid));
    return this.filterState.selectedCountyIds().filter((fid) => fids.has(fid)).length;
  });
}
