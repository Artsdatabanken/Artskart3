import { Injectable, Signal, computed, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AreaService } from '../area/area.service';
import { FilterStateService, imageFilterToWithImages, withImagesToImageFilter } from '../filter-state/filter-state.service';
import { OrganizationService } from '../organization/organization.service';
import { ObservationSearchFilter } from '../../types/api.types';

/**
 * Bygger observasjonsfilteret som sendes til backend, fra det brukeren har valgt
 * i filterpanelet.
 *
 * Ligger i én tjeneste med vilje. Filteret ble tidligere bygget hver for seg i
 * listevisningen og i eksporten, med nesten identisk kode — og «nesten» var
 * problemet: eksportvarianten manglet `taxonIds` og `registrationStatusId`.
 *
 * Konsekvensen var ikke bare feil innhold i filen. API-et teller opp treffene med
 * det samme filteret før eksporten startes, så et søk nedfiltrert til én art ble
 * talt som hele tabellen, traff radgrensen og ga brukeren «Eksport ikke mulig» —
 * for et søk som viste tre treff på skjermen.
 *
 * Nye filterfelt skal legges til HER, slik at søk og eksport ikke kan komme i
 * utakt igjen — og i `applyFilter`, som gjør det motsatte for lagrede filtre.
 */
@Injectable({
  providedIn: 'root',
})
export class SearchFilterService {
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly organizationService = inject(OrganizationService);

  /**
   * Filteret uten paginering. Listevisningen legger på `pageNumber` og
   * `resultsPerPage` selv; eksporten bruker det som det er.
   */
  readonly observationFilter: Signal<ObservationSearchFilter> = computed(
    () => {
      const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
      const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
      const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
      const periodFrom = this.filterState.periodFrom();
      const periodTo = this.filterState.periodTo();
      const periodMonths = this.filterState.selectedMonths();
      const hasCoordinatePrecision = coordinatePrecisionFrom != null || coordinatePrecisionTo != null;
      const hasPeriod = periodFrom != null || periodTo != null || periodMonths.length > 0;
      const datasetOrgIds = this.filterState.selectedDatasetIds();
      const projectOrgIds = this.filterState.selectedProjectIds();
      const catalogObservationIds = this.filterState.catalogObservationIds();
      const withImages = imageFilterToWithImages(this.filterState.imageFilter());

      return {
        categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
        organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
        behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
        basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
        registrationStatusId: this.filterState.selectedRegistrationStatusId() ?? undefined,
        taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
        taxonIds: this.filterState.selectedTaxonIds().length ? this.filterState.selectedTaxonIds() : undefined,
        countyIds: countyIds.length ? countyIds : undefined,
        municipalityIds: municipalityIds.length ? municipalityIds : undefined,
        oceanAreaIds: this.filterState.selectedOceanAreaIds().length ? this.filterState.selectedOceanAreaIds() : undefined,
        coordinatePrecision: hasCoordinatePrecision ? { from: coordinatePrecisionFrom, to: coordinatePrecisionTo } : undefined,
        datasetOrgIds: datasetOrgIds.length ? datasetOrgIds : undefined,
        projectOrgIds: projectOrgIds.length ? projectOrgIds : undefined,
        observationIds: catalogObservationIds.length ? catalogObservationIds : undefined,
        withImages: withImages,
        period: hasPeriod
          ? { from: periodFrom, to: periodTo, months: periodMonths.length ? periodMonths : undefined }
          : undefined,
      };
    },
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  readonly hasActiveFilter = computed(() => Object.values(this.observationFilter()).some((value) => value !== undefined));

  /**
   * Det motsatte av `observationFilter`: setter filterpanelet fra et lagret filter.
   *
   * Navnene på datasett, prosjekt og katalognummer ligger ikke i filteret, bare
   * ID-ene. De slås opp etterpå uten å vente — filteret virker uansett, det er bare
   * teksten i panelet og filterbrikkene som kommer litt senere.
   */
  async applyFilter(filter: ObservationSearchFilter): Promise<void> {
    await firstValueFrom(this.areaService.areasLoaded$);

    const state = this.filterState;
    state.clearAll();

    const areas = this.areaService.toSelection(filter.countyIds ?? [], filter.municipalityIds ?? []);
    state.selectedCountyIds.set(areas.countyIds);
    state.selectedMunicipalityIds.set(areas.municipalityIds);
    state.selectedOceanAreaIds.set(filter.oceanAreaIds ?? []);
    state.selectedCategoryIds.set(filter.categoryIds ?? []);
    state.selectedInstitutionIds.set(filter.organizationIds ?? []);
    state.selectedBehaviorIds.set(filter.behaviorIds ?? []);
    state.selectedBasisOfRecordIds.set(filter.basisOfRecordIds ?? []);
    state.selectedRegistrationStatusId.set(filter.registrationStatusId ?? null);
    state.selectedTaxonGroupIds.set(filter.taxonGroupIds ?? []);
    state.selectedTaxonIds.set(filter.taxonIds ?? []);
    state.setCoordinatePrecision(filter.coordinatePrecision?.from ?? null, filter.coordinatePrecision?.to ?? null);
    state.setPeriod(filter.period?.from ?? null, filter.period?.to ?? null);
    state.selectedMonths.set(filter.period?.months ?? []);
    state.selectedDatasets.set(withSingle(filter.datasetOrgIds, filter.datasetOrgId).map((id) => ({ id, name: '' })));
    state.selectedProjects.set(withSingle(filter.projectOrgIds, filter.projectOrgId).map((id) => ({ id, name: '' })));
    state.setCatalogObservationIds(filter.observationIds ?? []);
    state.setImageFilter(withImagesToImageFilter(filter.withImages));

    this.resolveDisplayNames(filter);
  }

  private resolveDisplayNames(filter: ObservationSearchFilter): void {
    const state = this.filterState;
    const { observationIds } = filter;

    // Et navn som kommer etter at brukeren har fjernet valget, gjør ingenting. Feiler
    // oppslaget (f.eks. slettet organisasjon), viser filterbrikken ID-en i stedet.
    for (const { id } of state.selectedDatasets()) {
      this.organizationService.getOrganization(id).subscribe({
        next: (organization) => organization.name && state.setDatasetName(id, organization.name),
        error: () => undefined,
      });
    }

    for (const { id } of state.selectedProjects()) {
      this.organizationService.getOrganization(id).subscribe({
        next: (organization) => organization.name && state.setProjectName(id, organization.name),
        error: () => undefined,
      });
    }

    if (observationIds?.length) {
      const fallback = observationIds.length > 1 ? `#${observationIds[0]} (+${observationIds.length - 1})` : `#${observationIds[0]}`;
      const setName = (name: string) => sameIds(state.catalogObservationIds(), observationIds) && state.setCatalogNumber(name);
      this.organizationService.getCatalogNumberForObservations(observationIds).subscribe({
        next: (match) => setName(match.catalogNumber || fallback),
        error: () => setName(fallback),
      });
    }
  }
}

/** Lagrede filtre fra før listene kom har bare én ID. */
function withSingle(ids: number[] | null | undefined, single: number | null | undefined): number[] {
  return [...new Set([...(ids ?? []), ...(single != null ? [single] : [])])];
}

function sameIds(a: number[], b: number[]): boolean {
  return a.length === b.length && a.every((id, i) => id === b[i]);
}
