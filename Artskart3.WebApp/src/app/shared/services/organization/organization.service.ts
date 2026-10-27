import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CatalogNumberMatchDto, OrganizationDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Organizations';
  private readonly datasetsEndpoint = '/api/Lookup/Datasets';
  private readonly projectsEndpoint = '/api/Lookup/Projects';
  private readonly catalogNumbersEndpoint = '/api/Lookup/CatalogNumbers';
  /** Backendens maks (SearchConstants.MaxOrganizationCount). */
  private readonly typeaheadMaxCount = 50;

  searchOrganizations(search: string): Observable<OrganizationDto[]> {
    return this.http.get<OrganizationDto[]>(this.endpoint, { params: { search } });
  }

  searchDatasets(search: string): Observable<OrganizationDto[]> {
    return this.http.get<OrganizationDto[]>(this.datasetsEndpoint, {
      params: { search, maxCount: this.typeaheadMaxCount },
    });
  }

  searchProjects(search: string): Observable<OrganizationDto[]> {
    return this.http.get<OrganizationDto[]>(this.projectsEndpoint, {
      params: { search, maxCount: this.typeaheadMaxCount },
    });
  }

  /** Prefikssøk, i motsetning til delstrengsøket for datasett og prosjekt. */
  searchCatalogNumbers(search: string): Observable<CatalogNumberMatchDto[]> {
    return this.http.get<CatalogNumberMatchDto[]>(this.catalogNumbersEndpoint, {
      params: { search, maxCount: this.typeaheadMaxCount },
    });
  }

  getOrganization(id: number): Observable<OrganizationDto> {
    return this.http.get<OrganizationDto>(`${this.endpoint}/${id}`);
  }

  getCatalogNumberForObservations(observationIds: number[]): Observable<CatalogNumberMatchDto> {
    return this.http.post<CatalogNumberMatchDto>(`${this.catalogNumbersEndpoint}/ByObservationIds`, observationIds);
  }
}
