import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TaxonTreeNodeDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class TaxonTreeService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/TaxonTree';

  getChildren(parentTaxonId?: number): Observable<TaxonTreeNodeDto[]> {
    if (parentTaxonId !== undefined) {
      return this.http.get<TaxonTreeNodeDto[]>(this.endpoint, {
        params: { parentTaxonId: parentTaxonId.toString() },
      });
    }
    return this.http.get<TaxonTreeNodeDto[]>(this.endpoint);
  }
}
