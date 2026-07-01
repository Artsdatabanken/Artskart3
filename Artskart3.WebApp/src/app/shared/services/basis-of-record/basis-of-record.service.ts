import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BasisOfRecordDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class BasisOfRecordService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/BasisOfRecords';

  getBasisOfRecords(): Observable<BasisOfRecordDto[]> {
    return this.http.get<BasisOfRecordDto[]>(this.endpoint);
  }
}
