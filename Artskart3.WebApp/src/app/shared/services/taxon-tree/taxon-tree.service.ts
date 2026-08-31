import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TaxonAncestryDto, TaxonTreeNodeDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class TaxonTreeService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/TaxonTree';
  private readonly ancestryEndpoint = '/api/Lookup/TaxonAncestry';

  getChildren(parentTaxonId?: number): Observable<TaxonTreeNodeDto[]> {
    if (parentTaxonId !== undefined) {
      return this.http.get<TaxonTreeNodeDto[]>(this.endpoint, {
        params: { parentTaxonId: parentTaxonId.toString() },
      });
    }
    return this.http.get<TaxonTreeNodeDto[]>(this.endpoint);
  }

  getAncestry(taxonIds: number[]): Observable<TaxonAncestryDto[]> {
    let params = new HttpParams();
    for (const id of taxonIds) {
      params = params.append('taxonIds', id.toString());
    }
    return this.http.get<TaxonAncestryDto[]>(this.ancestryEndpoint, { params });
  }
}
