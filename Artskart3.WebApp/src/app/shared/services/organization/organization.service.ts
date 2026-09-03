import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { components } from '../../types/api.generated';
import { Observable } from 'rxjs';

/**
 * Oppslag mot Lookup-endepunktene for filtre som sender ID, ikke tekst.
 *
 * Samling, prosjekt og katalognummer filtreres på ID. Brukeren skriver fritekst
 * her, velger et treff, og filteret får IDen. Selve søket mot 61M observasjoner
 * slipper dermed strengsammenligning — det var `LIKE '%x%'` som gjorde
 * katalognummer-filteret 18-21 sekunder.
 */
@Injectable({
  providedIn: 'root',
})
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Organizations';
  private readonly datasetsEndpoint = '/api/Lookup/Datasets';
  private readonly projectsEndpoint = '/api/Lookup/Projects';
  private readonly catalogNumbersEndpoint = '/api/Lookup/CatalogNumbers';

  searchOrganizations(search: string): Observable<components['schemas']['OrganizationDto'][]> {
    return this.http.get<components['schemas']['OrganizationDto'][]>(this.endpoint, { params: { search } });
  }

  /** Datasett (OrganizationTypeId = 2). Treffet gir datasetOrgId. */
  searchDatasets(search: string): Observable<components['schemas']['OrganizationDto'][]> {
    return this.http.get<components['schemas']['OrganizationDto'][]>(this.datasetsEndpoint, {
      params: { search },
    });
  }

  /** Prosjekt (OrganizationTypeId = 3). Treffet gir projectOrgId. */
  searchProjects(search: string): Observable<components['schemas']['OrganizationDto'][]> {
    return this.http.get<components['schemas']['OrganizationDto'][]>(this.projectsEndpoint, {
      params: { search },
    });
  }

  /**
   * Katalognummer. PREFIKSSØK — hvert treff kommer med ObservationId-ene det
   * peker på, så filteret kan sende IDer direkte uten et ekstra kall.
   */
  searchCatalogNumbers(search: string): Observable<components['schemas']['CatalogNumberMatchDto'][]> {
    return this.http.get<components['schemas']['CatalogNumberMatchDto'][]>(this.catalogNumbersEndpoint, {
      params: { search },
    });
  }
}
