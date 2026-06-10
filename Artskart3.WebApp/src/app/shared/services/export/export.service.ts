import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ObservationSearchFilter } from '../../types/api.types';

export interface StartExportRequest {
  filter: ObservationSearchFilter;
  selectedColumns: string[];
}

export interface StartExportResponse {
  jobId: number;
}

@Injectable({
  providedIn: 'root',
})
export class ExportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/export/csv';

  startExport(filter: ObservationSearchFilter): Observable<StartExportResponse> {
    const request: StartExportRequest = {
      filter,
      selectedColumns: [],
    };
    return this.http.post<StartExportResponse>(`${this.baseUrl}/start`, request);
  }
}
