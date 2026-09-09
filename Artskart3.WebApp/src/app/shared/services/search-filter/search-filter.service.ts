import { Injectable, Signal, computed, inject } from '@angular/core';
import { AreaService } from '../area/area.service';
import { FilterStateService, imageFilterToWithImages } from '../filter-state/filter-state.service';
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
 * utakt igjen.
 */
@Injectable({
  providedIn: 'root',
})
export class SearchFilterService {
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);

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
      const datasetOrgId = this.filterState.datasetOrgId();
      const projectOrgId = this.filterState.projectOrgId();
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
        datasetOrgId: datasetOrgId ?? undefined,
        projectOrgId: projectOrgId ?? undefined,
        observationIds: catalogObservationIds.length ? catalogObservationIds : undefined,
        withImages: withImages,
        period: hasPeriod
          ? { from: periodFrom, to: periodTo, months: periodMonths.length ? periodMonths : undefined }
          : undefined,
      };
    },
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );
}
